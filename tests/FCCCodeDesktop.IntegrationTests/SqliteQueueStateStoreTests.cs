using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class SqliteQueueStateStoreTests
{
    [Fact]
    public async Task QueueItemsSurviveStoreRecreationWithDeterministicOrdering()
    {
        using var workspace = new TemporaryDirectory("fccd p03 queue مساحة");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 0, 10, 0, TimeSpan.Zero);
        var sessionId = await CreateSessionAsync(options, workspace, createdUtc);
        var firstTaskId = Guid.NewGuid();
        var secondTaskId = Guid.NewGuid();
        var thirdTaskId = Guid.NewGuid();
        await CreateTaskAsync(options, firstTaskId, sessionId, createdUtc);
        await CreateTaskAsync(options, secondTaskId, sessionId, createdUtc.AddSeconds(1));
        await CreateTaskAsync(options, thirdTaskId, sessionId, createdUtc.AddSeconds(2));

        var firstItemId = Guid.NewGuid();
        var secondItemId = Guid.NewGuid();
        var thirdItemId = Guid.NewGuid();
        var writer = new SqliteQueueStateStore(options);
        await writer.UpsertQueueItemAsync(
            new PersistedQueueItem(
                firstItemId,
                firstTaskId,
                20,
                "QUEUED",
                createdUtc.AddMinutes(2),
                createdUtc.AddMinutes(2)),
            CancellationToken.None);
        await writer.UpsertQueueItemAsync(
            new PersistedQueueItem(
                secondItemId,
                secondTaskId,
                10,
                "QUEUED",
                createdUtc,
                createdUtc),
            CancellationToken.None);
        await writer.UpsertQueueItemAsync(
            new PersistedQueueItem(
                thirdItemId,
                thirdTaskId,
                10,
                "PAUSED",
                createdUtc.AddMinutes(1),
                createdUtc.AddMinutes(1)),
            CancellationToken.None);

        var reader = new SqliteQueueStateStore(options);
        var items = await reader.ListQueueItemsAsync(CancellationToken.None);
        var byId = await reader.GetQueueItemAsync(firstItemId, CancellationToken.None);
        var byTask = await reader.GetQueueItemByTaskIdAsync(thirdTaskId, CancellationToken.None);

        Assert.Equal(3, items.Count);
        Assert.Equal(secondItemId, items[0].Id);
        Assert.Equal(thirdItemId, items[1].Id);
        Assert.Equal(firstItemId, items[2].Id);
        Assert.Equal(10, items[0].OrderKey);
        Assert.Equal(10, items[1].OrderKey);
        Assert.Equal(20, items[2].OrderKey);
        Assert.Equal("PAUSED", items[1].State);

        Assert.NotNull(byId);
        Assert.Equal(firstTaskId, byId.TaskId);
        Assert.Equal(createdUtc.AddMinutes(2), byId.EnqueuedUtc);

        Assert.NotNull(byTask);
        Assert.Equal(thirdItemId, byTask.Id);
    }

    [Fact]
    public async Task UpsertPreservesQueueIdentityWhileUpdatingOrderAndState()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-queue-upsert");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 0, 30, 0, TimeSpan.Zero);
        var sessionId = await CreateSessionAsync(options, workspace, createdUtc);
        var taskId = Guid.NewGuid();
        await CreateTaskAsync(options, taskId, sessionId, createdUtc);

        var queueItemId = Guid.NewGuid();
        var store = new SqliteQueueStateStore(options);
        await store.UpsertQueueItemAsync(
            new PersistedQueueItem(queueItemId, taskId, 100, "QUEUED", createdUtc, createdUtc),
            CancellationToken.None);
        await store.UpsertQueueItemAsync(
            new PersistedQueueItem(
                queueItemId,
                taskId,
                25,
                "READY",
                createdUtc,
                createdUtc.AddMinutes(5)),
            CancellationToken.None);

        var persisted = await store.GetQueueItemAsync(queueItemId, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal(taskId, persisted.TaskId);
        Assert.Equal(25, persisted.OrderKey);
        Assert.Equal("READY", persisted.State);
        Assert.Equal(createdUtc, persisted.EnqueuedUtc);
        Assert.Equal(createdUtc.AddMinutes(5), persisted.UpdatedUtc);
    }

    [Fact]
    public async Task QueueItemCannotChangeTaskIdentityOrOriginalEnqueueTimestamp()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-queue-immutable");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 0, 45, 0, TimeSpan.Zero);
        var sessionId = await CreateSessionAsync(options, workspace, createdUtc);
        var firstTaskId = Guid.NewGuid();
        var secondTaskId = Guid.NewGuid();
        await CreateTaskAsync(options, firstTaskId, sessionId, createdUtc);
        await CreateTaskAsync(options, secondTaskId, sessionId, createdUtc.AddSeconds(1));

        var queueItemId = Guid.NewGuid();
        var store = new SqliteQueueStateStore(options);
        await store.UpsertQueueItemAsync(
            new PersistedQueueItem(queueItemId, firstTaskId, 1, "QUEUED", createdUtc, createdUtc),
            CancellationToken.None);

        var taskChangeFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpsertQueueItemAsync(
                new PersistedQueueItem(
                    queueItemId,
                    secondTaskId,
                    2,
                    "QUEUED",
                    createdUtc,
                    createdUtc.AddMinutes(1)),
                CancellationToken.None));
        Assert.Contains("cannot change", taskChangeFailure.Message, StringComparison.OrdinalIgnoreCase);

        var timeChangeFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpsertQueueItemAsync(
                new PersistedQueueItem(
                    queueItemId,
                    firstTaskId,
                    2,
                    "QUEUED",
                    createdUtc.AddSeconds(1),
                    createdUtc.AddMinutes(1)),
                CancellationToken.None));
        Assert.Contains("cannot change", timeChangeFailure.Message, StringComparison.OrdinalIgnoreCase);

        var persisted = await store.GetQueueItemAsync(queueItemId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(firstTaskId, persisted.TaskId);
        Assert.Equal(1, persisted.OrderKey);
        Assert.Equal(createdUtc, persisted.EnqueuedUtc);
        Assert.Equal(createdUtc, persisted.UpdatedUtc);
    }

    [Fact]
    public async Task DuplicateTaskAndOrphanTaskAreRejectedWithoutCorruptingQueue()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-queue-constraints");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 1, 0, 0, TimeSpan.Zero);
        var sessionId = await CreateSessionAsync(options, workspace, createdUtc);
        var taskId = Guid.NewGuid();
        await CreateTaskAsync(options, taskId, sessionId, createdUtc);

        var existingItemId = Guid.NewGuid();
        var store = new SqliteQueueStateStore(options);
        await store.UpsertQueueItemAsync(
            new PersistedQueueItem(existingItemId, taskId, 5, "QUEUED", createdUtc, createdUtc),
            CancellationToken.None);

        var duplicateFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpsertQueueItemAsync(
                new PersistedQueueItem(
                    Guid.NewGuid(),
                    taskId,
                    6,
                    "QUEUED",
                    createdUtc.AddSeconds(1),
                    createdUtc.AddSeconds(1)),
                CancellationToken.None));
        Assert.Contains("persist queue item", duplicateFailure.Message, StringComparison.OrdinalIgnoreCase);

        var orphanFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpsertQueueItemAsync(
                new PersistedQueueItem(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    7,
                    "QUEUED",
                    createdUtc.AddSeconds(2),
                    createdUtc.AddSeconds(2)),
                CancellationToken.None));
        Assert.Contains("persist queue item", orphanFailure.Message, StringComparison.OrdinalIgnoreCase);

        var items = await store.ListQueueItemsAsync(CancellationToken.None);
        Assert.Single(items);
        Assert.Equal(existingItemId, items[0].Id);
    }

    [Fact]
    public async Task InvalidQueueValuesAreRejectedBeforePersistence()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-queue-validation");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 1, 15, 0, TimeSpan.Zero);
        var sessionId = await CreateSessionAsync(options, workspace, createdUtc);
        var taskId = Guid.NewGuid();
        await CreateTaskAsync(options, taskId, sessionId, createdUtc);
        var store = new SqliteQueueStateStore(options);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.UpsertQueueItemAsync(
                new PersistedQueueItem(Guid.NewGuid(), taskId, -1, "QUEUED", createdUtc, createdUtc),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.UpsertQueueItemAsync(
                new PersistedQueueItem(Guid.NewGuid(), taskId, 1, "   ", createdUtc, createdUtc),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.UpsertQueueItemAsync(
                new PersistedQueueItem(
                    Guid.NewGuid(),
                    taskId,
                    1,
                    "QUEUED",
                    createdUtc,
                    createdUtc.AddSeconds(-1)),
                CancellationToken.None));

        Assert.Empty(await store.ListQueueItemsAsync(CancellationToken.None));
    }

    private static async Task<Guid> CreateSessionAsync(
        SqliteDatabaseOptions options,
        TemporaryDirectory workspace,
        DateTimeOffset createdUtc)
    {
        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var conversationStore = new SqliteConversationStateStore(options);
        await conversationStore.UpsertProjectAsync(
            new PersistedProject(
                projectId,
                workspace.GetPath($"project-{projectId:D}"),
                "Queue fixture",
                createdUtc,
                createdUtc),
            CancellationToken.None);
        await conversationStore.UpsertSessionAsync(
            new PersistedSession(
                sessionId,
                projectId,
                "queue-runtime-session",
                "Queue session",
                createdUtc,
                createdUtc),
            CancellationToken.None);
        return sessionId;
    }

    private static async Task CreateTaskAsync(
        SqliteDatabaseOptions options,
        Guid taskId,
        Guid sessionId,
        DateTimeOffset createdUtc)
    {
        await new SqliteExecutionJournalStore(options).UpsertTaskAsync(
            new PersistedTask(taskId, sessionId, "PENDING", "Queue fixture task", createdUtc, createdUtc),
            CancellationToken.None);
    }
}
