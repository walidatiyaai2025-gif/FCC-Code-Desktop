using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using FCCCodeDesktop.Application.Git;

namespace FCCCodeDesktop.Git;

/// <summary>
/// Performs bounded staged-index commits and non-force pushes of the current local branch.
/// </summary>
public sealed class GitCliCommitPushService : IGitCommitPushService
{
    public static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaximumOperationTimeout = TimeSpan.FromSeconds(90);

    public const int MaximumCommitMessageCharacters = 65_536;
    public const int MaximumRemoteNameCharacters = 1_024;
    private const int MaximumFailureMessageCharacters = 4_096;

    private readonly IGitService _gitService;
    private readonly string _gitExecutable;
    private readonly TimeSpan _operationTimeout;

    public GitCliCommitPushService(
        string gitExecutable = "git",
        TimeSpan? operationTimeout = null)
        : this(new GitCliService(gitExecutable), gitExecutable, operationTimeout)
    {
    }

    public GitCliCommitPushService(
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
                $"Git commit/push timeout must be greater than zero and no more than {MaximumOperationTimeout.TotalSeconds} seconds.");
        }

        _gitService = gitService;
        _gitExecutable = gitExecutable;
        _operationTimeout = resolvedTimeout;
    }

    public async Task<GitCommitPushResult> CommitAsync(
        string path,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (string.IsNullOrWhiteSpace(commitMessage) || commitMessage.Length > MaximumCommitMessageCharacters)
        {
            return EmptyResult(
                GitCommitPushStatus.InvalidCommitMessage,
                GitCommitPushKind.Commit,
                failureMessage: $"Commit message must contain non-whitespace text and be at most {MaximumCommitMessageCharacters} characters.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var preflight = await ResolveRepositoryAsync(path, GitCommitPushKind.Commit, cancellationToken).ConfigureAwait(false);
        if (preflight.Result is not null)
        {
            return preflight.Result;
        }

        var repositoryRoot = preflight.RepositoryRoot!;
        var currentBranch = await ReadCurrentBranchAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!currentBranch.Started)
        {
            return RepositoryResult(GitCommitPushStatus.GitUnavailable, GitCommitPushKind.Commit, repositoryRoot);
        }

        if (currentBranch.TimedOut)
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Commit,
                repositoryRoot,
                failureMessage: "Reading the current branch timed out.");
        }

        if (currentBranch.ExitCode == 1)
        {
            return RepositoryResult(GitCommitPushStatus.DetachedHead, GitCommitPushKind.Commit, repositoryRoot);
        }

        if (currentBranch.ExitCode != 0)
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Commit,
                repositoryRoot,
                failureMessage: NormalizeFailureMessage(currentBranch.StandardError));
        }

        var branchName = NormalizeOutput(currentBranch.StandardOutput);
        var staged = await ExecuteGitAsync(
            repositoryRoot,
            cancellationToken,
            "diff",
            "--cached",
            "--name-only",
            "-z",
            "--no-ext-diff",
            "--").ConfigureAwait(false);
        if (!staged.Started)
        {
            return RepositoryResult(GitCommitPushStatus.GitUnavailable, GitCommitPushKind.Commit, repositoryRoot, branchName);
        }

        if (staged.TimedOut || staged.ExitCode != 0)
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Commit,
                repositoryRoot,
                branchName,
                failureMessage: staged.TimedOut
                    ? "Checking staged changes timed out."
                    : NormalizeFailureMessage(staged.StandardError));
        }

        if (staged.StandardOutput.Length == 0)
        {
            return RepositoryResult(GitCommitPushStatus.NothingStaged, GitCommitPushKind.Commit, repositoryRoot, branchName);
        }

        var authorIdentity = await ExecuteGitAsync(
            repositoryRoot,
            cancellationToken,
            "var",
            "GIT_AUTHOR_IDENT").ConfigureAwait(false);
        var committerIdentity = await ExecuteGitAsync(
            repositoryRoot,
            cancellationToken,
            "var",
            "GIT_COMMITTER_IDENT").ConfigureAwait(false);
        if (!authorIdentity.Started || !committerIdentity.Started)
        {
            return RepositoryResult(GitCommitPushStatus.GitUnavailable, GitCommitPushKind.Commit, repositoryRoot, branchName);
        }

        if (authorIdentity.TimedOut || committerIdentity.TimedOut)
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Commit,
                repositoryRoot,
                branchName,
                failureMessage: "Resolving Git commit identity timed out.");
        }

        if (authorIdentity.ExitCode != 0 || committerIdentity.ExitCode != 0)
        {
            return RepositoryResult(
                GitCommitPushStatus.IdentityRequired,
                GitCommitPushKind.Commit,
                repositoryRoot,
                branchName,
                failureMessage: NormalizeFailureMessage(
                    authorIdentity.ExitCode != 0 ? authorIdentity.StandardError : committerIdentity.StandardError));
        }

        var beforeHead = await ReadHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        var commit = await ExecuteGitAsync(
            repositoryRoot,
            cancellationToken,
            "-c",
            "commit.gpgSign=false",
            "commit",
            "--no-verify",
            "--no-gpg-sign",
            "--cleanup=verbatim",
            "--message",
            commitMessage).ConfigureAwait(false);
        if (!commit.Started)
        {
            return RepositoryResult(GitCommitPushStatus.GitUnavailable, GitCommitPushKind.Commit, repositoryRoot, branchName);
        }

        if (commit.TimedOut)
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Commit,
                repositoryRoot,
                branchName,
                beforeHead,
                failureMessage: "Git commit timed out and its owned process tree was terminated.");
        }

        if (commit.ExitCode != 0)
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Commit,
                repositoryRoot,
                branchName,
                beforeHead,
                failureMessage: NormalizeFailureMessage(commit.StandardError));
        }

        var afterHead = await ReadHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(afterHead) || string.Equals(afterHead, beforeHead, StringComparison.Ordinal))
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Commit,
                repositoryRoot,
                branchName,
                afterHead,
                failureMessage: "Git reported commit success without producing a new HEAD commit.");
        }

        return RepositoryResult(
            GitCommitPushStatus.Success,
            GitCommitPushKind.Commit,
            repositoryRoot,
            branchName,
            afterHead);
    }

    public async Task<GitCommitPushResult> PushAsync(
        string path,
        string remoteName = "origin",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsValidRemoteName(remoteName))
        {
            return EmptyResult(
                GitCommitPushStatus.InvalidRemoteName,
                GitCommitPushKind.Push,
                remoteName,
                $"Remote name must be non-empty, must not start with '-', contain control characters, or exceed {MaximumRemoteNameCharacters} characters.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var preflight = await ResolveRepositoryAsync(path, GitCommitPushKind.Push, cancellationToken).ConfigureAwait(false);
        if (preflight.Result is not null)
        {
            return preflight.Result with { RemoteName = remoteName };
        }

        var repositoryRoot = preflight.RepositoryRoot!;
        var currentBranch = await ReadCurrentBranchAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!currentBranch.Started)
        {
            return RepositoryResult(GitCommitPushStatus.GitUnavailable, GitCommitPushKind.Push, repositoryRoot, remoteName: remoteName);
        }

        if (currentBranch.TimedOut)
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Push,
                repositoryRoot,
                remoteName: remoteName,
                failureMessage: "Reading the current branch timed out.");
        }

        if (currentBranch.ExitCode == 1)
        {
            return RepositoryResult(GitCommitPushStatus.DetachedHead, GitCommitPushKind.Push, repositoryRoot, remoteName: remoteName);
        }

        if (currentBranch.ExitCode != 0)
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Push,
                repositoryRoot,
                remoteName: remoteName,
                failureMessage: NormalizeFailureMessage(currentBranch.StandardError));
        }

        var branchName = NormalizeOutput(currentBranch.StandardOutput);
        var remote = await ExecuteGitAsync(
            repositoryRoot,
            cancellationToken,
            "remote",
            "get-url",
            "--all",
            remoteName).ConfigureAwait(false);
        if (!remote.Started)
        {
            return RepositoryResult(GitCommitPushStatus.GitUnavailable, GitCommitPushKind.Push, repositoryRoot, branchName, remoteName: remoteName);
        }

        if (remote.TimedOut)
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Push,
                repositoryRoot,
                branchName,
                remoteName: remoteName,
                failureMessage: "Resolving the configured Git remote timed out.");
        }

        if (remote.ExitCode != 0 || string.IsNullOrWhiteSpace(remote.StandardOutput))
        {
            return RepositoryResult(
                GitCommitPushStatus.RemoteNotFound,
                GitCommitPushKind.Push,
                repositoryRoot,
                branchName,
                remoteName: remoteName,
                failureMessage: NormalizeFailureMessage(remote.StandardError));
        }

        var head = await ReadHeadAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(head))
        {
            return RepositoryResult(
                GitCommitPushStatus.QueryFailed,
                GitCommitPushKind.Push,
                repositoryRoot,
                branchName,
                remoteName: remoteName,
                failureMessage: "The current branch has no commit to push.");
        }

        var push = await ExecuteGitAsync(
            repositoryRoot,
            cancellationToken,
            "push",
            "--porcelain",
            "--no-verify",
            remoteName,
            $"HEAD:refs/heads/{branchName}").ConfigureAwait(false);
        if (!push.Started)
        {
            return RepositoryResult(GitCommitPushStatus.GitUnavailable, GitCommitPushKind.Push, repositoryRoot, branchName, head, remoteName);
        }

        if (push.TimedOut)
        {
            return RepositoryResult(
                GitCommitPushStatus.PushRejected,
                GitCommitPushKind.Push,
                repositoryRoot,
                branchName,
                head,
                remoteName,
                "Git push timed out and its owned process tree was terminated.");
        }

        if (push.ExitCode != 0)
        {
            return RepositoryResult(
                GitCommitPushStatus.PushRejected,
                GitCommitPushKind.Push,
                repositoryRoot,
                branchName,
                head,
                remoteName,
                NormalizeFailureMessage(string.Concat(push.StandardError, Environment.NewLine, push.StandardOutput)));
        }

        return RepositoryResult(
            GitCommitPushStatus.Success,
            GitCommitPushKind.Push,
            repositoryRoot,
            branchName,
            head,
            remoteName);
    }

    private async Task<RepositoryPreflight> ResolveRepositoryAsync(
        string path,
        GitCommitPushKind kind,
        CancellationToken cancellationToken)
    {
        var detection = await _gitService.DetectRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
        return detection.Status switch
        {
            GitRepositoryDetectionStatus.NotRepository =>
                new RepositoryPreflight(null, EmptyResult(GitCommitPushStatus.NotRepository, kind)),
            GitRepositoryDetectionStatus.GitUnavailable =>
                new RepositoryPreflight(null, EmptyResult(GitCommitPushStatus.GitUnavailable, kind)),
            GitRepositoryDetectionStatus.ProbeFailed =>
                new RepositoryPreflight(null, EmptyResult(GitCommitPushStatus.QueryFailed, kind)),
            GitRepositoryDetectionStatus.Repository when detection.Repository is null =>
                new RepositoryPreflight(null, EmptyResult(GitCommitPushStatus.QueryFailed, kind)),
            GitRepositoryDetectionStatus.Repository when detection.Repository!.Kind == GitRepositoryKind.Bare =>
                new RepositoryPreflight(
                    null,
                    RepositoryResult(
                        GitCommitPushStatus.BareRepository,
                        kind,
                        detection.Repository.RepositoryRootPath)),
            GitRepositoryDetectionStatus.Repository =>
                new RepositoryPreflight(detection.Repository!.RepositoryRootPath, null),
            _ => new RepositoryPreflight(null, EmptyResult(GitCommitPushStatus.QueryFailed, kind)),
        };
    }

    private Task<GitCommandResult> ReadCurrentBranchAsync(
        string repositoryRoot,
        CancellationToken cancellationToken) =>
        ExecuteGitAsync(
            repositoryRoot,
            cancellationToken,
            "symbolic-ref",
            "--quiet",
            "--short",
            "HEAD");

    private async Task<string?> ReadHeadAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteGitAsync(
            repositoryRoot,
            cancellationToken,
            "rev-parse",
            "--verify",
            "HEAD").ConfigureAwait(false);
        return result.Started && !result.TimedOut && result.ExitCode == 0
            ? NormalizeOutput(result.StandardOutput)
            : null;
    }

    private async Task<GitCommandResult> ExecuteGitAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
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
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        startInfo.Environment["GIT_PAGER"] = "cat";
        startInfo.Environment["PAGER"] = "cat";

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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillOwnedProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            return new GitCommandResult(
                true,
                true,
                process.HasExited ? process.ExitCode : -1,
                await standardOutputTask.ConfigureAwait(false),
                await standardErrorTask.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            TryKillOwnedProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return new GitCommandResult(
            true,
            false,
            process.ExitCode,
            await standardOutputTask.ConfigureAwait(false),
            await standardErrorTask.ConfigureAwait(false));
    }

    private static void TryKillOwnedProcessTree(Process process)
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
        }
        catch (SystemException)
        {
        }
    }

    private static bool IsValidRemoteName(string remoteName)
    {
        if (string.IsNullOrWhiteSpace(remoteName) || remoteName.Length > MaximumRemoteNameCharacters || remoteName[0] == '-')
        {
            return false;
        }

        return remoteName.All(static character => !char.IsControl(character));
    }

    private static string NormalizeOutput(string value) => value.TrimEnd('\r', '\n');

    private static string? NormalizeFailureMessage(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed.Length <= MaximumFailureMessageCharacters
            ? trimmed
            : trimmed[..MaximumFailureMessageCharacters];
    }

    private static GitCommitPushResult EmptyResult(
        GitCommitPushStatus status,
        GitCommitPushKind kind,
        string? remoteName = null,
        string? failureMessage = null) =>
        new(status, kind, null, null, null, remoteName, failureMessage);

    private static GitCommitPushResult RepositoryResult(
        GitCommitPushStatus status,
        GitCommitPushKind kind,
        string repositoryRoot,
        string? currentBranchName = null,
        string? commitSha = null,
        string? remoteName = null,
        string? failureMessage = null) =>
        new(status, kind, repositoryRoot, currentBranchName, commitSha, remoteName, failureMessage);

    private sealed record RepositoryPreflight(string? RepositoryRoot, GitCommitPushResult? Result);

    private sealed record GitCommandResult(
        bool Started,
        bool TimedOut,
        int ExitCode,
        string StandardOutput,
        string StandardError)
    {
        public static GitCommandResult NotStarted { get; } = new(false, false, -1, string.Empty, string.Empty);
    }
}
