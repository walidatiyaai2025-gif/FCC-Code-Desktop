using System.Text;
using System.Text.RegularExpressions;
using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.Files;

public sealed class FileSystemProjectSearchService : IProjectSearchService
{
    public const int MaximumSupportedResults = WorkspaceScalePolicy.MaximumSupportedSearchResults;
    public const int MaximumSupportedFiles = WorkspaceScalePolicy.MaximumSupportedFilesPerOperation;
    public const long MaximumSupportedFileBytes = WorkspaceScalePolicy.MaximumSupportedSearchFileBytes;
    public static readonly TimeSpan RegularExpressionTimeout = TimeSpan.FromMilliseconds(250);

    private readonly WorkspaceScalePolicy _scalePolicy;

    public FileSystemProjectSearchService(WorkspaceScalePolicy? scalePolicy = null)
    {
        _scalePolicy = scalePolicy ?? WorkspaceScalePolicy.Default;
    }

    public Task<ProjectSearchResultSet> SearchAsync(ProjectSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var limits = ResolveLimits(request);
        return Task.Run(() => SearchCore(request, limits, cancellationToken), cancellationToken);
    }

    private ProjectSearchResultSet SearchCore(
        ProjectSearchRequest request,
        SearchLimits limits,
        CancellationToken cancellationToken)
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

        var matches = new List<ProjectSearchMatch>(Math.Min(limits.MaximumResults, 256));
        var pendingDirectories = new Stack<(string Path, int Depth)>();
        pendingDirectories.Push((normalizedRootPath, 0));
        var filesExamined = 0;
        var filesSkipped = 0;
        var directoriesSkipped = 0;
        var limitReached = false;
        var stopTraversal = false;

        while (pendingDirectories.Count > 0 && !stopTraversal)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directoryPath, directoryDepth) = pendingDirectories.Pop();
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
                        if (isReparsePoint || _scalePolicy.ShouldExcludeDirectory(directoryName))
                        {
                            directoriesSkipped++;
                            continue;
                        }
                        if (directoryDepth >= limits.MaximumTraversalDepth)
                        {
                            directoriesSkipped++;
                            limitReached = true;
                            continue;
                        }
                        pendingDirectories.Push((fullPath, directoryDepth + 1));
                        continue;
                    }

                    if (isReparsePoint)
                    {
                        filesSkipped++;
                        continue;
                    }

                    if (filesExamined >= limits.MaximumFiles)
                    {
                        limitReached = true;
                        stopTraversal = true;
                        break;
                    }

                    filesExamined++;
                    var relativePath = Path.GetRelativePath(normalizedRootPath, fullPath).Replace('\\', '/');
                    if (request.Mode == ProjectSearchMode.FileName)
                    {
                        if (Contains(relativePath, request.Query, request.MatchCase))
                        {
                            matches.Add(new ProjectSearchMatch(fullPath, relativePath, null, null, relativePath));
                            if (matches.Count >= limits.MaximumResults)
                            {
                                limitReached = true;
                                stopTraversal = true;
                                break;
                            }
                        }
                        continue;
                    }

                    if (!TryGetSearchableFileLength(fullPath, limits.MaximumFileBytes, out var searchable))
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
                        if (SearchTextFile(fullPath, relativePath, request, expression, limits, matches, cancellationToken))
                        {
                            limitReached = true;
                        }
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

                    if (matches.Count >= limits.MaximumResults)
                    {
                        limitReached = true;
                        stopTraversal = true;
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
            normalizedRootPath,
            request.Query,
            request.Mode,
            matches,
            filesExamined,
            filesSkipped,
            directoriesSkipped,
            limits.MaximumResults,
            limits.MaximumFiles,
            limits.MaximumFileBytes,
            limits.MaximumTraversalDepth,
            limits.MaximumMatchesPerFile,
            limits.MaximumPreviewCharacters,
            _scalePolicy.BinaryProbeBytes,
            limitReached);
    }

    private static bool SearchTextFile(
        string fullPath,
        string relativePath,
        ProjectSearchRequest request,
        Regex? expression,
        SearchLimits limits,
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
        var fileMatches = 0;
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            var fileLimitReached = request.Mode == ProjectSearchMode.Content
                ? AddLiteralMatches(line, fullPath, relativePath, lineNumber, request, limits, matches, ref fileMatches)
                : AddRegularExpressionMatches(
                    line,
                    fullPath,
                    relativePath,
                    lineNumber,
                    expression ?? throw new InvalidOperationException("Regular expression search was not initialized."),
                    limits,
                    matches,
                    ref fileMatches);

            if (fileLimitReached)
            {
                return true;
            }
            if (matches.Count >= limits.MaximumResults)
            {
                return false;
            }
        }

        return false;
    }

    private static bool AddLiteralMatches(
        string line,
        string fullPath,
        string relativePath,
        int lineNumber,
        ProjectSearchRequest request,
        SearchLimits limits,
        List<ProjectSearchMatch> matches,
        ref int fileMatches)
    {
        var comparison = request.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var startIndex = 0;
        while (startIndex <= line.Length - request.Query.Length)
        {
            var matchIndex = line.IndexOf(request.Query, startIndex, comparison);
            if (matchIndex < 0)
            {
                return false;
            }

            matches.Add(new ProjectSearchMatch(
                fullPath,
                relativePath,
                lineNumber,
                matchIndex + 1,
                BuildPreview(line, matchIndex, request.Query.Length, limits.MaximumPreviewCharacters)));
            fileMatches++;
            if (fileMatches >= limits.MaximumMatchesPerFile)
            {
                return true;
            }
            if (matches.Count >= limits.MaximumResults)
            {
                return false;
            }
            startIndex = matchIndex + Math.Max(request.Query.Length, 1);
        }

        return false;
    }

    private static bool AddRegularExpressionMatches(
        string line,
        string fullPath,
        string relativePath,
        int lineNumber,
        Regex expression,
        SearchLimits limits,
        List<ProjectSearchMatch> matches,
        ref int fileMatches)
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
                    fullPath,
                    relativePath,
                    lineNumber,
                    match.Index + 1,
                    BuildPreview(line, match.Index, Math.Max(match.Length, 1), limits.MaximumPreviewCharacters)));
                fileMatches++;
                if (fileMatches >= limits.MaximumMatchesPerFile)
                {
                    return true;
                }
                if (matches.Count >= limits.MaximumResults)
                {
                    return false;
                }
            }
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new ProjectSearchQueryException(
                "The regular expression exceeded the bounded evaluation time. Simplify the pattern and try again.", exception);
        }

        return false;
    }

    private static string BuildPreview(string line, int matchIndex, int matchLength, int maximumPreviewCharacters)
    {
        if (line.Length <= maximumPreviewCharacters)
        {
            return line;
        }
        var desiredStart = Math.Max(0, matchIndex - Math.Min(80, maximumPreviewCharacters / 3));
        var maximumStart = Math.Max(0, line.Length - maximumPreviewCharacters);
        var start = Math.Min(desiredStart, maximumStart);
        if (matchIndex + matchLength > start + maximumPreviewCharacters)
        {
            start = Math.Min(maximumStart, matchIndex + matchLength - maximumPreviewCharacters);
        }
        var length = Math.Min(maximumPreviewCharacters, line.Length - start);
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

    private bool LooksBinary(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            _scalePolicy.BinaryProbeBytes,
            FileOptions.SequentialScan);
        var buffer = new byte[_scalePolicy.BinaryProbeBytes];
        var bytesRead = stream.Read(buffer);
        var bytes = buffer.AsSpan(0, bytesRead);
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

    private SearchLimits ResolveLimits(ProjectSearchRequest request)
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

        ValidateBound(request.MaximumResults, _scalePolicy.MaximumSearchResults, nameof(request.MaximumResults), "Maximum results");
        ValidateBound(request.MaximumFiles, _scalePolicy.MaximumFilesPerOperation, nameof(request.MaximumFiles), "Maximum files");
        ValidateBound(request.MaximumFileBytes, _scalePolicy.MaximumSearchFileBytes, nameof(request.MaximumFileBytes), "Maximum searchable file size");

        var maximumTraversalDepth = request.MaximumTraversalDepth ?? _scalePolicy.MaximumTraversalDepth;
        ValidateBound(maximumTraversalDepth, _scalePolicy.MaximumTraversalDepth, nameof(request.MaximumTraversalDepth), "Maximum traversal depth");
        var maximumMatchesPerFile = request.MaximumMatchesPerFile ?? _scalePolicy.MaximumSearchMatchesPerFile;
        ValidateBound(maximumMatchesPerFile, _scalePolicy.MaximumSearchMatchesPerFile, nameof(request.MaximumMatchesPerFile), "Maximum matches per file");
        var maximumPreviewCharacters = request.MaximumPreviewCharacters ?? _scalePolicy.MaximumPreviewCharacters;
        if (maximumPreviewCharacters < WorkspaceScalePolicy.MinimumPreviewCharacters
            || maximumPreviewCharacters > _scalePolicy.MaximumPreviewCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                maximumPreviewCharacters,
                $"Maximum preview characters must be between {WorkspaceScalePolicy.MinimumPreviewCharacters} and {_scalePolicy.MaximumPreviewCharacters}.");
        }

        return new SearchLimits(
            request.MaximumResults,
            request.MaximumFiles,
            request.MaximumFileBytes,
            maximumTraversalDepth,
            maximumMatchesPerFile,
            maximumPreviewCharacters);
    }

    private static void ValidateBound(int value, int maximum, string parameterName, string description)
    {
        if (value < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{description} must be between 1 and {maximum}.");
        }
    }

    private static void ValidateBound(long value, long maximum, string parameterName, string description)
    {
        if (value < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{description} must be between 1 and {maximum}.");
        }
    }

    private readonly record struct SearchLimits(
        int MaximumResults,
        int MaximumFiles,
        long MaximumFileBytes,
        int MaximumTraversalDepth,
        int MaximumMatchesPerFile,
        int MaximumPreviewCharacters);
}
