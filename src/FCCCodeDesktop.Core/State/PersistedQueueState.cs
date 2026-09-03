namespace FCCCodeDesktop.Core.State;

public sealed record PersistedQueueItem(
    Guid Id,
    Guid TaskId,
    long OrderKey,
    string State,
    DateTimeOffset EnqueuedUtc,
    DateTimeOffset UpdatedUtc);
