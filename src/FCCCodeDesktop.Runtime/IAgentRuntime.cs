namespace FCCCodeDesktop.Runtime;

/// <summary>
/// Project-owned boundary for executing one coding-agent run without exposing transport-specific FCC details.
/// </summary>
public interface IAgentRuntime
{
    AgentRuntimeDescriptor Descriptor { get; }

    Task<IAgentRuntimeExecution> StartAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents one started runtime execution. Consumers stream normalized events and await one terminal result.
/// </summary>
public interface IAgentRuntimeExecution : IAsyncDisposable
{
    Guid TaskId { get; }

    Guid RunId { get; }

    IAsyncEnumerable<AgentRuntimeEvent> Events { get; }

    Task<AgentRuntimeResult> Completion { get; }

    ValueTask CancelAsync(CancellationToken cancellationToken = default);
}
