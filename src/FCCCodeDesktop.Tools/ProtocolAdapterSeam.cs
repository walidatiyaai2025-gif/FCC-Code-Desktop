using System.Collections.ObjectModel;

namespace FCCCodeDesktop.Tools;

/// <summary>
/// Provider-neutral identity for an optional external-tool protocol bridge. Protocol-specific
/// packages (for example DAP or MCP implementations) remain outside the Tool Gateway contracts.
/// </summary>
public sealed record ToolProtocolIdentity
{
    public ToolProtocolIdentity(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!string.Equals(id, id.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Tool protocol identity must not contain leading or trailing whitespace.",
                nameof(id));
        }

        if (id.Contains('\0'))
        {
            throw new ArgumentException(
                "Tool protocol identity must not contain NUL characters.",
                nameof(id));
        }

        Id = id;
    }

    public string Id { get; }
}

/// <summary>
/// Immutable project-scoped input supplied to a protocol-specific adapter factory. Options are
/// intentionally opaque to the Tool Gateway so protocol schemas and SDK types do not leak into
/// orchestration or core contracts.
/// </summary>
public sealed record ToolProtocolBinding
{
    private readonly ReadOnlyDictionary<string, string> _options;

    public ToolProtocolBinding(
        ToolIdentity tool,
        ProjectContext project,
        IEnumerable<KeyValuePair<string, string>>? options = null)
    {
        Tool = tool ?? throw new ArgumentNullException(nameof(tool));
        Project = project ?? throw new ArgumentNullException(nameof(project));
        _options = SnapshotOptions(options);
    }

    public ToolIdentity Tool { get; }

    public ProjectContext Project { get; }

    public IReadOnlyDictionary<string, string> Options => _options;

    private static ReadOnlyDictionary<string, string> SnapshotOptions(
        IEnumerable<KeyValuePair<string, string>>? options)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in options ?? Array.Empty<KeyValuePair<string, string>>())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            if (!string.Equals(pair.Key, pair.Key.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Protocol option names must not contain leading or trailing whitespace.",
                    nameof(options));
            }

            if (pair.Key.Contains('\0'))
            {
                throw new ArgumentException(
                    "Protocol option names must not contain NUL characters.",
                    nameof(options));
            }

            if (pair.Value is null)
            {
                throw new ArgumentException(
                    "Protocol option values must not be null.",
                    nameof(options));
            }

            if (pair.Value.Contains('\0'))
            {
                throw new ArgumentException(
                    "Protocol option values must not contain NUL characters.",
                    nameof(options));
            }

            if (!snapshot.TryAdd(pair.Key, pair.Value))
            {
                throw new ArgumentException(
                    $"Protocol option '{pair.Key}' is specified more than once.",
                    nameof(options));
            }
        }

        return new ReadOnlyDictionary<string, string>(snapshot);
    }
}

/// <summary>
/// Optional composition seam implemented by protocol-specific packages. The returned object is the
/// existing <see cref="IExternalToolAdapter"/> boundary, so orchestration never depends on DAP,
/// MCP, language-server, RPC, transport, or SDK-specific contracts.
/// </summary>
public interface IExternalToolProtocolAdapterFactory
{
    ToolProtocolIdentity Protocol { get; }

    Task<IExternalToolAdapter> CreateAsync(
        ToolProtocolBinding binding,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Immutable registry of optional protocol factories. An empty registry is valid and represents a
/// product installation with no protocol extensions loaded.
/// </summary>
public sealed class ExternalToolProtocolRegistry
{
    private readonly ReadOnlyDictionary<string, IExternalToolProtocolAdapterFactory> _factories;
    private readonly ReadOnlyCollection<ToolProtocolIdentity> _protocols;

    public ExternalToolProtocolRegistry(
        IEnumerable<IExternalToolProtocolAdapterFactory>? factories = null)
    {
        var byProtocol = new Dictionary<string, IExternalToolProtocolAdapterFactory>(
            StringComparer.OrdinalIgnoreCase);
        var protocols = new List<ToolProtocolIdentity>();

        foreach (var factory in factories ?? Array.Empty<IExternalToolProtocolAdapterFactory>())
        {
            ArgumentNullException.ThrowIfNull(factory);
            var protocol = factory.Protocol ?? throw new ArgumentException(
                "A protocol adapter factory must expose a protocol identity.",
                nameof(factories));

            if (!byProtocol.TryAdd(protocol.Id, factory))
            {
                throw new ArgumentException(
                    $"Protocol adapter factory '{protocol.Id}' is registered more than once.",
                    nameof(factories));
            }

            protocols.Add(protocol);
        }

        _factories = new ReadOnlyDictionary<string, IExternalToolProtocolAdapterFactory>(byProtocol);
        _protocols = Array.AsReadOnly(protocols.ToArray());
    }

    public IReadOnlyList<ToolProtocolIdentity> Protocols => _protocols;

    public bool TryGetFactory(
        ToolProtocolIdentity protocol,
        out IExternalToolProtocolAdapterFactory? factory)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        return _factories.TryGetValue(protocol.Id, out factory);
    }

    public IExternalToolProtocolAdapterFactory GetRequiredFactory(ToolProtocolIdentity protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        if (_factories.TryGetValue(protocol.Id, out var factory))
        {
            return factory;
        }

        throw new KeyNotFoundException(
            $"No optional external-tool protocol factory is registered for '{protocol.Id}'.");
    }
}
