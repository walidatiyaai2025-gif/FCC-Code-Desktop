namespace FCCCodeDesktop.Fcc;

public enum FccLoopbackHealthState
{
    Healthy,
    Unhealthy,
    Unreachable
}

public sealed record FccExecutableDiscovery(
    string LogicalName,
    string? ExecutablePath,
    string? VersionText,
    Version? ParsedVersion,
    string? ProbeFailure)
{
    public bool IsFound => !string.IsNullOrWhiteSpace(ExecutablePath);

    public bool IsVersionKnown => ParsedVersion is not null;
}

public sealed record FccLoopbackHealth(
    Uri Endpoint,
    FccLoopbackHealthState State,
    int? HttpStatusCode,
    string? Failure)
{
    public bool IsHealthy => State == FccLoopbackHealthState.Healthy;
}

public sealed record FccEnvironmentSnapshot(
    FccExecutableDiscovery FccClaude,
    FccExecutableDiscovery FccServer,
    FccLoopbackHealth LoopbackHealth)
{
    public bool IsFccClaudeAvailable => FccClaude.IsFound;
}
