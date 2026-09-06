namespace FCCCodeDesktop.Application.Git;

public enum GitDiffQueryStatus
{
    Success = 0,
    NotRepository = 1,
    BareRepository = 2,
    GitUnavailable = 3,
    QueryFailed = 4,
    TooLarge = 5,
}

public enum GitDiffSectionKind
{
    Staged = 0,
    WorkTree = 1,
}

public sealed record GitDiffSection(
    GitDiffSectionKind Kind,
    string Patch,
    bool IsBinary,
    bool WasTruncated)
{
    public bool HasChanges => Patch.Length > 0 || IsBinary || WasTruncated;
}

public sealed record GitFileDiffResult(
    GitDiffQueryStatus Status,
    string? RepositoryRootPath,
    string RepositoryRelativePath,
    GitDiffSection Staged,
    GitDiffSection WorkTree)
{
    public bool IsSuccess => Status == GitDiffQueryStatus.Success;

    public bool HasChanges => Staged.HasChanges || WorkTree.HasChanges;

    public bool WasTruncated => Staged.WasTruncated || WorkTree.WasTruncated;
}
