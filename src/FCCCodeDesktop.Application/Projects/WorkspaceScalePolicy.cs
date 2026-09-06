using System.Collections.ObjectModel;

namespace FCCCodeDesktop.Application.Projects;

public sealed class WorkspaceScalePolicy
{
    public const int DefaultMaximumDirectoryEntries = 2_048;
    public const int MaximumSupportedDirectoryEntries = 20_000;
    public const int DefaultMaximumTraversalDepth = 64;
    public const int MaximumSupportedTraversalDepth = 256;
    public const int DefaultMaximumFilesPerOperation = 20_000;
    public const int MaximumSupportedFilesPerOperation = 100_000;
    public const int DefaultMaximumSearchResults = 500;
    public const int MaximumSupportedSearchResults = 5_000;
    public const int DefaultMaximumSearchMatchesPerFile = 100;
    public const int MaximumSupportedSearchMatchesPerFile = 5_000;
    public const long DefaultMaximumTextFileBytes = 8L * 1024 * 1024;
    public const long MaximumSupportedTextFileBytes = 128L * 1024 * 1024;
    public const long DefaultMaximumSearchFileBytes = 4L * 1024 * 1024;
    public const long MaximumSupportedSearchFileBytes = 64L * 1024 * 1024;
    public const int DefaultMaximumPreviewCharacters = 240;
    public const int MinimumPreviewCharacters = 32;
    public const int MaximumSupportedPreviewCharacters = 4_096;
    public const int DefaultBinaryProbeBytes = 4_096;
    public const int MinimumBinaryProbeBytes = 64;
    public const int MaximumSupportedBinaryProbeBytes = 64 * 1024;
    public const int MaximumExcludedDirectoryNames = 256;

    private static readonly string[] BuiltInExcludedDirectoryNames =
    [
        ".git",
        ".hg",
        ".svn",
        ".vs",
        "bin",
        "obj",
        "node_modules",
        "packages",
        "dist",
        "build",
        "Library",
        "Temp",
        "Logs",
    ];

    private readonly ReadOnlySet<string> _excludedDirectoryNames;

    public WorkspaceScalePolicy(
        int maximumDirectoryEntries = DefaultMaximumDirectoryEntries,
        int maximumTraversalDepth = DefaultMaximumTraversalDepth,
        int maximumFilesPerOperation = DefaultMaximumFilesPerOperation,
        int maximumSearchResults = DefaultMaximumSearchResults,
        int maximumSearchMatchesPerFile = DefaultMaximumSearchMatchesPerFile,
        long maximumTextFileBytes = DefaultMaximumTextFileBytes,
        long maximumSearchFileBytes = DefaultMaximumSearchFileBytes,
        int maximumPreviewCharacters = DefaultMaximumPreviewCharacters,
        int binaryProbeBytes = DefaultBinaryProbeBytes,
        IEnumerable<string>? excludedDirectoryNames = null)
    {
        ValidateRange(
            maximumDirectoryEntries,
            1,
            MaximumSupportedDirectoryEntries,
            nameof(maximumDirectoryEntries),
            "Directory entry limit");
        ValidateRange(
            maximumTraversalDepth,
            1,
            MaximumSupportedTraversalDepth,
            nameof(maximumTraversalDepth),
            "Traversal depth limit");
        ValidateRange(
            maximumFilesPerOperation,
            1,
            MaximumSupportedFilesPerOperation,
            nameof(maximumFilesPerOperation),
            "File examination limit");
        ValidateRange(
            maximumSearchResults,
            1,
            MaximumSupportedSearchResults,
            nameof(maximumSearchResults),
            "Search result limit");
        ValidateRange(
            maximumSearchMatchesPerFile,
            1,
            MaximumSupportedSearchMatchesPerFile,
            nameof(maximumSearchMatchesPerFile),
            "Per-file search match limit");
        ValidateRange(
            maximumTextFileBytes,
            1,
            MaximumSupportedTextFileBytes,
            nameof(maximumTextFileBytes),
            "Text file byte limit");
        ValidateRange(
            maximumSearchFileBytes,
            1,
            MaximumSupportedSearchFileBytes,
            nameof(maximumSearchFileBytes),
            "Search file byte limit");
        ValidateRange(
            maximumPreviewCharacters,
            MinimumPreviewCharacters,
            MaximumSupportedPreviewCharacters,
            nameof(maximumPreviewCharacters),
            "Preview character limit");
        ValidateRange(
            binaryProbeBytes,
            MinimumBinaryProbeBytes,
            MaximumSupportedBinaryProbeBytes,
            nameof(binaryProbeBytes),
            "Binary probe byte limit");

        var exclusions = BuildExcludedDirectoryNames(excludedDirectoryNames);
        MaximumDirectoryEntries = maximumDirectoryEntries;
        MaximumTraversalDepth = maximumTraversalDepth;
        MaximumFilesPerOperation = maximumFilesPerOperation;
        MaximumSearchResults = maximumSearchResults;
        MaximumSearchMatchesPerFile = maximumSearchMatchesPerFile;
        MaximumTextFileBytes = maximumTextFileBytes;
        MaximumSearchFileBytes = maximumSearchFileBytes;
        MaximumPreviewCharacters = maximumPreviewCharacters;
        BinaryProbeBytes = binaryProbeBytes;
        _excludedDirectoryNames = new ReadOnlySet<string>(exclusions);
    }

    public static WorkspaceScalePolicy Default { get; } = new();

    public int MaximumDirectoryEntries { get; }

    public int MaximumTraversalDepth { get; }

    public int MaximumFilesPerOperation { get; }

    public int MaximumSearchResults { get; }

    public int MaximumSearchMatchesPerFile { get; }

    public long MaximumTextFileBytes { get; }

    public long MaximumSearchFileBytes { get; }

    public int MaximumPreviewCharacters { get; }

    public int BinaryProbeBytes { get; }

    public IReadOnlySet<string> ExcludedDirectoryNames => _excludedDirectoryNames;

    public bool ShouldExcludeDirectory(string directoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryName);
        return _excludedDirectoryNames.Contains(directoryName);
    }

    private static HashSet<string> BuildExcludedDirectoryNames(IEnumerable<string>? directoryNames)
    {
        var exclusions = new HashSet<string>(BuiltInExcludedDirectoryNames, StringComparer.OrdinalIgnoreCase);
        if (directoryNames is null)
        {
            return exclusions;
        }

        foreach (var rawName in directoryNames)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                throw new ArgumentException(
                    "Excluded directory names cannot contain a null, empty, or whitespace item.",
                    nameof(directoryNames));
            }

            var name = rawName.Trim();
            if (name is "." or ".."
                || name.Length > 255
                || name.Contains(Path.DirectorySeparatorChar)
                || name.Contains(Path.AltDirectorySeparatorChar)
                || name.Contains(Path.VolumeSeparatorChar))
            {
                throw new ArgumentException(
                    $"Excluded directory name is not a single safe path segment: {rawName}",
                    nameof(directoryNames));
            }

            _ = exclusions.Add(name);
            if (exclusions.Count > MaximumExcludedDirectoryNames)
            {
                throw new ArgumentException(
                    $"At most {MaximumExcludedDirectoryNames} excluded directory names are supported.",
                    nameof(directoryNames));
            }
        }

        return exclusions;
    }

    private static void ValidateRange(
        int value,
        int minimum,
        int maximum,
        string parameterName,
        string description)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{description} must be between {minimum} and {maximum}.");
        }
    }

    private static void ValidateRange(
        long value,
        long minimum,
        long maximum,
        string parameterName,
        string description)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{description} must be between {minimum} and {maximum}.");
        }
    }
}
