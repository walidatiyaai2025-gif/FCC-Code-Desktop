namespace FCCCodeDesktop.Application.Projects;

public enum ProjectTechnologyConfidence
{
    Marker = 1,
    Strong = 2,
    Definitive = 3,
}

public sealed record ProjectTechnologyDetection(
    string Id,
    string DisplayName,
    string Toolchain,
    string MarkerPath,
    ProjectTechnologyConfidence Confidence);

public sealed record ProjectTechnologyScanResult(
    string RootPath,
    IReadOnlyList<ProjectTechnologyDetection> Technologies,
    int EntriesExamined,
    int SkippedPaths,
    int MaximumDepth,
    int MaximumEntries,
    bool LimitReached)
{
    public bool HasDetections => Technologies.Count > 0;
}

public interface IProjectTechnologyDetectionService
{
    Task<ProjectTechnologyScanResult> DetectAsync(
        string rootPath,
        CancellationToken cancellationToken = default);
}
