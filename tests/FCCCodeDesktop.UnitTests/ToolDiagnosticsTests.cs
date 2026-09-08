using System.Runtime.CompilerServices;
using FCCCodeDesktop.Tools;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ToolDiagnosticsTests
{
    [Fact]
    public void DiagnosticAndHealthReportValidateStructuredImmutableState()
    {
        var identity = new ToolIdentity("fixture.tool", "Fixture Tool");
        var source = new List<ToolDiagnostic>
        {
            new(
                "fixture.health.ready",
                ToolDiagnosticSeverity.Information,
                "Fixture health probe completed."),
        };
        var observedAt = new DateTimeOffset(2026, 9, 8, 7, 45, 0, TimeSpan.FromHours(3));

        var report = new ToolHealthReport(identity, ToolHealthStatus.Healthy, observedAt, source);
        source.Clear();

        Assert.Same(identity, report.Identity);
        Assert.Equal(ToolHealthStatus.Healthy, report.Status);
        Assert.Equal(observedAt.ToUniversalTime(), report.ObservedAt);
        Assert.Single(report.Diagnostics);
        Assert.Equal("fixture.health.ready", report.Diagnostics[0].Code);

        var toolEvent = new ToolHealthReportEvent(report);
        Assert.Same(report, toolEvent.Report);
        Assert.IsAssignableFrom<ToolEvent>(toolEvent);

        Assert.Throws<ArgumentException>(() => new ToolDiagnostic(
            "bad code",
            ToolDiagnosticSeverity.Error,
            "Invalid code."));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolDiagnostic(
            "fixture.bad",
            ToolDiagnosticSeverity.Unknown,
            "Unknown severity."));
        Assert.Throws<ArgumentException>(() => new ToolDiagnostic(
            "fixture.bad",
            ToolDiagnosticSeverity.Error,
            " summary"));
        Assert.Throws<ArgumentException>(() => new ToolDiagnostic(
            "fixture.bad",
            ToolDiagnosticSeverity.Error,
            "Summary.",
            " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolHealthReport(
            identity,
            ToolHealthStatus.Unknown,
            observedAt));
        Assert.Throws<ArgumentException>(() => new ToolHealthReport(
            identity,
            ToolHealthStatus.Healthy,
            observedAt,
            new ToolDiagnostic[] { null! }));
    }

    [Fact]
    public async Task SpecializedProviderHealthFlowsThroughTheGatewayWithoutDiscoveryFallback()
    {
        var identity = new ToolIdentity("fixture.tool", "Fixture Tool");
        var observedAt = new DateTimeOffset(2026, 9, 8, 4, 45, 0, TimeSpan.Zero);
        var expected = new ToolHealthReport(
            identity,
            ToolHealthStatus.Healthy,
            observedAt,
            new[]
            {
                new ToolDiagnostic(
                    "fixture.health.ready",
                    ToolDiagnosticSeverity.Information,
                    "Fixture tool is ready."),
            });
        var adapter = new HealthFixtureAdapter(identity, (_, _) => Task.FromResult(expected));
        var registry = new ExternalToolRegistry(new IExternalToolAdapter[] { adapter });
        var service = new ExternalToolHealthService(registry);
        var project = CreateProjectContext();

        var actual = await service.CheckAsync("FIXTURE.TOOL", project);

        Assert.Same(expected, actual);
        Assert.Equal(1, adapter.HealthCalls);
        Assert.Equal(0, adapter.DiscoveryCalls);
        Assert.Same(project, adapter.LastHealthProject);
    }

    [Fact]
    public async Task DiscoveryFallbackClassifiesAvailableAndUnavailableAdaptersStructurally()
    {
        var available = new DiscoveryFixtureAdapter("available.tool", isAvailable: true);
        var unavailable = new DiscoveryFixtureAdapter("unavailable.tool", isAvailable: false);
        var registry = new ExternalToolRegistry(new IExternalToolAdapter[] { unavailable, available });
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 9, 8, 5, 0, 0, TimeSpan.Zero));
        var service = new ExternalToolHealthService(registry, timeProvider);
        var project = CreateProjectContext();

        var reports = await service.CheckAllAsync(project);

        Assert.Collection(
            reports,
            report =>
            {
                Assert.Equal("available.tool", report.Identity.Id);
                Assert.Equal(ToolHealthStatus.Degraded, report.Status);
                Assert.Equal("tool.health.specialized_probe_unavailable", Assert.Single(report.Diagnostics).Code);
                Assert.Equal(timeProvider.GetUtcNow(), report.ObservedAt);
            },
            report =>
            {
                Assert.Equal("unavailable.tool", report.Identity.Id);
                Assert.Equal(ToolHealthStatus.Unavailable, report.Status);
                Assert.Equal("tool.discovery.unavailable", Assert.Single(report.Diagnostics).Code);
                Assert.Equal(timeProvider.GetUtcNow(), report.ObservedAt);
            });
        Assert.Equal(1, available.DiscoveryCalls);
        Assert.Equal(1, unavailable.DiscoveryCalls);
    }

    [Fact]
    public async Task CatalogHealthCheckIsolatesProbeFailuresAndDoesNotExposeExceptionMessages()
    {
        var failed = new HealthFixtureAdapter(
            new ToolIdentity("alpha.tool", "Alpha Tool"),
            (_, _) => throw new InvalidOperationException("Authorization: Bearer secret-value"));
        var healthyIdentity = new ToolIdentity("beta.tool", "Beta Tool");
        var healthyReport = new ToolHealthReport(
            healthyIdentity,
            ToolHealthStatus.Healthy,
            DateTimeOffset.UtcNow);
        var healthy = new HealthFixtureAdapter(
            healthyIdentity,
            (_, _) => Task.FromResult(healthyReport));
        var registry = new ExternalToolRegistry(new IExternalToolAdapter[] { healthy, failed });
        var service = new ExternalToolHealthService(registry);

        var reports = await service.CheckAllAsync(CreateProjectContext());

        Assert.Collection(
            reports,
            report =>
            {
                Assert.Equal("alpha.tool", report.Identity.Id);
                Assert.Equal(ToolHealthStatus.Unhealthy, report.Status);
                var diagnostic = Assert.Single(report.Diagnostics);
                Assert.Equal("tool.health.probe_failed", diagnostic.Code);
                Assert.Contains(nameof(InvalidOperationException), diagnostic.Summary, StringComparison.Ordinal);
                Assert.DoesNotContain("secret-value", diagnostic.Summary, StringComparison.Ordinal);
                Assert.DoesNotContain("Bearer", diagnostic.Summary, StringComparison.OrdinalIgnoreCase);
            },
            report => Assert.Same(healthyReport, report));
        Assert.Equal(1, failed.HealthCalls);
        Assert.Equal(1, healthy.HealthCalls);
    }

    [Fact]
    public async Task ProviderContractViolationsFailClosedAsStructuredUnhealthyReports()
    {
        var expectedIdentity = new ToolIdentity("fixture.tool", "Fixture Tool");
        var mismatched = new HealthFixtureAdapter(
            expectedIdentity,
            (_, _) => Task.FromResult(new ToolHealthReport(
                new ToolIdentity("other.tool", "Other Tool"),
                ToolHealthStatus.Healthy,
                DateTimeOffset.UtcNow)));
        var nullReport = new HealthFixtureAdapter(
            new ToolIdentity("null.tool", "Null Tool"),
            (_, _) => Task.FromResult<ToolHealthReport>(null!));
        var registry = new ExternalToolRegistry(new IExternalToolAdapter[] { nullReport, mismatched });
        var service = new ExternalToolHealthService(registry);
        var project = CreateProjectContext();

        var mismatchReport = await service.CheckAsync("fixture.tool", project);
        var nullHealthReport = await service.CheckAsync("null.tool", project);

        Assert.Equal(ToolHealthStatus.Unhealthy, mismatchReport.Status);
        Assert.Equal("tool.health.identity_mismatch", Assert.Single(mismatchReport.Diagnostics).Code);
        Assert.Equal(expectedIdentity, mismatchReport.Identity);

        Assert.Equal(ToolHealthStatus.Unhealthy, nullHealthReport.Status);
        Assert.Equal("tool.health.probe_failed", Assert.Single(nullHealthReport.Diagnostics).Code);
    }

    [Fact]
    public async Task PreCancellationStopsHealthChecksBeforeCallingAdapters()
    {
        var adapter = new HealthFixtureAdapter(
            new ToolIdentity("fixture.tool", "Fixture Tool"),
            (_, _) => Task.FromResult(new ToolHealthReport(
                new ToolIdentity("fixture.tool", "Fixture Tool"),
                ToolHealthStatus.Healthy,
                DateTimeOffset.UtcNow)));
        var service = new ExternalToolHealthService(
            new ExternalToolRegistry(new IExternalToolAdapter[] { adapter }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CheckAsync("fixture.tool", CreateProjectContext(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CheckAllAsync(CreateProjectContext(), cancellation.Token));

        Assert.Equal(0, adapter.HealthCalls);
        Assert.Equal(0, adapter.DiscoveryCalls);
    }

    [Fact]
    public async Task CancellationRaisedByProviderPropagatesInsteadOfBecomingAnUnhealthyReport()
    {
        using var cancellation = new CancellationTokenSource();
        var adapter = new HealthFixtureAdapter(
            new ToolIdentity("fixture.tool", "Fixture Tool"),
            (_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Unreachable.");
            });
        var service = new ExternalToolHealthService(
            new ExternalToolRegistry(new IExternalToolAdapter[] { adapter }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CheckAsync("fixture.tool", CreateProjectContext(), cancellation.Token));

        Assert.Equal(1, adapter.HealthCalls);
    }

    private static ProjectContext CreateProjectContext()
    {
        return new ProjectContext(
            Guid.NewGuid(),
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"fccd-p09-006-{Guid.NewGuid():N}")));
    }

    private class DiscoveryFixtureAdapter : IExternalToolAdapter
    {
        private readonly ToolDiscoveryResult _discovery;

        public DiscoveryFixtureAdapter(string id, bool isAvailable)
            : this(new ToolIdentity(id, $"{id} display"), isAvailable)
        {
        }

        protected DiscoveryFixtureAdapter(ToolIdentity identity, bool isAvailable = true)
        {
            Identity = identity;
            _discovery = new FixtureDiscoveryResult(isAvailable);
        }

        public ToolIdentity Identity { get; }

        public int DiscoveryCalls { get; private set; }

        public Task<ToolDiscoveryResult> DiscoverAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(project);
            cancellationToken.ThrowIfCancellationRequested();
            DiscoveryCalls++;
            return Task.FromResult(_discovery);
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
            yield break;
        }
    }

    private sealed class HealthFixtureAdapter : DiscoveryFixtureAdapter, IExternalToolHealthProvider
    {
        private readonly Func<ProjectContext, CancellationToken, Task<ToolHealthReport>> _health;

        public HealthFixtureAdapter(
            ToolIdentity identity,
            Func<ProjectContext, CancellationToken, Task<ToolHealthReport>> health)
            : base(identity)
        {
            _health = health ?? throw new ArgumentNullException(nameof(health));
        }

        public int HealthCalls { get; private set; }

        public ProjectContext? LastHealthProject { get; private set; }

        public Task<ToolHealthReport> GetHealthAsync(
            ProjectContext project,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(project);
            cancellationToken.ThrowIfCancellationRequested();
            HealthCalls++;
            LastHealthProject = project;
            return _health(project, cancellationToken);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow.ToUniversalTime();
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed record FixtureDiscoveryResult(bool Available) : ToolDiscoveryResult(Available);

    private sealed record FixtureCapabilitySet : ToolCapabilitySet;
}
