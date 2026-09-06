namespace FCCCodeDesktop.Application.Git;

public enum GitCommitPushKind
{
    Commit = 0,
    Push = 1,
}

public enum GitCommitPushStatus
{
    Success = 0,
    NotRepository = 1,
    BareRepository = 2,
    GitUnavailable = 3,
    DetachedHead = 4,
    NothingStaged = 5,
    IdentityRequired = 6,
    InvalidCommitMessage = 7,
    InvalidRemoteName = 8,
    RemoteNotFound = 9,
    PushRejected = 10,
    QueryFailed = 11,
}

public sealed record GitCommitPushResult(
    GitCommitPushStatus Status,
    GitCommitPushKind Kind,
    string? RepositoryRootPath,
    string? CurrentBranchName,
    string? CommitSha,
    string? RemoteName,
    string? FailureMessage = null)
{
    public bool IsSuccess => Status == GitCommitPushStatus.Success;
}

/// <summary>
/// Explicit bounded commit and push mutations. Implementations must commit only the staged index,
/// preserve unstaged owner work, and must never force, delete, reset, clean, or rewrite remote history.
/// </summary>
public interface IGitCommitPushService
{
    Task<GitCommitPushResult> CommitAsync(
        string path,
        string commitMessage,
        CancellationToken cancellationToken = default);

    Task<GitCommitPushResult> PushAsync(
        string path,
        string remoteName = "origin",
        CancellationToken cancellationToken = default);
}
