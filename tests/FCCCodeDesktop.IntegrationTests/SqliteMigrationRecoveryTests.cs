using System.Globalization;
using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class SqliteMigrationRecoveryTests
{
    [Fact]
    public async Task CompletePhaseStateSurvivesReopenAndVerifiedBackupAfterPrimaryCorruption()
    {
        using var workspace = new TemporaryDirectory("fccd p03 recovery مساحة");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var state = await SeedCompletePhaseStateAsync(options, workspace);

        await AssertCompletePhaseStateAsync(options, state);

        var maintenance = new SqliteDatabaseMaintenanceService(
            options,
            new SqliteBackupOptions(workspace.GetPath("backups"), retentionCount: 3));
        var backup = await maintenance.CreateBackupAsync(CancellationToken.None);
        var backupOptions = new SqliteDatabaseOptions(backup.BackupPath);

        var backupIntegrity = await new SqliteDatabaseMaintenanceService(backupOptions)
            .CheckIntegrityAsync(CancellationToken.None);
        Assert.True(backupIntegrity.IsHealthy);
        await AssertCompletePhaseStateAsync(backupOptions, state);

        await File.WriteAllTextAsync(
            options.DatabasePath,
            "not-a-sqlite-database",
            CancellationToken.None);

        var corruptedIntegrity = await maintenance.CheckIntegrityAsync(CancellationToken.None);
        Assert.False(corruptedIntegrity.IsHealthy);

        var preservedBackupIntegrity = await new SqliteDatabaseMaintenanceService(backupOptions)
            .CheckIntegrityAsync(CancellationToken.None);
        Assert.True(preservedBackupIntegrity.IsHealthy);
        await AssertCompletePhaseStateAsync(backupOptions, state);
    }

    [Fact]
    public async Task HistoricalVersionTwoStateUpgradesSequentiallyWithoutDataLoss()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-migration-upgrade");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 0, 20, 0, TimeSpan.Zero);
        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var conversation = new SqliteConversationStateStore(options);
        await conversation.UpsertProjectAsync(
            new PersistedProject(
                projectId,
                workspace.GetPath("legacy project مشروع"),
                "Legacy project مشروع",
                createdUtc,
                createdUtc),
            CancellationToken.None);
        await conversation.UpsertSessionAsync(
            new PersistedSession(
                sessionId,
                projectId,
                "legacy-runtime-session",
                "Legacy session جلسة",
                createdUtc,
                createdUtc),
            CancellationToken.None);
        await conversation.AppendMessageAsync(
            new PersistedMessage(
                messageId,
                sessionId,
                0,
                "user",
                "legacy payload بيانات",
                createdUtc),
            CancellationToken.None);

        await DowngradeCurrentSchemaToVersionTwoAsync(options.DatabasePath);

        var upgraded = await new SqliteDatabaseInitializer(options)
            .InitializeAsync(CancellationToken.None);

        Assert.Equal(5, upgraded.CurrentVersion);
        Assert.Equal(3, upgraded.AppliedVersions.Count);
        Assert.Equal(3, upgraded.AppliedVersions[0]);
        Assert.Equal(4, upgraded.AppliedVersions[1]);
        Assert.Equal(5, upgraded.AppliedVersions[2]);

        var reopenedConversation = new SqliteConversationStateStore(options);
        var project = await reopenedConversation.GetProjectAsync(projectId, CancellationToken.None);
        var session = await reopenedConversation.GetSessionAsync(sessionId, CancellationToken.None);
        var messages = await reopenedConversation.ListMessagesAsync(sessionId, CancellationToken.None);

        Assert.NotNull(project);
        Assert.Equal("Legacy project مشروع", project.DisplayName);
        Assert.NotNull(session);
        Assert.Equal("legacy-runtime-session", session.RuntimeSessionId);
        var message = Assert.Single(messages);
        Assert.Equal(messageId, message.Id);
        Assert.Equal("legacy payload بيانات", message.Content);

        var taskId = Guid.NewGuid();
        await new SqliteExecutionJournalStore(options).UpsertTaskAsync(
            new PersistedTask(
                taskId,
                sessionId,
                "PENDING",
                "post-upgrade task",
                createdUtc.AddMinutes(1),
                createdUtc.AddMinutes(1)),
            CancellationToken.None);
        await new SqliteQueueStateStore(options).UpsertQueueItemAsync(
            new PersistedQueueItem(
                Guid.NewGuid(),
                taskId,
                1,
                "QUEUED",
                createdUtc.AddMinutes(2),
                createdUtc.AddMinutes(2)),
            CancellationToken.None);
        await new SqliteSettingsStore(options).UpsertProjectSettingAsync(
            projectId,
            new PersistedSetting(
                "workspace.layout",
                "{\"leftWidth\":320}",
                createdUtc.AddMinutes(3)),
            CancellationToken.None);

        Assert.NotNull(
            await new SqliteExecutionJournalStore(options)
                .GetTaskAsync(taskId, CancellationToken.None));
        Assert.NotNull(
            await new SqliteQueueStateStore(options)
                .GetQueueItemByTaskIdAsync(taskId, CancellationToken.None));
        Assert.NotNull(
            await new SqliteSettingsStore(options)
                .GetProjectSettingAsync(projectId, "workspace.layout", CancellationToken.None));
        Assert.True(
            (await new SqliteDatabaseMaintenanceService(options)
                .CheckIntegrityAsync(CancellationToken.None)).IsHealthy);
    }

    [Fact]
    public async Task FailedPostBaselineMigrationRollsBackAndRetryPreservesCompleteState()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-migration-retry-state");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var state = await SeedCompletePhaseStateAsync(options, workspace);

        var brokenMigration = new SqliteMigration(
            6,
            "create_recovery_probe",
            """
            CREATE TABLE RecoveryProbe (Id INTEGER NOT NULL PRIMARY KEY);
            THIS IS NOT VALID SQLITE;
            """);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SqliteDatabaseInitializer(options, [brokenMigration])
                .InitializeAsync(CancellationToken.None));

        Assert.Contains("migration 6", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await TableExistsAsync(options.DatabasePath, "RecoveryProbe"));
        Assert.Equal(5, await CountAppliedMigrationsAsync(options.DatabasePath));
        await AssertCompletePhaseStateAsync(options, state);

        var correctedMigration = new SqliteMigration(
            6,
            "create_recovery_probe",
            "CREATE TABLE RecoveryProbe (Id INTEGER NOT NULL PRIMARY KEY, Note TEXT NULL);");
        var recovered = await new SqliteDatabaseInitializer(options, [correctedMigration])
            .InitializeAsync(CancellationToken.None);

        Assert.Equal(6, recovered.CurrentVersion);
        Assert.Equal(6, Assert.Single(recovered.AppliedVersions));
        Assert.True(await TableExistsAsync(options.DatabasePath, "RecoveryProbe"));
        Assert.Equal(6, await CountAppliedMigrationsAsync(options.DatabasePath));
        await AssertCompletePhaseStateAsync(options, state);
    }

    [Fact]
    public async Task MigrationLedgerHoleAndFutureVersionAreRejectedWithoutDestroyingDomainState()
    {
        using var missingWorkspace = new TemporaryDirectory("fccd-p03-ledger-hole");
        var missingOptions = new SqliteDatabaseOptions(missingWorkspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(missingOptions).InitializeAsync(CancellationToken.None);
        var missingState = await SeedCompletePhaseStateAsync(missingOptions, missingWorkspace);
        await ExecuteSqlAsync(
            missingOptions.DatabasePath,
            "DELETE FROM SchemaMigrations WHERE Version = 4;");

        var missingFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SqliteDatabaseInitializer(missingOptions).InitializeAsync(CancellationToken.None));
        Assert.Contains("missing applied version 4", missingFailure.Message, StringComparison.OrdinalIgnoreCase);
        await AssertCompletePhaseStateAsync(missingOptions, missingState);

        using var futureWorkspace = new TemporaryDirectory("fccd-p03-ledger-future");
        var futureOptions = new SqliteDatabaseOptions(futureWorkspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(futureOptions).InitializeAsync(CancellationToken.None);
        var futureState = await SeedCompletePhaseStateAsync(futureOptions, futureWorkspace);
        await ExecuteSqlAsync(
            futureOptions.DatabasePath,
            """
            INSERT INTO SchemaMigrations (Version, Name, Checksum, AppliedUtc)
            VALUES (6, 'future_schema', 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA', '2026-09-04T00:00:00.0000000+00:00');
            """);

        var futureFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SqliteDatabaseInitializer(futureOptions).InitializeAsync(CancellationToken.None));
        Assert.Contains("newer than the application supports", futureFailure.Message, StringComparison.OrdinalIgnoreCase);
        await AssertCompletePhaseStateAsync(futureOptions, futureState);
    }

    private static async Task<PhaseStateIds> SeedCompletePhaseStateAsync(
        SqliteDatabaseOptions options,
        TemporaryDirectory workspace)
    {
        var createdUtc = new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero);
        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var toolRunId = Guid.NewGuid();
        var processRunId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var workingDirectory = workspace.GetPath("work مساحة");
        Directory.CreateDirectory(workingDirectory);

        var conversation = new SqliteConversationStateStore(options);
        await conversation.UpsertProjectAsync(
            new PersistedProject(
                projectId,
                workspace.GetPath("project مشروع"),
                "Recovery project مشروع",
                createdUtc,
                createdUtc),
            CancellationToken.None);
        await conversation.UpsertSessionAsync(
            new PersistedSession(
                sessionId,
                projectId,
                "runtime-session-recovery",
                "Recovery session جلسة",
                createdUtc,
                createdUtc),
            CancellationToken.None);
        await conversation.AppendMessageAsync(
            new PersistedMessage(
                messageId,
                sessionId,
                0,
                "user",
                "persist everything احفظ الكل",
                createdUtc),
            CancellationToken.None);

        var journal = new SqliteExecutionJournalStore(options);
        await journal.UpsertTaskAsync(
            new PersistedTask(
                taskId,
                sessionId,
                "RUNNING",
                "Recovery task مهمة",
                createdUtc,
                createdUtc),
            CancellationToken.None);
        await journal.UpsertAgentRunAsync(
            new PersistedAgentRun(
                agentRunId,
                taskId,
                "fcc-claude-structured",
                "RUNNING",
                createdUtc.AddSeconds(1),
                null),
            CancellationToken.None);
        await journal.UpsertToolRunAsync(
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
        await journal.UpsertProcessRunAsync(
            new PersistedProcessRun(
                processRunId,
                taskId,
                agentRunId,
                toolRunId,
                operationId,
                "dotnet",
                "test --configuration Release",
                workingDirectory,
                4407,
                "RUNNING",
                createdUtc.AddSeconds(3),
                null,
                null),
            CancellationToken.None);
        await journal.AppendEventAsync(
            new PersistedTaskEvent(
                eventId,
                taskId,
                0,
                ExecutionJournalCategory.Process,
                "process.started",
                agentRunId,
                toolRunId,
                processRunId,
                "{\"stream\":\"stdout\",\"note\":\"بدأ\"}",
                createdUtc.AddSeconds(4)),
            CancellationToken.None);

        await new SqliteQueueStateStore(options).UpsertQueueItemAsync(
            new PersistedQueueItem(
                queueItemId,
                taskId,
                10,
                "QUEUED",
                createdUtc.AddSeconds(5),
                createdUtc.AddSeconds(5)),
            CancellationToken.None);

        var settings = new SqliteSettingsStore(options);
        await settings.UpsertGlobalSettingAsync(
            new PersistedSetting(
                "appearance.theme",
                "\"dark\"",
                createdUtc.AddSeconds(6)),
            CancellationToken.None);
        await settings.UpsertProjectSettingAsync(
            projectId,
            new PersistedSetting(
                "workspace.layout",
                "{\"leftWidth\":288.5,\"label\":\"مساحة\"}",
                createdUtc.AddSeconds(7)),
            CancellationToken.None);

        return new PhaseStateIds(
            projectId,
            sessionId,
            messageId,
            taskId,
            agentRunId,
            toolRunId,
            processRunId,
            eventId,
            queueItemId,
            operationId);
    }

    private static async Task AssertCompletePhaseStateAsync(
        SqliteDatabaseOptions options,
        PhaseStateIds state)
    {
        var conversation = new SqliteConversationStateStore(options);
        var project = await conversation.GetProjectAsync(state.ProjectId, CancellationToken.None);
        var session = await conversation.GetSessionAsync(state.SessionId, CancellationToken.None);
        var messages = await conversation.ListMessagesAsync(state.SessionId, CancellationToken.None);

        Assert.NotNull(project);
        Assert.Equal("Recovery project مشروع", project.DisplayName);
        Assert.NotNull(session);
        Assert.Equal("runtime-session-recovery", session.RuntimeSessionId);
        var message = Assert.Single(messages);
        Assert.Equal(state.MessageId, message.Id);
        Assert.Equal("persist everything احفظ الكل", message.Content);

        var journal = new SqliteExecutionJournalStore(options);
        var task = await journal.GetTaskAsync(state.TaskId, CancellationToken.None);
        var agentRun = await journal.GetAgentRunAsync(state.AgentRunId, CancellationToken.None);
        var toolRun = await journal.GetToolRunAsync(state.ToolRunId, CancellationToken.None);
        var processRun = await journal.GetProcessRunAsync(state.ProcessRunId, CancellationToken.None);
        var events = await journal.ListEventsAsync(state.TaskId, CancellationToken.None);

        Assert.NotNull(task);
        Assert.Equal(state.SessionId, task.SessionId);
        Assert.Equal("RUNNING", task.State);
        Assert.NotNull(agentRun);
        Assert.Equal("fcc-claude-structured", agentRun.RuntimeKind);
        Assert.NotNull(toolRun);
        Assert.Equal(state.AgentRunId, toolRun.AgentRunId);
        Assert.NotNull(processRun);
        Assert.Equal(state.OperationId, processRun.OperationId);
        Assert.Equal(4407, processRun.ProcessId);
        var taskEvent = Assert.Single(events);
        Assert.Equal(state.EventId, taskEvent.Id);
        Assert.Equal(state.ProcessRunId, taskEvent.ProcessRunId);

        var queueItem = await new SqliteQueueStateStore(options)
            .GetQueueItemAsync(state.QueueItemId, CancellationToken.None);
        Assert.NotNull(queueItem);
        Assert.Equal(state.TaskId, queueItem.TaskId);
        Assert.Equal("QUEUED", queueItem.State);

        var settings = new SqliteSettingsStore(options);
        var globalSetting = await settings.GetGlobalSettingAsync(
            "appearance.theme",
            CancellationToken.None);
        var projectSetting = await settings.GetProjectSettingAsync(
            state.ProjectId,
            "workspace.layout",
            CancellationToken.None);
        Assert.NotNull(globalSetting);
        Assert.Equal("\"dark\"", globalSetting.ValueJson);
        Assert.NotNull(projectSetting);
        Assert.Contains("مساحة", projectSetting.ValueJson, StringComparison.Ordinal);
    }

    private static async Task DowngradeCurrentSchemaToVersionTwoAsync(string databasePath)
    {
        await ExecuteSqlAsync(
            databasePath,
            """
            PRAGMA foreign_keys = OFF;
            DROP TABLE ProjectSettings;
            DROP TABLE GlobalSettings;
            DROP TABLE QueueItems;
            DROP TABLE TaskEvents;
            DROP TABLE ProcessRuns;
            DROP TABLE ToolRuns;
            DROP TABLE AgentRuns;
            DROP TABLE Tasks;
            DELETE FROM SchemaMigrations WHERE Version >= 3;
            PRAGMA foreign_keys = ON;
            """);
    }

    private static async Task ExecuteSqlAsync(string databasePath, string sql)
    {
        await using var connection = await OpenConnectionAsync(databasePath);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task<bool> TableExistsAsync(string databasePath, string tableName)
    {
        await using var connection = await OpenConnectionAsync(databasePath);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.AddWithValue("$tableName", tableName);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(CancellationToken.None),
            CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<int> CountAppliedMigrationsAsync(string databasePath)
    {
        await using var connection = await OpenConnectionAsync(databasePath);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM SchemaMigrations;";
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(CancellationToken.None),
            CultureInfo.InvariantCulture);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(string databasePath)
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            }.ToString());
        await connection.OpenAsync(CancellationToken.None);
        return connection;
    }

    private sealed record PhaseStateIds(
        Guid ProjectId,
        Guid SessionId,
        Guid MessageId,
        Guid TaskId,
        Guid AgentRunId,
        Guid ToolRunId,
        Guid ProcessRunId,
        Guid EventId,
        Guid QueueItemId,
        Guid OperationId);
}
