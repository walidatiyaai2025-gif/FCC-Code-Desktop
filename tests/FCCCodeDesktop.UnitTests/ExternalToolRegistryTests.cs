using System.Runtime.CompilerServices;
using FCCCodeDesktop.Tools;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ExternalToolRegistryTests
{
    [Fact]
    public void RegistryMaterializesDeterministicImmutableCatalogAndResolvesIdsCaseInsensitively()
    {
        var alpha = new FixtureAdapter("alpha.tool");
        var zeta = new FixtureAdapter("zeta.tool");
        var source = new List<IExternalToolAdapter> { zeta, alpha };

        var registry = new ExternalToolRegistry(source);
        source.Clear();

        Assert.Collection(
            registry.RegisteredTools,
            identity => Assert.Equal("alpha.tool", identity.Id),
            identity => Assert.Equal("zeta.tool", identity.Id));
        Assert.Same(alpha, registry.GetRequiredAdapter("ALPHA.TOOL"));
        Assert.True(registry.TryGetAdapter("zeta.tool", out var resolved));
        Assert.Same(zeta, resolved);
        Assert.False(registry.TryGetAdapter("missing.tool", out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void RegistryRejectsDuplicateNullAndInvalidAdapterRegistrations()
    {
        Assert.Throws<InvalidOperationException>(() => new ExternalToolRegistry(
            new IExternalToolAdapter[]
            {
                new FixtureAdapter("fixture.tool"),
                new FixtureAdapter("FIXTURE.TOOL"),
            }));

        Assert.Throws<ArgumentException>(() => new ExternalToolRegistry(
            new IExternalToolAdapter[] { null! }));
        Assert.Throws<InvalidOperationException>(() => new ExternalToolRegistry(
            new IExternalToolAdapter[] { new NullIdentityAdapter() }));
    }

    [Fact]
    public async Task EmptyRegistryIsValidAndDiscoversNoTools()
    {
        var registry = new ExternalToolRegistry(Array.Empty<IExternalToolAdapter>());
        var project = CreateProjectContext();

        var results = await registry.DiscoverAllAsync(project);

        Assert.Empty(registry.RegisteredTools);
        Assert.Empty(results);
    }

    [Fact]
    public async Task DiscoverAllRoutesProjectContextInDeterministicOrderAndRetainsUnavailableAdapters()
    {
        var alpha = new FixtureAdapter("alpha.tool", isAvailable: true);
        var beta = new FixtureAdapter("beta.tool", isAvailable: false);
        var registry = new ExternalToolRegistry(new IExternalToolAdapter[] { beta, alpha });
        var project = CreateProjectContext();

        var results = await registry.DiscoverAllAsync(project);

        Assert.Collection(
            results,
            result =>
            {
                Assert.Equal("alpha.tool", result.Identity.Id);
                Assert.Same(alpha.DiscoveryResult, result.Discovery);
                Assert.True(result.Discovery.IsAvailable);
            },
            result =>
            {
                Assert.Equal("beta.tool", result.Identity.Id);
                Assert.Same(beta.DiscoveryResult, result.Discovery);
                Assert.False(result.Discovery.IsAvailable);
            });
        Assert.Same(project, alpha.LastDiscoveryProject);
        Assert.Same(project, beta.LastDiscoveryProject);
        Assert.Equal(1, alpha.DiscoveryCalls);
        Assert.Equal(1, beta.DiscoveryCalls);
        Assert.Equal(0, alpha.CapabilityCalls);
        Assert.Equal(0, beta.CapabilityCalls);
    }

    [Fact]
    public async Task TargetedDiscoveryAndCapabilitiesRouteThroughTheRegisteredAdapter()
    {
        var adapter = new FixtureAdapter("fixture.tool");
        var registry = new ExternalToolRegistry(new IExternalToolAdapter[] { adapter });
        var project = CreateProjectContext();

        var discovery = await registry.DiscoverAsync("FIXTURE.TOOL", project);
        var capabilities = await registry.GetCapabilitiesAsync("fixture.tool", project);

        Assert.Same(adapter.DiscoveryResult, discovery);
        Assert.Same(adapter.CapabilitySet, capabilities);
        Assert.Same(project, adapter.LastDiscoveryProject);
        Assert.Same(project, adapter.LastCapabilityProject);
        Assert.Equal(1, adapter.DiscoveryCalls);
        Assert.Equal(1, adapter.CapabilityCalls);
        Assert.Throws<KeyNotFoundException>(() => registry.GetRequiredAdapter("missing.tool"));
        Assert.Throws<ArgumentException>(() => registry.GetRequiredAdapter(" fixture.tool"));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => registry.DiscoverAsync("missing.tool", project));
    }

    [Fact]
    public async Task DiscoverAllStopsBeforeLaterAdaptersWhenCancellationIsObserved()
    {
        using var cancellation = new CancellationTokenSource();
        var alpha = new FixtureAdapter("alpha.tool", onDiscover: cancellation.Cancel);
        var beta = new FixtureAdapter("beta.tool");
        var registry = new ExternalToolRegistry(new IExternalToolAdapter[] { beta, alpha });
        var project = CreateProjectContext();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => registry.DiscoverAllAsync(project, cancellation.Token));

        Assert.Equal(1, alpha.DiscoveryCalls);
        Assert.Equal(0, beta.DiscoveryCalls);
    }

    [Fact]
    public async Task TargetedOperationsRespectPreCancelledTokensWithoutCallingAdapters()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var adapter = new FixtureAdapter("fixture.tool");
        var registry = new ExternalToolRegistry(new IExternalToolAdapter[] { adapter });
        var project = CreateProjectContext();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => registry.DiscoverAsync("fixture.tool", project, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => registry.GetCapabilitiesAsync("fixture.tool", project, cancellation.Token));

        Assert.Equal(0, adapter.DiscoveryCalls);
        Assert.Equal(0, adapter.CapabilityCalls);
    }

    [Fact]
    public async Task RegistryFailsClosedWhenAnAdapterReturnsNullContractResults()
    {
        var adapter = new FixtureAdapter(
            "fixture.tool",
            returnNullDiscovery: true,
            returnNullCapabilities: true);
        var registry = new ExternalToolRegistry(new IExternalToolAdapter[] { adapter });
        var project = CreateProjectContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.DiscoverAsync("fixture.tool", project));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.GetCapabilitiesAsync("fixture.tool", project));
    }

    private static ProjectContext CreateProjectContext()
    {
        return new ProjectContext(
            Guid.NewGuid(),
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"fccd-tool-registry-{Guid.NewGuid():N}")));
    }

    private sealed class FixtureAdapter : IExternalToolAdapter
    {
        private readonly Action? _onDiscover;
        private readonly bool _returnNullDiscovery;
        private readonly bool _returnNullCapabilities;

        public FixtureAdapter(
            string id,
            bool isAvailable = true,
            Action? onDiscover = null,
            bool returnNullDiscovery = false,
            bool returnNullCapabilities = false)
        {
            Identity = new ToolIdentity(id, $"{id} display");
            DiscoveryResult = new FixtureDiscoveryResult(isAvailable, id);
            CapabilitySet = new FixtureCapabilitySet(id);
            _onDiscover = onDiscover;
            _returnNullDiscovery = returnNullDiscovery;
            _returnNullCapabilities = returnNullCapabilities;
        }

        public ToolIdentity Identity { get; }

        public ToolDiscoveryResult DiscoveryResult { get; }

        public ToolCapabilitySet CapabilitySet { get; }

        public int DiscoveryCalls { get; private set; }

        public int CapabilityCalls { get; private set; }

        public ProjectContext? LastDiscoveryProject { get; private set; }

        public ProjectContext? LastCapabilityProject { get; private set; }

        public Task<ToolDiscoveryResult> DiscoverAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(project);
            cancellationToken.ThrowIfCancellationRequested();
            DiscoveryCalls++;
            LastDiscoveryProject = project;
            _onDiscover?.Invoke();
            return Task.FromResult(_returnNullDiscovery ? null! : DiscoveryResult);
        }

        public Task<ToolCapabilitySet> GetCapabilitiesAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(project);
            cancellationToken.ThrowIfCancellationRequested();
            CapabilityCalls++;
            LastCapabilityProject = project;
            return Task.FromResult(_returnNullCapabilities ? null! : CapabilitySet);
        }

        public async IAsyncEnumerable<ToolEvent> ExecuteAsync(
            ToolInvocation invocation,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield break;
        }
    }

    private sealed class NullIdentityAdapter : IExternalToolAdapter
    {
        public ToolIdentity Identity => null!;

        public Task<ToolDiscoveryResult> DiscoverAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ToolCapabilitySet> GetCapabilitiesAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<ToolEvent> ExecuteAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed record FixtureDiscoveryResult(bool Available, string Marker)
        : ToolDiscoveryResult(Available);

    private sealed record FixtureCapabilitySet(string Marker) : ToolCapabilitySet;
}
