using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace FCCCodeDesktop.Tools;

/// <summary>
/// Immutable discovery result associated with the adapter identity captured by the registry.
/// </summary>
public sealed record RegisteredToolDiscovery
{
    public RegisteredToolDiscovery(ToolIdentity identity, ToolDiscoveryResult discovery)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(discovery);

        Identity = identity;
        Discovery = discovery;
    }

    public ToolIdentity Identity { get; }

    public ToolDiscoveryResult Discovery { get; }
}

/// <summary>
/// Project-owned registry for external-tool adapter discovery and capability routing.
/// </summary>
public interface IExternalToolRegistry
{
    IReadOnlyList<ToolIdentity> RegisteredTools { get; }

    bool TryGetAdapter(
        string toolId,
        [NotNullWhen(true)] out IExternalToolAdapter? adapter);

    IExternalToolAdapter GetRequiredAdapter(string toolId);

    Task<ToolDiscoveryResult> DiscoverAsync(
        string toolId,
        ProjectContext project,
        CancellationToken cancellationToken = default);

    Task<ToolCapabilitySet> GetCapabilitiesAsync(
        string toolId,
        ProjectContext project,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegisteredToolDiscovery>> DiscoverAllAsync(
        ProjectContext project,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Immutable adapter registry. Registration performs no tool probing or filesystem/process work.
/// </summary>
public sealed class ExternalToolRegistry : IExternalToolRegistry
{
    private readonly ReadOnlyCollection<AdapterRegistration> _registrations;
    private readonly Dictionary<string, AdapterRegistration> _registrationsById;
    private readonly ReadOnlyCollection<ToolIdentity> _registeredTools;

    public ExternalToolRegistry(IEnumerable<IExternalToolAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var registrationsById = new Dictionary<string, AdapterRegistration>(StringComparer.OrdinalIgnoreCase);

        foreach (var adapter in adapters)
        {
            if (adapter is null)
            {
                throw new ArgumentException("External tool adapter collection cannot contain null entries.", nameof(adapters));
            }

            var identity = adapter.Identity
                ?? throw new InvalidOperationException("An external tool adapter returned a null identity.");
            var registration = new AdapterRegistration(identity, adapter);

            if (!registrationsById.TryAdd(identity.Id, registration))
            {
                throw new InvalidOperationException(
                    $"Duplicate external tool adapter identity '{identity.Id}' is not allowed.");
            }
        }

        var orderedRegistrations = registrationsById.Values
            .OrderBy(static registration => registration.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _registrations = Array.AsReadOnly(orderedRegistrations);
        _registeredTools = Array.AsReadOnly(
            orderedRegistrations.Select(static registration => registration.Identity).ToArray());
        _registrationsById = registrationsById;
    }

    public IReadOnlyList<ToolIdentity> RegisteredTools => _registeredTools;

    public bool TryGetAdapter(
        string toolId,
        [NotNullWhen(true)] out IExternalToolAdapter? adapter)
    {
        ValidateToolId(toolId);

        if (_registrationsById.TryGetValue(toolId, out var registration))
        {
            adapter = registration.Adapter;
            return true;
        }

        adapter = null;
        return false;
    }

    public IExternalToolAdapter GetRequiredAdapter(string toolId)
    {
        ValidateToolId(toolId);

        if (!_registrationsById.TryGetValue(toolId, out var registration))
        {
            throw new KeyNotFoundException($"External tool adapter '{toolId}' is not registered.");
        }

        return registration.Adapter;
    }

    public async Task<ToolDiscoveryResult> DiscoverAsync(
        string toolId,
        ProjectContext project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        var registration = GetRequiredRegistration(toolId);
        return await DiscoverRegisteredAsync(registration, project, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ToolCapabilitySet> GetCapabilitiesAsync(
        string toolId,
        ProjectContext project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        var registration = GetRequiredRegistration(toolId);
        var capabilities = await registration.Adapter
            .GetCapabilitiesAsync(project, cancellationToken)
            .ConfigureAwait(false);

        return capabilities
            ?? throw new InvalidOperationException(
                $"External tool adapter '{registration.Identity.Id}' returned a null capability set.");
    }

    public async Task<IReadOnlyList<RegisteredToolDiscovery>> DiscoverAllAsync(
        ProjectContext project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var results = new RegisteredToolDiscovery[_registrations.Count];
        for (var index = 0; index < _registrations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var registration = _registrations[index];
            var discovery = await DiscoverRegisteredAsync(registration, project, cancellationToken)
                .ConfigureAwait(false);
            results[index] = new RegisteredToolDiscovery(registration.Identity, discovery);
        }

        return Array.AsReadOnly(results);
    }

    private AdapterRegistration GetRequiredRegistration(string toolId)
    {
        ValidateToolId(toolId);

        if (!_registrationsById.TryGetValue(toolId, out var registration))
        {
            throw new KeyNotFoundException($"External tool adapter '{toolId}' is not registered.");
        }

        return registration;
    }

    private static async Task<ToolDiscoveryResult> DiscoverRegisteredAsync(
        AdapterRegistration registration,
        ProjectContext project,
        CancellationToken cancellationToken)
    {
        var discovery = await registration.Adapter
            .DiscoverAsync(project, cancellationToken)
            .ConfigureAwait(false);

        return discovery
            ?? throw new InvalidOperationException(
                $"External tool adapter '{registration.Identity.Id}' returned a null discovery result.");
    }

    private static void ValidateToolId(string toolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);

        if (!string.Equals(toolId, toolId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "External tool identity lookup must not contain leading or trailing whitespace.",
                nameof(toolId));
        }
    }

    private sealed record AdapterRegistration(ToolIdentity Identity, IExternalToolAdapter Adapter);
}
