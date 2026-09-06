namespace FCCCodeDesktop.Application.Git;

public enum GitStatusQueryStatus
{
    Success = 0,
    NotRepository = 1,
    BareRepository = 2,
    GitUnavailable = 3,
    QueryFailed = 4,
}

public enum GitFileChangeKind
{
    None = 0,
    Modified = 1,
    Added = 2,
    Deleted = 3,
    Renamed = 4,
    Copied = 5,
    TypeChanged = 6,
    Unmerged = 7,
    Untracked = 8,
}

public sealed record GitFileStatusEntry(
    string Path,
    GitFileChangeKind IndexChange,
    GitFileChangeKind WorkTreeChange,
    string? OriginalPath = null)
{
    public bool IsStaged => IndexChange != GitFileChangeKind.None;

    public bool HasWorkTreeChange => WorkTreeChange != GitFileChangeKind.None;

    public bool IsUntracked => WorkTreeChange == GitFileChangeKind.Untracked;

    public bool IsConflicted =>
        IndexChange == GitFileChangeKind.Unmerged || WorkTreeChange == GitFileChangeKind.Unmerged;
}

public sealed record GitStatusResult(
    GitStatusQueryStatus Status,
    string? RepositoryRootPath,
    IReadOnlyList<GitFileStatusEntry> Files)
{
    public bool IsSuccess => Status == GitStatusQueryStatus.Success;

    public bool IsClean => IsSuccess && Files.Count == 0;
}
