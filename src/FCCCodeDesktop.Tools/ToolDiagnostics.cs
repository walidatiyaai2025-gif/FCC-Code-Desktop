using System.Collections.ObjectModel;

namespace FCCCodeDesktop.Tools;

/// <summary>
/// Provider-neutral health state for one registered external tool.
/// </summary>
public enum ToolHealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unavailable = 3,
    Unhealthy = 4,
}

/// <summary>
/// Severity attached to a structured external-tool diagnostic.
/// </summary>
public enum ToolDiagnosticSeverity
{
    Unknown = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>
/// Stable structured diagnostic intended for orchestration and later UI presentation.
/// </summary>
public sealed record ToolDiagnostic
{
    public ToolDiagnostic(
        string code,
        ToolDiagnosticSeverity severity,
        string summary,
        string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        if (!string.Equals(code, code.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Tool diagnostic code must not contain leading or trailing whitespace.",
                nameof(code));
        }

        if (!IsValidCode(code))
        {
            throw new ArgumentException(
                "Tool diagnostic code may contain only ASCII letters, digits, '.', '_' and '-'.",
                nameof(code));
        }

        if (!Enum.IsDefined(severity) || severity == ToolDiagnosticSeverity.Unknown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(severity),
                severity,
                "A concrete tool diagnostic severity is required.");
        }

        if (!string.Equals(summary, summary.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Tool diagnostic summary must not contain leading or trailing whitespace.",
                nameof(summary));
        }

        if (detail is not null)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                throw new ArgumentException(
                    "Tool diagnostic detail must contain non-whitespace text when supplied.",
                    nameof(detail));
            }

            if (!string.Equals(detail, detail.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Tool diagnostic detail must not contain leading or trailing whitespace.",
                    nameof(detail));
            }
        }

        Code = code;
        Severity = severity;
        Summary = summary;
        Detail = detail;
    }

    public string Code { get; }

    public ToolDiagnosticSeverity Severity { get; }

    public string Summary { get; }

    public string? Detail { get; }

    private static bool IsValidCode(string code)
    {
        foreach (var character in code)
        {
            var valid = character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '.'
                or '_'
                or '-';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Immutable health snapshot returned through the external-tool gateway.
/// </summary>
public sealed record ToolHealthReport
{
    private readonly ReadOnlyCollection<ToolDiagnostic> _diagnostics;

    public ToolHealthReport(
        ToolIdentity identity,
        ToolHealthStatus status,
        DateTimeOffset observedAt,
        IEnumerable<ToolDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (!Enum.IsDefined(status) || status == ToolHealthStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "A concrete tool health status is required.");
        }

        var diagnosticSnapshot = new List<ToolDiagnostic>();
        foreach (var diagnostic in diagnostics ?? Array.Empty<ToolDiagnostic>())
        {
            diagnosticSnapshot.Add(
                diagnostic ?? throw new ArgumentException(
                    "Tool health diagnostics cannot contain null entries.",
                    nameof(diagnostics)));
        }

        Identity = identity;
        Status = status;
        ObservedAt = observedAt.ToUniversalTime();
        _diagnostics = Array.AsReadOnly(diagnosticSnapshot.ToArray());
    }

    public ToolIdentity Identity { get; }

    public ToolHealthStatus Status { get; }

    public DateTimeOffset ObservedAt { get; }

    public IReadOnlyList<ToolDiagnostic> Diagnostics => _diagnostics;
}

/// <summary>
/// Standard event for exposing a structured health snapshot through the existing tool-event seam.
/// </summary>
public sealed record ToolHealthReportEvent : ToolEvent
{
    public ToolHealthReportEvent(ToolHealthReport report)
    {
        Report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public ToolHealthReport Report { get; }
}

/// <summary>
/// Optional adapter-owned health probe for tools that can provide richer diagnostics than discovery alone.
/// </summary>
public interface IExternalToolHealthProvider
{
    Task<ToolHealthReport> GetHealthAsync(
        ProjectContext project,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Project-owned orchestration surface for targeted and catalog-wide external-tool health checks.
/// </summary>
public interface IExternalToolHealthService
{
    Task<ToolHealthReport> CheckAsync(
        string toolId,
        ProjectContext project,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ToolHealthReport>> CheckAllAsync(
        ProjectContext project,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Routes health checks through the registered adapter catalog without coupling callers to tool-specific probes.
/// </summary>
public sealed class ExternalToolHealthService : IExternalToolHealthService
{
    private readonly IExternalToolRegistry _registry;
    private readonly TimeProvider _timeProvider;

    public ExternalToolHealthService(
        IExternalToolRegistry registry,
        TimeProvider? timeProvider = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ToolHealthReport> CheckAsync(
        string toolId,
        ProjectContext project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        var adapter = _registry.GetRequiredAdapter(toolId);
        return await CheckAdapterAsync(adapter, project, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ToolHealthReport>> CheckAllAsync(
        ProjectContext project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        var reports = new List<ToolHealthReport>(_registry.RegisteredTools.Count);
        foreach (var identity in _registry.RegisteredTools)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var adapter = _registry.GetRequiredAdapter(identity.Id);
            reports.Add(await CheckAdapterAsync(adapter, project, cancellationToken).ConfigureAwait(false));
        }

        return new ReadOnlyCollection<ToolHealthReport>(reports);
    }

    private async Task<ToolHealthReport> CheckAdapterAsync(
        IExternalToolAdapter adapter,
        ProjectContext project,
        CancellationToken cancellationToken)
    {
        var identity = adapter.Identity
            ?? throw new InvalidOperationException("An external tool adapter returned a null identity.");

        if (adapter is IExternalToolHealthProvider healthProvider)
        {
            try
            {
                var healthTask = healthProvider.GetHealthAsync(project, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"External tool health provider '{identity.Id}' returned a null health task.");
                var report = await healthTask.ConfigureAwait(false);
                if (report is null)
                {
                    throw new InvalidOperationException(
                        $"External tool health provider '{identity.Id}' returned a null health report.");
                }

                if (!Equals(report.Identity, identity))
                {
                    return CreateFrameworkFailure(
                        identity,
                        "tool.health.identity_mismatch",
                        "The tool health provider returned a report for a different adapter identity.");
                }

                return report;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return CreateFrameworkFailure(
                    identity,
                    "tool.health.probe_failed",
                    $"The tool health probe failed with {exception.GetType().Name}.");
            }
        }

        try
        {
            var discoveryTask = adapter.DiscoverAsync(project, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"External tool adapter '{identity.Id}' returned a null discovery task.");
            var discovery = await discoveryTask.ConfigureAwait(false);
            if (discovery is null)
            {
                throw new InvalidOperationException(
                    $"External tool adapter '{identity.Id}' returned a null discovery result.");
            }

            if (!discovery.IsAvailable)
            {
                return new ToolHealthReport(
                    identity,
                    ToolHealthStatus.Unavailable,
                    _timeProvider.GetUtcNow(),
                    new[]
                    {
                        new ToolDiagnostic(
                            "tool.discovery.unavailable",
                            ToolDiagnosticSeverity.Warning,
                            "The tool is not available for this project context."),
                    });
            }

            return new ToolHealthReport(
                identity,
                ToolHealthStatus.Degraded,
                _timeProvider.GetUtcNow(),
                new[]
                {
                    new ToolDiagnostic(
                        "tool.health.specialized_probe_unavailable",
                        ToolDiagnosticSeverity.Warning,
                        "The tool is discoverable but does not expose a specialized health probe."),
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CreateFrameworkFailure(
                identity,
                "tool.discovery.probe_failed",
                $"The tool discovery fallback failed with {exception.GetType().Name}.");
        }
    }

    private ToolHealthReport CreateFrameworkFailure(
        ToolIdentity identity,
        string code,
        string summary)
    {
        return new ToolHealthReport(
            identity,
            ToolHealthStatus.Unhealthy,
            _timeProvider.GetUtcNow(),
            new[]
            {
                new ToolDiagnostic(code, ToolDiagnosticSeverity.Error, summary),
            });
    }
}
