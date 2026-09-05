using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.Files;

public sealed class SystemProjectDirectoryProbe : IProjectDirectoryProbe
{
    public string NormalizeRootPath(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(rootPath));
        }

        return Path.GetFullPath(rootPath);
    }

    public bool DirectoryExists(string normalizedRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRootPath);
        return Directory.Exists(normalizedRootPath);
    }

    public string GetDisplayName(string normalizedRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRootPath);
        var directory = new DirectoryInfo(normalizedRootPath);
        if (!string.IsNullOrWhiteSpace(directory.Name))
        {
            return directory.Name;
        }

        var root = Path.GetPathRoot(normalizedRootPath);
        return string.IsNullOrWhiteSpace(root) ? normalizedRootPath : root;
    }
}
