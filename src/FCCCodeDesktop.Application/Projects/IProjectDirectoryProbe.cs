namespace FCCCodeDesktop.Application.Projects;

public interface IProjectDirectoryProbe
{
    string NormalizeRootPath(string rootPath);

    bool DirectoryExists(string normalizedRootPath);

    string GetDisplayName(string normalizedRootPath);
}
