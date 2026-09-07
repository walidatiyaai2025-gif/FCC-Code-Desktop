using System.Runtime.CompilerServices;
using FCCCodeDesktop.Tools;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ExternalToolAdapterContractTests
{
    [Fact]
    public void ToolIdentityRequiresStableTrimmedValues()
    {
        var identity = new ToolIdentity("fixture.tool", "Fixture Tool");

        Assert.Equal("fixture.tool", identity.Id);
        Assert.Equal("Fixture Tool", identity.DisplayName);
        Assert.Throws<ArgumentException>(() => new ToolIdentity("", "Fixture Tool"));
        Assert.Throws<ArgumentException>(() => new ToolIdentity("fixture.tool", " "));
        Assert.Throws<ArgumentException>(() => new ToolIdentity(" fixture.tool", "Fixture Tool"));
        Assert.Throws<ArgumentException>(() => new ToolIdentity("fixture.tool", "Fixture Tool "));
    }

    [Fact]
    public void ProjectContextRequiresIdentityAndFullyQualifiedRootWithoutTouchingDisk()
    {
        var projectId = Guid.NewGuid();
        var rootPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fccd-tool-contract", "مساحة project"));

        var context = new ProjectContext(projectId, rootPath);

        Assert.Equal(projectId, context.ProjectId);
        Assert.Equal(rootPath, context.RootPath);
        Assert.False(Directory.Exists(rootPath));
        Assert.Throws<ArgumentException>(() => new ProjectContext(Guid.Empty, rootPath));
        Assert.Throws<ArgumentException>(() => new ProjectContext(Guid.NewGuid(), "."));
        Assert.Throws<ArgumentException>(() => new ProjectContext(Guid.NewGuid(), " "));
    }

    [Fact]
    public async Task AdapterContractSupportsDiscoveryCapabilitiesAndStreamingExecution()
    {
        var adapter = new FixtureAdapter();
        var context = CreateProjectContext();

        var discovery = await adapter.DiscoverAsync(context);
        var capabilities = await adapter.GetCapabilitiesAsync(context);
        var invocation = new FixtureInvocation(context);
        var events = new List<ToolEvent>();

        await foreach (var toolEvent in adapter.ExecuteAsync(invocation))
        {
            events.Add(toolEvent);
        }

        Assert.Equal("fixture.tool", adapter.Identity.Id);
        Assert.True(discovery.IsAvailable);
        Assert.IsType<FixtureCapabilitySet>(capabilities);
        Assert.Single(events);
        Assert.IsType<FixtureEvent>(events[0]);
        Assert.Same(context, invocation.Project);
    }

    [Fact]
    public async Task ContractCarriesCancellationAcrossEveryCancellableOperation()
    {
        var adapter = new FixtureAdapter();
        var context = CreateProjectContext();
        var invocation = new FixtureInvocation(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => adapter.DiscoverAsync(context, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => adapter.GetCapabilitiesAsync(context, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in adapter.ExecuteAsync(invocation, cancellation.Token))
            {
            }
        });
    }

    [Fact]
    public void ToolInvocationRequiresProjectContext()
    {
        Assert.Throws<ArgumentNullException>(() => new FixtureInvocation(null!));
    }

    private static ProjectContext CreateProjectContext()
    {
        return new ProjectContext(
            Guid.NewGuid(),
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fccd-tool-contract-project")));
    }

    private sealed class FixtureAdapter : IExternalToolAdapter
    {
        public ToolIdentity Identity { get; } = new("fixture.tool", "Fixture Tool");

        public Task<ToolDiscoveryResult> DiscoverAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(project);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ToolDiscoveryResult>(new FixtureDiscoveryResult(isAvailable: true));
        }

        public Task<ToolCapabilitySet> GetCapabilitiesAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(project);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ToolCapabilitySet>(new FixtureCapabilitySet());
        }

        public async IAsyncEnumerable<ToolEvent> ExecuteAsync(
            ToolInvocation invocation,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new FixtureEvent();
        }
    }

    private sealed record FixtureDiscoveryResult : ToolDiscoveryResult
    {
        public FixtureDiscoveryResult(bool isAvailable)
            : base(isAvailable)
        {
        }
    }

    private sealed record FixtureCapabilitySet : ToolCapabilitySet;

    private sealed record FixtureInvocation(ProjectContext Context) : ToolInvocation(Context);

    private sealed record FixtureEvent : ToolEvent;
}
