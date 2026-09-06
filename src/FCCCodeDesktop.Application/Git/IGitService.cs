namespace FCCCodeDesktop.Application.Git;

public enum GitRepositoryDetectionStatus
{
    Repository = 0,
    NotRepository = 1,
    GitUnavailable = 2,
    ProbeFailed = 3,
}

public enum GitRepositoryKind
{
    WorkTree = 0,
    Bare = 1,
}

public sealed record GitRepositoryInfo(
    string ProbePath,
    string RepositoryRootPath,
    string GitDirectoryPath,
    GitRepositoryKind Kind);

public sealed record GitRepositoryDetectionResult(
    GitRepositoryDetectionStatus Status,
    GitRepositoryInfo? Repository = null)
{
    public bool IsRepository => Status == GitRepositoryDetectionStatus.Repository;

    public bool GitAvailable => Status != GitRepositoryDetectionStatus.GitUnavailable;
}

public interface IGitService
{
    Task<GitRepositoryDetectionResult> DetectRepositoryAsync(
        string path,
        CancellationToken cancellationToken = default);
}
