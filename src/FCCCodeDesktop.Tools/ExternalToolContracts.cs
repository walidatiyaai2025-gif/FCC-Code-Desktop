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
/// Nominal base for structured events streamed by an external-tool adapter.
/// </summary>
public abstract record ToolEvent;

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
