using FCCCodeDesktop.Application.Git;

namespace FCCCodeDesktop.Git;

/// <summary>
/// Read-only provenance tracker for dirty paths that existed before an autonomous operation began.
/// The service is intentionally conservative: any current path that intersects a baseline path or
/// rename alias remains pre-existing/owner-sensitive, even if additional edits occurred later.
/// </summary>
public sealed class GitChangeProvenanceService : IGitChangeProvenanceService
{
    public const int DefaultMaximumDirtyPaths = 4096;
    public const int MaximumDirtyPaths = 65536;

    private readonly IGitService _gitService;
    private readonly TimeProvider _timeProvider;
    private readonly int _maximumDirtyPaths;

    public GitChangeProvenanceService(
        IGitService? gitService = null,
        TimeProvider? timeProvider = null,
        int maximumDirtyPaths = DefaultMaximumDirtyPaths)
    {
        if (maximumDirtyPaths <= 0 || maximumDirtyPaths > MaximumDirtyPaths)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDirtyPaths),
                maximumDirtyPaths,
                $"Dirty provenance path limit must be greater than zero and no more than {MaximumDirtyPaths}.");
        }

        _gitService = gitService ?? new GitCliService();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maximumDirtyPaths = maximumDirtyPaths;
    }

    public async Task<GitDirtyBaselineCaptureResult> CaptureBaselineAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var status = await _gitService.GetStatusAsync(path, cancellationToken).ConfigureAwait(false);
        var queryStatus = MapStatus(status.Status);
        if (queryStatus != GitChangeProvenanceQueryStatus.Success)
        {
            return new GitDirtyBaselineCaptureResult(queryStatus, status.RepositoryRootPath, null);
        }

        if (string.IsNullOrWhiteSpace(status.RepositoryRootPath))
        {
            return new GitDirtyBaselineCaptureResult(
                GitChangeProvenanceQueryStatus.QueryFailed,
                status.RepositoryRootPath,
                null);
        }

        if (status.Files.Count > _maximumDirtyPaths)
        {
            return new GitDirtyBaselineCaptureResult(
                GitChangeProvenanceQueryStatus.TooManyChanges,
                status.RepositoryRootPath,
                null);
        }

        var entries = status.Files
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .Select(ToBaselineEntry)
            .ToArray();
        var repositoryRoot = NormalizeRepositoryRoot(status.RepositoryRootPath);
        var baseline = new GitDirtyBaselineSnapshot(
            repositoryRoot,
            _timeProvider.GetUtcNow(),
            entries);

        return new GitDirtyBaselineCaptureResult(
            GitChangeProvenanceQueryStatus.Success,
            repositoryRoot,
            baseline);
    }

    public async Task<GitChangeProvenanceResult> CompareAsync(
        string path,
        GitDirtyBaselineSnapshot baseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseline.RepositoryRootPath);
        ArgumentNullException.ThrowIfNull(baseline.Entries);
        cancellationToken.ThrowIfCancellationRequested();

        if (baseline.Entries.Count > _maximumDirtyPaths)
        {
            return EmptyComparison(GitChangeProvenanceQueryStatus.TooManyChanges, null, baseline);
        }

        var status = await _gitService.GetStatusAsync(path, cancellationToken).ConfigureAwait(false);
        var queryStatus = MapStatus(status.Status);
        if (queryStatus != GitChangeProvenanceQueryStatus.Success)
        {
            return EmptyComparison(queryStatus, status.RepositoryRootPath, baseline);
        }

        if (string.IsNullOrWhiteSpace(status.RepositoryRootPath))
        {
            return EmptyComparison(GitChangeProvenanceQueryStatus.QueryFailed, null, baseline);
        }

        var repositoryRoot = NormalizeRepositoryRoot(status.RepositoryRootPath);
        var baselineRoot = NormalizeRepositoryRoot(baseline.RepositoryRootPath);
        if (!RepositoryRootsEqual(repositoryRoot, baselineRoot))
        {
            return EmptyComparison(
                GitChangeProvenanceQueryStatus.BaselineRepositoryMismatch,
                repositoryRoot,
                baseline);
        }

        if (status.Files.Count > _maximumDirtyPaths)
        {
            return EmptyComparison(
                GitChangeProvenanceQueryStatus.TooManyChanges,
                repositoryRoot,
                baseline);
        }

        var aliasMap = BuildBaselineAliasMap(baseline.Entries);
        var matchedBaselineIndexes = new HashSet<int>();
        var currentChanges = new List<GitChangeProvenanceEntry>(status.Files.Count);

        foreach (var file in status.Files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matchIndexes = new SortedSet<int>();
            AddMatches(aliasMap, file.Path, matchIndexes);
            if (file.OriginalPath is not null)
            {
                AddMatches(aliasMap, file.OriginalPath, matchIndexes);
            }

            foreach (var index in matchIndexes)
            {
                matchedBaselineIndexes.Add(index);
            }

            var matches = matchIndexes
                .Select(index => baseline.Entries[index])
                .ToArray();
            var origin = matches.Length == 0
                ? GitChangeProvenanceOrigin.CreatedSinceBaseline
                : GitChangeProvenanceOrigin.PreExistingDirty;

            currentChanges.Add(
                new GitChangeProvenanceEntry(
                    file.Path,
                    file.OriginalPath,
                    file.IndexChange,
                    file.WorkTreeChange,
                    origin,
                    matches));
        }

        var resolvedPreExistingChanges = baseline.Entries
            .Select((entry, index) => (entry, index))
            .Where(pair => !matchedBaselineIndexes.Contains(pair.index))
            .Select(pair => pair.entry)
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToArray();

        return new GitChangeProvenanceResult(
            GitChangeProvenanceQueryStatus.Success,
            repositoryRoot,
            baseline,
            currentChanges,
            resolvedPreExistingChanges);
    }

    private static GitChangeProvenanceQueryStatus MapStatus(GitStatusQueryStatus status) =>
        status switch
        {
            GitStatusQueryStatus.Success => GitChangeProvenanceQueryStatus.Success,
            GitStatusQueryStatus.NotRepository => GitChangeProvenanceQueryStatus.NotRepository,
            GitStatusQueryStatus.BareRepository => GitChangeProvenanceQueryStatus.BareRepository,
            GitStatusQueryStatus.GitUnavailable => GitChangeProvenanceQueryStatus.GitUnavailable,
            GitStatusQueryStatus.QueryFailed => GitChangeProvenanceQueryStatus.QueryFailed,
            _ => GitChangeProvenanceQueryStatus.QueryFailed,
        };

    private static GitDirtyBaselineEntry ToBaselineEntry(GitFileStatusEntry file) =>
        new(
            NormalizeRepositoryRelativePath(file.Path),
            file.OriginalPath is null ? null : NormalizeRepositoryRelativePath(file.OriginalPath),
            file.IndexChange,
            file.WorkTreeChange);

    private static Dictionary<string, List<int>> BuildBaselineAliasMap(
        IReadOnlyList<GitDirtyBaselineEntry> entries)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var aliasMap = new Dictionary<string, List<int>>(comparer);
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            AddAlias(aliasMap, entry.Path, index);
            if (entry.OriginalPath is not null)
            {
                AddAlias(aliasMap, entry.OriginalPath, index);
            }
        }

        return aliasMap;
    }

    private static void AddAlias(
        Dictionary<string, List<int>> aliasMap,
        string path,
        int index)
    {
        var normalizedPath = NormalizeRepositoryRelativePath(path);
        if (!aliasMap.TryGetValue(normalizedPath, out var indexes))
        {
            indexes = [];
            aliasMap.Add(normalizedPath, indexes);
        }

        if (!indexes.Contains(index))
        {
            indexes.Add(index);
        }
    }

    private static void AddMatches(
        Dictionary<string, List<int>> aliasMap,
        string path,
        SortedSet<int> matches)
    {
        var normalizedPath = NormalizeRepositoryRelativePath(path);
        if (!aliasMap.TryGetValue(normalizedPath, out var indexes))
        {
            return;
        }

        foreach (var index in indexes)
        {
            matches.Add(index);
        }
    }

    private static GitChangeProvenanceResult EmptyComparison(
        GitChangeProvenanceQueryStatus status,
        string? repositoryRootPath,
        GitDirtyBaselineSnapshot baseline) =>
        new(
            status,
            repositoryRootPath,
            baseline,
            Array.Empty<GitChangeProvenanceEntry>(),
            Array.Empty<GitDirtyBaselineEntry>());

    private static string NormalizeRepositoryRelativePath(string path) =>
        path.Replace('\\', '/');

    private static string NormalizeRepositoryRoot(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool RepositoryRootsEqual(string left, string right) =>
        string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
