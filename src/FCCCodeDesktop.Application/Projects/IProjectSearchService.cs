namespace FCCCodeDesktop.Application.Projects;

public interface IProjectSearchService
{
    Task<ProjectSearchResultSet> SearchAsync(
        ProjectSearchRequest request,
        CancellationToken cancellationToken = default);
}

public enum ProjectSearchMode
{
    Content,
    FileName,
    RegularExpression,
}

public sealed record ProjectSearchRequest(
    string ProjectRootPath,
    string Query,
    ProjectSearchMode Mode,
    bool MatchCase = false,
    int MaximumResults = 500,
    int MaximumFiles = 20_000,
    long MaximumFileBytes = 4 * 1024 * 1024,
    int? MaximumTraversalDepth = null,
    int? MaximumMatchesPerFile = null,
    int? MaximumPreviewCharacters = null);

public sealed record ProjectSearchMatch(
    string FullPath,
    string RelativePath,
    int? LineNumber,
    int? ColumnNumber,
    string Preview)
{
    public string LocationLabel => LineNumber is int line && ColumnNumber is int column
        ? $"{RelativePath}:{line}:{column}"
        : RelativePath;
}

public sealed record ProjectSearchResultSet(
    string ProjectRootPath,
    string Query,
    ProjectSearchMode Mode,
    IReadOnlyList<ProjectSearchMatch> Matches,
    int FilesExamined,
    int FilesSkipped,
    int DirectoriesSkipped,
    int MaximumResults,
    int MaximumFiles,
    long MaximumFileBytes,
    int MaximumTraversalDepth,
    int MaximumMatchesPerFile,
    int MaximumPreviewCharacters,
    int BinaryProbeBytes,
    bool LimitReached)
{
    public bool HasMatches => Matches.Count > 0;
}

public sealed class ProjectSearchQueryException : Exception
{
    public ProjectSearchQueryException(string message)
        : base(message)
    {
    }

    public ProjectSearchQueryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
