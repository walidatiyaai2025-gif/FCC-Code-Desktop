using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.Application.Persistence;

public interface IConversationStateStore
{
    Task UpsertProjectAsync(PersistedProject project, CancellationToken cancellationToken = default);

    Task<PersistedProject?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task UpsertSessionAsync(PersistedSession session, CancellationToken cancellationToken = default);

    Task<PersistedSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersistedSession>> ListSessionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task AppendMessageAsync(PersistedMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersistedMessage>> ListMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
