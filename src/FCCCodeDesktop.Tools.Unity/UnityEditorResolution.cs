using System.Collections.ObjectModel;

namespace FCCCodeDesktop.Tools.Unity;

/// <summary>
/// Provenance for a resolved Unity Editor installation.
/// </summary>
public enum UnityEditorInstallationSource
{
    ConfiguredRoot = 1,
    UnityHubDefault = 2,
}

/// <summary>
/// Stable resolution states for an exact Unity Editor request.
/// </summary>
public enum UnityEditorResolutionStatus
{
    ExactMatch = 1,
    NotInstalled = 2,
}

/// <summary>
/// Exact Unity Editor installation selected for a project request.
/// </summary>
public sealed record UnityEditorInstallation(
    string Version,
    string InstallationRoot,
    string EditorExecutablePath,
    UnityEditorInstallationSource Source);

/// <summary>
/// Immutable result for exact Unity Editor resolution.
/// </summary>
public sealed record UnityEditorResolutionResult(
    string RequiredVersion,
    UnityEditorResolutionStatus Status,
    UnityEditorInstallation? Installation,
    IReadOnlyList<string> SearchRoots)
{
    public bool IsResolved => Status == UnityEditorResolutionStatus.ExactMatch;
}

/// <summary>
/// Resolves the exact installed Unity Editor requested by a Unity project.
/// </summary>
public interface IUnityEditorResolver
{
    Task<UnityEditorResolutionResult> ResolveAsync(
        string requiredVersion,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only resolver for configured editor roots and the documented Unity Hub default editor root.
/// It does not launch Unity, mutate Hub configuration, or silently substitute a different editor version.
/// </summary>
public sealed class UnityEditorResolver : IUnityEditorResolver
{
    private const string UnityExecutableName = "Unity.exe";

    private readonly ReadOnlyCollection<SearchRoot> _searchRoots;
    private readonly ReadOnlyCollection<string> _searchRootPaths;

    public UnityEditorResolver(
        IEnumerable<string>? configuredEditorRoots = null,
        bool includeDefaultHubRoot = true,
        string? programFilesDirectoryOverride = null)
    {
        var roots = new List<SearchRoot>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (configuredEditorRoots is not null)
        {
            foreach (var configuredRoot in configuredEditorRoots)
            {
                AddSearchRoot(
                    roots,
                    seen,
                    NormalizeRoot(configuredRoot, nameof(configuredEditorRoots)),
                    UnityEditorInstallationSource.ConfiguredRoot);
            }
        }

        if (includeDefaultHubRoot)
        {
            var programFilesDirectory = programFilesDirectoryOverride
                ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            if (!string.IsNullOrWhiteSpace(programFilesDirectory))
            {
                var normalizedProgramFiles = NormalizeRoot(
                    programFilesDirectory,
                    nameof(programFilesDirectoryOverride));
                var hubEditorRoot = Path.Combine(normalizedProgramFiles, "Unity", "Hub", "Editor");
                AddSearchRoot(
                    roots,
                    seen,
                    hubEditorRoot,
                    UnityEditorInstallationSource.UnityHubDefault);
            }
        }

        _searchRoots = roots.AsReadOnly();
        _searchRootPaths = roots.Select(static root => root.Path).ToList().AsReadOnly();
    }

    public Task<UnityEditorResolutionResult> ResolveAsync(
        string requiredVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredVersion(requiredVersion);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var searchRoot in _searchRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var installationRoot in EnumerateCandidateInstallationRoots(searchRoot.Path, requiredVersion))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var editorExecutablePath = Path.Combine(
                    installationRoot,
                    "Editor",
                    UnityExecutableName);

                if (!File.Exists(editorExecutablePath))
                {
                    continue;
                }

                var installation = new UnityEditorInstallation(
                    requiredVersion,
                    installationRoot,
                    editorExecutablePath,
                    searchRoot.Source);

                return Task.FromResult(new UnityEditorResolutionResult(
                    requiredVersion,
                    UnityEditorResolutionStatus.ExactMatch,
                    installation,
                    _searchRootPaths));
            }
        }

        return Task.FromResult(new UnityEditorResolutionResult(
            requiredVersion,
            UnityEditorResolutionStatus.NotInstalled,
            Installation: null,
            _searchRootPaths));
    }

    private static void AddSearchRoot(
        ICollection<SearchRoot> roots,
        HashSet<string> seen,
        string path,
        UnityEditorInstallationSource source)
    {
        if (seen.Add(path))
        {
            roots.Add(new SearchRoot(path, source));
        }
    }

    private static IEnumerable<string> EnumerateCandidateInstallationRoots(
        string searchRoot,
        string requiredVersion)
    {
        if (string.Equals(
            Path.GetFileName(searchRoot),
            requiredVersion,
            StringComparison.OrdinalIgnoreCase))
        {
            yield return searchRoot;
        }

        yield return Path.Combine(searchRoot, requiredVersion);
    }

    private static string NormalizeRoot(string root, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root, parameterName);
        if (!string.Equals(root, root.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unity editor search roots must not contain leading or trailing whitespace.",
                parameterName);
        }

        if (root.Contains('\0'))
        {
            throw new ArgumentException(
                "Unity editor search roots must not contain NUL characters.",
                parameterName);
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    private static void ValidateRequiredVersion(string requiredVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredVersion);
        if (!string.Equals(requiredVersion, requiredVersion.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unity editor version must not contain leading or trailing whitespace.",
                nameof(requiredVersion));
        }

        var segments = requiredVersion.Split('.');
        if (segments.Length != 3 ||
            !ContainsOnlyAsciiDigits(segments[0]) ||
            !ContainsOnlyAsciiDigits(segments[1]) ||
            !IsValidUnityPatchSegment(segments[2]))
        {
            throw new ArgumentException(
                "Unity editor version must use a canonical Unity version such as 2022.3.75f1 or 6000.0.42f1.",
                nameof(requiredVersion));
        }
    }

    private static bool IsValidUnityPatchSegment(string segment)
    {
        var index = 0;
        while (index < segment.Length && char.IsAsciiDigit(segment[index]))
        {
            index++;
        }

        if (index == 0 || index >= segment.Length)
        {
            return false;
        }

        var channel = segment[index++];
        if (channel is not ('a' or 'b' or 'f' or 'p'))
        {
            return false;
        }

        var channelNumberStart = index;
        while (index < segment.Length && char.IsAsciiDigit(segment[index]))
        {
            index++;
        }

        if (index == channelNumberStart)
        {
            return false;
        }

        if (index == segment.Length)
        {
            return true;
        }

        if (segment[index++] != 'c')
        {
            return false;
        }

        var chinaRevisionStart = index;
        while (index < segment.Length && char.IsAsciiDigit(segment[index]))
        {
            index++;
        }

        return index == segment.Length && index > chinaRevisionStart;
    }

    private static bool ContainsOnlyAsciiDigits(string value) =>
        value.Length > 0 && value.All(static character => char.IsAsciiDigit(character));

    private sealed record SearchRoot(string Path, UnityEditorInstallationSource Source);
}
