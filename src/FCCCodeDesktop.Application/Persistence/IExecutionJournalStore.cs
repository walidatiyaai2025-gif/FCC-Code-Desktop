using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.Application.Persistence;

public interface IExecutionJournalStore
{
    Task UpsertTaskAsync(PersistedTask task, CancellationToken cancellationToken = default);

    Task<PersistedTask?> GetTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    Task UpsertAgentRunAsync(PersistedAgentRun run, CancellationToken cancellationToken = default);

    Task<PersistedAgentRun?> GetAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken = default);

    Task UpsertToolRunAsync(PersistedToolRun run, CancellationToken cancellationToken = default);

    Task<PersistedToolRun?> GetToolRunAsync(Guid toolRunId, CancellationToken cancellationToken = default);

    Task UpsertProcessRunAsync(PersistedProcessRun run, CancellationToken cancellationToken = default);

    Task<PersistedProcessRun?> GetProcessRunAsync(Guid processRunId, CancellationToken cancellationToken = default);

    Task AppendEventAsync(PersistedTaskEvent taskEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersistedTaskEvent>> ListEventsAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);
}
