namespace FCCCodeDesktop.Fcc;

public enum FccRuntimeAvailabilityState
{
    Unknown = 0,
    Unavailable = 1,
    Available = 2
}

public enum FccRuntimeVersionEvidenceState
{
    Unknown = 0,
    RuntimeMissing = 1,
    TestedBaseline = 2,
    DetectedUntestedVersion = 3,
    UnverifiedVersion = 4
}

/// <summary>
/// Evidence-aware health and version assessment for the discovered local FCC runtime.
/// </summary>
/// <remarks>
/// Loopback health is intentionally retained as a separate signal. A healthy FCC loopback endpoint
/// does not establish provider readiness or successful prompt execution.
/// </remarks>
public sealed record FccRuntimeHealthCompatibilitySnapshot
{
    public FccRuntimeHealthCompatibilitySnapshot(
        FccEnvironmentSnapshot environment,
        FccRuntimeAvailabilityState availability,
        FccRuntimeVersionEvidenceState versionEvidence,
        string testedBaselineVersion,
        string summary)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (availability == FccRuntimeAvailabilityState.Unknown)
        {
            throw new ArgumentException("Runtime availability must be explicit.", nameof(availability));
        }

        if (versionEvidence == FccRuntimeVersionEvidenceState.Unknown)
        {
            throw new ArgumentException("Runtime version evidence must be explicit.", nameof(versionEvidence));
        }

        if (string.IsNullOrWhiteSpace(testedBaselineVersion))
        {
            throw new ArgumentException("Tested baseline version is required.", nameof(testedBaselineVersion));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Compatibility summary is required.", nameof(summary));
        }

        Environment = environment;
        Availability = availability;
        VersionEvidence = versionEvidence;
        TestedBaselineVersion = testedBaselineVersion.Trim();
        Summary = summary.Trim();
    }

    public FccEnvironmentSnapshot Environment { get; }

    public FccRuntimeAvailabilityState Availability { get; }

    public FccRuntimeVersionEvidenceState VersionEvidence { get; }

    public string TestedBaselineVersion { get; }

    public string Summary { get; }

    public bool CanAttemptRuntime => Availability == FccRuntimeAvailabilityState.Available;

    public bool IsLoopbackHealthy => Environment.LoopbackHealth.IsHealthy;

    public bool RequiresCompatibilitySmokeCheck =>
        VersionEvidence is FccRuntimeVersionEvidenceState.DetectedUntestedVersion
            or FccRuntimeVersionEvidenceState.UnverifiedVersion;
}

/// <summary>
/// Combines local FCC discovery with the exact evidence-backed P00 fcc-claude version baseline.
/// </summary>
public sealed class FccRuntimeHealthCompatibilityService
{
    public const string TestedFccClaudeVersionText = "2.1.251";

    private readonly FccEnvironmentDiscoveryService _discoveryService;
    private readonly Version _testedFccClaudeVersion = new(2, 1, 251);

    public FccRuntimeHealthCompatibilityService(FccEnvironmentDiscoveryService discoveryService)
    {
        ArgumentNullException.ThrowIfNull(discoveryService);
        _discoveryService = discoveryService;
    }

    public async Task<FccRuntimeHealthCompatibilitySnapshot> InspectAsync(
        CancellationToken cancellationToken = default)
    {
        var environment = await _discoveryService.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        return Evaluate(environment);
    }

    public FccRuntimeHealthCompatibilitySnapshot Evaluate(FccEnvironmentSnapshot environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var availability = environment.FccClaude.IsFound
            ? FccRuntimeAvailabilityState.Available
            : FccRuntimeAvailabilityState.Unavailable;
        var versionEvidence = ClassifyVersion(environment.FccClaude);
        var summary = BuildSummary(environment, versionEvidence);

        return new FccRuntimeHealthCompatibilitySnapshot(
            environment,
            availability,
            versionEvidence,
            TestedFccClaudeVersionText,
            summary);
    }

    private FccRuntimeVersionEvidenceState ClassifyVersion(FccExecutableDiscovery discovery)
    {
        if (!discovery.IsFound)
        {
            return FccRuntimeVersionEvidenceState.RuntimeMissing;
        }

        if (discovery.ParsedVersion is null)
        {
            return FccRuntimeVersionEvidenceState.UnverifiedVersion;
        }

        return discovery.ParsedVersion.Equals(_testedFccClaudeVersion)
            ? FccRuntimeVersionEvidenceState.TestedBaseline
            : FccRuntimeVersionEvidenceState.DetectedUntestedVersion;
    }

    private static string BuildSummary(
        FccEnvironmentSnapshot environment,
        FccRuntimeVersionEvidenceState versionEvidence)
    {
        var loopback = environment.LoopbackHealth.State switch
        {
            FccLoopbackHealthState.Healthy => "FCC loopback health is healthy",
            FccLoopbackHealthState.Unhealthy => "FCC loopback health is unhealthy",
            FccLoopbackHealthState.Unreachable => "FCC loopback health is unreachable",
            _ => "FCC loopback health is unknown"
        };

        return versionEvidence switch
        {
            FccRuntimeVersionEvidenceState.RuntimeMissing =>
                $"fcc-claude is unavailable; runtime launch cannot be attempted. {loopback}. Provider readiness is not established.",
            FccRuntimeVersionEvidenceState.TestedBaseline =>
                $"fcc-claude {TestedFccClaudeVersionText} matches the exact tested P00 baseline. {loopback}. Provider readiness is not implied by loopback health.",
            FccRuntimeVersionEvidenceState.DetectedUntestedVersion =>
                $"fcc-claude {environment.FccClaude.ParsedVersion} is detected but differs from the exact tested P00 baseline {TestedFccClaudeVersionText}; compatibility smoke validation is required. {loopback}. Provider readiness is not established.",
            FccRuntimeVersionEvidenceState.UnverifiedVersion =>
                $"fcc-claude is detected but its version is unverified against the exact tested P00 baseline {TestedFccClaudeVersionText}; compatibility smoke validation is required. {loopback}. Provider readiness is not established.",
            _ => throw new InvalidOperationException("Runtime version evidence was not classified.")
        };
    }
}
