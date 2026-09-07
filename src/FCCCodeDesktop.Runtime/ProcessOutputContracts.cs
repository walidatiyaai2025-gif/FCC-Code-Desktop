namespace FCCCodeDesktop.Runtime;

public enum ProcessOutputSource
{
    StandardOutput = 0,
    StandardError = 1,
}

public enum ProcessOutputStreamState
{
    Active = 0,
    Completed = 1,
    ReadFailed = 2,
}

/// <summary>
/// Correlates process output with durable execution-journal identities when they are available.
/// Process ownership and the root PID are added by the supervisor after launch.
/// </summary>
public sealed class ProcessOutputCorrelation
{
    public ProcessOutputCorrelation(
        Guid? taskId = null,
        Guid? agentRunId = null,
        Guid? toolRunId = null,
        Guid? processRunId = null,
        Guid? operationId = null)
    {
        ValidateOptionalIdentity(taskId, nameof(taskId));
        ValidateOptionalIdentity(agentRunId, nameof(agentRunId));
        ValidateOptionalIdentity(toolRunId, nameof(toolRunId));
        ValidateOptionalIdentity(processRunId, nameof(processRunId));
        ValidateOptionalIdentity(operationId, nameof(operationId));

        TaskId = taskId;
        AgentRunId = agentRunId;
        ToolRunId = toolRunId;
        ProcessRunId = processRunId;
        OperationId = operationId;
    }

    public Guid? TaskId { get; }

    public Guid? AgentRunId { get; }

    public Guid? ToolRunId { get; }

    public Guid? ProcessRunId { get; }

    public Guid? OperationId { get; }

    private static void ValidateOptionalIdentity(Guid? identity, string parameterName)
    {
        if (identity == Guid.Empty)
        {
            throw new ArgumentException("An optional process-output identity cannot be empty.", parameterName);
        }
    }
}

public sealed record ProcessOutputIdentity(
    Guid OwnershipId,
    int RootProcessId,
    Guid? TaskId,
    Guid? AgentRunId,
    Guid? ToolRunId,
    Guid? ProcessRunId,
    Guid? OperationId);

public sealed record ProcessLogEntry(
    ProcessOutputIdentity Identity,
    long Sequence,
    DateTimeOffset TimestampUtc,
    ProcessOutputSource Source,
    string Text,
    int RetainedUtf8Bytes,
    bool IsTruncated,
    long TruncatedCharacters);

public sealed record ProcessOutputStatistics(
    long AcceptedEntries,
    long AcceptedUtf8Bytes,
    int RetainedEntries,
    long RetainedUtf8Bytes,
    long EvictedEntries,
    long EvictedUtf8Bytes,
    long TruncatedEntries,
    long TruncatedCharacters,
    long DroppedDeliveryEntries,
    long DroppedDeliveryUtf8Bytes,
    ProcessOutputStreamState StandardOutputState,
    ProcessOutputStreamState StandardErrorState,
    bool IsCompleted);

public sealed record ProcessOutputSnapshot(
    IReadOnlyList<ProcessLogEntry> Entries,
    ProcessOutputStatistics Statistics);

/// <summary>
/// Bounded output surface for one supervised process. The live stream is a single-consumer,
/// best-effort notification path; <see cref="GetSnapshot"/> is the authoritative bounded history.
/// </summary>
public interface IProcessOutput
{
    ProcessOutputPolicy Policy { get; }

    Task<ProcessOutputStatistics> Completion { get; }

    ProcessOutputSnapshot GetSnapshot();

    IAsyncEnumerable<ProcessLogEntry> ReadEntriesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validated memory and delivery limits for one process-output pipeline.
/// </summary>
public sealed class ProcessOutputPolicy
{
    public const int DefaultMaximumRetainedEntries = 4_096;
    public const int MaximumSupportedRetainedEntries = 100_000;
    public const int DefaultMaximumRetainedUtf8Bytes = 4 * 1024 * 1024;
    public const int MaximumSupportedRetainedUtf8Bytes = 128 * 1024 * 1024;
    public const int DefaultMaximumEntryCharacters = 16 * 1024;
    public const int MaximumSupportedEntryCharacters = 1024 * 1024;
    public const int DefaultMaximumEntryUtf8Bytes = 64 * 1024;
    public const int MaximumSupportedEntryUtf8Bytes = 4 * 1024 * 1024;
    public const int DefaultMaximumPartialLineCharacters = 16 * 1024;
    public const int MaximumSupportedPartialLineCharacters = 1024 * 1024;
    public const int DefaultMaximumPendingDeliveryEntries = 512;
    public const int MaximumSupportedPendingDeliveryEntries = 16 * 1024;
    public const int DefaultReadBufferCharacters = 4 * 1024;
    public const int MaximumSupportedReadBufferCharacters = 64 * 1024;

    public static ProcessOutputPolicy Default { get; } = new();

    public ProcessOutputPolicy(
        int maximumRetainedEntries = DefaultMaximumRetainedEntries,
        int maximumRetainedUtf8Bytes = DefaultMaximumRetainedUtf8Bytes,
        int maximumEntryCharacters = DefaultMaximumEntryCharacters,
        int maximumEntryUtf8Bytes = DefaultMaximumEntryUtf8Bytes,
        int maximumPartialLineCharacters = DefaultMaximumPartialLineCharacters,
        int maximumPendingDeliveryEntries = DefaultMaximumPendingDeliveryEntries,
        int readBufferCharacters = DefaultReadBufferCharacters)
    {
        MaximumRetainedEntries = ValidateRange(
            maximumRetainedEntries,
            MaximumSupportedRetainedEntries,
            nameof(maximumRetainedEntries));
        MaximumRetainedUtf8Bytes = ValidateRange(
            maximumRetainedUtf8Bytes,
            MaximumSupportedRetainedUtf8Bytes,
            nameof(maximumRetainedUtf8Bytes));
        MaximumEntryCharacters = ValidateRange(
            maximumEntryCharacters,
            MaximumSupportedEntryCharacters,
            nameof(maximumEntryCharacters));
        MaximumEntryUtf8Bytes = ValidateRange(
            maximumEntryUtf8Bytes,
            MaximumSupportedEntryUtf8Bytes,
            nameof(maximumEntryUtf8Bytes));
        MaximumPartialLineCharacters = ValidateRange(
            maximumPartialLineCharacters,
            MaximumSupportedPartialLineCharacters,
            nameof(maximumPartialLineCharacters));
        MaximumPendingDeliveryEntries = ValidateRange(
            maximumPendingDeliveryEntries,
            MaximumSupportedPendingDeliveryEntries,
            nameof(maximumPendingDeliveryEntries));
        ReadBufferCharacters = ValidateRange(
            readBufferCharacters,
            MaximumSupportedReadBufferCharacters,
            nameof(readBufferCharacters));

        if (MaximumEntryUtf8Bytes > MaximumRetainedUtf8Bytes)
        {
            throw new ArgumentException(
                "The per-entry UTF-8 byte limit cannot exceed the retained-history byte limit.",
                nameof(maximumEntryUtf8Bytes));
        }

        if (MaximumEntryCharacters > MaximumPartialLineCharacters)
        {
            throw new ArgumentException(
                "The per-entry character limit cannot exceed the partial-line buffer limit.",
                nameof(maximumEntryCharacters));
        }
    }

    public int MaximumRetainedEntries { get; }

    public int MaximumRetainedUtf8Bytes { get; }

    public int MaximumEntryCharacters { get; }

    public int MaximumEntryUtf8Bytes { get; }

    public int MaximumPartialLineCharacters { get; }

    public int MaximumPendingDeliveryEntries { get; }

    public int ReadBufferCharacters { get; }

    private static int ValidateRange(int value, int maximum, string parameterName)
    {
        if (value <= 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The value must be between 1 and {maximum}.");
        }

        return value;
    }
}
