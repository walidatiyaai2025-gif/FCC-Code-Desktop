namespace FCCCodeDesktop.Application.Git;

public enum GitHistoryStatus
{
    Success = 0,
    EmptyRepository = 1,
    NotRepository = 2,
    GitUnavailable = 3,
    InvalidQuery = 4,
    TooLarge = 5,
    QueryFailed = 6,
}

public sealed record GitHistoryQuery(
    int MaxCount = 50,
    string? RelativePath = null,
    string? BeforeCommitSha = null);

public sealed record GitHistoryCommit(
    string Sha,
    string AbbreviatedSha,
    IReadOnlyList<string> ParentShas,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorDate,
    string Subject);

public sealed record GitHistoryResult(
    GitHistoryStatus Status,
    string? RepositoryRootPath,
    IReadOnlyList<GitHistoryCommit> Commits,
    string? NextCursorSha = null,
    string? FailureMessage = null)
{
    public bool IsSuccess => Status is GitHistoryStatus.Success or GitHistoryStatus.EmptyRepository;

    public bool HasMore => NextCursorSha is not null;
}

/// <summary>
/// Read-only bounded Git history queries. Implementations must not mutate refs, the index,
/// the work tree, repository configuration, or contact remotes.
/// </summary>
public interface IGitHistoryService
{
    Task<GitHistoryResult> GetHistoryAsync(
        string path,
        GitHistoryQuery? query = null,
        CancellationToken cancellationToken = default);
}
