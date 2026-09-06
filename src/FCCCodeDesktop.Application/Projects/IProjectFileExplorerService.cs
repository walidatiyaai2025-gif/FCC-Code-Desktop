namespace FCCCodeDesktop.Application.Projects;

public interface IProjectFileExplorerService
{
    Task<ProjectDirectoryListing> ListChildrenAsync(
        string projectRootPath,
        string directoryPath,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectFileSystemEntry(
    string Name,
    string FullPath,
    string RelativePath,
    bool IsDirectory,
    bool IsReparsePoint,
    ProjectFileTraversalRestriction TraversalRestriction = ProjectFileTraversalRestriction.None)
{
    public bool CanExpand => IsDirectory
        && !IsReparsePoint
        && TraversalRestriction == ProjectFileTraversalRestriction.None;
}

public enum ProjectFileTraversalRestriction
{
    None,
    ReparsePoint,
    ExcludedDirectory,
    MaximumDepth,
}

public sealed record ProjectDirectoryListing(
    string ProjectRootPath,
    string DirectoryPath,
    IReadOnlyList<ProjectFileSystemEntry> Entries,
    int SkippedEntries,
    int MaximumEntries,
    bool LimitReached,
    int DirectoryDepth = 0,
    int ExcludedDirectories = 0,
    int DepthLimitedDirectories = 0)
{
    public bool IsPartial => LimitReached || SkippedEntries > 0;
}
