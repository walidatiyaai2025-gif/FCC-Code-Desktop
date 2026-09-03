using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.Application.Persistence;

public interface IQueueStateStore
{
    Task UpsertQueueItemAsync(
        PersistedQueueItem item,
        CancellationToken cancellationToken = default);

    Task<PersistedQueueItem?> GetQueueItemAsync(
        Guid queueItemId,
        CancellationToken cancellationToken = default);

    Task<PersistedQueueItem?> GetQueueItemByTaskIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersistedQueueItem>> ListQueueItemsAsync(
        CancellationToken cancellationToken = default);
}
