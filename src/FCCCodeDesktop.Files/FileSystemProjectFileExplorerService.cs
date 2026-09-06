using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.Files;

public sealed class FileSystemProjectFileExplorerService : IProjectFileExplorerService
{
    public const int DefaultMaximumEntriesPerDirectory = WorkspaceScalePolicy.DefaultMaximumDirectoryEntries;
    public const int MaximumSupportedEntriesPerDirectory = WorkspaceScalePolicy.MaximumSupportedDirectoryEntries;

    private static readonly char[] DirectorySeparators =
    [
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar,
    ];

    private readonly int _maximumEntriesPerDirectory;
    private readonly WorkspaceScalePolicy _policy;

    public FileSystemProjectFileExplorerService()
        : this(WorkspaceScalePolicy.Default)
    {
    }

    public FileSystemProjectFileExplorerService(int maximumEntriesPerDirectory)
        : this(new WorkspaceScalePolicy(maximumDirectoryEntries: maximumEntriesPerDirectory))
    {
    }

    public FileSystemProjectFileExplorerService(WorkspaceScalePolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _maximumEntriesPerDirectory = policy.MaximumDirectoryEntries;
    }

    public Task<ProjectDirectoryListing> ListChildrenAsync(
        string projectRootPath,
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(projectRootPath));
        }

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path is required.", nameof(directoryPath));
        }

        return Task.Run(
            () => ListChildrenCore(projectRootPath, directoryPath, cancellationToken),
            cancellationToken);
    }

    private ProjectDirectoryListing ListChildrenCore(
        string projectRootPath,
        string directoryPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRootPath = Path.GetFullPath(projectRootPath);
        var normalizedDirectoryPath = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(normalizedRootPath))
        {
            throw new DirectoryNotFoundException($"Project folder does not exist: {normalizedRootPath}");
        }

        EnsurePathInsideProject(normalizedRootPath, normalizedDirectoryPath);
        if (!Directory.Exists(normalizedDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Project directory does not exist: {normalizedDirectoryPath}");
        }

        EnsureNoReparseTraversal(normalizedRootPath, normalizedDirectoryPath);
        var directoryDepth = GetDirectoryDepth(normalizedRootPath, normalizedDirectoryPath);
        if (directoryDepth > _policy.MaximumTraversalDepth)
        {
            throw new InvalidOperationException(
                $"The requested directory exceeds the configured {_policy.MaximumTraversalDepth}-level traversal depth.");
        }

        string[] boundedEntries;
        try
        {
            boundedEntries = Directory.EnumerateFileSystemEntries(normalizedDirectoryPath)
                .Take(_maximumEntriesPerDirectory + 1)
                .ToArray();
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException(
                $"Access to project directory was denied: {normalizedDirectoryPath}",
                exception);
        }
        catch (IOException exception)
        {
            throw new IOException(
                $"Project directory could not be enumerated: {normalizedDirectoryPath}",
                exception);
        }

        var limitReached = boundedEntries.Length > _maximumEntriesPerDirectory;
        var entriesToInspect = limitReached
            ? boundedEntries[.._maximumEntriesPerDirectory]
            : boundedEntries;
        var entries = new List<ProjectFileSystemEntry>(entriesToInspect.Length);
        var skippedEntries = 0;
        var excludedDirectories = 0;
        var depthLimitedDirectories = 0;

        foreach (var entryPath in entriesToInspect)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(entryPath);
            }
            catch (UnauthorizedAccessException)
            {
                skippedEntries++;
                continue;
            }
            catch (IOException)
            {
                skippedEntries++;
                continue;
            }

            var fullPath = Path.GetFullPath(entryPath);
            if (!IsPathInsideProject(normalizedRootPath, fullPath))
            {
                skippedEntries++;
                continue;
            }

            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
            var name = Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(name))
            {
                skippedEntries++;
                continue;
            }

            var relativePath = Path
                .GetRelativePath(normalizedRootPath, fullPath)
                .Replace('\\', '/');
            var traversalRestriction = ProjectFileTraversalRestriction.None;
            if (isReparsePoint)
            {
                traversalRestriction = ProjectFileTraversalRestriction.ReparsePoint;
            }
            else if (isDirectory && _policy.ShouldExcludeDirectory(name))
            {
                traversalRestriction = ProjectFileTraversalRestriction.ExcludedDirectory;
                excludedDirectories++;
            }
            else if (isDirectory && directoryDepth >= _policy.MaximumTraversalDepth)
            {
                traversalRestriction = ProjectFileTraversalRestriction.MaximumDepth;
                depthLimitedDirectories++;
            }

            entries.Add(
                new ProjectFileSystemEntry(
                    name,
                    fullPath,
                    relativePath,
                    isDirectory,
                    isReparsePoint,
                    traversalRestriction));
        }

        var orderedEntries = entries
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();

        return new ProjectDirectoryListing(
            normalizedRootPath,
            normalizedDirectoryPath,
            orderedEntries,
            skippedEntries,
            _maximumEntriesPerDirectory,
            limitReached,
            directoryDepth,
            excludedDirectories,
            depthLimitedDirectories);
    }

    private static int GetDirectoryDepth(string rootPath, string directoryPath)
    {
        if (PathsEqual(rootPath, directoryPath))
        {
            return 0;
        }

        return Path.GetRelativePath(rootPath, directoryPath)
            .Split(DirectorySeparators, StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    private static void EnsureNoReparseTraversal(string rootPath, string directoryPath)
    {
        if (PathsEqual(rootPath, directoryPath))
        {
            return;
        }

        var relativeDirectory = Path.GetRelativePath(rootPath, directoryPath);
        var currentPath = rootPath;
        foreach (var segment in relativeDirectory.Split(
                     DirectorySeparators,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            if (HasReparsePointAttribute(currentPath))
            {
                throw new IOException(
                    "Reparse-point directories are visible but are not traversed by the project explorer.");
            }
        }
    }

    private static bool HasReparsePointAttribute(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException($"Access to project path was denied: {path}", exception);
        }
        catch (IOException exception)
        {
            throw new IOException($"Project path attributes could not be read: {path}", exception);
        }
    }

    private static void EnsurePathInsideProject(string rootPath, string candidatePath)
    {
        if (!IsPathInsideProject(rootPath, candidatePath))
        {
            throw new InvalidOperationException("The requested explorer path is outside the active project root.");
        }
    }

    private static bool IsPathInsideProject(string rootPath, string candidatePath)
    {
        if (PathsEqual(rootPath, candidatePath))
        {
            return true;
        }

        var relativePath = Path.GetRelativePath(rootPath, candidatePath);
        if (Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
