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
    public const int MaximumSupportedMatchesPerFile = WorkspaceScalePolicy.MaximumSupportedSearchMatchesPerFile;
    public const int MaximumSupportedTraversalDepth = WorkspaceScalePolicy.MaximumSupportedTraversalDepth;

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
        var pendingDirectories = new Stack<SearchDirectory>();
        pendingDirectories.Push(new SearchDirectory(normalizedRootPath, 0));
        var filesExamined = 0;
        var filesSkipped = 0;
        var directoriesSkipped = 0;
        var limitReached = false;
        var stopSearch = false;

        while (pendingDirectories.Count > 0 && !stopSearch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = pendingDirectories.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(currentDirectory.Path);
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
                        if (isReparsePoint || _policy.ShouldExcludeDirectory(directoryName))
                        {
                            directoriesSkipped++;
                            continue;
                        }

                        if (currentDirectory.Depth >= request.MaximumTraversalDepth)
                        {
                            directoriesSkipped++;
                            limitReached = true;
                            continue;
                        }

                        pendingDirectories.Push(new SearchDirectory(fullPath, currentDirectory.Depth + 1));
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
                        stopSearch = true;
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
                                stopSearch = true;
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
                        var fileOutcome = SearchTextFile(
                            fullPath,
                            relativePath,
                            request,
                            expression,
                            matches,
                            cancellationToken);
                        limitReached |= fileOutcome.PerFileLimitReached || fileOutcome.GlobalLimitReached;
                        if (fileOutcome.GlobalLimitReached)
                        {
                            stopSearch = true;
                            break;
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
            request.MaximumResults,
            request.MaximumFiles,
            request.MaximumFileBytes,
            limitReached,
            request.MaximumMatchesPerFile,
            request.MaximumTraversalDepth,
            _policy.MaximumPreviewCharacters,
            _policy.BinaryProbeBytes);
    }

    private SearchFileOutcome SearchTextFile(
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
            4096,
            FileOptions.SequentialScan);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: false);

        var lineNumber = 0;
        var fileMatchCount = 0;
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            var outcome = request.Mode == ProjectSearchMode.Content
                ? AddLiteralMatches(
                    line,
                    fullPath,
                    relativePath,
                    lineNumber,
                    request,
                    matches,
                    ref fileMatchCount)
                : AddRegularExpressionMatches(
                    line,
                    fullPath,
                    relativePath,
                    lineNumber,
                    expression ?? throw new InvalidOperationException("Regular expression search was not initialized."),
                    request,
                    matches,
                    ref fileMatchCount);

            if (outcome.PerFileLimitReached || outcome.GlobalLimitReached)
            {
                return outcome;
            }
        }

        return SearchFileOutcome.Complete;
    }

    private SearchFileOutcome AddLiteralMatches(
        string line,
        string fullPath,
        string relativePath,
        int lineNumber,
        ProjectSearchRequest request,
        List<ProjectSearchMatch> matches,
        ref int fileMatchCount)
    {
        var comparison = request.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var startIndex = 0;
        while (startIndex <= line.Length - request.Query.Length)
        {
            var matchIndex = line.IndexOf(request.Query, startIndex, comparison);
            if (matchIndex < 0)
            {
                return SearchFileOutcome.Complete;
            }

            var budget = CheckMatchBudget(request, matches.Count, fileMatchCount);
            if (budget is not null)
            {
                return budget.Value;
            }

            matches.Add(
                new ProjectSearchMatch(
                    fullPath,
                    relativePath,
                    lineNumber,
                    matchIndex + 1,
                    BuildPreview(line, matchIndex, request.Query.Length)));
            fileMatchCount++;
            startIndex = matchIndex + Math.Max(request.Query.Length, 1);
        }

        return SearchFileOutcome.Complete;
    }

    private SearchFileOutcome AddRegularExpressionMatches(
        string line,
        string fullPath,
        string relativePath,
        int lineNumber,
        Regex expression,
        ProjectSearchRequest request,
        List<ProjectSearchMatch> matches,
        ref int fileMatchCount)
    {
        try
        {
            foreach (Match match in expression.Matches(line))
            {
                if (!match.Success)
                {
                    continue;
                }

                var budget = CheckMatchBudget(request, matches.Count, fileMatchCount);
                if (budget is not null)
                {
                    return budget.Value;
                }

                matches.Add(
                    new ProjectSearchMatch(
                        fullPath,
                        relativePath,
                        lineNumber,
                        match.Index + 1,
                        BuildPreview(line, match.Index, Math.Max(match.Length, 1))));
                fileMatchCount++;
            }
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new ProjectSearchQueryException(
                "The regular expression exceeded the bounded evaluation time. Simplify the pattern and try again.",
                exception);
        }

        return SearchFileOutcome.Complete;
    }

    private static SearchFileOutcome? CheckMatchBudget(
        ProjectSearchRequest request,
        int totalMatchCount,
        int fileMatchCount)
    {
        if (totalMatchCount >= request.MaximumResults)
        {
            return SearchFileOutcome.GlobalLimit;
        }

        if (fileMatchCount >= request.MaximumMatchesPerFile)
        {
            return SearchFileOutcome.PerFileLimit;
        }

        return null;
    }

    private string BuildPreview(string line, int matchIndex, int matchLength)
    {
        var maximumPreviewCharacters = _policy.MaximumPreviewCharacters;
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
        if (start > 0 && preview.Length > 0)
        {
            preview = $"…{preview[1..]}";
        }

        if (start + length < line.Length && preview.Length > 0)
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

        ValidateBound(
            request.MaximumResults,
            _policy.MaximumSearchResults,
            MaximumSupportedResults,
            nameof(request.MaximumResults),
            "Maximum results");
        ValidateBound(
            request.MaximumFiles,
            _policy.MaximumFilesPerOperation,
            MaximumSupportedFiles,
            nameof(request.MaximumFiles),
            "Maximum files");
        ValidateBound(
            request.MaximumFileBytes,
            _policy.MaximumSearchFileBytes,
            MaximumSupportedFileBytes,
            nameof(request.MaximumFileBytes),
            "Maximum searchable file size");
        ValidateBound(
            request.MaximumMatchesPerFile,
            _policy.MaximumSearchMatchesPerFile,
            MaximumSupportedMatchesPerFile,
            nameof(request.MaximumMatchesPerFile),
            "Maximum matches per file");
        ValidateBound(
            request.MaximumTraversalDepth,
            _policy.MaximumTraversalDepth,
            MaximumSupportedTraversalDepth,
            nameof(request.MaximumTraversalDepth),
            "Maximum traversal depth");
    }

    private static void ValidateBound(
        int value,
        int policyMaximum,
        int supportedMaximum,
        string parameterName,
        string label)
    {
        if (value < 1 || value > policyMaximum || value > supportedMaximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{label} must be between 1 and the active workspace policy maximum of {policyMaximum}.");
        }
    }

    private static void ValidateBound(
        long value,
        long policyMaximum,
        long supportedMaximum,
        string parameterName,
        string label)
    {
        if (value < 1 || value > policyMaximum || value > supportedMaximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{label} must be between 1 and the active workspace policy maximum of {policyMaximum}.");
        }
    }

    private readonly record struct SearchDirectory(string Path, int Depth);

    private readonly record struct SearchFileOutcome(bool PerFileLimitReached, bool GlobalLimitReached)
    {
        public static SearchFileOutcome Complete { get; } = new(false, false);

        public static SearchFileOutcome PerFileLimit { get; } = new(true, false);

        public static SearchFileOutcome GlobalLimit { get; } = new(false, true);
    }
}
