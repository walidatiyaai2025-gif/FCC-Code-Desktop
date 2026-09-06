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
    bool IsReparsePoint)
{
    public bool CanExpand => IsDirectory && !IsReparsePoint;
}

public sealed record ProjectDirectoryListing(
    string ProjectRootPath,
    string DirectoryPath,
    IReadOnlyList<ProjectFileSystemEntry> Entries,
    int SkippedEntries,
    int MaximumEntries,
    bool LimitReached);
