using System.Text;

namespace FCCCodeDesktop.Tools.Unity;

/// <summary>
/// Stable classifications for read-only Unity project detection.
/// </summary>
public enum UnityProjectDetectionStatus
{
    ValidUnityProject = 1,
    ProjectRootNotFound = 2,
    NotUnityProject = 3,
    UnityProjectIncomplete = 4,
    ProjectVersionMissing = 5,
    ProjectVersionReadFailure = 6,
}

/// <summary>
/// Snapshot of the canonical Unity project markers observed without launching Unity.
/// </summary>
public sealed record UnityProjectMarkerState(
    bool RootExists,
    bool AssetsDirectoryExists,
    bool PackagesDirectoryExists,
    bool ProjectSettingsDirectoryExists,
    bool ProjectVersionFileExists)
{
    public bool HasAnyUnityMarker =>
        AssetsDirectoryExists ||
        PackagesDirectoryExists ||
        ProjectSettingsDirectoryExists ||
        ProjectVersionFileExists;

    public bool HasCompleteProjectLayout =>
        RootExists &&
        AssetsDirectoryExists &&
        PackagesDirectoryExists &&
        ProjectSettingsDirectoryExists &&
        ProjectVersionFileExists;
}

/// <summary>
/// Parsed values from ProjectSettings/ProjectVersion.txt.
/// </summary>
public sealed record UnityProjectVersionInfo(string RequiredEditorVersion, string? Revision);

/// <summary>
/// Immutable read-only result used by the Unity adapter and later editor-resolution work.
/// </summary>
public sealed record UnityProjectDetectionResult(
    string RootPath,
    UnityProjectDetectionStatus Status,
    UnityProjectMarkerState Markers,
    UnityProjectVersionInfo? Version)
{
    public bool IsValid => Status == UnityProjectDetectionStatus.ValidUnityProject;
}

/// <summary>
/// Detects Unity project identity and the exact editor version requested by the project.
/// This contract never launches Unity and never mutates the supplied project.
/// </summary>
public interface IUnityProjectDetector
{
    Task<UnityProjectDetectionResult> DetectAsync(
        string projectRoot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Filesystem-backed Unity project detector aligned with the verified P00 Unity contract.
/// </summary>
public sealed class UnityProjectDetector : IUnityProjectDetector
{
    private const string AssetsDirectoryName = "Assets";
    private const string PackagesDirectoryName = "Packages";
    private const string ProjectSettingsDirectoryName = "ProjectSettings";
    private const string ProjectVersionFileName = "ProjectVersion.txt";
    private const string EditorVersionPrefix = "m_EditorVersion:";
    private const string EditorVersionWithRevisionPrefix = "m_EditorVersionWithRevision:";

    public async Task<UnityProjectDetectionResult> DetectAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        if (!string.Equals(projectRoot, projectRoot.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unity project root must not contain leading or trailing whitespace.",
                nameof(projectRoot));
        }

        if (projectRoot.Contains('\0'))
        {
            throw new ArgumentException("Unity project root must not contain NUL characters.", nameof(projectRoot));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var rootPath = Path.GetFullPath(projectRoot);
        var rootExists = Directory.Exists(rootPath);
        var projectSettingsPath = Path.Combine(rootPath, ProjectSettingsDirectoryName);
        var projectVersionPath = Path.Combine(projectSettingsPath, ProjectVersionFileName);
        var markers = new UnityProjectMarkerState(
            rootExists,
            rootExists && Directory.Exists(Path.Combine(rootPath, AssetsDirectoryName)),
            rootExists && Directory.Exists(Path.Combine(rootPath, PackagesDirectoryName)),
            rootExists && Directory.Exists(projectSettingsPath),
            rootExists && File.Exists(projectVersionPath));

        if (!rootExists)
        {
            return new UnityProjectDetectionResult(
                rootPath,
                UnityProjectDetectionStatus.ProjectRootNotFound,
                markers,
                Version: null);
        }

        if (!markers.HasAnyUnityMarker)
        {
            return new UnityProjectDetectionResult(
                rootPath,
                UnityProjectDetectionStatus.NotUnityProject,
                markers,
                Version: null);
        }

        if (!markers.ProjectVersionFileExists)
        {
            return new UnityProjectDetectionResult(
                rootPath,
                UnityProjectDetectionStatus.UnityProjectIncomplete,
                markers,
                Version: null);
        }

        string projectVersionText;
        try
        {
            projectVersionText = await File.ReadAllTextAsync(
                projectVersionPath,
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return new UnityProjectDetectionResult(
                rootPath,
                UnityProjectDetectionStatus.ProjectVersionReadFailure,
                markers,
                Version: null);
        }
        catch (UnauthorizedAccessException)
        {
            return new UnityProjectDetectionResult(
                rootPath,
                UnityProjectDetectionStatus.ProjectVersionReadFailure,
                markers,
                Version: null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var version = ParseVersion(projectVersionText);
        if (version is null)
        {
            return new UnityProjectDetectionResult(
                rootPath,
                UnityProjectDetectionStatus.ProjectVersionMissing,
                markers,
                Version: null);
        }

        return new UnityProjectDetectionResult(
            rootPath,
            markers.HasCompleteProjectLayout
                ? UnityProjectDetectionStatus.ValidUnityProject
                : UnityProjectDetectionStatus.UnityProjectIncomplete,
            markers,
            version);
    }

    private static UnityProjectVersionInfo? ParseVersion(string text)
    {
        string? editorVersion = null;
        string? versionFromRevisionLine = null;
        string? revision = null;

        foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith(EditorVersionPrefix, StringComparison.Ordinal))
            {
                var value = line[EditorVersionPrefix.Length..].Trim();
                if (value.Length > 0)
                {
                    editorVersion = value;
                }

                continue;
            }

            if (!line.StartsWith(EditorVersionWithRevisionPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var versionWithRevision = line[EditorVersionWithRevisionPrefix.Length..].Trim();
            if (versionWithRevision.Length == 0)
            {
                continue;
            }

            var revisionStart = versionWithRevision.LastIndexOf(" (", StringComparison.Ordinal);
            if (revisionStart > 0 && versionWithRevision.EndsWith(')'))
            {
                var parsedVersion = versionWithRevision[..revisionStart].Trim();
                var parsedRevision = versionWithRevision[(revisionStart + 2)..^1].Trim();
                if (parsedVersion.Length > 0)
                {
                    versionFromRevisionLine = parsedVersion;
                }

                if (parsedRevision.Length > 0)
                {
                    revision = parsedRevision;
                }
            }
            else
            {
                versionFromRevisionLine = versionWithRevision;
            }
        }

        var requiredVersion = editorVersion ?? versionFromRevisionLine;
        return string.IsNullOrWhiteSpace(requiredVersion)
            ? null
            : new UnityProjectVersionInfo(requiredVersion, revision);
    }
}
