namespace FCCCodeDesktop.Application.Git;

public enum GitRemoteSyncKind
{
    Fetch = 0,
    PullFastForward = 1,
}

public enum GitRemoteSyncStatus
{
    Success = 0,
    NotRepository = 1,
    BareRepository = 2,
    GitUnavailable = 3,
    InvalidRemoteName = 4,
    RemoteNotFound = 5,
    InvalidRemoteBranch = 6,
    DetachedHead = 7,
    DirtyWorkTree = 8,
    NonFastForward = 9,
    RemoteFailure = 10,
    PullBlocked = 11,
    QueryFailed = 12,
}

public sealed record GitRemoteSyncResult(
    GitRemoteSyncStatus Status,
    GitRemoteSyncKind Kind,
    string RemoteName,
    string? RemoteBranchName,
    string? RepositoryRootPath,
    string? CurrentBranchName,
    string? PreviousHead,
    string? CurrentHead,
    string? FailureMessage = null)
{
    public bool IsSuccess => Status == GitRemoteSyncStatus.Success;
}

/// <summary>
/// Explicit remote synchronization operations. Pull implementations must preserve owner work and
/// must not reset, clean, autostash, rebase, force-update, commit, or push.
/// </summary>
public interface IGitRemoteService
{
    Task<GitRemoteSyncResult> FetchAsync(
        string path,
        string remoteName = "origin",
        CancellationToken cancellationToken = default);

    Task<GitRemoteSyncResult> PullFastForwardAsync(
        string path,
        string remoteName,
        string remoteBranchName,
        CancellationToken cancellationToken = default);
}
