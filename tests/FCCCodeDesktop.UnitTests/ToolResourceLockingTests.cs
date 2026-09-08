using FCCCodeDesktop.Tools;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ToolResourceLockingTests
{
    [Fact]
    public void LockKeyRejectsAmbiguousValues()
    {
        Assert.Throws<ArgumentNullException>(() => new ToolResourceLockKey(null!));
        Assert.Throws<ArgumentException>(() => new ToolResourceLockKey(" "));
        Assert.Throws<ArgumentException>(() => new ToolResourceLockKey(" project:alpha"));
        Assert.Throws<ArgumentException>(() => new ToolResourceLockKey("project:alpha "));
        Assert.Throws<ArgumentException>(() => new ToolResourceLockKey("project:\0alpha"));
    }

    [Fact]
    public async Task SameKeySerializesCaseInsensitivelyAndLeaseReleaseIsIdempotent()
    {
        var manager = new ToolResourceLockManager();
        var first = await manager.AcquireAsync(new[] { new ToolResourceLockKey("project:alpha") });

        var waiting = manager.AcquireAsync(
            new[] { new ToolResourceLockKey("PROJECT:ALPHA") }).AsTask();

        await Task.Yield();
        Assert.False(waiting.IsCompleted);

        await first.DisposeAsync();
        first.Dispose();

        await using var second = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(second.Keys);
        Assert.Equal("PROJECT:ALPHA", second.Keys[0].Value);
    }

    [Fact]
    public async Task IndependentKeysCanBeHeldConcurrently()
    {
        var manager = new ToolResourceLockManager();
        await using var first = await manager.AcquireAsync(
            new[] { new ToolResourceLockKey("project:alpha") });

        var secondTask = manager.AcquireAsync(
            new[] { new ToolResourceLockKey("project:beta") }).AsTask();

        await using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("project:beta", Assert.Single(second.Keys).Value);
    }

    [Fact]
    public async Task DuplicateAndReversedMultiKeyRequestsDoNotDeadlock()
    {
        var manager = new ToolResourceLockManager();
        var alpha = new ToolResourceLockKey("project:alpha");
        var beta = new ToolResourceLockKey("editor:shared");

        var first = await manager.AcquireAsync(new[] { beta, alpha, alpha });
        Assert.Equal(new[] { "editor:shared", "project:alpha" }, first.Keys.Select(key => key.Value));

        var waiting = manager.AcquireAsync(new[] { alpha, beta }).AsTask();
        await Task.Yield();
        Assert.False(waiting.IsCompleted);

        first.Dispose();

        await using var second = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "editor:shared", "project:alpha" }, second.Keys.Select(key => key.Value));
    }

    [Fact]
    public async Task CancellationWhileWaitingDoesNotPoisonTheResource()
    {
        var manager = new ToolResourceLockManager();
        var key = new ToolResourceLockKey("project:alpha");
        var first = await manager.AcquireAsync(new[] { key });
        using var cancellation = new CancellationTokenSource();

        var waiting = manager.AcquireAsync(new[] { key }, cancellation.Token).AsTask();
        await Task.Yield();
        Assert.False(waiting.IsCompleted);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await waiting;
        });
        first.Dispose();

        await using var recovered = await manager.AcquireAsync(new[] { key }).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(recovered.Keys);
    }

    [Fact]
    public async Task CancellationDuringMultiKeyWaitReleasesAlreadyAcquiredKeys()
    {
        var manager = new ToolResourceLockManager();
        var alpha = new ToolResourceLockKey("a:project");
        var beta = new ToolResourceLockKey("b:editor");
        var betaOwner = await manager.AcquireAsync(new[] { beta });
        using var cancellation = new CancellationTokenSource();

        var partial = manager.AcquireAsync(new[] { beta, alpha }, cancellation.Token).AsTask();
        await Task.Yield();
        Assert.False(partial.IsCompleted);

        var alphaWaiter = manager.AcquireAsync(new[] { alpha }).AsTask();
        await Task.Yield();
        Assert.False(alphaWaiter.IsCompleted);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await partial;
        });

        await using var alphaLease = await alphaWaiter.WaitAsync(TimeSpan.FromSeconds(5));
        betaOwner.Dispose();
    }

    [Fact]
    public async Task CoordinatorRequiresExplicitAdapterDeclarationAndUsesDeclaredKeys()
    {
        var manager = new ToolResourceLockManager();
        var coordinator = new ExternalToolResourceLockCoordinator(manager);
        var project = CreateProjectContext();
        var invocation = new FixtureInvocation(project);

        var missingDeclaration = new FixtureAdapter();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await coordinator.AcquireAsync(missingDeclaration, invocation);
        });

        var declared = new LockingFixtureAdapter(new ToolResourceLockKey("project:fixture"));
        await using var first = await coordinator.AcquireAsync(declared, invocation);

        var waiting = coordinator.AcquireAsync(declared, invocation).AsTask();
        await Task.Yield();
        Assert.False(waiting.IsCompleted);

        first.Dispose();
        await using var second = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("project:fixture", Assert.Single(second.Keys).Value);
    }

    [Fact]
    public async Task EmptyLockSetIsExplicitAndHonorsPreCancellation()
    {
        var manager = new ToolResourceLockManager();
        await using var empty = await manager.AcquireAsync(Array.Empty<ToolResourceLockKey>());
        Assert.Empty(empty.Keys);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await manager.AcquireAsync(Array.Empty<ToolResourceLockKey>(), cancellation.Token);
        });
    }

    private static ProjectContext CreateProjectContext()
    {
        var rootPath = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), $"fccd-p09-004-{Guid.NewGuid():N}", "project مساحة"));
        return new ProjectContext(Guid.NewGuid(), rootPath);
    }

    private sealed record FixtureInvocation : ToolInvocation
    {
        public FixtureInvocation(ProjectContext project)
            : base(project)
        {
        }
    }

    private class FixtureAdapter : IExternalToolAdapter
    {
        public ToolIdentity Identity { get; } = new("fixture", "Fixture");

        public Task<ToolDiscoveryResult> DiscoverAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ToolDiscoveryResult>(new FixtureDiscoveryResult());

        public Task<ToolCapabilitySet> GetCapabilitiesAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ToolCapabilitySet>(new FixtureCapabilitySet());

        public IAsyncEnumerable<ToolEvent> ExecuteAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default) => EmptyEvents();

        private static async IAsyncEnumerable<ToolEvent> EmptyEvents()
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed class LockingFixtureAdapter : FixtureAdapter, IExternalToolResourceLockProvider
    {
        private readonly IReadOnlyCollection<ToolResourceLockKey> _keys;

        public LockingFixtureAdapter(params ToolResourceLockKey[] keys)
        {
            _keys = Array.AsReadOnly(keys);
        }

        public IReadOnlyCollection<ToolResourceLockKey> GetResourceLockKeys(ToolInvocation invocation) => _keys;
    }

    private sealed record FixtureDiscoveryResult() : ToolDiscoveryResult(isAvailable: true);

    private sealed record FixtureCapabilitySet : ToolCapabilitySet;
}
