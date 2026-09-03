namespace FCCCodeDesktop.Core.State;

public enum ExecutionJournalCategory
{
    Task,
    Agent,
    Tool,
    Process
}

public sealed record PersistedTask(
    Guid Id,
    Guid SessionId,
    string State,
    string? Summary,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record PersistedAgentRun(
    Guid Id,
    Guid TaskId,
    string RuntimeKind,
    string State,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc);

public sealed record PersistedToolRun(
    Guid Id,
    Guid TaskId,
    Guid? AgentRunId,
    string ToolKind,
    string Operation,
    string State,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc);

public sealed record PersistedProcessRun(
    Guid Id,
    Guid TaskId,
    Guid? AgentRunId,
    Guid? ToolRunId,
    Guid OperationId,
    string Executable,
    string ArgumentsSanitized,
    string WorkingDirectory,
    int? ProcessId,
    string State,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc,
    int? ExitCode);

public sealed record PersistedTaskEvent(
    Guid Id,
    Guid TaskId,
    long Sequence,
    ExecutionJournalCategory Category,
    string EventType,
    Guid? AgentRunId,
    Guid? ToolRunId,
    Guid? ProcessRunId,
    string? DataJson,
    DateTimeOffset OccurredUtc);
