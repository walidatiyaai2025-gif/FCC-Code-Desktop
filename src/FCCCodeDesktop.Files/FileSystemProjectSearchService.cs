using System.Buffers;
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

    private readonly WorkspaceScalePolicy _policy;

    public FileSystemProjectSearchService()
        : this(WorkspaceScalePolicy.Default)
    {
    }

    public FileSystemProjectSearchService(WorkspaceScalePolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public Task<ProjectSearchResultSet> SearchAsync(
        ProjectSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        return Task.Run(() => SearchCore(request, cancellationToken), cancellationToken);
    }

    private ProjectSearchResultSet SearchCore(
        ProjectSearchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRootPath = Path.GetFullPath(request.ProjectRootPath);
        if (!Directory.Exists(normalizedRootPath))
        {
            throw new DirectoryNotFoundException($"Project folder does not exist: {normalizedRootPath}");
        }

        var expression = BuildExpression(request);
        var matches = new List<ProjectSearchMatch>(Math.Min(request.MaximumResults, 256));
        var pendingDirectories = new Stack<PendingDirectory>();
        pendingDirectories.Push(new PendingDirectory(normalizedRootPath, 0));
        var filesExamined = 0;
        var filesSkipped = 0;
        var directoriesSkipped = 0;
        var limitReasons = ProjectSearchLimitReason.None;
        var stopTraversal = false;

        while (pendingDirectories.Count > 0 && !stopTraversal)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pendingDirectory = pendingDirectories.Pop();
            string[] entries;
            bool directoryEntryLimitReached;
            try
            {
                entries = EnumerateDirectoryEntries(
                    pendingDirectory.Path,
                    cancellationToken,
                    out directoryEntryLimitReached);
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

            if (directoryEntryLimitReached)
            {
                limitReasons |= ProjectSearchLimitReason.DirectoryEntries;
            }

            var childDirectories = new List<string>();
            foreach (var entryPath in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryGetAttributes(entryPath, out var attributes))
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
                    if (isReparsePoint || _policy.ShouldExcludeDirectory(directoryName))
                    {
                        directoriesSkipped++;
                    }
                    else if (pendingDirectory.Depth >= request.MaximumTraversalDepth)
                    {
                        directoriesSkipped++;
                        limitReasons |= ProjectSearchLimitReason.TraversalDepth;
                    }
                    else
                    {
                        childDirectories.Add(fullPath);
                    }
                    continue;
                }

                if (isReparsePoint)
                {
                    filesSkipped++;
                    continue;
                }

                if (filesExamined >= request.MaximumFiles)
                {
                    limitReasons |= ProjectSearchLimitReason.Files;
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
                        if (matches.Count >= request.MaximumResults)
                        {
                            limitReasons |= ProjectSearchLimitReason.Results;
                            stopTraversal = true;
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
                    var perFileLimitReached = SearchTextFile(
                        fullPath,
                        relativePath,
                        request,
                        expression,
                        matches,
                        cancellationToken);
                    if (perFileLimitReached)
                    {
                        limitReasons |= ProjectSearchLimitReason.MatchesPerFile;
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

                if (matches.Count >= request.MaximumResults)
                {
                    limitReasons |= ProjectSearchLimitReason.Results;
                    stopTraversal = true;
                    break;
                }
            }

            for (var index = childDirectories.Count - 1; index >= 0; index--)
            {
                pendingDirectories.Push(
                    new PendingDirectory(childDirectories[index], pendingDirectory.Depth + 1));
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
            request.MaximumResults,
            request.MaximumFiles,
            request.MaximumFileBytes,
            limitReasons != ProjectSearchLimitReason.None,
            request.MaximumTraversalDepth,
            request.MaximumMatchesPerFile,
            request.MaximumPreviewCharacters,
            limitReasons);
    }

    private static Regex? BuildExpression(ProjectSearchRequest request)
    {
        if (request.Mode != ProjectSearchMode.RegularExpression)
        {
            return null;
        }

        try
        {
            return new Regex(
                request.Query,
                RegexOptions.CultureInvariant | (request.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase),
                RegularExpressionTimeout);
        }
        catch (ArgumentException exception)
        {
            throw new ProjectSearchQueryException("The regular expression is invalid.", exception);
        }
    }

    private string[] EnumerateDirectoryEntries(
        string directoryPath,
        CancellationToken cancellationToken,
        out bool limitReached)
    {
        var entries = new List<string>(Math.Min(_policy.MaximumDirectoryEntries + 1, 256));
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(entryPath);
            if (entries.Count > _policy.MaximumDirectoryEntries)
            {
                break;
            }
        }

        limitReached = entries.Count > _policy.MaximumDirectoryEntries;
        if (limitReached)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        return entries
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            attributes = default;
            return false;
        }
        catch (IOException)
        {
            attributes = default;
            return false;
        }
    }

    private static bool SearchTextFile(
        string fullPath,
        string relativePath,
        ProjectSearchRequest request,
        Regex? expression,
        List<ProjectSearchMatch> matches,
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4_096,
            FileOptions.SequentialScan);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4_096,
            leaveOpen: false);

        var lineNumber = 0;
        var fileMatchCount = 0;
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            var maximumToAdd = Math.Min(
                request.MaximumResults - matches.Count,
                request.MaximumMatchesPerFile - fileMatchCount);
            if (maximumToAdd <= 0)
            {
                return fileMatchCount >= request.MaximumMatchesPerFile;
            }

            var added = request.Mode == ProjectSearchMode.Content
                ? AddLiteralMatches(
                    line,
                    fullPath,
                    relativePath,
                    lineNumber,
                    request,
                    maximumToAdd,
                    matches)
                : AddRegularExpressionMatches(
                    line,
                    fullPath,
                    relativePath,
                    lineNumber,
                    expression ?? throw new InvalidOperationException("Regular expression search was not initialized."),
                    request.MaximumPreviewCharacters,
                    maximumToAdd,
                    matches);
            fileMatchCount += added;
            if (matches.Count >= request.MaximumResults)
            {
                return false;
            }
            if (fileMatchCount >= request.MaximumMatchesPerFile)
            {
                return true;
            }
        }

        return false;
    }

    private static int AddLiteralMatches(
        string line,
        string fullPath,
        string relativePath,
        int lineNumber,
        ProjectSearchRequest request,
        int maximumToAdd,
        List<ProjectSearchMatch> matches)
    {
        var comparison = request.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var startIndex = 0;
        var added = 0;
        while (startIndex <= line.Length - request.Query.Length && added < maximumToAdd)
        {
            var matchIndex = line.IndexOf(request.Query, startIndex, comparison);
            if (matchIndex < 0)
            {
                break;
            }
            matches.Add(
                new ProjectSearchMatch(
                    fullPath,
                    relativePath,
                    lineNumber,
                    matchIndex + 1,
                    BuildPreview(
                        line,
                        matchIndex,
                        request.Query.Length,
                        request.MaximumPreviewCharacters)));
            added++;
            startIndex = matchIndex + Math.Max(request.Query.Length, 1);
        }

        return added;
    }

    private static int AddRegularExpressionMatches(
        string line,
        string fullPath,
        string relativePath,
        int lineNumber,
        Regex expression,
        int maximumPreviewCharacters,
        int maximumToAdd,
        List<ProjectSearchMatch> matches)
    {
        var added = 0;
        try
        {
            foreach (Match match in expression.Matches(line))
            {
                if (!match.Success)
                {
                    continue;
                }
                matches.Add(
                    new ProjectSearchMatch(
                        fullPath,
                        relativePath,
                        lineNumber,
                        match.Index + 1,
                        BuildPreview(
                            line,
                            match.Index,
                            Math.Max(match.Length, 1),
                            maximumPreviewCharacters)));
                added++;
                if (added >= maximumToAdd)
                {
                    break;
                }
            }
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new ProjectSearchQueryException(
                "The regular expression exceeded the bounded evaluation time. Simplify the pattern and try again.",
                exception);
        }

        return added;
    }

    private static string BuildPreview(
        string line,
        int matchIndex,
        int matchLength,
        int maximumPreviewCharacters)
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

    private static bool TryGetSearchableFileLength(
        string path,
        long maximumFileBytes,
        out bool searchable)
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
        var buffer = ArrayPool<byte>.Shared.Rent(_policy.BinaryProbeBytes);
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                Math.Min(_policy.BinaryProbeBytes, 64 * 1024),
                FileOptions.SequentialScan);
            var bytesRead = stream.Read(buffer, 0, _policy.BinaryProbeBytes);
            var bytes = buffer.AsSpan(0, bytesRead);
            return !HasUnicodeBom(bytes) && bytes.Contains((byte)0);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
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

    private void ValidateRequest(ProjectSearchRequest request)
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
        ValidateLimit(request.MaximumResults, _policy.MaximumSearchResults, "Maximum results");
        ValidateLimit(request.MaximumFiles, _policy.MaximumFilesPerOperation, "Maximum files");
        ValidateLimit(request.MaximumFileBytes, _policy.MaximumSearchFileBytes, "Maximum searchable file size");
        ValidateLimit(request.MaximumTraversalDepth, _policy.MaximumTraversalDepth, "Maximum traversal depth");
        ValidateLimit(request.MaximumMatchesPerFile, _policy.MaximumSearchMatchesPerFile, "Maximum matches per file");
        ValidateLimit(request.MaximumPreviewCharacters, _policy.MaximumPreviewCharacters, "Maximum preview characters");
    }

    private static void ValidateLimit(int value, int maximum, string description)
    {
        if (value < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"{description} must be between 1 and {maximum}.");
        }
    }

    private static void ValidateLimit(long value, long maximum, string description)
    {
        if (value < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"{description} must be between 1 and {maximum}.");
        }
    }

    private readonly record struct PendingDirectory(string Path, int Depth);
}
