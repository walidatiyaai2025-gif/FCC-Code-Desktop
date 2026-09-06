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

public sealed record GitFileStatusEntry
{
    private string? _originalPath;

    public GitFileStatusEntry(
        string path,
        GitFileChangeKind indexChange,
        GitFileChangeKind workTreeChange,
        string? originalPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = NormalizeRepositoryRelativePath(path);
        IndexChange = indexChange;
        WorkTreeChange = workTreeChange;
        OriginalPath = originalPath;
    }

    public string Path { get; }

    public GitFileChangeKind IndexChange { get; }

    public GitFileChangeKind WorkTreeChange { get; }

    public string? OriginalPath
    {
        get => _originalPath;
        init => _originalPath = value is null ? null : NormalizeRepositoryRelativePath(value);
    }

    public bool IsStaged => IndexChange != GitFileChangeKind.None;

    public bool HasWorkTreeChange => WorkTreeChange != GitFileChangeKind.None;

    public bool IsUntracked => WorkTreeChange == GitFileChangeKind.Untracked;

    public bool IsConflicted =>
        IndexChange == GitFileChangeKind.Unmerged || WorkTreeChange == GitFileChangeKind.Unmerged;

    private static string NormalizeRepositoryRelativePath(string path) =>
        path.Replace('\\', '/');
}

public sealed record GitStatusResult(
    GitStatusQueryStatus Status,
    string? RepositoryRootPath,
    IReadOnlyList<GitFileStatusEntry> Files)
{
    public bool IsSuccess => Status == GitStatusQueryStatus.Success;

    public bool IsClean => IsSuccess && Files.Count == 0;
}
