namespace FCCCodeDesktop.Application.Git;

public enum GitIndexMutationKind
{
    Stage = 0,
    Unstage = 1,
}

public enum GitIndexMutationStatus
{
    Success = 0,
    NotRepository = 1,
    BareRepository = 2,
    GitUnavailable = 3,
    QueryFailed = 4,
}

public sealed record GitIndexMutationResult(
    GitIndexMutationStatus Status,
    GitIndexMutationKind Kind,
    string? RepositoryRootPath,
    IReadOnlyList<string> RequestedPaths,
    IReadOnlyList<string> EffectivePaths,
    string? FailureMessage = null)
{
    public bool IsSuccess => Status == GitIndexMutationStatus.Success;
}

/// <summary>
/// Explicit index-only Git mutations. Implementations must never modify work-tree file contents.
/// </summary>
public interface IGitIndexService
{
    Task<GitIndexMutationResult> StageAsync(
        string path,
        IReadOnlyCollection<string> repositoryRelativePaths,
        CancellationToken cancellationToken = default);

    Task<GitIndexMutationResult> UnstageAsync(
        string path,
        IReadOnlyCollection<string> repositoryRelativePaths,
        CancellationToken cancellationToken = default);
}
