using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace FCCCodeDesktop.Tools;

/// <summary>
/// Provider-neutral artifact shape expected from an external-tool invocation.
/// </summary>
public enum ToolArtifactKind
{
    File = 1,
    Directory = 2,
}

/// <summary>
/// Immutable declaration of one artifact expected beneath a manifest root.
/// Paths are always relative, traversal-free and normalized before any filesystem access occurs.
/// </summary>
public sealed record ToolArtifactDefinition
{
    public ToolArtifactDefinition(
        string id,
        string relativePath,
        ToolArtifactKind kind,
        bool requireNonEmpty = false,
        string? expectedSha256 = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!string.Equals(id, id.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Artifact ids must not contain leading or trailing whitespace.", nameof(id));
        }

        if (id.Contains('\0'))
        {
            throw new ArgumentException("Artifact ids must not contain NUL characters.", nameof(id));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "A supported artifact kind is required.");
        }

        Id = id;
        RelativePath = NormalizeRelativePath(relativePath);
        Kind = kind;
        RequireNonEmpty = requireNonEmpty;
        ExpectedSha256 = NormalizeExpectedSha256(kind, expectedSha256);
    }

    public string Id { get; }

    public string RelativePath { get; }

    public ToolArtifactKind Kind { get; }

    public bool RequireNonEmpty { get; }

    public string? ExpectedSha256 { get; }

    private static string NormalizeRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (relativePath.Contains('\0'))
        {
            throw new ArgumentException("Artifact paths must not contain NUL characters.", nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath) || Path.IsPathFullyQualified(relativePath))
        {
            throw new ArgumentException("Artifact paths must be relative to the manifest root.", nameof(relativePath));
        }

        var normalized = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.None);
        if (segments.Any(segment =>
                segment.Length == 0 ||
                string.Equals(segment, ".", StringComparison.Ordinal) ||
                string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Artifact paths must not contain empty, current-directory or parent-directory segments.",
                nameof(relativePath));
        }

        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    private static string? NormalizeExpectedSha256(ToolArtifactKind kind, string? expectedSha256)
    {
        if (expectedSha256 is null)
        {
            return null;
        }

        if (kind != ToolArtifactKind.File)
        {
            throw new ArgumentException("SHA-256 expectations are valid only for file artifacts.", nameof(expectedSha256));
        }

        if (expectedSha256.Length != 64 || expectedSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Expected SHA-256 values must contain exactly 64 hexadecimal characters.", nameof(expectedSha256));
        }

        return expectedSha256.ToLowerInvariant();
    }
}

/// <summary>
/// Immutable manifest of artifacts expected beneath one fully-qualified output root.
/// </summary>
public sealed record ToolArtifactManifest
{
    private readonly IReadOnlyList<ToolArtifactDefinition> _artifacts;

    public ToolArtifactManifest(string rootPath, IEnumerable<ToolArtifactDefinition> artifacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!Path.IsPathFullyQualified(rootPath))
        {
            throw new ArgumentException("Artifact manifest roots must be fully qualified.", nameof(rootPath));
        }

        ArgumentNullException.ThrowIfNull(artifacts);

        RootPath = Path.GetFullPath(rootPath);
        _artifacts = SnapshotArtifacts(artifacts);
    }

    public string RootPath { get; }

    public IReadOnlyList<ToolArtifactDefinition> Artifacts => _artifacts;

    private static ReadOnlyCollection<ToolArtifactDefinition> SnapshotArtifacts(
        IEnumerable<ToolArtifactDefinition> artifacts)
    {
        var snapshot = new List<ToolArtifactDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var artifact in artifacts)
        {
            if (artifact is null)
            {
                throw new ArgumentException("Artifact manifests must not contain null entries.", nameof(artifacts));
            }

            if (!ids.Add(artifact.Id))
            {
                throw new ArgumentException($"Artifact id '{artifact.Id}' is declared more than once.", nameof(artifacts));
            }

            if (!paths.Add(artifact.RelativePath))
            {
                throw new ArgumentException(
                    $"Artifact path '{artifact.RelativePath}' is declared more than once.",
                    nameof(artifacts));
            }

            snapshot.Add(artifact);
        }

        return Array.AsReadOnly(snapshot.ToArray());
    }
}

public enum ToolArtifactValidationStatus
{
    Valid = 1,
    Missing = 2,
    TypeMismatch = 3,
    Empty = 4,
    HashMismatch = 5,
    UnsafePath = 6,
    Unreadable = 7,
}

/// <summary>
/// Immutable validation evidence for one declared artifact.
/// File evidence includes actual byte length and SHA-256 when the file can be read safely.
/// </summary>
public sealed record ToolArtifactValidationEntry
{
    internal ToolArtifactValidationEntry(
        ToolArtifactDefinition definition,
        ToolArtifactValidationStatus status,
        string fullPath,
        long? length,
        string? sha256,
        string? diagnostic)
    {
        Definition = definition;
        Status = status;
        FullPath = fullPath;
        Length = length;
        Sha256 = sha256;
        Diagnostic = diagnostic;
    }

    public ToolArtifactDefinition Definition { get; }

    public ToolArtifactValidationStatus Status { get; }

    public string FullPath { get; }

    public long? Length { get; }

    public string? Sha256 { get; }

    public string? Diagnostic { get; }
}

/// <summary>
/// Complete validation report for a manifest. Success requires every declared artifact to validate.
/// </summary>
public sealed record ToolArtifactValidationReport
{
    private readonly IReadOnlyList<ToolArtifactValidationEntry> _entries;

    internal ToolArtifactValidationReport(
        ToolArtifactManifest manifest,
        IEnumerable<ToolArtifactValidationEntry> entries)
    {
        Manifest = manifest;
        _entries = Array.AsReadOnly(entries.ToArray());
    }

    public ToolArtifactManifest Manifest { get; }

    public IReadOnlyList<ToolArtifactValidationEntry> Entries => _entries;

    public bool Succeeded => _entries.All(entry => entry.Status == ToolArtifactValidationStatus.Valid);
}

public interface IToolArtifactValidator
{
    Task<ToolArtifactValidationReport> ValidateAsync(
        ToolArtifactManifest manifest,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Filesystem validator shared by all external-tool adapters. It refuses traversal/reparse paths,
/// validates file-vs-directory shape, optional non-empty requirements and optional expected hashes,
/// and records SHA-256 for readable file artifacts even when no expected hash was supplied.
/// </summary>
public sealed class ToolArtifactValidator : IToolArtifactValidator
{
    public async Task<ToolArtifactValidationReport> ValidateAsync(
        ToolArtifactManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        var entries = new List<ToolArtifactValidationEntry>(manifest.Artifacts.Count);
        foreach (var definition in manifest.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(await ValidateOneAsync(manifest, definition, cancellationToken).ConfigureAwait(false));
        }

        return new ToolArtifactValidationReport(manifest, entries);
    }

    private static async Task<ToolArtifactValidationEntry> ValidateOneAsync(
        ToolArtifactManifest manifest,
        ToolArtifactDefinition definition,
        CancellationToken cancellationToken)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(manifest.RootPath, definition.RelativePath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Create(definition, ToolArtifactValidationStatus.UnsafePath, manifest.RootPath, diagnostic: "Artifact path could not be normalized safely.");
        }

        if (!IsContainedByRoot(manifest.RootPath, fullPath))
        {
            return Create(definition, ToolArtifactValidationStatus.UnsafePath, fullPath, diagnostic: "Artifact path escaped the manifest root.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileExists = File.Exists(fullPath);
            var directoryExists = Directory.Exists(fullPath);
            if (!fileExists && !directoryExists)
            {
                return Create(definition, ToolArtifactValidationStatus.Missing, fullPath, diagnostic: "Declared artifact does not exist.");
            }

            if (ContainsReparsePoint(manifest.RootPath, fullPath))
            {
                return Create(definition, ToolArtifactValidationStatus.UnsafePath, fullPath, diagnostic: "Artifact path contains a reparse point.");
            }

            if (definition.Kind == ToolArtifactKind.File && !fileExists ||
                definition.Kind == ToolArtifactKind.Directory && !directoryExists)
            {
                return Create(definition, ToolArtifactValidationStatus.TypeMismatch, fullPath, diagnostic: "Artifact filesystem type does not match the manifest.");
            }

            if (directoryExists)
            {
                if (definition.RequireNonEmpty && !Directory.EnumerateFileSystemEntries(fullPath).Any())
                {
                    return Create(definition, ToolArtifactValidationStatus.Empty, fullPath, diagnostic: "Artifact directory is empty.");
                }

                return Create(definition, ToolArtifactValidationStatus.Valid, fullPath);
            }

            var fileInfo = new FileInfo(fullPath);
            if (definition.RequireNonEmpty && fileInfo.Length == 0)
            {
                return Create(
                    definition,
                    ToolArtifactValidationStatus.Empty,
                    fullPath,
                    length: 0,
                    diagnostic: "Artifact file is empty.");
            }

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            var sha256 = Convert.ToHexString(hash).ToLowerInvariant();
            if (definition.ExpectedSha256 is not null &&
                !string.Equals(definition.ExpectedSha256, sha256, StringComparison.OrdinalIgnoreCase))
            {
                return Create(
                    definition,
                    ToolArtifactValidationStatus.HashMismatch,
                    fullPath,
                    fileInfo.Length,
                    sha256,
                    "Artifact SHA-256 does not match the manifest expectation.");
            }

            return Create(
                definition,
                ToolArtifactValidationStatus.Valid,
                fullPath,
                fileInfo.Length,
                sha256);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Create(
                definition,
                ToolArtifactValidationStatus.Unreadable,
                fullPath,
                diagnostic: $"Artifact could not be read safely ({exception.GetType().Name}).");
        }
    }

    private static bool IsContainedByRoot(string rootPath, string fullPath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsReparsePoint(string rootPath, string fullPath)
    {
        var current = Path.GetFullPath(rootPath);
        if (Exists(current) && IsReparsePoint(current))
        {
            return true;
        }

        var relative = Path.GetRelativePath(current, fullPath);
        var segments = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (!Exists(current))
            {
                break;
            }

            if (IsReparsePoint(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static ToolArtifactValidationEntry Create(
        ToolArtifactDefinition definition,
        ToolArtifactValidationStatus status,
        string fullPath,
        long? length = null,
        string? sha256 = null,
        string? diagnostic = null) =>
        new(definition, status, fullPath, length, sha256, diagnostic);
}

/// <summary>
/// Standard structured event for publishing artifact validation through the external-tool stream.
/// </summary>
public sealed record ToolArtifactValidationEvent : ToolEvent
{
    public ToolArtifactValidationEvent(ToolArtifactValidationReport report)
    {
        Report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public ToolArtifactValidationReport Report { get; }
}
