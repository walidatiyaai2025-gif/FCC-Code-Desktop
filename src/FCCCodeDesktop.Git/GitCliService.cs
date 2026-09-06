using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using FCCCodeDesktop.Application.Git;

namespace FCCCodeDesktop.Git;

public sealed class GitCliService : IGitService
{
    public static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaximumProbeTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultStatusTimeout = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan MaximumStatusTimeout = TimeSpan.FromSeconds(60);

    private const string NotRepositoryMessage = "not a git repository";
    private readonly string _gitExecutable;
    private readonly TimeSpan _probeTimeout;
    private readonly TimeSpan _statusTimeout;

    public GitCliService(
        string gitExecutable = "git",
        TimeSpan? probeTimeout = null,
        TimeSpan? statusTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitExecutable);

        var resolvedProbeTimeout = probeTimeout ?? DefaultProbeTimeout;
        if (resolvedProbeTimeout <= TimeSpan.Zero || resolvedProbeTimeout > MaximumProbeTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probeTimeout),
                probeTimeout,
                $"Git probe timeout must be greater than zero and no more than {MaximumProbeTimeout.TotalSeconds} seconds.");
        }

        var resolvedStatusTimeout = statusTimeout ?? DefaultStatusTimeout;
        if (resolvedStatusTimeout <= TimeSpan.Zero || resolvedStatusTimeout > MaximumStatusTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statusTimeout),
                statusTimeout,
                $"Git status timeout must be greater than zero and no more than {MaximumStatusTimeout.TotalSeconds} seconds.");
        }

        _gitExecutable = gitExecutable;
        _probeTimeout = resolvedProbeTimeout;
        _statusTimeout = resolvedStatusTimeout;
    }

    public async Task<GitRepositoryDetectionResult> DetectRepositoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var probePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(probePath))
        {
            throw new DirectoryNotFoundException($"Git repository probe path does not exist: {probePath}");
        }

        var classification = await ExecuteGitAsync(
            probePath,
            ["rev-parse", "--is-inside-work-tree", "--is-bare-repository"],
            _probeTimeout,
            cancellationToken).ConfigureAwait(false);

        if (!classification.Started)
        {
            return new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.GitUnavailable);
        }

        if (classification.TimedOut)
        {
            return new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.ProbeFailed);
        }

        if (classification.ExitCode != 0)
        {
            return classification.StandardError.Contains(NotRepositoryMessage, StringComparison.OrdinalIgnoreCase)
                ? new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.NotRepository)
                : new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.ProbeFailed);
        }

        var classificationLines = ReadOutputLines(classification.StandardOutput);
        if (classificationLines.Length != 2
            || !bool.TryParse(classificationLines[0], out var insideWorkTree)
            || !bool.TryParse(classificationLines[1], out var isBare)
            || (!insideWorkTree && !isBare))
        {
            return new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.ProbeFailed);
        }

        return isBare
            ? await BuildBareRepositoryResultAsync(probePath, cancellationToken).ConfigureAwait(false)
            : await BuildWorkTreeResultAsync(probePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GitStatusResult> GetStatusAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var detection = await DetectRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
        switch (detection.Status)
        {
            case GitRepositoryDetectionStatus.NotRepository:
                return EmptyStatus(GitStatusQueryStatus.NotRepository);
            case GitRepositoryDetectionStatus.GitUnavailable:
                return EmptyStatus(GitStatusQueryStatus.GitUnavailable);
            case GitRepositoryDetectionStatus.ProbeFailed:
                return EmptyStatus(GitStatusQueryStatus.QueryFailed);
            case GitRepositoryDetectionStatus.Repository:
                break;
            default:
                return EmptyStatus(GitStatusQueryStatus.QueryFailed);
        }

        var repository = detection.Repository;
        if (repository is null)
        {
            return EmptyStatus(GitStatusQueryStatus.QueryFailed);
        }

        if (repository.Kind == GitRepositoryKind.Bare)
        {
            return new GitStatusResult(
                GitStatusQueryStatus.BareRepository,
                repository.RepositoryRootPath,
                Array.Empty<GitFileStatusEntry>());
        }

        var statusCommand = await ExecuteGitAsync(
            repository.RepositoryRootPath,
            ["status", "--porcelain=v2", "-z", "--untracked-files=all", "--renames"],
            _statusTimeout,
            cancellationToken).ConfigureAwait(false);

        if (!statusCommand.Started)
        {
            return EmptyStatus(GitStatusQueryStatus.GitUnavailable);
        }

        if (statusCommand.TimedOut || statusCommand.ExitCode != 0)
        {
            return new GitStatusResult(
                GitStatusQueryStatus.QueryFailed,
                repository.RepositoryRootPath,
                Array.Empty<GitFileStatusEntry>());
        }

        if (!TryParseStatus(statusCommand.StandardOutput, out var files))
        {
            return new GitStatusResult(
                GitStatusQueryStatus.QueryFailed,
                repository.RepositoryRootPath,
                Array.Empty<GitFileStatusEntry>());
        }

        return new GitStatusResult(
            GitStatusQueryStatus.Success,
            repository.RepositoryRootPath,
            files);
    }

    private async Task<GitRepositoryDetectionResult> BuildWorkTreeResultAsync(
        string probePath,
        CancellationToken cancellationToken)
    {
        var details = await ExecuteGitAsync(
            probePath,
            ["rev-parse", "--show-toplevel", "--absolute-git-dir"],
            _probeTimeout,
            cancellationToken).ConfigureAwait(false);

        if (!details.Started)
        {
            return new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.GitUnavailable);
        }

        if (details.TimedOut || details.ExitCode != 0)
        {
            return new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.ProbeFailed);
        }

        var lines = ReadOutputLines(details.StandardOutput);
        if (lines.Length != 2)
        {
            return new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.ProbeFailed);
        }

        return new GitRepositoryDetectionResult(
            GitRepositoryDetectionStatus.Repository,
            new GitRepositoryInfo(
                probePath,
                NormalizeGitPath(lines[0], probePath),
                NormalizeGitPath(lines[1], probePath),
                GitRepositoryKind.WorkTree));
    }

    private async Task<GitRepositoryDetectionResult> BuildBareRepositoryResultAsync(
        string probePath,
        CancellationToken cancellationToken)
    {
        var details = await ExecuteGitAsync(
            probePath,
            ["rev-parse", "--absolute-git-dir"],
            _probeTimeout,
            cancellationToken).ConfigureAwait(false);

        if (!details.Started)
        {
            return new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.GitUnavailable);
        }

        if (details.TimedOut || details.ExitCode != 0)
        {
            return new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.ProbeFailed);
        }

        var lines = ReadOutputLines(details.StandardOutput);
        if (lines.Length != 1)
        {
            return new GitRepositoryDetectionResult(GitRepositoryDetectionStatus.ProbeFailed);
        }

        var gitDirectoryPath = NormalizeGitPath(lines[0], probePath);
        return new GitRepositoryDetectionResult(
            GitRepositoryDetectionStatus.Repository,
            new GitRepositoryInfo(
                probePath,
                gitDirectoryPath,
                gitDirectoryPath,
                GitRepositoryKind.Bare));
    }

    private async Task<GitCommandResult> ExecuteGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(_gitExecutable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["LC_ALL"] = "C";
        startInfo.Environment["LANG"] = "C";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return GitCommandResult.NotStarted;
            }
        }
        catch (Win32Exception)
        {
            return GitCommandResult.NotStarted;
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardErrorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            await TerminateProcessAsync(process).ConfigureAwait(false);
            var cancelledOutput = await standardOutputTask.ConfigureAwait(false);
            var cancelledError = await standardErrorTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new GitCommandResult(true, null, cancelledOutput, cancelledError, TimedOut: true);
        }

        return new GitCommandResult(
            true,
            process.ExitCode,
            await standardOutputTask.ConfigureAwait(false),
            await standardErrorTask.ConfigureAwait(false),
            TimedOut: false);
    }

    private static GitStatusResult EmptyStatus(GitStatusQueryStatus status) =>
        new(status, null, Array.Empty<GitFileStatusEntry>());

    private static bool TryParseStatus(string output, out IReadOnlyList<GitFileStatusEntry> files)
    {
        var parsed = new List<GitFileStatusEntry>();
        var records = output.Split('\0');

        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Length == 0)
            {
                continue;
            }

            switch (record[0])
            {
                case '1':
                    if (!TryParseOrdinaryEntry(record, out var ordinaryEntry))
                    {
                        files = Array.Empty<GitFileStatusEntry>();
                        return false;
                    }

                    parsed.Add(ordinaryEntry);
                    break;

                case '2':
                    if (!TryParseRenameOrCopyEntry(record, out var renameEntry)
                        || index + 1 >= records.Length
                        || string.IsNullOrEmpty(records[index + 1]))
                    {
                        files = Array.Empty<GitFileStatusEntry>();
                        return false;
                    }

                    parsed.Add(renameEntry with { OriginalPath = records[++index] });
                    break;

                case 'u':
                    if (!TryParseUnmergedEntry(record, out var unmergedEntry))
                    {
                        files = Array.Empty<GitFileStatusEntry>();
                        return false;
                    }

                    parsed.Add(unmergedEntry);
                    break;

                case '?':
                    if (record.Length < 3 || record[1] != ' ')
                    {
                        files = Array.Empty<GitFileStatusEntry>();
                        return false;
                    }

                    parsed.Add(new GitFileStatusEntry(
                        record[2..],
                        GitFileChangeKind.None,
                        GitFileChangeKind.Untracked));
                    break;

                case '!':
                    // Ignored records are not requested, but tolerate them defensively.
                    break;

                default:
                    files = Array.Empty<GitFileStatusEntry>();
                    return false;
            }
        }

        files = parsed
            .OrderBy(static entry => entry.Path, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private static bool TryParseOrdinaryEntry(string record, out GitFileStatusEntry entry)
    {
        var fields = record.Split(' ', 9, StringSplitOptions.None);
        if (fields.Length != 9 || fields[0] != "1" || fields[1].Length != 2 || string.IsNullOrEmpty(fields[8]))
        {
            entry = default!;
            return false;
        }

        if (!TryMapChange(fields[1][0], out var indexChange)
            || !TryMapChange(fields[1][1], out var workTreeChange))
        {
            entry = default!;
            return false;
        }

        entry = new GitFileStatusEntry(fields[8], indexChange, workTreeChange);
        return true;
    }

    private static bool TryParseRenameOrCopyEntry(string record, out GitFileStatusEntry entry)
    {
        var fields = record.Split(' ', 10, StringSplitOptions.None);
        if (fields.Length != 10 || fields[0] != "2" || fields[1].Length != 2 || string.IsNullOrEmpty(fields[9]))
        {
            entry = default!;
            return false;
        }

        if (!TryMapChange(fields[1][0], out var indexChange)
            || !TryMapChange(fields[1][1], out var workTreeChange))
        {
            entry = default!;
            return false;
        }

        entry = new GitFileStatusEntry(fields[9], indexChange, workTreeChange);
        return true;
    }

    private static bool TryParseUnmergedEntry(string record, out GitFileStatusEntry entry)
    {
        var fields = record.Split(' ', 11, StringSplitOptions.None);
        if (fields.Length != 11 || fields[0] != "u" || fields[1].Length != 2 || string.IsNullOrEmpty(fields[10]))
        {
            entry = default!;
            return false;
        }

        entry = new GitFileStatusEntry(
            fields[10],
            GitFileChangeKind.Unmerged,
            GitFileChangeKind.Unmerged);
        return true;
    }

    private static bool TryMapChange(char status, out GitFileChangeKind change)
    {
        change = status switch
        {
            '.' => GitFileChangeKind.None,
            'M' => GitFileChangeKind.Modified,
            'A' => GitFileChangeKind.Added,
            'D' => GitFileChangeKind.Deleted,
            'R' => GitFileChangeKind.Renamed,
            'C' => GitFileChangeKind.Copied,
            'T' => GitFileChangeKind.TypeChanged,
            'U' => GitFileChangeKind.Unmerged,
            _ => GitFileChangeKind.None,
        };

        return status is '.' or 'M' or 'A' or 'D' or 'R' or 'C' or 'T' or 'U';
    }

    private static async Task TerminateProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }

        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // The process already exited while cleanup was being reconciled.
        }
    }

    private static string[] ReadOutputLines(string output) =>
        output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NormalizeGitPath(string gitPath, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitPath);
        var candidate = Path.IsPathRooted(gitPath)
            ? gitPath
            : Path.Combine(workingDirectory, gitPath);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
    }

    private sealed record GitCommandResult(
        bool Started,
        int? ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut)
    {
        public static GitCommandResult NotStarted { get; } = new(false, null, string.Empty, string.Empty, TimedOut: false);
    }
}
