using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.Files;

public sealed class FileSystemProjectFileExplorerService : IProjectFileExplorerService
{
    public const int DefaultMaximumEntriesPerDirectory = 2048;
    public const int MaximumSupportedEntriesPerDirectory = 20_000;

    private readonly int _maximumEntriesPerDirectory;

    public FileSystemProjectFileExplorerService(
        int maximumEntriesPerDirectory = DefaultMaximumEntriesPerDirectory)
    {
        if (maximumEntriesPerDirectory < 1
            || maximumEntriesPerDirectory > MaximumSupportedEntriesPerDirectory)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEntriesPerDirectory),
                maximumEntriesPerDirectory,
                $"Directory entry limit must be between 1 and {MaximumSupportedEntriesPerDirectory}.");
        }

        _maximumEntriesPerDirectory = maximumEntriesPerDirectory;
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

        if (!PathsEqual(normalizedRootPath, normalizedDirectoryPath)
            && HasReparsePointAttribute(normalizedDirectoryPath))
        {
            throw new IOException("Reparse-point directories are visible but are not traversed by the project explorer.");
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
            entries.Add(
                new ProjectFileSystemEntry(
                    name,
                    fullPath,
                    relativePath,
                    isDirectory,
                    isReparsePoint));
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
            limitReached);
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
