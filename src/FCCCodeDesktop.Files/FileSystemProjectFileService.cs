using System.Security.Cryptography;
using System.Text;
using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.Files;

public sealed class FileSystemProjectFileService : IProjectFileService
{
    public const int DefaultMaximumFileBytes = 8 * 1024 * 1024;
    public const int MaximumSupportedFileBytes = 128 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly UnicodeEncoding StrictUtf16LittleEndian = new(false, false, true);
    private static readonly UnicodeEncoding StrictUtf16BigEndian = new(true, false, true);

    private readonly int _maximumFileBytes;

    public FileSystemProjectFileService(int maximumFileBytes = DefaultMaximumFileBytes)
    {
        if (maximumFileBytes < 1 || maximumFileBytes > MaximumSupportedFileBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFileBytes),
                maximumFileBytes,
                $"File byte limit must be between 1 and {MaximumSupportedFileBytes}.");
        }

        _maximumFileBytes = maximumFileBytes;
    }

    public async Task<ProjectTextFileSnapshot> ReadTextAsync(
        string projectRootPath,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var paths = ValidatePaths(projectRootPath, filePath, requireExistingFile: true);
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = await ReadBoundedBytesAsync(paths.FullPath, cancellationToken).ConfigureAwait(false);
        var decoded = DecodeText(bytes);
        var version = BuildVersion(paths.FullPath, bytes);

        return new ProjectTextFileSnapshot(
            paths.ProjectRootPath,
            paths.FullPath,
            paths.RelativePath,
            decoded.Text,
            decoded.Encoding,
            DetectNewLineStyle(decoded.Text),
            EndsWithNewLine(decoded.Text),
            version);
    }

    public async Task<ProjectFileWriteResult> WriteTextAsync(
        ProjectTextFileWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Text);

        var paths = ValidatePaths(request.ProjectRootPath, request.FilePath, requireExistingFile: false);
        cancellationToken.ThrowIfCancellationRequested();

        var targetExisted = File.Exists(paths.FullPath);
        if (targetExisted && request.ExpectedVersion is null)
        {
            throw new ProjectFileConflictException(
                "Refusing to overwrite an existing project file without the version observed by the caller.");
        }

        if (!targetExisted && request.ExpectedVersion is not null)
        {
            throw new ProjectFileConflictException(
                "The project file no longer exists, so the requested version cannot be replaced safely.");
        }

        if (request.ExpectedVersion is not null)
        {
            await EnsureExpectedVersionAsync(
                    paths.FullPath,
                    request.ExpectedVersion,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var encodedBytes = EncodeText(request.Text, request.Encoding);
        if (encodedBytes.Length > _maximumFileBytes)
        {
            throw new IOException(
                $"The encoded project file is {encodedBytes.Length} bytes, which exceeds the configured {_maximumFileBytes}-byte safety limit.");
        }

        var directoryPath = Path.GetDirectoryName(paths.FullPath)
            ?? throw new InvalidOperationException("The project file path does not have a parent directory.");
        var fileName = Path.GetFileName(paths.FullPath);
        var temporaryPath = Path.Combine(
            directoryPath,
            $".{fileName}.fccd-{Guid.NewGuid():N}.tmp");

        try
        {
            await WriteTemporaryFileAsync(temporaryPath, encodedBytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (request.ExpectedVersion is not null)
            {
                await EnsureExpectedVersionAsync(
                        paths.FullPath,
                        request.ExpectedVersion,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (File.Exists(paths.FullPath))
            {
                throw new ProjectFileConflictException(
                    "The project file was created by another process before this write completed.");
            }

            File.Move(temporaryPath, paths.FullPath, overwrite: targetExisted);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException(
                $"Access to project file was denied: {paths.FullPath}",
                exception);
        }
        catch (ProjectFileConflictException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException exception)
        {
            throw new IOException($"Project file could not be written safely: {paths.FullPath}", exception);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }

        var committedBytes = await ReadBoundedBytesAsync(paths.FullPath, cancellationToken).ConfigureAwait(false);
        return new ProjectFileWriteResult(
            paths.FullPath,
            paths.RelativePath,
            BuildVersion(paths.FullPath, committedBytes));
    }

    private async Task<byte[]> ReadBoundedBytesAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > _maximumFileBytes)
        {
            throw new IOException(
                $"The project file is {fileInfo.Length} bytes, which exceeds the configured {_maximumFileBytes}-byte safety limit.");
        }

        if (fileInfo.Length > int.MaxValue)
        {
            throw new IOException("The project file is too large to materialize safely.");
        }

        await using var stream = new FileStream(
            filePath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = 64 * 1024,
            });

        if (stream.Length > _maximumFileBytes || stream.Length > int.MaxValue)
        {
            throw new IOException(
                $"The project file grew beyond the configured {_maximumFileBytes}-byte safety limit while it was being opened.");
        }

        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    private static async Task WriteTemporaryFileAsync(
        string temporaryPath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            temporaryPath,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                BufferSize = 64 * 1024,
            });

        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureExpectedVersionAsync(
        string filePath,
        ProjectFileVersion expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new ProjectFileConflictException(
                "The project file was removed after it was read; refusing to recreate it implicitly.");
        }

        var currentBytes = await ReadBoundedBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var currentVersion = BuildVersion(filePath, currentBytes);
        if (currentVersion != expectedVersion)
        {
            throw new ProjectFileConflictException(
                "The project file changed after it was read; reload it before saving to avoid overwriting external work.");
        }
    }

    private static ValidatedProjectFilePaths ValidatePaths(
        string projectRootPath,
        string filePath,
        bool requireExistingFile)
    {
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(projectRootPath));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Project file path is required.", nameof(filePath));
        }

        var normalizedRootPath = Path.GetFullPath(projectRootPath);
        if (!Directory.Exists(normalizedRootPath))
        {
            throw new DirectoryNotFoundException($"Project folder does not exist: {normalizedRootPath}");
        }

        var normalizedFilePath = Path.GetFullPath(
            Path.IsPathFullyQualified(filePath)
                ? filePath
                : Path.Combine(normalizedRootPath, filePath));
        EnsurePathInsideProject(normalizedRootPath, normalizedFilePath);

        if (PathsEqual(normalizedRootPath, normalizedFilePath))
        {
            throw new InvalidOperationException("A project file operation cannot target the project root directory itself.");
        }

        var directoryPath = Path.GetDirectoryName(normalizedFilePath)
            ?? throw new InvalidOperationException("The project file path does not have a parent directory.");
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Project file parent directory does not exist: {directoryPath}");
        }

        EnsureNoReparseTraversal(normalizedRootPath, directoryPath);

        if (File.Exists(normalizedFilePath))
        {
            if (HasReparsePointAttribute(normalizedFilePath))
            {
                throw new IOException("Reparse-point project files are not read or written by the safe file service.");
            }
        }
        else if (Directory.Exists(normalizedFilePath))
        {
            throw new IOException("The requested project file path identifies a directory.");
        }
        else if (requireExistingFile)
        {
            throw new FileNotFoundException("Project file does not exist.", normalizedFilePath);
        }

        var relativePath = Path.GetRelativePath(normalizedRootPath, normalizedFilePath).Replace('\\', '/');
        return new ValidatedProjectFilePaths(normalizedRootPath, normalizedFilePath, relativePath);
    }

    private static void EnsureNoReparseTraversal(string rootPath, string directoryPath)
    {
        if (PathsEqual(rootPath, directoryPath))
        {
            return;
        }

        var relativeDirectory = Path.GetRelativePath(rootPath, directoryPath);
        var segments = relativeDirectory.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var currentPath = rootPath;

        foreach (var segment in segments)
        {
            currentPath = Path.Combine(currentPath, segment);
            if (HasReparsePointAttribute(currentPath))
            {
                throw new IOException(
                    "Project file operations do not traverse reparse-point directories below the active project root.");
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
        if (PathsEqual(rootPath, candidatePath))
        {
            return;
        }

        var relativePath = Path.GetRelativePath(rootPath, candidatePath);
        if (Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The requested file path is outside the active project root.");
        }
    }

    private static DecodedText DecodeText(byte[] bytes)
    {
        try
        {
            if (bytes.AsSpan().StartsWith([0xEF, 0xBB, 0xBF]))
            {
                return new DecodedText(
                    StrictUtf8.GetString(bytes, 3, bytes.Length - 3),
                    ProjectTextEncoding.Utf8WithBom);
            }

            if (bytes.AsSpan().StartsWith([0xFF, 0xFE]))
            {
                return new DecodedText(
                    StrictUtf16LittleEndian.GetString(bytes, 2, bytes.Length - 2),
                    ProjectTextEncoding.Utf16LittleEndian);
            }

            if (bytes.AsSpan().StartsWith([0xFE, 0xFF]))
            {
                return new DecodedText(
                    StrictUtf16BigEndian.GetString(bytes, 2, bytes.Length - 2),
                    ProjectTextEncoding.Utf16BigEndian);
            }

            return new DecodedText(StrictUtf8.GetString(bytes), ProjectTextEncoding.Utf8);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "The project file is not valid UTF-8 or BOM-identified UTF-16 text; refusing to guess an encoding that could corrupt data.",
                exception);
        }
    }

    private static byte[] EncodeText(string text, ProjectTextEncoding encoding) =>
        encoding switch
        {
            ProjectTextEncoding.Utf8 => StrictUtf8.GetBytes(text),
            ProjectTextEncoding.Utf8WithBom => CombinePreamble([0xEF, 0xBB, 0xBF], StrictUtf8.GetBytes(text)),
            ProjectTextEncoding.Utf16LittleEndian => CombinePreamble([0xFF, 0xFE], StrictUtf16LittleEndian.GetBytes(text)),
            ProjectTextEncoding.Utf16BigEndian => CombinePreamble([0xFE, 0xFF], StrictUtf16BigEndian.GetBytes(text)),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unsupported project text encoding."),
        };

    private static byte[] CombinePreamble(byte[] preamble, byte[] payload)
    {
        var result = new byte[preamble.Length + payload.Length];
        preamble.CopyTo(result, 0);
        payload.CopyTo(result, preamble.Length);
        return result;
    }

    private static ProjectFileVersion BuildVersion(string filePath, byte[] bytes) =>
        new(
            bytes.LongLength,
            File.GetLastWriteTimeUtc(filePath).Ticks,
            Convert.ToHexString(SHA256.HashData(bytes)));

    private static ProjectNewLineStyle DetectNewLineStyle(string text)
    {
        var sawCrLf = false;
        var sawLf = false;
        var sawCr = false;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    sawCrLf = true;
                    index++;
                }
                else
                {
                    sawCr = true;
                }
            }
            else if (text[index] == '\n')
            {
                sawLf = true;
            }
        }

        var styleCount = (sawCrLf ? 1 : 0) + (sawLf ? 1 : 0) + (sawCr ? 1 : 0);
        if (styleCount > 1)
        {
            return ProjectNewLineStyle.Mixed;
        }

        if (sawCrLf)
        {
            return ProjectNewLineStyle.CrLf;
        }

        if (sawLf)
        {
            return ProjectNewLineStyle.Lf;
        }

        return sawCr ? ProjectNewLineStyle.Cr : ProjectNewLineStyle.None;
    }

    private static bool EndsWithNewLine(string text) =>
        text.EndsWith("\n", StringComparison.Ordinal)
        || text.EndsWith("\r", StringComparison.Ordinal);

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup must not hide the primary write/cancellation result.
        }
        catch (IOException)
        {
            // Best-effort cleanup must not hide the primary write/cancellation result.
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed record ValidatedProjectFilePaths(
        string ProjectRootPath,
        string FullPath,
        string RelativePath);

    private sealed record DecodedText(string Text, ProjectTextEncoding Encoding);
}
