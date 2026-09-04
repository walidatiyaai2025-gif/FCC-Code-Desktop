namespace FCCCodeDesktop.Runtime;

public enum AgentRuntimeEventKind
{
    Unknown = 0,
    RuntimeStatus = 1,
    AssistantTextDelta = 2,
    ToolStarted = 3,
    ToolProgress = 4,
    ToolResult = 5,
    SessionIdentified = 6,
    Usage = 7,
    Retry = 8,
    Error = 9,
    Completion = 10
}

/// <summary>
/// Transport-neutral event emitted by an <see cref="IAgentRuntimeExecution"/>.
/// </summary>
/// <remarks>
/// <paramref name="sourceType"/> and <paramref name="payloadJson"/> preserve sanitized upstream
/// information so unknown FCC/Claude event types are not discarded while stable product behavior
/// consumes <paramref name="kind"/>.
/// </remarks>
public sealed record AgentRuntimeEvent
{
    public AgentRuntimeEvent(
        long sequence,
        DateTimeOffset occurredUtc,
        AgentRuntimeEventKind kind,
        string? text = null,
        string? sessionId = null,
        string? correlationId = null,
        string? sourceType = null,
        string? payloadJson = null)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Event sequence must be non-negative.");
        }

        if (kind == AgentRuntimeEventKind.Unknown && string.IsNullOrWhiteSpace(sourceType))
        {
            throw new ArgumentException(
                "Unknown normalized events must retain the upstream source type.",
                nameof(sourceType));
        }

        Sequence = sequence;
        OccurredUtc = occurredUtc.ToUniversalTime();
        Kind = kind;
        Text = text;
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim();
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        SourceType = string.IsNullOrWhiteSpace(sourceType) ? null : sourceType.Trim();
        PayloadJson = payloadJson;
    }

    public long Sequence { get; }

    public DateTimeOffset OccurredUtc { get; }

    public AgentRuntimeEventKind Kind { get; }

    public string? Text { get; }

    public string? SessionId { get; }

    public string? CorrelationId { get; }

    public string? SourceType { get; }

    public string? PayloadJson { get; }
}
