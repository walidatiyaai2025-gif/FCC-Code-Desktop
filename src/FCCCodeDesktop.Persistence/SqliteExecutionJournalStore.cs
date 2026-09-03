using System.Globalization;
using System.Text.Json;
using FCCCodeDesktop.Application.Persistence;
using FCCCodeDesktop.Core.State;
using Microsoft.Data.Sqlite;

namespace FCCCodeDesktop.Persistence;

public sealed class SqliteExecutionJournalStore : IExecutionJournalStore
{
    private readonly SqliteDatabaseOptions _options;

    public SqliteExecutionJournalStore(SqliteDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task UpsertTaskAsync(
        PersistedTask task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ValidateTask(task);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Tasks (Id, SessionId, State, Summary, CreatedUtc, UpdatedUtc)
            VALUES ($id, $sessionId, $state, $summary, $createdUtc, $updatedUtc)
            ON CONFLICT(Id) DO UPDATE SET
                State = excluded.State,
                Summary = excluded.Summary,
                UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(task.Id));
        command.Parameters.AddWithValue("$sessionId", FormatGuid(task.SessionId));
        command.Parameters.AddWithValue("$state", task.State.Trim());
        command.Parameters.AddWithValue("$summary", task.Summary is null ? DBNull.Value : task.Summary.Trim());
        command.Parameters.AddWithValue("$createdUtc", FormatTimestamp(task.CreatedUtc));
        command.Parameters.AddWithValue("$updatedUtc", FormatTimestamp(task.UpdatedUtc));

        await ExecuteWriteAsync(command, "persist task", cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersistedTask?> GetTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(taskId, nameof(taskId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, SessionId, State, Summary, CreatedUtc, UpdatedUtc
            FROM Tasks
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(taskId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadTask(reader) : null;
    }

    public async Task UpsertAgentRunAsync(
        PersistedAgentRun run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ValidateAgentRun(run);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO AgentRuns (Id, TaskId, RuntimeKind, State, StartedUtc, CompletedUtc)
            VALUES ($id, $taskId, $runtimeKind, $state, $startedUtc, $completedUtc)
            ON CONFLICT(Id) DO UPDATE SET
                State = excluded.State,
                CompletedUtc = excluded.CompletedUtc;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(run.Id));
        command.Parameters.AddWithValue("$taskId", FormatGuid(run.TaskId));
        command.Parameters.AddWithValue("$runtimeKind", run.RuntimeKind.Trim());
        command.Parameters.AddWithValue("$state", run.State.Trim());
        command.Parameters.AddWithValue("$startedUtc", FormatTimestamp(run.StartedUtc));
        command.Parameters.AddWithValue("$completedUtc", FormatNullableTimestamp(run.CompletedUtc));

        await ExecuteWriteAsync(command, "persist agent run", cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersistedAgentRun?> GetAgentRunAsync(
        Guid agentRunId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(agentRunId, nameof(agentRunId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskId, RuntimeKind, State, StartedUtc, CompletedUtc
            FROM AgentRuns
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(agentRunId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadAgentRun(reader) : null;
    }

    public async Task UpsertToolRunAsync(
        PersistedToolRun run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ValidateToolRun(run);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ToolRuns (Id, TaskId, AgentRunId, ToolKind, Operation, State, StartedUtc, CompletedUtc)
            VALUES ($id, $taskId, $agentRunId, $toolKind, $operation, $state, $startedUtc, $completedUtc)
            ON CONFLICT(Id) DO UPDATE SET
                State = excluded.State,
                CompletedUtc = excluded.CompletedUtc;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(run.Id));
        command.Parameters.AddWithValue("$taskId", FormatGuid(run.TaskId));
        command.Parameters.AddWithValue("$agentRunId", FormatNullableGuid(run.AgentRunId));
        command.Parameters.AddWithValue("$toolKind", run.ToolKind.Trim());
        command.Parameters.AddWithValue("$operation", run.Operation.Trim());
        command.Parameters.AddWithValue("$state", run.State.Trim());
        command.Parameters.AddWithValue("$startedUtc", FormatTimestamp(run.StartedUtc));
        command.Parameters.AddWithValue("$completedUtc", FormatNullableTimestamp(run.CompletedUtc));

        await ExecuteWriteAsync(command, "persist tool run", cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersistedToolRun?> GetToolRunAsync(
        Guid toolRunId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(toolRunId, nameof(toolRunId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskId, AgentRunId, ToolKind, Operation, State, StartedUtc, CompletedUtc
            FROM ToolRuns
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(toolRunId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadToolRun(reader) : null;
    }

    public async Task UpsertProcessRunAsync(
        PersistedProcessRun run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ValidateProcessRun(run);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ProcessRuns (
                Id, TaskId, AgentRunId, ToolRunId, OperationId, Executable, ArgumentsSanitized,
                WorkingDirectory, ProcessId, State, StartedUtc, CompletedUtc, ExitCode)
            VALUES (
                $id, $taskId, $agentRunId, $toolRunId, $operationId, $executable, $argumentsSanitized,
                $workingDirectory, $processId, $state, $startedUtc, $completedUtc, $exitCode)
            ON CONFLICT(Id) DO UPDATE SET
                ProcessId = excluded.ProcessId,
                State = excluded.State,
                CompletedUtc = excluded.CompletedUtc,
                ExitCode = excluded.ExitCode;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(run.Id));
        command.Parameters.AddWithValue("$taskId", FormatGuid(run.TaskId));
        command.Parameters.AddWithValue("$agentRunId", FormatNullableGuid(run.AgentRunId));
        command.Parameters.AddWithValue("$toolRunId", FormatNullableGuid(run.ToolRunId));
        command.Parameters.AddWithValue("$operationId", FormatGuid(run.OperationId));
        command.Parameters.AddWithValue("$executable", run.Executable.Trim());
        command.Parameters.AddWithValue("$argumentsSanitized", run.ArgumentsSanitized);
        command.Parameters.AddWithValue("$workingDirectory", Path.GetFullPath(run.WorkingDirectory));
        command.Parameters.AddWithValue("$processId", run.ProcessId is null ? DBNull.Value : run.ProcessId.Value);
        command.Parameters.AddWithValue("$state", run.State.Trim());
        command.Parameters.AddWithValue("$startedUtc", FormatTimestamp(run.StartedUtc));
        command.Parameters.AddWithValue("$completedUtc", FormatNullableTimestamp(run.CompletedUtc));
        command.Parameters.AddWithValue("$exitCode", run.ExitCode is null ? DBNull.Value : run.ExitCode.Value);

        await ExecuteWriteAsync(command, "persist process run", cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersistedProcessRun?> GetProcessRunAsync(
        Guid processRunId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(processRunId, nameof(processRunId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskId, AgentRunId, ToolRunId, OperationId, Executable, ArgumentsSanitized,
                   WorkingDirectory, ProcessId, State, StartedUtc, CompletedUtc, ExitCode
            FROM ProcessRuns
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(processRunId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadProcessRun(reader) : null;
    }

    public async Task AppendEventAsync(
        PersistedTaskEvent taskEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskEvent);
        ValidateTaskEvent(taskEvent);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await using (var insertCommand = connection.CreateCommand())
            {
                insertCommand.Transaction = transaction;
                insertCommand.CommandText =
                    """
                    INSERT INTO TaskEvents (
                        Id, TaskId, Sequence, Category, EventType, AgentRunId, ToolRunId,
                        ProcessRunId, DataJson, OccurredUtc)
                    VALUES (
                        $id, $taskId, $sequence, $category, $eventType, $agentRunId, $toolRunId,
                        $processRunId, $dataJson, $occurredUtc);
                    """;
                insertCommand.Parameters.AddWithValue("$id", FormatGuid(taskEvent.Id));
                insertCommand.Parameters.AddWithValue("$taskId", FormatGuid(taskEvent.TaskId));
                insertCommand.Parameters.AddWithValue("$sequence", taskEvent.Sequence);
                insertCommand.Parameters.AddWithValue("$category", FormatCategory(taskEvent.Category));
                insertCommand.Parameters.AddWithValue("$eventType", taskEvent.EventType.Trim());
                insertCommand.Parameters.AddWithValue("$agentRunId", FormatNullableGuid(taskEvent.AgentRunId));
                insertCommand.Parameters.AddWithValue("$toolRunId", FormatNullableGuid(taskEvent.ToolRunId));
                insertCommand.Parameters.AddWithValue("$processRunId", FormatNullableGuid(taskEvent.ProcessRunId));
                insertCommand.Parameters.AddWithValue("$dataJson", taskEvent.DataJson is null ? DBNull.Value : taskEvent.DataJson);
                insertCommand.Parameters.AddWithValue("$occurredUtc", FormatTimestamp(taskEvent.OccurredUtc));

                await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var taskCommand = connection.CreateCommand())
            {
                taskCommand.Transaction = transaction;
                taskCommand.CommandText =
                    """
                    UPDATE Tasks
                    SET UpdatedUtc = CASE
                        WHEN UpdatedUtc < $eventUtc THEN $eventUtc
                        ELSE UpdatedUtc
                    END
                    WHERE Id = $taskId;
                    """;
                taskCommand.Parameters.AddWithValue("$eventUtc", FormatTimestamp(taskEvent.OccurredUtc));
                taskCommand.Parameters.AddWithValue("$taskId", FormatGuid(taskEvent.TaskId));

                var affectedRows = await taskCommand
                    .ExecuteNonQueryAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (affectedRows != 1)
                {
                    throw new InvalidOperationException(
                        $"Cannot append an execution event because task '{taskEvent.TaskId:D}' does not exist.");
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException(
                $"Could not append execution event '{taskEvent.Id:D}' to task '{taskEvent.TaskId:D}'.",
                exception);
        }
    }

    public async Task<IReadOnlyList<PersistedTaskEvent>> ListEventsAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(taskId, nameof(taskId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskId, Sequence, Category, EventType, AgentRunId, ToolRunId,
                   ProcessRunId, DataJson, OccurredUtc
            FROM TaskEvents
            WHERE TaskId = $taskId
            ORDER BY Sequence ASC;
            """;
        command.Parameters.AddWithValue("$taskId", FormatGuid(taskId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var events = new List<PersistedTaskEvent>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(ReadTaskEvent(reader));
        }

        return events.AsReadOnly();
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _options.DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using (var foreignKeysCommand = connection.CreateCommand())
            {
                foreignKeysCommand.CommandText = "PRAGMA foreign_keys = ON;";
                await foreignKeysCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            var busyTimeoutMilliseconds = checked((int)_options.BusyTimeout.TotalMilliseconds);
            await using (var timeoutCommand = connection.CreateCommand())
            {
                timeoutCommand.CommandText = $"PRAGMA busy_timeout = {busyTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture)};";
                await timeoutCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task ExecuteWriteAsync(
        SqliteCommand command,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException($"SQLite could not {operation}.", exception);
        }
    }

    private static PersistedTask ReadTask(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            ParseTimestamp(reader.GetString(4)),
            ParseTimestamp(reader.GetString(5)));

    private static PersistedAgentRun ReadAgentRun(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            ParseTimestamp(reader.GetString(4)),
            ParseNullableTimestamp(reader, 5));

    private static PersistedToolRun ReadToolRun(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            ReadNullableGuid(reader, 2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            ParseTimestamp(reader.GetString(6)),
            ParseNullableTimestamp(reader, 7));

    private static PersistedProcessRun ReadProcessRun(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            ReadNullableGuid(reader, 2),
            ReadNullableGuid(reader, 3),
            Guid.Parse(reader.GetString(4)),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetInt32(8),
            reader.GetString(9),
            ParseTimestamp(reader.GetString(10)),
            ParseNullableTimestamp(reader, 11),
            reader.IsDBNull(12) ? null : reader.GetInt32(12));

    private static PersistedTaskEvent ReadTaskEvent(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetInt64(2),
            Enum.Parse<ExecutionJournalCategory>(reader.GetString(3), ignoreCase: true),
            reader.GetString(4),
            ReadNullableGuid(reader, 5),
            ReadNullableGuid(reader, 6),
            ReadNullableGuid(reader, 7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            ParseTimestamp(reader.GetString(9)));

    private static Guid? ReadNullableGuid(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Guid.Parse(reader.GetString(ordinal));

    private static string FormatGuid(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    private static object FormatNullableGuid(Guid? value) =>
        value is null ? DBNull.Value : FormatGuid(value.Value);

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static object FormatNullableTimestamp(DateTimeOffset? value) =>
        value is null ? DBNull.Value : FormatTimestamp(value.Value);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateTimeOffset? ParseNullableTimestamp(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ParseTimestamp(reader.GetString(ordinal));

    private static string FormatCategory(ExecutionJournalCategory category) =>
        category.ToString().ToUpperInvariant();

    private static void ValidateTask(PersistedTask task)
    {
        EnsureNonEmptyGuid(task.Id, nameof(task));
        EnsureNonEmptyGuid(task.SessionId, nameof(task));
        EnsureRequiredText(task.State, "Task state is required.", nameof(task));
        if (task.Summary is not null && string.IsNullOrWhiteSpace(task.Summary))
        {
            throw new ArgumentException("Task summary cannot be whitespace.", nameof(task));
        }

        EnsureChronological(task.CreatedUtc, task.UpdatedUtc, "Task UpdatedUtc cannot precede CreatedUtc.", nameof(task));
    }

    private static void ValidateAgentRun(PersistedAgentRun run)
    {
        EnsureNonEmptyGuid(run.Id, nameof(run));
        EnsureNonEmptyGuid(run.TaskId, nameof(run));
        EnsureRequiredText(run.RuntimeKind, "Agent runtime kind is required.", nameof(run));
        EnsureRequiredText(run.State, "Agent run state is required.", nameof(run));
        EnsureCompletionTimestamp(run.StartedUtc, run.CompletedUtc, "Agent run CompletedUtc cannot precede StartedUtc.", nameof(run));
    }

    private static void ValidateToolRun(PersistedToolRun run)
    {
        EnsureNonEmptyGuid(run.Id, nameof(run));
        EnsureNonEmptyGuid(run.TaskId, nameof(run));
        EnsureNullableNonEmptyGuid(run.AgentRunId, nameof(run));
        EnsureRequiredText(run.ToolKind, "Tool kind is required.", nameof(run));
        EnsureRequiredText(run.Operation, "Tool operation is required.", nameof(run));
        EnsureRequiredText(run.State, "Tool run state is required.", nameof(run));
        EnsureCompletionTimestamp(run.StartedUtc, run.CompletedUtc, "Tool run CompletedUtc cannot precede StartedUtc.", nameof(run));
    }

    private static void ValidateProcessRun(PersistedProcessRun run)
    {
        EnsureNonEmptyGuid(run.Id, nameof(run));
        EnsureNonEmptyGuid(run.TaskId, nameof(run));
        EnsureNullableNonEmptyGuid(run.AgentRunId, nameof(run));
        EnsureNullableNonEmptyGuid(run.ToolRunId, nameof(run));
        EnsureNonEmptyGuid(run.OperationId, nameof(run));
        EnsureRequiredText(run.Executable, "Process executable is required.", nameof(run));
        ArgumentNullException.ThrowIfNull(run.ArgumentsSanitized);
        EnsureRequiredText(run.WorkingDirectory, "Process working directory is required.", nameof(run));
        EnsureRequiredText(run.State, "Process state is required.", nameof(run));
        if (run.ProcessId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(run), "Process ID must be positive when present.");
        }

        EnsureCompletionTimestamp(run.StartedUtc, run.CompletedUtc, "Process run CompletedUtc cannot precede StartedUtc.", nameof(run));
    }

    private static void ValidateTaskEvent(PersistedTaskEvent taskEvent)
    {
        EnsureNonEmptyGuid(taskEvent.Id, nameof(taskEvent));
        EnsureNonEmptyGuid(taskEvent.TaskId, nameof(taskEvent));
        if (taskEvent.Sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(taskEvent), "Task event sequence must be non-negative.");
        }

        if (!Enum.IsDefined(taskEvent.Category))
        {
            throw new ArgumentOutOfRangeException(nameof(taskEvent), "Task event category is not supported.");
        }

        EnsureRequiredText(taskEvent.EventType, "Task event type is required.", nameof(taskEvent));
        EnsureNullableNonEmptyGuid(taskEvent.AgentRunId, nameof(taskEvent));
        EnsureNullableNonEmptyGuid(taskEvent.ToolRunId, nameof(taskEvent));
        EnsureNullableNonEmptyGuid(taskEvent.ProcessRunId, nameof(taskEvent));

        if (taskEvent.DataJson is not null)
        {
            if (string.IsNullOrWhiteSpace(taskEvent.DataJson))
            {
                throw new ArgumentException("Task event DataJson cannot be whitespace.", nameof(taskEvent));
            }

            try
            {
                using var document = JsonDocument.Parse(taskEvent.DataJson);
                _ = document.RootElement.ValueKind;
            }
            catch (JsonException exception)
            {
                throw new ArgumentException("Task event DataJson must contain valid JSON.", nameof(taskEvent), exception);
            }
        }
    }

    private static void EnsureRequiredText(string value, string message, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    private static void EnsureChronological(
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc,
        string message,
        string parameterName)
    {
        if (completedUtc < startedUtc)
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    private static void EnsureCompletionTimestamp(
        DateTimeOffset startedUtc,
        DateTimeOffset? completedUtc,
        string message,
        string parameterName)
    {
        if (completedUtc < startedUtc)
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    private static void EnsureNullableNonEmptyGuid(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Optional identifier must be null or non-empty.", parameterName);
        }
    }

    private static void EnsureNonEmptyGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must not be empty.", parameterName);
        }
    }
}
