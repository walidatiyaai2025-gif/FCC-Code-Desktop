namespace FCCCodeDesktop.Application.Projects;

public interface IProjectFileService
{
    Task<ProjectTextFileSnapshot> ReadTextAsync(
        string projectRootPath,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<ProjectFileWriteResult> WriteTextAsync(
        ProjectTextFileWriteRequest request,
        CancellationToken cancellationToken = default);
}

public enum ProjectTextEncoding
{
    Utf8,
    Utf8WithBom,
    Utf16LittleEndian,
    Utf16BigEndian,
}

public enum ProjectNewLineStyle
{
    None,
    CrLf,
    Lf,
    Cr,
    Mixed,
}

public sealed record ProjectFileVersion(
    long Length,
    long LastWriteTimeUtcTicks,
    string Sha256);

public sealed record ProjectTextFileSnapshot(
    string ProjectRootPath,
    string FullPath,
    string RelativePath,
    string Text,
    ProjectTextEncoding Encoding,
    ProjectNewLineStyle NewLineStyle,
    bool EndsWithNewLine,
    ProjectFileVersion Version);

public sealed record ProjectTextFileWriteRequest(
    string ProjectRootPath,
    string FilePath,
    string Text,
    ProjectTextEncoding Encoding,
    ProjectFileVersion? ExpectedVersion = null);

public sealed record ProjectFileWriteResult(
    string FullPath,
    string RelativePath,
    ProjectFileVersion Version);

public sealed class ProjectFileConflictException : IOException
{
    public ProjectFileConflictException(string message)
        : base(message)
    {
    }
}
