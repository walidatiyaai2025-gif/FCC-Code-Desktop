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
    public static readonly TimeSpan DefaultDiffTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan MaximumDiffTimeout = TimeSpan.FromSeconds(60);

    public const int DefaultMaxDiffCharacters = 2 * 1024 * 1024;
    public const int MaximumDiffCharacters = 8 * 1024 * 1024;

    private const string NotRepositoryMessage = "not a git repository";
    private readonly string _gitExecutable;
    private readonly TimeSpan _probeTimeout;
    private readonly TimeSpan _statusTimeout;
    private readonly TimeSpan _diffTimeout;
    private readonly int _maxDiffCharacters;

    public GitCliService(
        string gitExecutable = "git",
        TimeSpan? probeTimeout = null,
        TimeSpan? statusTimeout = null,
        TimeSpan? diffTimeout = null,
        int maxDiffCharacters = DefaultMaxDiffCharacters)
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

        var resolvedDiffTimeout = diffTimeout ?? DefaultDiffTimeout;
        if (resolvedDiffTimeout <= TimeSpan.Zero || resolvedDiffTimeout > MaximumDiffTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diffTimeout),
                diffTimeout,
                $"Git diff timeout must be greater than zero and no more than {MaximumDiffTimeout.TotalSeconds} seconds.");
        }

        if (maxDiffCharacters <= 0 || maxDiffCharacters > MaximumDiffCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDiffCharacters),
                maxDiffCharacters,
                $"Git diff materialization limit must be greater than zero and no more than {MaximumDiffCharacters} characters.");
        }

        _gitExecutable = gitExecutable;
        _probeTimeout = resolvedProbeTimeout;
        _statusTimeout = resolvedStatusTimeout;
        _diffTimeout = resolvedDiffTimeout;
        _maxDiffCharacters = maxDiffCharacters;
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

    public async Task<GitFileDiffResult> GetDiffAsync(
        string path,
        string repositoryRelativePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalizedPath = NormalizeRepositoryRelativePath(repositoryRelativePath);
        cancellationToken.ThrowIfCancellationRequested();

        var detection = await DetectRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
        switch (detection.Status)
        {
            case GitRepositoryDetectionStatus.NotRepository:
                return EmptyDiff(GitDiffQueryStatus.NotRepository, normalizedPath);
            case GitRepositoryDetectionStatus.GitUnavailable:
                return EmptyDiff(GitDiffQueryStatus.GitUnavailable, normalizedPath);
            case GitRepositoryDetectionStatus.ProbeFailed:
                return EmptyDiff(GitDiffQueryStatus.QueryFailed, normalizedPath);
            case GitRepositoryDetectionStatus.Repository:
                break;
            default:
                return EmptyDiff(GitDiffQueryStatus.QueryFailed, normalizedPath);
        }

        var repository = detection.Repository;
        if (repository is null)
        {
            return EmptyDiff(GitDiffQueryStatus.QueryFailed, normalizedPath);
        }

        if (repository.Kind == GitRepositoryKind.Bare)
        {
            return EmptyDiff(
                GitDiffQueryStatus.BareRepository,
                normalizedPath,
                repository.RepositoryRootPath);
        }

        var status = await GetStatusAsync(repository.RepositoryRootPath, cancellationToken).ConfigureAwait(false);
        if (status.Status != GitStatusQueryStatus.Success)
        {
            return EmptyDiff(
                status.Status switch
                {
                    GitStatusQueryStatus.NotRepository => GitDiffQueryStatus.NotRepository,
                    GitStatusQueryStatus.BareRepository => GitDiffQueryStatus.BareRepository,
                    GitStatusQueryStatus.GitUnavailable => GitDiffQueryStatus.GitUnavailable,
                    _ => GitDiffQueryStatus.QueryFailed,
                },
                normalizedPath,
                repository.RepositoryRootPath);
        }

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var statusEntry = status.Files.FirstOrDefault(
            entry => string.Equals(entry.Path, normalizedPath, pathComparison));

        if (statusEntry?.IsUntracked == true)
        {
            var untracked = await ExecuteUntrackedDiffAsync(
                repository.RepositoryRootPath,
                normalizedPath,
                cancellationToken).ConfigureAwait(false);
            return BuildDiffResult(
                untracked.Status,
                repository.RepositoryRootPath,
                normalizedPath,
                EmptyDiffSection(GitDiffSectionKind.Staged),
                untracked.Section);
        }

        var staged = await ExecuteTrackedDiffSectionAsync(
            repository.RepositoryRootPath,
            normalizedPath,
            GitDiffSectionKind.Staged,
            cancellationToken).ConfigureAwait(false);
        if (staged.Status != GitDiffQueryStatus.Success)
        {
            return BuildDiffResult(
                staged.Status,
                repository.RepositoryRootPath,
                normalizedPath,
                staged.Section,
                EmptyDiffSection(GitDiffSectionKind.WorkTree));
        }

        var workTree = await ExecuteTrackedDiffSectionAsync(
            repository.RepositoryRootPath,
            normalizedPath,
            GitDiffSectionKind.WorkTree,
            cancellationToken).ConfigureAwait(false);

        var finalStatus = workTree.Status == GitDiffQueryStatus.Success
            ? GitDiffQueryStatus.Success
            : workTree.Status;
        return BuildDiffResult(
            finalStatus,
            repository.RepositoryRootPath,
            normalizedPath,
            staged.Section,
            workTree.Section);
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

    private async Task<DiffSectionQueryResult> ExecuteTrackedDiffSectionAsync(
        string repositoryRoot,
        string repositoryRelativePath,
        GitDiffSectionKind kind,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "-c",
            "core.quotePath=false",
            "diff",
        };

        if (kind == GitDiffSectionKind.Staged)
        {
            arguments.Add("--cached");
        }

        arguments.Add("--no-color");
        arguments.Add("--no-ext-diff");
        arguments.Add("--no-textconv");
        arguments.Add("--find-renames");
        arguments.Add("--unified=3");
        arguments.Add("--");
        arguments.Add($":(literal){repositoryRelativePath}");

        var command = await ExecuteGitAsync(
            repositoryRoot,
            arguments,
            _diffTimeout,
            cancellationToken,
            _maxDiffCharacters).ConfigureAwait(false);

        if (!command.Started)
        {
            return new DiffSectionQueryResult(
                GitDiffQueryStatus.GitUnavailable,
                EmptyDiffSection(kind));
        }

        if (command.TimedOut || command.ExitCode != 0)
        {
            return new DiffSectionQueryResult(
                GitDiffQueryStatus.QueryFailed,
                EmptyDiffSection(kind));
        }

        if (command.StandardOutputTruncated)
        {
            return new DiffSectionQueryResult(
                GitDiffQueryStatus.TooLarge,
                new GitDiffSection(kind, string.Empty, IsBinary: false, WasTruncated: true));
        }

        return new DiffSectionQueryResult(
            GitDiffQueryStatus.Success,
            new GitDiffSection(
                kind,
                command.StandardOutput,
                IsBinaryDiff(command.StandardOutput),
                WasTruncated: false));
    }

    private async Task<DiffSectionQueryResult> ExecuteUntrackedDiffAsync(
        string repositoryRoot,
        string repositoryRelativePath,
        CancellationToken cancellationToken)
    {
        var fullPath = ResolveRepositoryRelativePath(repositoryRoot, repositoryRelativePath);
        if (!File.Exists(fullPath))
        {
            return new DiffSectionQueryResult(
                GitDiffQueryStatus.QueryFailed,
                EmptyDiffSection(GitDiffSectionKind.WorkTree));
        }

        var emptyFilePath = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
        var arguments = new List<string>
        {
            "-c",
            "core.quotePath=false",
            "diff",
            "--no-index",
            "--no-color",
            "--no-ext-diff",
            "--no-textconv",
            "--unified=3",
            "--",
            emptyFilePath,
            fullPath,
        };

        var command = await ExecuteGitAsync(
            repositoryRoot,
            arguments,
            _diffTimeout,
            cancellationToken,
            _maxDiffCharacters).ConfigureAwait(false);

        if (!command.Started)
        {
            return new DiffSectionQueryResult(
                GitDiffQueryStatus.GitUnavailable,
                EmptyDiffSection(GitDiffSectionKind.WorkTree));
        }

        if (command.TimedOut || command.ExitCode is not (0 or 1))
        {
            return new DiffSectionQueryResult(
                GitDiffQueryStatus.QueryFailed,
                EmptyDiffSection(GitDiffSectionKind.WorkTree));
        }

        if (command.StandardOutputTruncated)
        {
            return new DiffSectionQueryResult(
                GitDiffQueryStatus.TooLarge,
                new GitDiffSection(
                    GitDiffSectionKind.WorkTree,
                    string.Empty,
                    IsBinary: false,
                    WasTruncated: true,
                    IsNewFile: true));
        }

        return new DiffSectionQueryResult(
            GitDiffQueryStatus.Success,
            new GitDiffSection(
                GitDiffSectionKind.WorkTree,
                command.StandardOutput,
                IsBinaryDiff(command.StandardOutput),
                WasTruncated: false,
                IsNewFile: true));
    }

    private async Task<GitCommandResult> ExecuteGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        int? maxStandardOutputCharacters = null)
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
        startInfo.Environment["GIT_PAGER"] = "cat";
        startInfo.Environment["PAGER"] = "cat";
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

        var standardOutputTask = ReadProcessOutputAsync(
            process.StandardOutput,
            maxStandardOutputCharacters);
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
            return new GitCommandResult(
                true,
                null,
                cancelledOutput.Text,
                cancelledError,
                TimedOut: true,
                cancelledOutput.WasTruncated);
        }

        var standardOutput = await standardOutputTask.ConfigureAwait(false);
        return new GitCommandResult(
            true,
            process.ExitCode,
            standardOutput.Text,
            await standardErrorTask.ConfigureAwait(false),
            TimedOut: false,
            standardOutput.WasTruncated);
    }

    private static async Task<BoundedTextResult> ReadProcessOutputAsync(
        StreamReader reader,
        int? maxCharacters)
    {
        if (maxCharacters is null)
        {
            return new BoundedTextResult(
                await reader.ReadToEndAsync(CancellationToken.None).ConfigureAwait(false),
                WasTruncated: false);
        }

        var builder = new StringBuilder(Math.Min(maxCharacters.Value, 64 * 1024));
        var buffer = new char[8192];
        var wasTruncated = false;

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            var remaining = maxCharacters.Value - builder.Length;
            if (remaining > 0)
            {
                builder.Append(buffer, 0, Math.Min(remaining, read));
            }

            if (read > remaining)
            {
                wasTruncated = true;
            }
        }

        return new BoundedTextResult(builder.ToString(), wasTruncated);
    }

    private static GitStatusResult EmptyStatus(GitStatusQueryStatus status) =>
        new(status, null, Array.Empty<GitFileStatusEntry>());

    private static GitFileDiffResult EmptyDiff(
        GitDiffQueryStatus status,
        string repositoryRelativePath,
        string? repositoryRoot = null) =>
        BuildDiffResult(
            status,
            repositoryRoot,
            repositoryRelativePath,
            EmptyDiffSection(GitDiffSectionKind.Staged),
            EmptyDiffSection(GitDiffSectionKind.WorkTree));

    private static GitFileDiffResult BuildDiffResult(
        GitDiffQueryStatus status,
        string? repositoryRoot,
        string repositoryRelativePath,
        GitDiffSection staged,
        GitDiffSection workTree) =>
        new(status, repositoryRoot, repositoryRelativePath, staged, workTree);

    private static GitDiffSection EmptyDiffSection(GitDiffSectionKind kind) =>
        new(kind, string.Empty, IsBinary: false, WasTruncated: false);

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

    private static string NormalizeRepositoryRelativePath(string repositoryRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativePath);
        var normalized = repositoryRelativePath.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        if (normalized.Length == 0 || normalized.StartsWith("/", StringComparison.Ordinal) || Path.IsPathRooted(normalized))
        {
            throw new ArgumentException("Git diff path must be repository-relative.", nameof(repositoryRelativePath));
        }

        var segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Any(static segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException("Git diff path must not contain empty, current-directory, or parent-directory segments.", nameof(repositoryRelativePath));
        }

        return string.Join('/', segments);
    }

    private static string ResolveRepositoryRelativePath(string repositoryRoot, string repositoryRelativePath)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        var nativeRelativePath = repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, nativeRelativePath));
        var rootPrefix = fullRoot + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!fullPath.StartsWith(rootPrefix, comparison))
        {
            throw new ArgumentException("Git diff path escapes the detected repository root.", nameof(repositoryRelativePath));
        }

        return fullPath;
    }

    private static bool IsBinaryDiff(string patch) =>
        patch.Contains("GIT binary patch", StringComparison.Ordinal)
        || patch.Contains("Binary files ", StringComparison.Ordinal);

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

    private sealed record DiffSectionQueryResult(
        GitDiffQueryStatus Status,
        GitDiffSection Section);

    private sealed record BoundedTextResult(
        string Text,
        bool WasTruncated);

    private sealed record GitCommandResult(
        bool Started,
        int? ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut,
        bool StandardOutputTruncated)
    {
        public static GitCommandResult NotStarted { get; } =
            new(false, null, string.Empty, string.Empty, TimedOut: false, StandardOutputTruncated: false);
    }
}
