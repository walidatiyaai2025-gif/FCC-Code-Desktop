using System.Text;
using System.Text.RegularExpressions;
using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.Files;

public sealed class FileSystemProjectSearchService : IProjectSearchService
{
    public const int MaximumSupportedResults = 5_000;
    public const int MaximumSupportedFiles = 100_000;
    public const long MaximumSupportedFileBytes = 64L * 1024 * 1024;
    public static readonly TimeSpan RegularExpressionTimeout = TimeSpan.FromMilliseconds(250);

    private const int BinaryProbeBytes = 4096;
    private const int MaximumPreviewCharacters = 240;

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".hg", ".svn", ".vs", "bin", "obj", "node_modules", "packages", "dist", "build", "Library", "Temp", "Logs",
    };

    public Task<ProjectSearchResultSet> SearchAsync(ProjectSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        return Task.Run(() => SearchCore(request, cancellationToken), cancellationToken);
    }

    private static ProjectSearchResultSet SearchCore(ProjectSearchRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRootPath = Path.GetFullPath(request.ProjectRootPath);
        if (!Directory.Exists(normalizedRootPath))
        {
            throw new DirectoryNotFoundException($"Project folder does not exist: {normalizedRootPath}");
        }

        Regex? expression = null;
        if (request.Mode == ProjectSearchMode.RegularExpression)
        {
            try
            {
                expression = new Regex(
                    request.Query,
                    RegexOptions.CultureInvariant | (request.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase),
                    RegularExpressionTimeout);
            }
            catch (ArgumentException exception)
            {
                throw new ProjectSearchQueryException("The regular expression is invalid.", exception);
            }
        }

        var matches = new List<ProjectSearchMatch>(Math.Min(request.MaximumResults, 256));
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(normalizedRootPath);
        var filesExamined = 0;
        var filesSkipped = 0;
        var directoriesSkipped = 0;
        var limitReached = false;

        while (pendingDirectories.Count > 0 && !limitReached)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryPath = pendingDirectories.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(directoryPath);
            }
            catch (UnauthorizedAccessException)
            {
                directoriesSkipped++;
                continue;
            }
            catch (IOException)
            {
                directoriesSkipped++;
                continue;
            }

            try
            {
                foreach (var entryPath in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FileAttributes attributes;
                    try
                    {
                        attributes = File.GetAttributes(entryPath);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        filesSkipped++;
                        continue;
                    }
                    catch (IOException)
                    {
                        filesSkipped++;
                        continue;
                    }

                    var fullPath = Path.GetFullPath(entryPath);
                    if (!IsPathInsideProject(normalizedRootPath, fullPath))
                    {
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            directoriesSkipped++;
                        }
                        else
                        {
                            filesSkipped++;
                        }
                        continue;
                    }

                    var isDirectory = (attributes & FileAttributes.Directory) != 0;
                    var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
                    if (isDirectory)
                    {
                        var directoryName = Path.GetFileName(fullPath);
                        if (isReparsePoint || IgnoredDirectoryNames.Contains(directoryName))
                        {
                            directoriesSkipped++;
                            continue;
                        }
                        pendingDirectories.Push(fullPath);
                        continue;
                    }

                    if (isReparsePoint)
                    {
                        filesSkipped++;
                        continue;
                    }

                    if (filesExamined >= request.MaximumFiles)
                    {
                        limitReached = true;
                        break;
                    }

                    filesExamined++;
                    var relativePath = Path.GetRelativePath(normalizedRootPath, fullPath).Replace('\\', '/');
                    if (request.Mode == ProjectSearchMode.FileName)
                    {
                        if (Contains(relativePath, request.Query, request.MatchCase))
                        {
                            matches.Add(new ProjectSearchMatch(fullPath, relativePath, null, null, relativePath));
                            if (matches.Count >= request.MaximumResults)
                            {
                                limitReached = true;
                                break;
                            }
                        }
                        continue;
                    }

                    if (!TryGetSearchableFileLength(fullPath, request.MaximumFileBytes, out var searchable))
                    {
                        filesSkipped++;
                        continue;
                    }
                    if (!searchable || LooksBinary(fullPath))
                    {
                        filesSkipped++;
                        continue;
                    }

                    try
                    {
                        SearchTextFile(fullPath, relativePath, request, expression, matches, cancellationToken);
                    }
                    catch (DecoderFallbackException)
                    {
                        filesSkipped++;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        filesSkipped++;
                    }
                    catch (IOException)
                    {
                        filesSkipped++;
                    }

                    if (matches.Count >= request.MaximumResults)
                    {
                        limitReached = true;
                        break;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                directoriesSkipped++;
            }
            catch (IOException)
            {
                directoriesSkipped++;
            }
        }

        return new ProjectSearchResultSet(
            normalizedRootPath, request.Query, request.Mode, matches, filesExamined, filesSkipped, directoriesSkipped,
            request.MaximumResults, request.MaximumFiles, request.MaximumFileBytes, limitReached);
    }

    private static void SearchTextFile(
        string fullPath,
        string relativePath,
        ProjectSearchRequest request,
        Regex? expression,
        List<ProjectSearchMatch> matches,
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);

        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            if (request.Mode == ProjectSearchMode.Content)
            {
                AddLiteralMatches(line, fullPath, relativePath, lineNumber, request, matches);
            }
            else
            {
                AddRegularExpressionMatches(
                    line,
                    fullPath,
                    relativePath,
                    lineNumber,
                    expression ?? throw new InvalidOperationException("Regular expression search was not initialized."),
                    request.MaximumResults,
                    matches);
            }
            if (matches.Count >= request.MaximumResults)
            {
                return;
            }
        }
    }

    private static void AddLiteralMatches(
        string line,
        string fullPath,
        string relativePath,
        int lineNumber,
        ProjectSearchRequest request,
        List<ProjectSearchMatch> matches)
    {
        var comparison = request.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var startIndex = 0;
        while (startIndex <= line.Length - request.Query.Length)
        {
            var matchIndex = line.IndexOf(request.Query, startIndex, comparison);
            if (matchIndex < 0)
            {
                return;
            }
            matches.Add(new ProjectSearchMatch(
                fullPath, relativePath, lineNumber, matchIndex + 1, BuildPreview(line, matchIndex, request.Query.Length)));
            if (matches.Count >= request.MaximumResults)
            {
                return;
            }
            startIndex = matchIndex + Math.Max(request.Query.Length, 1);
        }
    }

    private static void AddRegularExpressionMatches(
        string line,
        string fullPath,
        string relativePath,
        int lineNumber,
        Regex expression,
        int maximumResults,
        List<ProjectSearchMatch> matches)
    {
        try
        {
            foreach (Match match in expression.Matches(line))
            {
                if (!match.Success)
                {
                    continue;
                }
                matches.Add(new ProjectSearchMatch(
                    fullPath, relativePath, lineNumber, match.Index + 1, BuildPreview(line, match.Index, Math.Max(match.Length, 1))));
                if (matches.Count >= maximumResults)
                {
                    return;
                }
            }
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new ProjectSearchQueryException(
                "The regular expression exceeded the bounded evaluation time. Simplify the pattern and try again.", exception);
        }
    }

    private static string BuildPreview(string line, int matchIndex, int matchLength)
    {
        if (line.Length <= MaximumPreviewCharacters)
        {
            return line;
        }
        var desiredStart = Math.Max(0, matchIndex - 80);
        var maximumStart = Math.Max(0, line.Length - MaximumPreviewCharacters);
        var start = Math.Min(desiredStart, maximumStart);
        if (matchIndex + matchLength > start + MaximumPreviewCharacters)
        {
            start = Math.Min(maximumStart, matchIndex + matchLength - MaximumPreviewCharacters);
        }
        var length = Math.Min(MaximumPreviewCharacters, line.Length - start);
        var preview = line.Substring(start, length);
        if (start > 0)
        {
            preview = $"…{preview[1..]}";
        }
        if (start + length < line.Length)
        {
            preview = $"{preview[..^1]}…";
        }
        return preview;
    }

    private static bool TryGetSearchableFileLength(string path, long maximumFileBytes, out bool searchable)
    {
        try
        {
            searchable = new FileInfo(path).Length <= maximumFileBytes;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            searchable = false;
            return false;
        }
        catch (IOException)
        {
            searchable = false;
            return false;
        }
    }

    private static bool LooksBinary(string path)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BinaryProbeBytes, FileOptions.SequentialScan);
        Span<byte> buffer = stackalloc byte[BinaryProbeBytes];
        var bytesRead = stream.Read(buffer);
        var bytes = buffer[..bytesRead];
        if (HasUnicodeBom(bytes))
        {
            return false;
        }
        return bytes.Contains((byte)0);
    }

    private static bool HasUnicodeBom(ReadOnlySpan<byte> bytes) =>
        (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        || (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        || (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        || (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF);

    private static bool Contains(string source, string query, bool matchCase) =>
        source.Contains(query, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    private static bool IsPathInsideProject(string rootPath, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, candidatePath);
        return !Path.IsPathRooted(relativePath)
               && !relativePath.Equals("..", StringComparison.Ordinal)
               && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static void ValidateRequest(ProjectSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectRootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ProjectSearchQueryException("Enter a search query.");
        }
        if (!Enum.IsDefined(request.Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "Search mode is not supported.");
        }
        if (request.MaximumResults < 1 || request.MaximumResults > MaximumSupportedResults)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.MaximumResults,
                $"Maximum results must be between 1 and {MaximumSupportedResults}.");
        }
        if (request.MaximumFiles < 1 || request.MaximumFiles > MaximumSupportedFiles)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.MaximumFiles,
                $"Maximum files must be between 1 and {MaximumSupportedFiles}.");
        }
        if (request.MaximumFileBytes < 1 || request.MaximumFileBytes > MaximumSupportedFileBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.MaximumFileBytes,
                $"Maximum searchable file size must be between 1 and {MaximumSupportedFileBytes} bytes.");
        }
    }
}
