using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class SqliteExecutionJournalStoreTests
{
    [Fact]
    public async Task TaskRunsAndEventsSurviveStoreRecreationWithDeterministicOrdering()
    {
        using var workspace = new TemporaryDirectory("fccd p03 journal مساحة");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var sessionId = Guid.NewGuid();
        var createdUtc = new DateTimeOffset(2026, 9, 3, 20, 40, 0, TimeSpan.Zero);
        await CreateSessionAsync(options, workspace, sessionId, createdUtc);

        var taskId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var toolRunId = Guid.NewGuid();
        var processRunId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var workingDirectory = workspace.GetPath("مشروع source");
        Directory.CreateDirectory(workingDirectory);

        var writer = new SqliteExecutionJournalStore(options);
        await writer.UpsertTaskAsync(
            new PersistedTask(taskId, sessionId, "RUNNING", "تحقق البناء", createdUtc, createdUtc),
            CancellationToken.None);
        await writer.UpsertAgentRunAsync(
            new PersistedAgentRun(
                agentRunId,
                taskId,
                "fcc-claude-structured",
                "RUNNING",
                createdUtc.AddSeconds(1),
                null),
            CancellationToken.None);
        await writer.UpsertToolRunAsync(
            new PersistedToolRun(
                toolRunId,
                taskId,
                agentRunId,
                "build",
                "dotnet-test",
                "RUNNING",
                createdUtc.AddSeconds(2),
                null),
            CancellationToken.None);
        await writer.UpsertProcessRunAsync(
            new PersistedProcessRun(
                processRunId,
                taskId,
                agentRunId,
                toolRunId,
                operationId,
                "dotnet",
                "test --configuration Release",
                workingDirectory,
                4812,
                "RUNNING",
                createdUtc.AddSeconds(3),
                null,
                null),
            CancellationToken.None);

        await writer.AppendEventAsync(
            new PersistedTaskEvent(
                Guid.NewGuid(),
                taskId,
                1,
                ExecutionJournalCategory.Process,
                "process.started",
                agentRunId,
                toolRunId,
                processRunId,
                "{\"stream\":\"stdout\",\"note\":\"بدأ\"}",
                createdUtc.AddSeconds(5)),
            CancellationToken.None);
        await writer.AppendEventAsync(
            new PersistedTaskEvent(
                Guid.NewGuid(),
                taskId,
                0,
                ExecutionJournalCategory.Task,
                "task.started",
                null,
                null,
                null,
                "{\"source\":\"user\"}",
                createdUtc.AddSeconds(4)),
            CancellationToken.None);

        var reader = new SqliteExecutionJournalStore(options);
        var persistedTask = await reader.GetTaskAsync(taskId, CancellationToken.None);
        var persistedAgentRun = await reader.GetAgentRunAsync(agentRunId, CancellationToken.None);
        var persistedToolRun = await reader.GetToolRunAsync(toolRunId, CancellationToken.None);
        var persistedProcessRun = await reader.GetProcessRunAsync(processRunId, CancellationToken.None);
        var events = await reader.ListEventsAsync(taskId, CancellationToken.None);

        Assert.NotNull(persistedTask);
        Assert.Equal(sessionId, persistedTask.SessionId);
        Assert.Equal("RUNNING", persistedTask.State);
        Assert.Equal("تحقق البناء", persistedTask.Summary);
        Assert.Equal(createdUtc, persistedTask.CreatedUtc);
        Assert.Equal(createdUtc.AddSeconds(5), persistedTask.UpdatedUtc);

        Assert.NotNull(persistedAgentRun);
        Assert.Equal(taskId, persistedAgentRun.TaskId);
        Assert.Equal("fcc-claude-structured", persistedAgentRun.RuntimeKind);
        Assert.Equal("RUNNING", persistedAgentRun.State);
        Assert.Null(persistedAgentRun.CompletedUtc);

        Assert.NotNull(persistedToolRun);
        Assert.Equal(agentRunId, persistedToolRun.AgentRunId);
        Assert.Equal("build", persistedToolRun.ToolKind);
        Assert.Equal("dotnet-test", persistedToolRun.Operation);

        Assert.NotNull(persistedProcessRun);
        Assert.Equal(operationId, persistedProcessRun.OperationId);
        Assert.Equal("dotnet", persistedProcessRun.Executable);
        Assert.Equal("test --configuration Release", persistedProcessRun.ArgumentsSanitized);
        Assert.Equal(Path.GetFullPath(workingDirectory), persistedProcessRun.WorkingDirectory);
        Assert.Equal(4812, persistedProcessRun.ProcessId);
        Assert.Null(persistedProcessRun.ExitCode);

        Assert.Equal(2, events.Count);
        Assert.Equal(0, events[0].Sequence);
        Assert.Equal(ExecutionJournalCategory.Task, events[0].Category);
        Assert.Equal("task.started", events[0].EventType);
        Assert.Equal("{\"source\":\"user\"}", events[0].DataJson);
        Assert.Equal(1, events[1].Sequence);
        Assert.Equal(ExecutionJournalCategory.Process, events[1].Category);
        Assert.Equal(processRunId, events[1].ProcessRunId);
        Assert.Equal("{\"stream\":\"stdout\",\"note\":\"بدأ\"}", events[1].DataJson);
    }

    [Fact]
    public async Task UpsertsPreserveRunIdentityAndOriginalTimestampsWhileUpdatingLifecycleState()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-journal-upsert");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var sessionId = Guid.NewGuid();
        var createdUtc = new DateTimeOffset(2026, 9, 3, 21, 0, 0, TimeSpan.Zero);
        await CreateSessionAsync(options, workspace, sessionId, createdUtc);

        var taskId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var toolRunId = Guid.NewGuid();
        var processRunId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var workingDirectory = workspace.GetPath("work");
        Directory.CreateDirectory(workingDirectory);
        var store = new SqliteExecutionJournalStore(options);

        await store.UpsertTaskAsync(
            new PersistedTask(taskId, sessionId, "STARTING", "initial", createdUtc, createdUtc),
            CancellationToken.None);
        await store.UpsertAgentRunAsync(
            new PersistedAgentRun(agentRunId, taskId, "structured", "STARTING", createdUtc, null),
            CancellationToken.None);
        await store.UpsertToolRunAsync(
            new PersistedToolRun(toolRunId, taskId, agentRunId, "build", "compile", "STARTING", createdUtc, null),
            CancellationToken.None);
        await store.UpsertProcessRunAsync(
            new PersistedProcessRun(
                processRunId,
                taskId,
                agentRunId,
                toolRunId,
                operationId,
                "dotnet",
                "build",
                workingDirectory,
                null,
                "STARTING",
                createdUtc,
                null,
                null),
            CancellationToken.None);

        await store.UpsertTaskAsync(
            new PersistedTask(taskId, sessionId, "COMPLETED", "done", createdUtc.AddHours(1), createdUtc.AddHours(2)),
            CancellationToken.None);
        await store.UpsertAgentRunAsync(
            new PersistedAgentRun(agentRunId, taskId, "changed-runtime", "COMPLETED", createdUtc.AddHours(1), createdUtc.AddMinutes(30)),
            CancellationToken.None);
        await store.UpsertToolRunAsync(
            new PersistedToolRun(toolRunId, taskId, agentRunId, "changed-tool", "changed-operation", "COMPLETED", createdUtc.AddHours(1), createdUtc.AddMinutes(31)),
            CancellationToken.None);
        await store.UpsertProcessRunAsync(
            new PersistedProcessRun(
                processRunId,
                taskId,
                agentRunId,
                toolRunId,
                operationId,
                "different-executable",
                "different arguments",
                workspace.GetPath("other"),
                9912,
                "COMPLETED",
                createdUtc.AddHours(1),
                createdUtc.AddMinutes(32),
                0),
            CancellationToken.None);

        var task = await store.GetTaskAsync(taskId, CancellationToken.None);
        var agent = await store.GetAgentRunAsync(agentRunId, CancellationToken.None);
        var tool = await store.GetToolRunAsync(toolRunId, CancellationToken.None);
        var process = await store.GetProcessRunAsync(processRunId, CancellationToken.None);

        Assert.NotNull(task);
        Assert.Equal(createdUtc, task.CreatedUtc);
        Assert.Equal(createdUtc.AddHours(2), task.UpdatedUtc);
        Assert.Equal("COMPLETED", task.State);
        Assert.Equal("done", task.Summary);

        Assert.NotNull(agent);
        Assert.Equal("structured", agent.RuntimeKind);
        Assert.Equal(createdUtc, agent.StartedUtc);
        Assert.Equal(createdUtc.AddMinutes(30), agent.CompletedUtc);
        Assert.Equal("COMPLETED", agent.State);

        Assert.NotNull(tool);
        Assert.Equal("build", tool.ToolKind);
        Assert.Equal("compile", tool.Operation);
        Assert.Equal(createdUtc, tool.StartedUtc);
        Assert.Equal(createdUtc.AddMinutes(31), tool.CompletedUtc);
        Assert.Equal("COMPLETED", tool.State);

        Assert.NotNull(process);
        Assert.Equal(operationId, process.OperationId);
        Assert.Equal("dotnet", process.Executable);
        Assert.Equal("build", process.ArgumentsSanitized);
        Assert.Equal(Path.GetFullPath(workingDirectory), process.WorkingDirectory);
        Assert.Equal(createdUtc, process.StartedUtc);
        Assert.Equal(createdUtc.AddMinutes(32), process.CompletedUtc);
        Assert.Equal(9912, process.ProcessId);
        Assert.Equal(0, process.ExitCode);
        Assert.Equal("COMPLETED", process.State);
    }

    [Fact]
    public async Task DuplicateEventSequenceRollsBackWithoutAdvancingTaskTimestamp()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-journal-duplicate");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var sessionId = Guid.NewGuid();
        var createdUtc = new DateTimeOffset(2026, 9, 3, 21, 20, 0, TimeSpan.Zero);
        await CreateSessionAsync(options, workspace, sessionId, createdUtc);
        var taskId = Guid.NewGuid();
        var store = new SqliteExecutionJournalStore(options);
        await store.UpsertTaskAsync(
            new PersistedTask(taskId, sessionId, "RUNNING", null, createdUtc, createdUtc),
            CancellationToken.None);

        await store.AppendEventAsync(
            new PersistedTaskEvent(
                Guid.NewGuid(),
                taskId,
                0,
                ExecutionJournalCategory.Task,
                "task.running",
                null,
                null,
                null,
                null,
                createdUtc.AddMinutes(1)),
            CancellationToken.None);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendEventAsync(
                new PersistedTaskEvent(
                    Guid.NewGuid(),
                    taskId,
                    0,
                    ExecutionJournalCategory.Task,
                    "task.duplicate",
                    null,
                    null,
                    null,
                    null,
                    createdUtc.AddMinutes(2)),
                CancellationToken.None));

        Assert.Contains("append execution event", failure.Message, StringComparison.OrdinalIgnoreCase);
        var events = await store.ListEventsAsync(taskId, CancellationToken.None);
        var task = await store.GetTaskAsync(taskId, CancellationToken.None);
        Assert.Single(events);
        Assert.NotNull(task);
        Assert.Equal(createdUtc.AddMinutes(1), task.UpdatedUtc);
    }

    [Fact]
    public async Task TaskScopedForeignKeysRejectCrossTaskRunAndEventCorrelation()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-journal-scope");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var sessionId = Guid.NewGuid();
        var createdUtc = new DateTimeOffset(2026, 9, 3, 21, 40, 0, TimeSpan.Zero);
        await CreateSessionAsync(options, workspace, sessionId, createdUtc);
        var firstTaskId = Guid.NewGuid();
        var secondTaskId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var store = new SqliteExecutionJournalStore(options);

        await store.UpsertTaskAsync(
            new PersistedTask(firstTaskId, sessionId, "RUNNING", null, createdUtc, createdUtc),
            CancellationToken.None);
        await store.UpsertTaskAsync(
            new PersistedTask(secondTaskId, sessionId, "RUNNING", null, createdUtc, createdUtc),
            CancellationToken.None);
        await store.UpsertAgentRunAsync(
            new PersistedAgentRun(agentRunId, firstTaskId, "structured", "RUNNING", createdUtc, null),
            CancellationToken.None);

        var toolFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpsertToolRunAsync(
                new PersistedToolRun(
                    Guid.NewGuid(),
                    secondTaskId,
                    agentRunId,
                    "build",
                    "compile",
                    "RUNNING",
                    createdUtc,
                    null),
                CancellationToken.None));
        Assert.Contains("persist tool run", toolFailure.Message, StringComparison.OrdinalIgnoreCase);

        var eventFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendEventAsync(
                new PersistedTaskEvent(
                    Guid.NewGuid(),
                    secondTaskId,
                    0,
                    ExecutionJournalCategory.Agent,
                    "agent.started",
                    agentRunId,
                    null,
                    null,
                    null,
                    createdUtc.AddSeconds(1)),
                CancellationToken.None));
        Assert.Contains("append execution event", eventFailure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await store.ListEventsAsync(secondTaskId, CancellationToken.None));
    }

    [Fact]
    public async Task MalformedJsonAndInvalidProcessIdentityAreRejectedBeforePersistence()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-journal-validation");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var sessionId = Guid.NewGuid();
        var createdUtc = new DateTimeOffset(2026, 9, 3, 22, 0, 0, TimeSpan.Zero);
        await CreateSessionAsync(options, workspace, sessionId, createdUtc);
        var taskId = Guid.NewGuid();
        var store = new SqliteExecutionJournalStore(options);
        await store.UpsertTaskAsync(
            new PersistedTask(taskId, sessionId, "RUNNING", null, createdUtc, createdUtc),
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.AppendEventAsync(
                new PersistedTaskEvent(
                    Guid.NewGuid(),
                    taskId,
                    0,
                    ExecutionJournalCategory.Task,
                    "task.payload",
                    null,
                    null,
                    null,
                    "{not-json",
                    createdUtc),
                CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.UpsertProcessRunAsync(
                new PersistedProcessRun(
                    Guid.NewGuid(),
                    taskId,
                    null,
                    null,
                    Guid.NewGuid(),
                    "dotnet",
                    string.Empty,
                    workspace.Path,
                    0,
                    "STARTING",
                    createdUtc,
                    null,
                    null),
                CancellationToken.None));

        Assert.Empty(await store.ListEventsAsync(taskId, CancellationToken.None));
    }

    private static async Task CreateSessionAsync(
        SqliteDatabaseOptions options,
        TemporaryDirectory workspace,
        Guid sessionId,
        DateTimeOffset createdUtc)
    {
        var conversationStore = new SqliteConversationStateStore(options);
        var projectId = Guid.NewGuid();
        await conversationStore.UpsertProjectAsync(
            new PersistedProject(
                projectId,
                workspace.GetPath($"project-{projectId:D}"),
                "Journal fixture",
                createdUtc,
                createdUtc),
            CancellationToken.None);
        await conversationStore.UpsertSessionAsync(
            new PersistedSession(
                sessionId,
                projectId,
                "runtime-session",
                "Journal session",
                createdUtc,
                createdUtc),
            CancellationToken.None);
    }
}
