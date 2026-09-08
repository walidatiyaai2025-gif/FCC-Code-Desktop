using System.Collections.ObjectModel;

namespace FCCCodeDesktop.Tools;

/// <summary>
/// Stable identity exposed by an external-tool adapter.
/// </summary>
public sealed record ToolIdentity
{
    public ToolIdentity(string id, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (!string.Equals(id, id.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Tool identity must not contain leading or trailing whitespace.", nameof(id));
        }

        if (!string.Equals(displayName, displayName.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Tool display name must not contain leading or trailing whitespace.", nameof(displayName));
        }

        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }

    public string DisplayName { get; }
}

/// <summary>
/// Project-scoped context shared by tool discovery, capability resolution and invocation.
/// </summary>
public sealed record ProjectContext
{
    public ProjectContext(Guid projectId, string rootPath)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project identity cannot be empty.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!Path.IsPathFullyQualified(rootPath))
        {
            throw new ArgumentException("Project root path must be fully qualified.", nameof(rootPath));
        }

        ProjectId = projectId;
        RootPath = Path.GetFullPath(rootPath);
    }

    public Guid ProjectId { get; }

    public string RootPath { get; }
}

/// <summary>
/// Base discovery result shared by all adapters. P09 discovery work adds concrete evidence/version results.
/// </summary>
public abstract record ToolDiscoveryResult
{
    protected ToolDiscoveryResult(bool isAvailable)
    {
        IsAvailable = isAvailable;
    }

    public bool IsAvailable { get; }
}

/// <summary>
/// Nominal base for immutable capability sets. Concrete capability semantics belong to the capability layer.
/// </summary>
public abstract record ToolCapabilitySet;

/// <summary>
/// Nominal base for structured tool invocations. Every invocation is bound to exactly one project context.
/// </summary>
public abstract record ToolInvocation
{
    protected ToolInvocation(ProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(project);
        Project = project;
    }

    public ProjectContext Project { get; }
}

/// <summary>
/// Reusable provider-neutral invocation contract for adapters that execute an ordered operation.
/// Arguments remain discrete values and are never collapsed into a shell command string.
/// </summary>
public abstract record StructuredToolInvocation : ToolInvocation
{
    private readonly IReadOnlyList<string> _arguments;
    private readonly IReadOnlyDictionary<string, string> _environment;

    protected StructuredToolInvocation(
        ProjectContext project,
        string operation,
        IEnumerable<string>? arguments = null,
        string? workingDirectory = null,
        IEnumerable<KeyValuePair<string, string>>? environment = null)
        : base(project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (!string.Equals(operation, operation.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Tool operation must not contain leading or trailing whitespace.",
                nameof(operation));
        }

        if (operation.Contains('\0'))
        {
            throw new ArgumentException("Tool operation must not contain NUL characters.", nameof(operation));
        }

        Operation = operation;
        WorkingDirectory = NormalizeWorkingDirectory(project, workingDirectory);
        _arguments = SnapshotArguments(arguments);
        _environment = SnapshotEnvironment(environment);
    }

    public string Operation { get; }

    /// <summary>
    /// Exact ordered argument values. Empty values, Unicode, quotes and shell metacharacters
    /// are preserved as individual arguments for a later adapter/process builder.
    /// </summary>
    public IReadOnlyList<string> Arguments => _arguments;

    /// <summary>
    /// Fully-qualified working directory. Defaults to the project root without touching disk.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// Immutable environment overlay. Keys use Windows-compatible case-insensitive uniqueness.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment => _environment;

    private static ReadOnlyCollection<string> SnapshotArguments(IEnumerable<string>? arguments)
    {
        var snapshot = new List<string>();
        foreach (var argument in arguments ?? Array.Empty<string>())
        {
            if (argument is null)
            {
                throw new ArgumentException("Tool arguments must not contain null values.", nameof(arguments));
            }

            if (argument.Contains('\0'))
            {
                throw new ArgumentException("Tool arguments must not contain NUL characters.", nameof(arguments));
            }

            snapshot.Add(argument);
        }

        return Array.AsReadOnly(snapshot.ToArray());
    }

    private static ReadOnlyDictionary<string, string> SnapshotEnvironment(
        IEnumerable<KeyValuePair<string, string>>? environment)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in environment ?? Array.Empty<KeyValuePair<string, string>>())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            if (!string.Equals(pair.Key, pair.Key.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Environment variable names must not contain leading or trailing whitespace.",
                    nameof(environment));
            }

            if (pair.Key.Contains('=') || pair.Key.Contains('\0'))
            {
                throw new ArgumentException(
                    "Environment variable names must not contain '=' or NUL characters.",
                    nameof(environment));
            }

            if (pair.Value is null)
            {
                throw new ArgumentException(
                    "Environment variable values must not be null.",
                    nameof(environment));
            }

            if (pair.Value.Contains('\0'))
            {
                throw new ArgumentException(
                    "Environment variable values must not contain NUL characters.",
                    nameof(environment));
            }

            if (!snapshot.TryAdd(pair.Key, pair.Value))
            {
                throw new ArgumentException(
                    $"Environment variable '{pair.Key}' is specified more than once.",
                    nameof(environment));
            }
        }

        return new ReadOnlyDictionary<string, string>(snapshot);
    }

    private static string NormalizeWorkingDirectory(ProjectContext project, string? workingDirectory)
    {
        var candidate = workingDirectory ?? project.RootPath;
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);
        if (!Path.IsPathFullyQualified(candidate))
        {
            throw new ArgumentException(
                "Tool working directory must be fully qualified.",
                nameof(workingDirectory));
        }

        return Path.GetFullPath(candidate);
    }
}

/// <summary>
/// Provider-neutral terminal status for a completed tool operation.
/// </summary>
public enum ToolResultStatus
{
    Unknown = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
}

/// <summary>
/// Base immutable result returned by adapter-specific result types.
/// Provider-specific payloads remain on derived result contracts rather than leaking into core orchestration.
/// </summary>
public abstract record ToolResult
{
    protected ToolResult(ToolResultStatus status, string? summary = null)
    {
        if (!Enum.IsDefined(status) || status == ToolResultStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "A terminal tool result status is required.");
        }

        if (summary is not null && string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Tool result summary must contain non-whitespace text when supplied.", nameof(summary));
        }

        Status = status;
        Summary = summary;
    }

    public ToolResultStatus Status { get; }

    public string? Summary { get; }
}

/// <summary>
/// Nominal base for structured events streamed by an external-tool adapter.
/// </summary>
public abstract record ToolEvent;

/// <summary>
/// Standard terminal event that carries a typed tool result through the streaming adapter contract.
/// </summary>
public sealed record ToolResultEvent : ToolEvent
{
    public ToolResultEvent(ToolResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public ToolResult Result { get; }
}

/// <summary>
/// Project-owned extensibility boundary for all external developer/content tools.
/// </summary>
public interface IExternalToolAdapter
{
    ToolIdentity Identity { get; }

    Task<ToolDiscoveryResult> DiscoverAsync(
        ProjectContext project,
        CancellationToken cancellationToken = default);

    Task<ToolCapabilitySet> GetCapabilitiesAsync(
        ProjectContext project,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ToolEvent> ExecuteAsync(
        ToolInvocation invocation,
        CancellationToken cancellationToken = default);
}
