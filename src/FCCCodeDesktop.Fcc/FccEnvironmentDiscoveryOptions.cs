namespace FCCCodeDesktop.Fcc;

public sealed class FccEnvironmentDiscoveryOptions
{
    public string? FccClaudeExecutablePath { get; init; }

    public string? FccServerExecutablePath { get; init; }

    public string? PathValue { get; init; }

    public string? PathExtensions { get; init; }

    public int? FccServerPort { get; init; }

    public Uri? HealthUri { get; init; }

    public TimeSpan ProcessTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan HealthTimeout { get; init; } = TimeSpan.FromSeconds(3);
}
