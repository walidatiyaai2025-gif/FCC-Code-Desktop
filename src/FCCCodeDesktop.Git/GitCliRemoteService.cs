using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using FCCCodeDesktop.Application.Git;

namespace FCCCodeDesktop.Git;

/// <summary>
/// Performs bounded Git fetch and clean-tree fast-forward pull operations without rewriting owner work.
/// </summary>
public sealed class GitCliRemoteService : IGitRemoteService
{
    public static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan MaximumOperationTimeout = TimeSpan.FromMinutes(5);

    public const int MaximumRemoteNameCharacters = 256;
    public const int MaximumRemoteBranchCharacters = 1024;
    private const int MaximumFailureMessageCharacters = 4096;

    private readonly IGitService _gitService;
    private readonly string _gitExecutable;
    private readonly TimeSpan _operationTimeout;

    public GitCliRemoteService(
        string gitExecutable = "git",
        TimeSpan? operationTimeout = null)
        : this(new GitCliService(gitExecutable), gitExecutable, operationTimeout)
    {
    }

    public GitCliRemoteService(
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
                $"Git remote operation timeout must be greater than zero and no more than {MaximumOperationTimeout.TotalSeconds} seconds.");
        }

        _gitService = gitService;
        _gitExecutable = gitExecutable;
        _operationTimeout = resolvedTimeout;
    }

    public async Task<GitRemoteSyncResult> FetchAsync(
        string path,
        string remoteName = "origin",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSafeRemoteName(remoteName))
        {
            return EmptyResult(GitRemoteSyncStatus.InvalidRemoteName, GitRemoteSyncKind.Fetch, remoteName);
        }

        var repositoryResult = await ResolveWorkTreeRepositoryAsync(
            path,
            GitRemoteSyncKind.Fetch,
            remoteName,
            remoteBranchName: null,
            cancellationToken).ConfigureAwait(false);
        if (repositoryResult.Failure is not null)
        {
            return repositoryResult.Failure;
        }

        var repositoryRoot = repositoryResult.RepositoryRoot!;
        var remoteCheck = await CheckRemoteExistsAsync(repositoryRoot, remoteName, cancellationToken).ConfigureAwait(false);
        if (!remoteCheck.Started)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.GitUnavailable,
                GitRemoteSyncKind.Fetch,
                remoteName,
                null,
                repositoryRoot);
        }

        if (remoteCheck.TimedOut)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.QueryFailed,
                GitRemoteSyncKind.Fetch,
                remoteName,
                null,
                repositoryRoot,
                failureMessage: "Git remote lookup timed out.");
        }

        if (remoteCheck.ExitCode != 0)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.RemoteNotFound,
                GitRemoteSyncKind.Fetch,
                remoteName,
                null,
                repositoryRoot,
                failureMessage: NormalizeFailureMessage(remoteCheck.StandardError));
        }

        var previousHeadResult = await ReadHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!IsSuccessfulCommand(previousHeadResult))
        {
            return QueryFailureFromCommand(
                GitRemoteSyncKind.Fetch,
                remoteName,
                null,
                repositoryRoot,
                previousHeadResult,
                "Git HEAD lookup failed before fetch.");
        }

        var previousHead = NormalizeSingleLine(previousHeadResult.StandardOutput);
        var fetchResult = await ExecuteGitAsync(
            repositoryRoot,
            ["fetch", "--no-tags", "--no-recurse-submodules", remoteName],
            cancellationToken).ConfigureAwait(false);
        if (!fetchResult.Started)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.GitUnavailable,
                GitRemoteSyncKind.Fetch,
                remoteName,
                null,
                repositoryRoot,
                previousHead: previousHead,
                currentHead: previousHead);
        }

        if (fetchResult.TimedOut)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.RemoteFailure,
                GitRemoteSyncKind.Fetch,
                remoteName,
                null,
                repositoryRoot,
                previousHead: previousHead,
                currentHead: previousHead,
                failureMessage: "Git fetch timed out.");
        }

        if (fetchResult.ExitCode != 0)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.RemoteFailure,
                GitRemoteSyncKind.Fetch,
                remoteName,
                null,
                repositoryRoot,
                previousHead: previousHead,
                currentHead: previousHead,
                failureMessage: NormalizeFailureMessage(fetchResult.StandardError));
        }

        var currentHeadResult = await ReadHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!IsSuccessfulCommand(currentHeadResult))
        {
            return QueryFailureFromCommand(
                GitRemoteSyncKind.Fetch,
                remoteName,
                null,
                repositoryRoot,
                currentHeadResult,
                "Git HEAD lookup failed after fetch.",
                previousHead);
        }

        var currentHead = NormalizeSingleLine(currentHeadResult.StandardOutput);
        if (!string.Equals(previousHead, currentHead, StringComparison.Ordinal))
        {
            return RepositoryResult(
                GitRemoteSyncStatus.QueryFailed,
                GitRemoteSyncKind.Fetch,
                remoteName,
                null,
                repositoryRoot,
                previousHead: previousHead,
                currentHead: currentHead,
                failureMessage: "Repository HEAD changed while fetch was running; no pull mutation was attempted.");
        }

        return RepositoryResult(
            GitRemoteSyncStatus.Success,
            GitRemoteSyncKind.Fetch,
            remoteName,
            null,
            repositoryRoot,
            previousHead: previousHead,
            currentHead: currentHead);
    }

    public async Task<GitRemoteSyncResult> PullFastForwardAsync(
        string path,
        string remoteName,
        string remoteBranchName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSafeRemoteName(remoteName))
        {
            return EmptyResult(
                GitRemoteSyncStatus.InvalidRemoteName,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName);
        }

        if (string.IsNullOrWhiteSpace(remoteBranchName) || remoteBranchName.Length > MaximumRemoteBranchCharacters)
        {
            return EmptyResult(
                GitRemoteSyncStatus.InvalidRemoteBranch,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName);
        }

        var repositoryResult = await ResolveWorkTreeRepositoryAsync(
            path,
            GitRemoteSyncKind.PullFastForward,
            remoteName,
            remoteBranchName,
            cancellationToken).ConfigureAwait(false);
        if (repositoryResult.Failure is not null)
        {
            return repositoryResult.Failure;
        }

        var repositoryRoot = repositoryResult.RepositoryRoot!;
        var branchValidation = await ExecuteGitAsync(
            repositoryRoot,
            ["check-ref-format", "--branch", remoteBranchName],
            cancellationToken).ConfigureAwait(false);
        if (!branchValidation.Started)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.GitUnavailable,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot);
        }

        if (branchValidation.TimedOut || branchValidation.ExitCode != 0)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.InvalidRemoteBranch,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                failureMessage: branchValidation.TimedOut
                    ? "Git branch-name validation timed out."
                    : NormalizeFailureMessage(branchValidation.StandardError));
        }

        var remoteCheck = await CheckRemoteExistsAsync(repositoryRoot, remoteName, cancellationToken).ConfigureAwait(false);
        if (!remoteCheck.Started)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.GitUnavailable,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot);
        }

        if (remoteCheck.TimedOut)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.QueryFailed,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                failureMessage: "Git remote lookup timed out.");
        }

        if (remoteCheck.ExitCode != 0)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.RemoteNotFound,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                failureMessage: NormalizeFailureMessage(remoteCheck.StandardError));
        }

        var currentBranchResult = await ReadCurrentBranchAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!currentBranchResult.Started)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.GitUnavailable,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot);
        }

        if (currentBranchResult.TimedOut)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.QueryFailed,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                failureMessage: "Git current-branch lookup timed out.");
        }

        if (currentBranchResult.ExitCode != 0)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.DetachedHead,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                failureMessage: NormalizeFailureMessage(currentBranchResult.StandardError));
        }

        var currentBranch = NormalizeSingleLine(currentBranchResult.StandardOutput);
        var status = await _gitService.GetStatusAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        var statusFailure = MapStatusFailure(status, remoteName, remoteBranchName, repositoryRoot, currentBranch);
        if (statusFailure is not null)
        {
            return statusFailure;
        }

        if (!status.IsClean)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.DirtyWorkTree,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                failureMessage: "Fast-forward pull requires a clean index and work tree; no stash/reset/clean fallback was attempted.");
        }

        var previousHeadResult = await ReadHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!IsSuccessfulCommand(previousHeadResult))
        {
            return QueryFailureFromCommand(
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                previousHeadResult,
                "Git HEAD lookup failed before pull.",
                currentBranchName: currentBranch);
        }

        var previousHead = NormalizeSingleLine(previousHeadResult.StandardOutput);
        var fetchResult = await ExecuteGitAsync(
            repositoryRoot,
            ["fetch", "--no-tags", "--no-recurse-submodules", remoteName, remoteBranchName],
            cancellationToken).ConfigureAwait(false);
        if (!fetchResult.Started)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.GitUnavailable,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                previousHead);
        }

        if (fetchResult.TimedOut || fetchResult.ExitCode != 0)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.RemoteFailure,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                previousHead,
                fetchResult.TimedOut ? "Git fetch timed out during pull." : NormalizeFailureMessage(fetchResult.StandardError));
        }

        var fetchedHeadResult = await ExecuteGitAsync(
            repositoryRoot,
            ["rev-parse", "--verify", "FETCH_HEAD"],
            cancellationToken).ConfigureAwait(false);
        if (!IsSuccessfulCommand(fetchedHeadResult))
        {
            return QueryFailureFromCommand(
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                fetchedHeadResult,
                "Git did not expose a valid FETCH_HEAD after fetch.",
                previousHead,
                currentBranch);
        }

        var fetchedHead = NormalizeSingleLine(fetchedHeadResult.StandardOutput);
        var recheckBranch = await ReadCurrentBranchAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        var recheckHead = await ReadHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        var recheckStatus = await _gitService.GetStatusAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!IsSuccessfulCommand(recheckBranch) || !IsSuccessfulCommand(recheckHead))
        {
            return RepositoryResult(
                GitRemoteSyncStatus.PullBlocked,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                previousHead,
                "Repository branch/HEAD could not be revalidated after fetch; merge was not attempted.");
        }

        if (!string.Equals(currentBranch, NormalizeSingleLine(recheckBranch.StandardOutput), StringComparison.Ordinal) ||
            !string.Equals(previousHead, NormalizeSingleLine(recheckHead.StandardOutput), StringComparison.Ordinal))
        {
            return RepositoryResult(
                GitRemoteSyncStatus.PullBlocked,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                NormalizeSingleLine(recheckHead.StandardOutput),
                "Repository branch or HEAD changed concurrently after fetch; merge was not attempted.");
        }

        if (!recheckStatus.IsSuccess || !recheckStatus.IsClean)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.DirtyWorkTree,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                previousHead,
                "Repository became dirty after fetch; merge was not attempted.");
        }

        var ancestry = await ExecuteGitAsync(
            repositoryRoot,
            ["merge-base", "--is-ancestor", "HEAD", "FETCH_HEAD"],
            cancellationToken).ConfigureAwait(false);
        if (!ancestry.Started)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.GitUnavailable,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                previousHead);
        }

        if (ancestry.TimedOut)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.QueryFailed,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                previousHead,
                "Fast-forward ancestry check timed out.");
        }

        if (ancestry.ExitCode == 1)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.NonFastForward,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                previousHead,
                "Remote update is not a fast-forward of the current HEAD; no merge/rebase/reset was attempted.");
        }

        if (ancestry.ExitCode != 0)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.QueryFailed,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                previousHead,
                NormalizeFailureMessage(ancestry.StandardError));
        }

        if (string.Equals(previousHead, fetchedHead, StringComparison.Ordinal))
        {
            return RepositoryResult(
                GitRemoteSyncStatus.Success,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                previousHead);
        }

        var mergeResult = await ExecuteGitAsync(
            repositoryRoot,
            ["merge", "--ff-only", "--no-edit", "FETCH_HEAD"],
            cancellationToken).ConfigureAwait(false);
        if (!mergeResult.Started)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.GitUnavailable,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                previousHead);
        }

        if (mergeResult.TimedOut || mergeResult.ExitCode != 0)
        {
            var headAfterFailure = await TryReadHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
            return RepositoryResult(
                GitRemoteSyncStatus.PullBlocked,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                headAfterFailure ?? previousHead,
                mergeResult.TimedOut ? "Fast-forward merge timed out." : NormalizeFailureMessage(mergeResult.StandardError));
        }

        var finalHead = await TryReadHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        var finalStatus = await _gitService.GetStatusAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(finalHead, fetchedHead, StringComparison.Ordinal) || !finalStatus.IsSuccess || !finalStatus.IsClean)
        {
            return RepositoryResult(
                GitRemoteSyncStatus.QueryFailed,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch,
                previousHead,
                finalHead,
                "Fast-forward pull completed but final HEAD/work-tree verification did not match FETCH_HEAD cleanly.");
        }

        return RepositoryResult(
            GitRemoteSyncStatus.Success,
            GitRemoteSyncKind.PullFastForward,
            remoteName,
            remoteBranchName,
            repositoryRoot,
            currentBranch,
            previousHead,
            finalHead);
    }

    private async Task<RepositoryResolution> ResolveWorkTreeRepositoryAsync(
        string path,
        GitRemoteSyncKind kind,
        string remoteName,
        string? remoteBranchName,
        CancellationToken cancellationToken)
    {
        var detection = await _gitService.DetectRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
        return detection.Status switch
        {
            GitRepositoryDetectionStatus.NotRepository => new(
                null,
                EmptyResult(GitRemoteSyncStatus.NotRepository, kind, remoteName, remoteBranchName)),
            GitRepositoryDetectionStatus.GitUnavailable => new(
                null,
                EmptyResult(GitRemoteSyncStatus.GitUnavailable, kind, remoteName, remoteBranchName)),
            GitRepositoryDetectionStatus.ProbeFailed => new(
                null,
                EmptyResult(GitRemoteSyncStatus.QueryFailed, kind, remoteName, remoteBranchName)),
            GitRepositoryDetectionStatus.Repository when detection.Repository is null => new(
                null,
                EmptyResult(GitRemoteSyncStatus.QueryFailed, kind, remoteName, remoteBranchName)),
            GitRepositoryDetectionStatus.Repository when detection.Repository!.Kind == GitRepositoryKind.Bare => new(
                null,
                RepositoryResult(
                    GitRemoteSyncStatus.BareRepository,
                    kind,
                    remoteName,
                    remoteBranchName,
                    detection.Repository.RepositoryRootPath)),
            GitRepositoryDetectionStatus.Repository => new(detection.Repository!.RepositoryRootPath, null),
            _ => new(null, EmptyResult(GitRemoteSyncStatus.QueryFailed, kind, remoteName, remoteBranchName)),
        };
    }

    private Task<GitCommandResult> CheckRemoteExistsAsync(
        string repositoryRoot,
        string remoteName,
        CancellationToken cancellationToken) =>
        ExecuteGitAsync(repositoryRoot, ["remote", "get-url", remoteName], cancellationToken);

    private Task<GitCommandResult> ReadCurrentBranchAsync(
        string repositoryRoot,
        CancellationToken cancellationToken) =>
        ExecuteGitAsync(repositoryRoot, ["symbolic-ref", "--quiet", "--short", "HEAD"], cancellationToken);

    private Task<GitCommandResult> ReadHeadAsync(
        string repositoryRoot,
        CancellationToken cancellationToken) =>
        ExecuteGitAsync(repositoryRoot, ["rev-parse", "--verify", "HEAD"], cancellationToken);

    private async Task<string?> TryReadHeadAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        var result = await ReadHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        return IsSuccessfulCommand(result) ? NormalizeSingleLine(result.StandardOutput) : null;
    }

    private async Task<GitCommandResult> ExecuteGitAsync(
        string workingDirectory,
        List<string> arguments,
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

    private static GitRemoteSyncResult? MapStatusFailure(
        GitStatusResult status,
        string remoteName,
        string remoteBranchName,
        string repositoryRoot,
        string? currentBranch) =>
        status.Status switch
        {
            GitStatusQueryStatus.Success => null,
            GitStatusQueryStatus.NotRepository => RepositoryResult(
                GitRemoteSyncStatus.NotRepository,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch),
            GitStatusQueryStatus.BareRepository => RepositoryResult(
                GitRemoteSyncStatus.BareRepository,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch),
            GitStatusQueryStatus.GitUnavailable => RepositoryResult(
                GitRemoteSyncStatus.GitUnavailable,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch),
            _ => RepositoryResult(
                GitRemoteSyncStatus.QueryFailed,
                GitRemoteSyncKind.PullFastForward,
                remoteName,
                remoteBranchName,
                repositoryRoot,
                currentBranch),
        };

    private static GitRemoteSyncResult QueryFailureFromCommand(
        GitRemoteSyncKind kind,
        string remoteName,
        string? remoteBranchName,
        string repositoryRoot,
        GitCommandResult command,
        string fallbackMessage,
        string? previousHead = null,
        string? currentBranchName = null) =>
        RepositoryResult(
            command.Started ? GitRemoteSyncStatus.QueryFailed : GitRemoteSyncStatus.GitUnavailable,
            kind,
            remoteName,
            remoteBranchName,
            repositoryRoot,
            currentBranchName,
            previousHead,
            previousHead,
            command.TimedOut ? fallbackMessage : NormalizeFailureMessage(command.StandardError) ?? fallbackMessage);

    private static bool IsSuccessfulCommand(GitCommandResult result) =>
        result.Started && !result.TimedOut && result.ExitCode == 0;

    private static bool IsSafeRemoteName(string remoteName)
    {
        if (string.IsNullOrWhiteSpace(remoteName) ||
            remoteName.Length > MaximumRemoteNameCharacters ||
            remoteName[0] == '-')
        {
            return false;
        }

        foreach (var character in remoteName)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }

    private static GitRemoteSyncResult EmptyResult(
        GitRemoteSyncStatus status,
        GitRemoteSyncKind kind,
        string remoteName,
        string? remoteBranchName = null) =>
        new(status, kind, remoteName, remoteBranchName, null, null, null, null);

    private static GitRemoteSyncResult RepositoryResult(
        GitRemoteSyncStatus status,
        GitRemoteSyncKind kind,
        string remoteName,
        string? remoteBranchName,
        string repositoryRoot,
        string? currentBranchName = null,
        string? previousHead = null,
        string? currentHead = null,
        string? failureMessage = null) =>
        new(
            status,
            kind,
            remoteName,
            remoteBranchName,
            repositoryRoot,
            currentBranchName,
            previousHead,
            currentHead,
            failureMessage);

    private static string? NormalizeSingleLine(string value)
    {
        var normalized = value.Trim();
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

    private sealed record RepositoryResolution(string? RepositoryRoot, GitRemoteSyncResult? Failure);

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
