using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using FCCCodeDesktop.Application.Git;

namespace FCCCodeDesktop.Git;

/// <summary>
/// Performs bounded, explicit Git index mutations while leaving work-tree contents untouched.
/// </summary>
public sealed class GitCliIndexService : IGitIndexService
{
    public static readonly TimeSpan DefaultMutationTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan MaximumMutationTimeout = TimeSpan.FromSeconds(60);

    public const int MaximumMutationPaths = 64;
    public const int MaximumMutationPathCharacters = 12 * 1024;
    private const int MaximumEffectivePathCharacters = 24 * 1024;
    private const int MaximumFailureMessageCharacters = 4096;

    private readonly IGitService _gitService;
    private readonly string _gitExecutable;
    private readonly TimeSpan _mutationTimeout;

    public GitCliIndexService(
        string gitExecutable = "git",
        TimeSpan? mutationTimeout = null)
        : this(new GitCliService(gitExecutable), gitExecutable, mutationTimeout)
    {
    }

    public GitCliIndexService(
        IGitService gitService,
        string gitExecutable = "git",
        TimeSpan? mutationTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(gitService);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitExecutable);

        var resolvedTimeout = mutationTimeout ?? DefaultMutationTimeout;
        if (resolvedTimeout <= TimeSpan.Zero || resolvedTimeout > MaximumMutationTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mutationTimeout),
                mutationTimeout,
                $"Git index mutation timeout must be greater than zero and no more than {MaximumMutationTimeout.TotalSeconds} seconds.");
        }

        _gitService = gitService;
        _gitExecutable = gitExecutable;
        _mutationTimeout = resolvedTimeout;
    }

    public Task<GitIndexMutationResult> StageAsync(
        string path,
        IReadOnlyCollection<string> repositoryRelativePaths,
        CancellationToken cancellationToken = default) =>
        MutateAsync(path, repositoryRelativePaths, GitIndexMutationKind.Stage, cancellationToken);

    public Task<GitIndexMutationResult> UnstageAsync(
        string path,
        IReadOnlyCollection<string> repositoryRelativePaths,
        CancellationToken cancellationToken = default) =>
        MutateAsync(path, repositoryRelativePaths, GitIndexMutationKind.Unstage, cancellationToken);

    private async Task<GitIndexMutationResult> MutateAsync(
        string path,
        IReadOnlyCollection<string> repositoryRelativePaths,
        GitIndexMutationKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var requestedPaths = NormalizeMutationPaths(repositoryRelativePaths);
        cancellationToken.ThrowIfCancellationRequested();

        var detection = await _gitService.DetectRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
        switch (detection.Status)
        {
            case GitRepositoryDetectionStatus.NotRepository:
                return EmptyResult(GitIndexMutationStatus.NotRepository, kind, requestedPaths);
            case GitRepositoryDetectionStatus.GitUnavailable:
                return EmptyResult(GitIndexMutationStatus.GitUnavailable, kind, requestedPaths);
            case GitRepositoryDetectionStatus.ProbeFailed:
                return EmptyResult(GitIndexMutationStatus.QueryFailed, kind, requestedPaths);
            case GitRepositoryDetectionStatus.Repository:
                break;
            default:
                return EmptyResult(GitIndexMutationStatus.QueryFailed, kind, requestedPaths);
        }

        var repository = detection.Repository;
        if (repository is null)
        {
            return EmptyResult(GitIndexMutationStatus.QueryFailed, kind, requestedPaths);
        }

        if (repository.Kind == GitRepositoryKind.Bare)
        {
            return new GitIndexMutationResult(
                GitIndexMutationStatus.BareRepository,
                kind,
                repository.RepositoryRootPath,
                requestedPaths,
                requestedPaths);
        }

        var status = await _gitService.GetStatusAsync(repository.RepositoryRootPath, cancellationToken).ConfigureAwait(false);
        if (status.Status != GitStatusQueryStatus.Success)
        {
            return new GitIndexMutationResult(
                MapStatus(status.Status),
                kind,
                repository.RepositoryRootPath,
                requestedPaths,
                requestedPaths);
        }

        var effectivePaths = ExpandRenamePairs(requestedPaths, status.Files);
        if (effectivePaths.Sum(static pathValue => pathValue.Length) > MaximumEffectivePathCharacters)
        {
            throw new ArgumentException(
                $"Expanded Git index mutation paths exceed the {MaximumEffectivePathCharacters}-character safety limit.",
                nameof(repositoryRelativePaths));
        }

        var commandResult = kind == GitIndexMutationKind.Stage
            ? await ExecuteStageAsync(repository.RepositoryRootPath, effectivePaths, cancellationToken).ConfigureAwait(false)
            : await ExecuteUnstageAsync(repository.RepositoryRootPath, effectivePaths, cancellationToken).ConfigureAwait(false);

        if (!commandResult.Started)
        {
            return new GitIndexMutationResult(
                GitIndexMutationStatus.GitUnavailable,
                kind,
                repository.RepositoryRootPath,
                requestedPaths,
                effectivePaths);
        }

        if (commandResult.TimedOut || commandResult.ExitCode != 0)
        {
            return new GitIndexMutationResult(
                GitIndexMutationStatus.QueryFailed,
                kind,
                repository.RepositoryRootPath,
                requestedPaths,
                effectivePaths,
                NormalizeFailureMessage(commandResult.StandardError));
        }

        return new GitIndexMutationResult(
            GitIndexMutationStatus.Success,
            kind,
            repository.RepositoryRootPath,
            requestedPaths,
            effectivePaths);
    }

    private Task<GitCommandResult> ExecuteStageAsync(
        string repositoryRoot,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>(paths.Count + 2)
        {
            "add",
            "--",
        };
        AddLiteralPathspecs(arguments, paths);
        return ExecuteGitAsync(repositoryRoot, arguments, cancellationToken);
    }

    private async Task<GitCommandResult> ExecuteUnstageAsync(
        string repositoryRoot,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var headProbe = await ExecuteGitAsync(
            repositoryRoot,
            ["rev-parse", "--verify", "--quiet", "HEAD"],
            cancellationToken).ConfigureAwait(false);
        if (!headProbe.Started || headProbe.TimedOut || headProbe.ExitCode is not (0 or 1))
        {
            return headProbe.Started && !headProbe.TimedOut
                ? headProbe with { ExitCode = headProbe.ExitCode ?? 1 }
                : headProbe;
        }

        var arguments = new List<string>(paths.Count + 5);
        if (headProbe.ExitCode == 0)
        {
            arguments.Add("restore");
            arguments.Add("--staged");
            arguments.Add("--");
        }
        else
        {
            // An unborn repository has no HEAD to restore from. Removing only cached entries
            // is the index-only equivalent and deliberately leaves work-tree files untouched.
            arguments.Add("rm");
            arguments.Add("--cached");
            arguments.Add("--force");
            arguments.Add("--ignore-unmatch");
            arguments.Add("--");
        }

        AddLiteralPathspecs(arguments, paths);
        return await ExecuteGitAsync(repositoryRoot, arguments, cancellationToken).ConfigureAwait(false);
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
        startInfo.Environment["GIT_EDITOR"] = "true";
        startInfo.Environment["GIT_SEQUENCE_EDITOR"] = "true";
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
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
        timeoutSource.CancelAfter(_mutationTimeout);

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
                Started: true,
                ExitCode: null,
                cancelledOutput,
                cancelledError,
                TimedOut: true);
        }

        return new GitCommandResult(
            Started: true,
            process.ExitCode,
            await standardOutputTask.ConfigureAwait(false),
            await standardErrorTask.ConfigureAwait(false),
            TimedOut: false);
    }

    private static IReadOnlyList<string> NormalizeMutationPaths(
        IReadOnlyCollection<string> repositoryRelativePaths)
    {
        ArgumentNullException.ThrowIfNull(repositoryRelativePaths);
        if (repositoryRelativePaths.Count == 0 || repositoryRelativePaths.Count > MaximumMutationPaths)
        {
            throw new ArgumentException(
                $"Git index mutation requires between 1 and {MaximumMutationPaths} explicit repository-relative paths.",
                nameof(repositoryRelativePaths));
        }

        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var unique = new HashSet<string>(comparer);
        var normalizedPaths = new List<string>(repositoryRelativePaths.Count);
        var totalCharacters = 0;

        foreach (var pathValue in repositoryRelativePaths)
        {
            var normalized = NormalizeRepositoryRelativePath(pathValue);
            if (unique.Add(normalized))
            {
                normalizedPaths.Add(normalized);
                totalCharacters += normalized.Length;
            }
        }

        if (normalizedPaths.Count == 0 || totalCharacters > MaximumMutationPathCharacters)
        {
            throw new ArgumentException(
                $"Git index mutation paths exceed the {MaximumMutationPathCharacters}-character safety limit.",
                nameof(repositoryRelativePaths));
        }

        return normalizedPaths;
    }

    private static string NormalizeRepositoryRelativePath(string repositoryRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativePath);
        var normalized = repositoryRelativePath.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        if (normalized.Length == 0 || normalized[0] == '/' || Path.IsPathRooted(normalized))
        {
            throw new ArgumentException("Git index mutation path must be repository-relative.", nameof(repositoryRelativePath));
        }

        var segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Any(static segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException(
                "Git index mutation path must not contain empty, current-directory, or parent-directory segments.",
                nameof(repositoryRelativePath));
        }

        if (string.Equals(segments[0], ".git", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Git repository metadata cannot be staged or unstaged.", nameof(repositoryRelativePath));
        }

        return string.Join('/', segments);
    }

    private static IReadOnlyList<string> ExpandRenamePairs(
        IReadOnlyList<string> requestedPaths,
        IReadOnlyList<GitFileStatusEntry> statusEntries)
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var effective = new List<string>(requestedPaths);
        var seen = new HashSet<string>(requestedPaths, comparer);

        foreach (var entry in statusEntries)
        {
            if (entry.OriginalPath is null
                || (entry.IndexChange != GitFileChangeKind.Renamed
                    && entry.WorkTreeChange != GitFileChangeKind.Renamed))
            {
                continue;
            }

            var selected = seen.Contains(entry.Path) || seen.Contains(entry.OriginalPath);
            if (!selected)
            {
                continue;
            }

            if (seen.Add(entry.Path))
            {
                effective.Add(entry.Path);
            }

            if (seen.Add(entry.OriginalPath))
            {
                effective.Add(entry.OriginalPath);
            }
        }

        return effective;
    }

    private static void AddLiteralPathspecs(List<string> arguments, IReadOnlyList<string> paths)
    {
        foreach (var pathValue in paths)
        {
            arguments.Add($":(literal){pathValue}");
        }
    }

    private static GitIndexMutationStatus MapStatus(GitStatusQueryStatus status) =>
        status switch
        {
            GitStatusQueryStatus.NotRepository => GitIndexMutationStatus.NotRepository,
            GitStatusQueryStatus.BareRepository => GitIndexMutationStatus.BareRepository,
            GitStatusQueryStatus.GitUnavailable => GitIndexMutationStatus.GitUnavailable,
            _ => GitIndexMutationStatus.QueryFailed,
        };

    private static GitIndexMutationResult EmptyResult(
        GitIndexMutationStatus status,
        GitIndexMutationKind kind,
        IReadOnlyList<string> requestedPaths) =>
        new(status, kind, null, requestedPaths, requestedPaths);

    private static string? NormalizeFailureMessage(string standardError)
    {
        var normalized = standardError.Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= MaximumFailureMessageCharacters
            ? normalized
            : normalized[..MaximumFailureMessageCharacters];
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
        }
    }

    private sealed record GitCommandResult(
        bool Started,
        int? ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut)
    {
        public static GitCommandResult NotStarted { get; } =
            new(false, null, string.Empty, string.Empty, TimedOut: false);
    }
}
