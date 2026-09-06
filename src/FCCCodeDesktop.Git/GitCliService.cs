using System.ComponentModel;
using System.Diagnostics;
using FCCCodeDesktop.Application.Git;

namespace FCCCodeDesktop.Git;

public sealed class GitCliService : IGitService
{
    public static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaximumProbeTimeout = TimeSpan.FromSeconds(30);

    private const string NotRepositoryMessage = "not a git repository";
    private readonly string _gitExecutable;
    private readonly TimeSpan _probeTimeout;

    public GitCliService(string gitExecutable = "git", TimeSpan? probeTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitExecutable);
        var resolvedTimeout = probeTimeout ?? DefaultProbeTimeout;
        if (resolvedTimeout <= TimeSpan.Zero || resolvedTimeout > MaximumProbeTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(probeTimeout),
                probeTimeout,
                $"Git probe timeout must be greater than zero and no more than {MaximumProbeTimeout.TotalSeconds} seconds.");
        }

        _gitExecutable = gitExecutable;
        _probeTimeout = resolvedTimeout;
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

    private async Task<GitRepositoryDetectionResult> BuildWorkTreeResultAsync(
        string probePath,
        CancellationToken cancellationToken)
    {
        var details = await ExecuteGitAsync(
            probePath,
            ["rev-parse", "--show-toplevel", "--absolute-git-dir"],
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
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(_gitExecutable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_probeTimeout);

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
