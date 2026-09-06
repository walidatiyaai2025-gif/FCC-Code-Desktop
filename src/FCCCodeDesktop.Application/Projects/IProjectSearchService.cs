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
    int MaximumResults = WorkspaceScalePolicy.DefaultMaximumSearchResults,
    int MaximumFiles = WorkspaceScalePolicy.DefaultMaximumFilesPerOperation,
    long MaximumFileBytes = WorkspaceScalePolicy.DefaultMaximumSearchFileBytes,
    int MaximumTraversalDepth = WorkspaceScalePolicy.DefaultMaximumTraversalDepth,
    int MaximumMatchesPerFile = WorkspaceScalePolicy.DefaultMaximumSearchMatchesPerFile,
    int MaximumPreviewCharacters = WorkspaceScalePolicy.DefaultMaximumPreviewCharacters);

[Flags]
public enum ProjectSearchLimitReason
{
    None = 0,
    Results = 1,
    Files = 2,
    MatchesPerFile = 4,
    TraversalDepth = 8,
    DirectoryEntries = 16,
}

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
    bool LimitReached,
    int MaximumTraversalDepth = WorkspaceScalePolicy.DefaultMaximumTraversalDepth,
    int MaximumMatchesPerFile = WorkspaceScalePolicy.DefaultMaximumSearchMatchesPerFile,
    int MaximumPreviewCharacters = WorkspaceScalePolicy.DefaultMaximumPreviewCharacters,
    int BinaryProbeBytes = WorkspaceScalePolicy.DefaultBinaryProbeBytes,
    ProjectSearchLimitReason LimitReasons = ProjectSearchLimitReason.None)
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
