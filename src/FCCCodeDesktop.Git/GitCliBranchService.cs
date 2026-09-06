using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using FCCCodeDesktop.Application.Git;

namespace FCCCodeDesktop.Git;

/// <summary>
/// Performs bounded local branch create/checkout operations without forcing or discarding owner work.
/// </summary>
public sealed class GitCliBranchService : IGitBranchService
{
    public static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan MaximumOperationTimeout = TimeSpan.FromSeconds(60);

    public const int MaximumBranchNameCharacters = 1024;
    private const int MaximumFailureMessageCharacters = 4096;

    private readonly IGitService _gitService;
    private readonly string _gitExecutable;
    private readonly TimeSpan _operationTimeout;

    public GitCliBranchService(
        string gitExecutable = "git",
        TimeSpan? operationTimeout = null)
        : this(new GitCliService(gitExecutable), gitExecutable, operationTimeout)
    {
    }

    public GitCliBranchService(
        IGitService gitService,
        string gitExecutable = "git",
        TimeSpan? operationTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(gitService);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitExecutable);

        var resolvedTimeout = operationTimeout ?? DefaultOperationTimeout;
        if (resolvedTimeout <= TimeSpan.Zero || resolvedTimeout > MaximumOperationTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationTimeout),
                operationTimeout,
                $"Git branch operation timeout must be greater than zero and no more than {MaximumOperationTimeout.TotalSeconds} seconds.");
        }

        _gitService = gitService;
        _gitExecutable = gitExecutable;
        _operationTimeout = resolvedTimeout;
    }

    public Task<GitBranchMutationResult> CreateAndCheckoutAsync(
        string path,
        string branchName,
        CancellationToken cancellationToken = default) =>
        MutateAsync(path, branchName, GitBranchMutationKind.CreateAndCheckout, cancellationToken);

    public Task<GitBranchMutationResult> CheckoutAsync(
        string path,
        string branchName,
        CancellationToken cancellationToken = default) =>
        MutateAsync(path, branchName, GitBranchMutationKind.Checkout, cancellationToken);

    private async Task<GitBranchMutationResult> MutateAsync(
        string path,
        string branchName,
        GitBranchMutationKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        if (branchName.Length > MaximumBranchNameCharacters)
        {
            throw new ArgumentException(
                $"Git branch name exceeds the {MaximumBranchNameCharacters}-character safety limit.",
                nameof(branchName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var detection = await _gitService.DetectRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
        switch (detection.Status)
        {
            case GitRepositoryDetectionStatus.NotRepository:
                return EmptyResult(GitBranchMutationStatus.NotRepository, kind, branchName);
            case GitRepositoryDetectionStatus.GitUnavailable:
                return EmptyResult(GitBranchMutationStatus.GitUnavailable, kind, branchName);
            case GitRepositoryDetectionStatus.ProbeFailed:
                return EmptyResult(GitBranchMutationStatus.QueryFailed, kind, branchName);
            case GitRepositoryDetectionStatus.Repository:
                break;
            default:
                return EmptyResult(GitBranchMutationStatus.QueryFailed, kind, branchName);
        }

        var repository = detection.Repository;
        if (repository is null)
        {
            return EmptyResult(GitBranchMutationStatus.QueryFailed, kind, branchName);
        }

        if (repository.Kind == GitRepositoryKind.Bare)
        {
            return RepositoryResult(
                GitBranchMutationStatus.BareRepository,
                kind,
                branchName,
                repository.RepositoryRootPath);
        }

        var repositoryRoot = repository.RepositoryRootPath;
        var validation = await ExecuteGitAsync(
            repositoryRoot,
            ["check-ref-format", "--branch", branchName],
            cancellationToken).ConfigureAwait(false);
        if (!validation.Started)
        {
            return RepositoryResult(GitBranchMutationStatus.GitUnavailable, kind, branchName, repositoryRoot);
        }

        if (validation.TimedOut)
        {
            return RepositoryResult(
                GitBranchMutationStatus.QueryFailed,
                kind,
                branchName,
                repositoryRoot,
                failureMessage: "Git branch-name validation timed out.");
        }

        if (validation.ExitCode != 0)
        {
            return RepositoryResult(
                GitBranchMutationStatus.InvalidBranchName,
                kind,
                branchName,
                repositoryRoot,
                failureMessage: NormalizeFailureMessage(validation.StandardError));
        }

        var previousBranch = await ReadCurrentBranchAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!previousBranch.Started)
        {
            return RepositoryResult(GitBranchMutationStatus.GitUnavailable, kind, branchName, repositoryRoot);
        }

        if (previousBranch.TimedOut || previousBranch.ExitCode is not (0 or 1))
        {
            return RepositoryResult(
                GitBranchMutationStatus.QueryFailed,
                kind,
                branchName,
                repositoryRoot,
                failureMessage: NormalizeFailureMessage(previousBranch.StandardError));
        }

        var previousBranchName = NormalizeBranchOutput(previousBranch.StandardOutput);
        var existence = await ExecuteGitAsync(
            repositoryRoot,
            ["show-ref", "--verify", "--quiet", $"refs/heads/{branchName}"],
            cancellationToken).ConfigureAwait(false);
        if (!existence.Started)
        {
            return RepositoryResult(
                GitBranchMutationStatus.GitUnavailable,
                kind,
                branchName,
                repositoryRoot,
                previousBranchName);
        }

        if (existence.TimedOut || existence.ExitCode is not (0 or 1))
        {
            return RepositoryResult(
                GitBranchMutationStatus.QueryFailed,
                kind,
                branchName,
                repositoryRoot,
                previousBranchName,
                failureMessage: NormalizeFailureMessage(existence.StandardError));
        }

        var branchExists = existence.ExitCode == 0;
        if (kind == GitBranchMutationKind.CreateAndCheckout && branchExists)
        {
            return RepositoryResult(
                GitBranchMutationStatus.BranchAlreadyExists,
                kind,
                branchName,
                repositoryRoot,
                previousBranchName,
                previousBranchName);
        }

        if (kind == GitBranchMutationKind.Checkout && !branchExists)
        {
            return RepositoryResult(
                GitBranchMutationStatus.BranchNotFound,
                kind,
                branchName,
                repositoryRoot,
                previousBranchName,
                previousBranchName);
        }

        IReadOnlyList<string> switchArguments = kind == GitBranchMutationKind.CreateAndCheckout
            ? ["switch", "--create", branchName]
            : ["switch", branchName];
        var switchResult = await ExecuteGitAsync(repositoryRoot, switchArguments, cancellationToken).ConfigureAwait(false);
        if (!switchResult.Started)
        {
            return RepositoryResult(
                GitBranchMutationStatus.GitUnavailable,
                kind,
                branchName,
                repositoryRoot,
                previousBranchName,
                previousBranchName);
        }

        if (switchResult.TimedOut)
        {
            return RepositoryResult(
                GitBranchMutationStatus.QueryFailed,
                kind,
                branchName,
                repositoryRoot,
                previousBranchName,
                previousBranchName,
                "Git branch switch timed out.");
        }

        if (switchResult.ExitCode != 0)
        {
            var currentAfterFailure = await TryReadCurrentBranchNameAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
            return RepositoryResult(
                GitBranchMutationStatus.CheckoutBlocked,
                kind,
                branchName,
                repositoryRoot,
                previousBranchName,
                currentAfterFailure ?? previousBranchName,
                NormalizeFailureMessage(switchResult.StandardError));
        }

        var currentBranch = await ReadCurrentBranchAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!currentBranch.Started || currentBranch.TimedOut || currentBranch.ExitCode != 0)
        {
            return RepositoryResult(
                currentBranch.Started ? GitBranchMutationStatus.QueryFailed : GitBranchMutationStatus.GitUnavailable,
                kind,
                branchName,
                repositoryRoot,
                previousBranchName,
                failureMessage: NormalizeFailureMessage(currentBranch.StandardError));
        }

        var currentBranchName = NormalizeBranchOutput(currentBranch.StandardOutput);
        if (!string.Equals(currentBranchName, branchName, StringComparison.Ordinal))
        {
            return RepositoryResult(
                GitBranchMutationStatus.QueryFailed,
                kind,
                branchName,
                repositoryRoot,
                previousBranchName,
                currentBranchName,
                "Git completed branch switch but reported an unexpected current branch.");
        }

        return RepositoryResult(
            GitBranchMutationStatus.Success,
            kind,
            branchName,
            repositoryRoot,
            previousBranchName,
            currentBranchName);
    }

    private Task<GitCommandResult> ReadCurrentBranchAsync(
        string repositoryRoot,
        CancellationToken cancellationToken) =>
        ExecuteGitAsync(
            repositoryRoot,
            ["symbolic-ref", "--quiet", "--short", "HEAD"],
            cancellationToken);

    private async Task<string?> TryReadCurrentBranchNameAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var result = await ReadCurrentBranchAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        return result.Started && !result.TimedOut && result.ExitCode == 0
            ? NormalizeBranchOutput(result.StandardOutput)
            : null;
    }

    private async Task<GitCommandResult> ExecuteGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        GitCommandSafetyPolicy.EnsureAllowed(arguments);

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
        timeoutSource.CancelAfter(_operationTimeout);

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

    private static GitBranchMutationResult EmptyResult(
        GitBranchMutationStatus status,
        GitBranchMutationKind kind,
        string branchName) =>
        new(status, kind, branchName, null, null, null);

    private static GitBranchMutationResult RepositoryResult(
        GitBranchMutationStatus status,
        GitBranchMutationKind kind,
        string branchName,
        string repositoryRoot,
        string? previousBranchName = null,
        string? currentBranchName = null,
        string? failureMessage = null) =>
        new(
            status,
            kind,
            branchName,
            repositoryRoot,
            previousBranchName,
            currentBranchName,
            failureMessage);

    private static string? NormalizeBranchOutput(string output)
    {
        var normalized = output.Trim();
        return normalized.Length == 0 ? null : normalized;
    }

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
