using FCCCodeDesktop.Tools;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ProtocolAdapterSeamTests
{
    [Fact]
    public void EmptyRegistryIsValidAndRequiresNoProtocolImplementation()
    {
        var registry = new ExternalToolProtocolRegistry();
        var protocol = new ToolProtocolIdentity("mcp");

        Assert.Empty(registry.Protocols);
        Assert.False(registry.TryGetFactory(protocol, out var factory));
        Assert.Null(factory);
        Assert.Throws<KeyNotFoundException>(() => registry.GetRequiredFactory(protocol));
    }

    [Fact]
    public void RegistryResolvesProtocolFactoriesCaseInsensitivelyWithoutCoreProtocolTypes()
    {
        var registeredProtocol = new ToolProtocolIdentity("mcp");
        var factory = new FixtureProtocolFactory(registeredProtocol);
        var registry = new ExternalToolProtocolRegistry(new[] { factory });

        Assert.Single(registry.Protocols);
        Assert.Same(registeredProtocol, registry.Protocols[0]);
        Assert.True(
            registry.TryGetFactory(
                new ToolProtocolIdentity("MCP"),
                out var resolved));
        Assert.Same(factory, resolved);
        Assert.Same(
            factory,
            registry.GetRequiredFactory(new ToolProtocolIdentity("McP")));
    }

    [Fact]
    public void RegistryRejectsDuplicateProtocolFactoriesIgnoringCase()
    {
        var factories = new IExternalToolProtocolAdapterFactory[]
        {
            new FixtureProtocolFactory(new ToolProtocolIdentity("dap")),
            new FixtureProtocolFactory(new ToolProtocolIdentity("DAP")),
        };

        Assert.Throws<ArgumentException>(() => new ExternalToolProtocolRegistry(factories));
    }

    [Fact]
    public async Task FactoryReceivesImmutableBindingAndReturnsExistingAdapterBoundary()
    {
        var options = new List<KeyValuePair<string, string>>
        {
            new("transport", "stdio"),
            new("endpoint", "fixture://local"),
        };
        var binding = new ToolProtocolBinding(
            new ToolIdentity("fixture-tool", "Fixture Tool"),
            CreateProjectContext(),
            options);
        options[0] = new KeyValuePair<string, string>("transport", "mutated");

        var factory = new FixtureProtocolFactory(new ToolProtocolIdentity("fixture-protocol"));
        IExternalToolAdapter adapter = await factory.CreateAsync(binding);

        Assert.Same(binding, factory.LastBinding);
        Assert.Equal("stdio", binding.Options["transport"]);
        Assert.Equal("fixture://local", binding.Options["endpoint"]);
        Assert.Equal("fixture-tool", adapter.Identity.Id);
        Assert.IsAssignableFrom<IExternalToolAdapter>(adapter);
    }

    [Fact]
    public void ProtocolIdentityRejectsAmbiguousValues()
    {
        Assert.Throws<ArgumentException>(() => new ToolProtocolIdentity(" "));
        Assert.Throws<ArgumentException>(() => new ToolProtocolIdentity(" mcp"));
        Assert.Throws<ArgumentException>(() => new ToolProtocolIdentity("mcp "));
        Assert.Throws<ArgumentException>(() => new ToolProtocolIdentity("mc\0p"));
    }

    [Fact]
    public void BindingRejectsUnsafeOrAmbiguousOptions()
    {
        var tool = new ToolIdentity("fixture-tool", "Fixture Tool");
        var project = CreateProjectContext();

        Assert.Throws<ArgumentException>(() => new ToolProtocolBinding(
            tool,
            project,
            new[] { new KeyValuePair<string, string>(" key", "value") }));
        Assert.Throws<ArgumentException>(() => new ToolProtocolBinding(
            tool,
            project,
            new[] { new KeyValuePair<string, string>("key\0tail", "value") }));
        Assert.Throws<ArgumentException>(() => new ToolProtocolBinding(
            tool,
            project,
            new[] { new KeyValuePair<string, string>("key", "value\0tail") }));
        Assert.Throws<ArgumentException>(() => new ToolProtocolBinding(
            tool,
            project,
            new[]
            {
                new KeyValuePair<string, string>("Endpoint", "one"),
                new KeyValuePair<string, string>("endpoint", "two"),
            }));
    }

    private static ProjectContext CreateProjectContext()
    {
        var rootPath = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), $"fccd-p09-008-{Guid.NewGuid():N}"));
        return new ProjectContext(Guid.NewGuid(), rootPath);
    }

    private sealed class FixtureProtocolFactory : IExternalToolProtocolAdapterFactory
    {
        public FixtureProtocolFactory(ToolProtocolIdentity protocol)
        {
            Protocol = protocol;
        }

        public ToolProtocolIdentity Protocol { get; }

        public ToolProtocolBinding? LastBinding { get; private set; }

        public Task<IExternalToolAdapter> CreateAsync(
            ToolProtocolBinding binding,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(binding);
            cancellationToken.ThrowIfCancellationRequested();
            LastBinding = binding;
            return Task.FromResult<IExternalToolAdapter>(new FixtureAdapter(binding.Tool));
        }
    }

    private sealed class FixtureAdapter : IExternalToolAdapter
    {
        public FixtureAdapter(ToolIdentity identity)
        {
            Identity = identity;
        }

        public ToolIdentity Identity { get; }

        public Task<ToolDiscoveryResult> DiscoverAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ToolCapabilitySet> GetCapabilitiesAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<ToolEvent> ExecuteAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
