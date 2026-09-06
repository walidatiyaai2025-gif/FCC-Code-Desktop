namespace FCCCodeDesktop.Application.Git;

public enum GitChangeProvenanceQueryStatus
{
    Success = 0,
    NotRepository = 1,
    BareRepository = 2,
    GitUnavailable = 3,
    QueryFailed = 4,
    TooManyChanges = 5,
    BaselineRepositoryMismatch = 6,
}

public enum GitChangeProvenanceOrigin
{
    PreExistingDirty = 0,
    CreatedSinceBaseline = 1,
}

public sealed record GitDirtyBaselineEntry(
    string Path,
    string? OriginalPath,
    GitFileChangeKind IndexChange,
    GitFileChangeKind WorkTreeChange)
{
    public bool IsConflicted =>
        IndexChange == GitFileChangeKind.Unmerged || WorkTreeChange == GitFileChangeKind.Unmerged;
}

public sealed record GitDirtyBaselineSnapshot(
    string RepositoryRootPath,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<GitDirtyBaselineEntry> Entries)
{
    public bool WasClean => Entries.Count == 0;
}

public sealed record GitDirtyBaselineCaptureResult(
    GitChangeProvenanceQueryStatus Status,
    string? RepositoryRootPath,
    GitDirtyBaselineSnapshot? Baseline)
{
    public bool IsSuccess => Status == GitChangeProvenanceQueryStatus.Success && Baseline is not null;
}

public sealed record GitChangeProvenanceEntry(
    string Path,
    string? OriginalPath,
    GitFileChangeKind IndexChange,
    GitFileChangeKind WorkTreeChange,
    GitChangeProvenanceOrigin Origin,
    IReadOnlyList<GitDirtyBaselineEntry> BaselineMatches)
{
    public bool WasDirtyAtBaseline => Origin == GitChangeProvenanceOrigin.PreExistingDirty;

    public bool IsConflicted =>
        IndexChange == GitFileChangeKind.Unmerged || WorkTreeChange == GitFileChangeKind.Unmerged;
}

public sealed record GitChangeProvenanceResult(
    GitChangeProvenanceQueryStatus Status,
    string? RepositoryRootPath,
    GitDirtyBaselineSnapshot Baseline,
    IReadOnlyList<GitChangeProvenanceEntry> CurrentChanges,
    IReadOnlyList<GitDirtyBaselineEntry> ResolvedPreExistingChanges)
{
    public bool IsSuccess => Status == GitChangeProvenanceQueryStatus.Success;

    public bool HasPreExistingOverlap =>
        CurrentChanges.Any(change => change.Origin == GitChangeProvenanceOrigin.PreExistingDirty);

    public bool HasNewChanges =>
        CurrentChanges.Any(change => change.Origin == GitChangeProvenanceOrigin.CreatedSinceBaseline);
}

/// <summary>
/// Captures and reconciles repository dirty-path lineage without mutating refs, the index,
/// the work tree, repository configuration, or remotes. A path that overlaps the baseline
/// remains owner-sensitive; this contract deliberately does not infer byte-level actor ownership.
/// </summary>
public interface IGitChangeProvenanceService
{
    Task<GitDirtyBaselineCaptureResult> CaptureBaselineAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task<GitChangeProvenanceResult> CompareAsync(
        string path,
        GitDirtyBaselineSnapshot baseline,
        CancellationToken cancellationToken = default);
}
