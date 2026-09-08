using System.Runtime.CompilerServices;
using FCCCodeDesktop.Tools;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ExternalToolGatewayPhaseExitTests
{
    [Fact]
    public async Task FixtureAdapterSatisfiesExternalToolGatewayExitContract()
    {
        var root = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), $"fccd-p09-exit-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(root);

        try
        {
            var project = new ProjectContext(Guid.NewGuid(), root);
            var adapter = new FixtureGatewayAdapter();
            var registry = new ExternalToolRegistry(new[] { adapter });

            var discovery = await registry
                .DiscoverAsync(adapter.Identity.Id, project)
                .ConfigureAwait(false);
            var capabilities = await registry
                .GetCapabilitiesAsync(adapter.Identity.Id, project)
                .ConfigureAwait(false);

            Assert.True(discovery.IsAvailable);
            Assert.IsType<FixtureCapabilities>(capabilities);
            Assert.Same(adapter, registry.GetRequiredAdapter("FIXTURE.GATEWAY"));

            var invocation = new FixtureInvocation(project, "fixture.produce");
            var lockCoordinator = new ExternalToolResourceLockCoordinator(new ToolResourceLockManager());

            await using (var lease = await lockCoordinator
                .AcquireAsync(adapter, invocation)
                .ConfigureAwait(false))
            {
                Assert.Collection(
                    lease.Keys,
                    key => Assert.Equal($"fixture.gateway:{project.ProjectId:N}", key.Value));

                using var contenderCancellation = new CancellationTokenSource();
                var contender = lockCoordinator
                    .AcquireAsync(adapter, invocation, contenderCancellation.Token)
                    .AsTask();

                Assert.False(contender.IsCompleted);
                contenderCancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    async () => await contender.ConfigureAwait(false));

                var events = await CollectAsync(adapter.ExecuteAsync(invocation)).ConfigureAwait(false);
                var resultEvent = Assert.IsType<ToolResultEvent>(Assert.Single(events));
                var result = Assert.IsType<FixtureResult>(resultEvent.Result);
                Assert.Equal(ToolResultStatus.Succeeded, result.Status);
                Assert.Equal("Fixture artifact created.", result.Summary);
            }

            var manifest = new ToolArtifactManifest(
                root,
                new[]
                {
                    new ToolArtifactDefinition(
                        "fixture-result",
                        Path.Combine("artifacts", "result.txt"),
                        ToolArtifactKind.File,
                        requireNonEmpty: true),
                });
            var artifactReport = await new ToolArtifactValidator()
                .ValidateAsync(manifest)
                .ConfigureAwait(false);

            Assert.True(artifactReport.Succeeded);
            var artifact = Assert.Single(artifactReport.Entries);
            Assert.Equal(ToolArtifactValidationStatus.Valid, artifact.Status);
            Assert.NotNull(artifact.Sha256);
            Assert.Equal(64, artifact.Sha256!.Length);
            Assert.IsAssignableFrom<ToolEvent>(new ToolArtifactValidationEvent(artifactReport));

            var health = await new ExternalToolHealthService(registry)
                .CheckAsync(adapter.Identity.Id, project)
                .ConfigureAwait(false);

            Assert.Equal(ToolHealthStatus.Healthy, health.Status);
            Assert.Equal(adapter.Identity, health.Identity);
            Assert.Collection(
                health.Diagnostics,
                diagnostic =>
                {
                    Assert.Equal("fixture.gateway.ready", diagnostic.Code);
                    Assert.Equal(ToolDiagnosticSeverity.Information, diagnostic.Severity);
                });
            Assert.IsAssignableFrom<ToolEvent>(new ToolHealthReportEvent(health));

            var protocolRegistry = new ExternalToolProtocolRegistry();
            Assert.Empty(protocolRegistry.Protocols);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FixtureAdapterCancellationPropagatesWithoutProducingTerminalSuccess()
    {
        var root = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), $"fccd-p09-exit-cancel-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(root);

        try
        {
            var project = new ProjectContext(Guid.NewGuid(), root);
            var adapter = new FixtureGatewayAdapter();
            var invocation = new FixtureInvocation(project, "fixture.block");
            var lockCoordinator = new ExternalToolResourceLockCoordinator(new ToolResourceLockManager());

            await using var lease = await lockCoordinator
                .AcquireAsync(adapter, invocation)
                .ConfigureAwait(false);
            using var cancellation = new CancellationTokenSource();

            var collection = CollectAsync(adapter.ExecuteAsync(invocation, cancellation.Token));
            await adapter.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await collection.ConfigureAwait(false));
            Assert.False(File.Exists(Path.Combine(root, "artifacts", "result.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<List<ToolEvent>> CollectAsync(IAsyncEnumerable<ToolEvent> source)
    {
        var events = new List<ToolEvent>();
        await foreach (var item in source.ConfigureAwait(false))
        {
            events.Add(item);
        }

        return events;
    }

    private sealed class FixtureGatewayAdapter :
        IExternalToolAdapter,
        IExternalToolResourceLockProvider,
        IExternalToolHealthProvider
    {
        public ToolIdentity Identity { get; } = new("fixture.gateway", "Fixture Gateway");

        public TaskCompletionSource ExecutionStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ToolDiscoveryResult> DiscoverAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(project);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ToolDiscoveryResult>(new FixtureDiscovery(isAvailable: true));
        }

        public Task<ToolCapabilitySet> GetCapabilitiesAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(project);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ToolCapabilitySet>(new FixtureCapabilities());
        }

        public IReadOnlyCollection<ToolResourceLockKey> GetResourceLockKeys(ToolInvocation invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            return new[]
            {
                new ToolResourceLockKey($"fixture.gateway:{invocation.Project.ProjectId:N}"),
            };
        }

        public Task<ToolHealthReport> GetHealthAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(project);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new ToolHealthReport(
                    Identity,
                    ToolHealthStatus.Healthy,
                    DateTimeOffset.UtcNow,
                    new[]
                    {
                        new ToolDiagnostic(
                            "fixture.gateway.ready",
                            ToolDiagnosticSeverity.Information,
                            "Fixture gateway is ready."),
                    }));
        }

        public async IAsyncEnumerable<ToolEvent> ExecuteAsync(
            ToolInvocation invocation,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            if (invocation is not FixtureInvocation fixture)
            {
                throw new ArgumentException("Fixture adapter requires a fixture invocation.", nameof(invocation));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (fixture.Operation == "fixture.block")
            {
                ExecutionStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                yield break;
            }

            if (fixture.Operation != "fixture.produce")
            {
                throw new InvalidOperationException("Unknown fixture operation.");
            }

            var artifactDirectory = Path.Combine(fixture.Project.RootPath, "artifacts");
            Directory.CreateDirectory(artifactDirectory);
            await File.WriteAllTextAsync(
                    Path.Combine(artifactDirectory, "result.txt"),
                    "validated fixture artifact",
                    cancellationToken)
                .ConfigureAwait(false);

            yield return new ToolResultEvent(
                new FixtureResult(ToolResultStatus.Succeeded, "Fixture artifact created."));
        }
    }

    private sealed record FixtureDiscovery : ToolDiscoveryResult
    {
        public FixtureDiscovery(bool isAvailable)
            : base(isAvailable)
        {
        }
    }

    private sealed record FixtureCapabilities : ToolCapabilitySet;

    private sealed record FixtureInvocation : StructuredToolInvocation
    {
        public FixtureInvocation(ProjectContext project, string operation)
            : base(project, operation)
        {
        }
    }

    private sealed record FixtureResult : ToolResult
    {
        public FixtureResult(ToolResultStatus status, string summary)
            : base(status, summary)
        {
        }
    }
}
