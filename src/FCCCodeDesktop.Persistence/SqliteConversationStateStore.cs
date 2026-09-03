using System.Globalization;
using FCCCodeDesktop.Application.Persistence;
using FCCCodeDesktop.Core.State;
using Microsoft.Data.Sqlite;

namespace FCCCodeDesktop.Persistence;

public sealed class SqliteConversationStateStore : IConversationStateStore
{
    private readonly SqliteDatabaseOptions _options;

    public SqliteConversationStateStore(SqliteDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task UpsertProjectAsync(
        PersistedProject project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ValidateProject(project);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Projects (Id, RootPath, DisplayName, CreatedUtc, UpdatedUtc)
            VALUES ($id, $rootPath, $displayName, $createdUtc, $updatedUtc)
            ON CONFLICT(Id) DO UPDATE SET
                RootPath = excluded.RootPath,
                DisplayName = excluded.DisplayName,
                UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(project.Id));
        command.Parameters.AddWithValue("$rootPath", Path.GetFullPath(project.RootPath));
        command.Parameters.AddWithValue("$displayName", project.DisplayName.Trim());
        command.Parameters.AddWithValue("$createdUtc", FormatTimestamp(project.CreatedUtc));
        command.Parameters.AddWithValue("$updatedUtc", FormatTimestamp(project.UpdatedUtc));

        await ExecuteWriteAsync(command, "persist project", cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersistedProject?> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(projectId, nameof(projectId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, RootPath, DisplayName, CreatedUtc, UpdatedUtc
            FROM Projects
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(projectId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadProject(reader);
    }

    public async Task UpsertSessionAsync(
        PersistedSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ValidateSession(session);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Sessions (Id, ProjectId, RuntimeSessionId, Title, CreatedUtc, UpdatedUtc)
            VALUES ($id, $projectId, $runtimeSessionId, $title, $createdUtc, $updatedUtc)
            ON CONFLICT(Id) DO UPDATE SET
                ProjectId = excluded.ProjectId,
                RuntimeSessionId = excluded.RuntimeSessionId,
                Title = excluded.Title,
                UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(session.Id));
        command.Parameters.AddWithValue("$projectId", FormatGuid(session.ProjectId));
        command.Parameters.AddWithValue(
            "$runtimeSessionId",
            session.RuntimeSessionId is null ? DBNull.Value : session.RuntimeSessionId.Trim());
        command.Parameters.AddWithValue("$title", session.Title.Trim());
        command.Parameters.AddWithValue("$createdUtc", FormatTimestamp(session.CreatedUtc));
        command.Parameters.AddWithValue("$updatedUtc", FormatTimestamp(session.UpdatedUtc));

        await ExecuteWriteAsync(command, "persist session", cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersistedSession?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(sessionId, nameof(sessionId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ProjectId, RuntimeSessionId, Title, CreatedUtc, UpdatedUtc
            FROM Sessions
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(sessionId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadSession(reader);
    }

    public async Task<IReadOnlyList<PersistedSession>> ListSessionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(projectId, nameof(projectId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ProjectId, RuntimeSessionId, Title, CreatedUtc, UpdatedUtc
            FROM Sessions
            WHERE ProjectId = $projectId
            ORDER BY UpdatedUtc DESC, Id ASC;
            """;
        command.Parameters.AddWithValue("$projectId", FormatGuid(projectId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var sessions = new List<PersistedSession>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            sessions.Add(ReadSession(reader));
        }

        return sessions.AsReadOnly();
    }

    public async Task AppendMessageAsync(
        PersistedMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateMessage(message);

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
                    INSERT INTO Messages (Id, SessionId, Sequence, Role, Content, CreatedUtc)
                    VALUES ($id, $sessionId, $sequence, $role, $content, $createdUtc);
                    """;
                insertCommand.Parameters.AddWithValue("$id", FormatGuid(message.Id));
                insertCommand.Parameters.AddWithValue("$sessionId", FormatGuid(message.SessionId));
                insertCommand.Parameters.AddWithValue("$sequence", message.Sequence);
                insertCommand.Parameters.AddWithValue("$role", message.Role.Trim());
                insertCommand.Parameters.AddWithValue("$content", message.Content);
                insertCommand.Parameters.AddWithValue("$createdUtc", FormatTimestamp(message.CreatedUtc));

                await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var sessionCommand = connection.CreateCommand())
            {
                sessionCommand.Transaction = transaction;
                sessionCommand.CommandText =
                    """
                    UPDATE Sessions
                    SET UpdatedUtc = CASE
                        WHEN UpdatedUtc < $messageUtc THEN $messageUtc
                        ELSE UpdatedUtc
                    END
                    WHERE Id = $sessionId;
                    """;
                sessionCommand.Parameters.AddWithValue("$messageUtc", FormatTimestamp(message.CreatedUtc));
                sessionCommand.Parameters.AddWithValue("$sessionId", FormatGuid(message.SessionId));

                var affectedRows = await sessionCommand
                    .ExecuteNonQueryAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (affectedRows != 1)
                {
                    throw new InvalidOperationException(
                        $"Cannot append a message because session '{message.SessionId:D}' does not exist.");
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException(
                $"Could not append message '{message.Id:D}' to session '{message.SessionId:D}'.",
                exception);
        }
    }

    public async Task<IReadOnlyList<PersistedMessage>> ListMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(sessionId, nameof(sessionId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, SessionId, Sequence, Role, Content, CreatedUtc
            FROM Messages
            WHERE SessionId = $sessionId
            ORDER BY Sequence ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", FormatGuid(sessionId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var messages = new List<PersistedMessage>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messages.Add(ReadMessage(reader));
        }

        return messages.AsReadOnly();
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

    private static PersistedProject ReadProject(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            ParseTimestamp(reader.GetString(3)),
            ParseTimestamp(reader.GetString(4)));

    private static PersistedSession ReadSession(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            ParseTimestamp(reader.GetString(4)),
            ParseTimestamp(reader.GetString(5)));

    private static PersistedMessage ReadMessage(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetInt64(2),
            reader.GetString(3),
            reader.GetString(4),
            ParseTimestamp(reader.GetString(5)));

    private static string FormatGuid(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void ValidateProject(PersistedProject project)
    {
        EnsureNonEmptyGuid(project.Id, nameof(project));
        if (string.IsNullOrWhiteSpace(project.RootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(project));
        }

        if (string.IsNullOrWhiteSpace(project.DisplayName))
        {
            throw new ArgumentException("Project display name is required.", nameof(project));
        }

        if (project.UpdatedUtc < project.CreatedUtc)
        {
            throw new ArgumentException("Project UpdatedUtc cannot precede CreatedUtc.", nameof(project));
        }
    }

    private static void ValidateSession(PersistedSession session)
    {
        EnsureNonEmptyGuid(session.Id, nameof(session));
        EnsureNonEmptyGuid(session.ProjectId, nameof(session));
        if (session.RuntimeSessionId is not null && string.IsNullOrWhiteSpace(session.RuntimeSessionId))
        {
            throw new ArgumentException("Runtime session ID cannot be whitespace.", nameof(session));
        }

        if (string.IsNullOrWhiteSpace(session.Title))
        {
            throw new ArgumentException("Session title is required.", nameof(session));
        }

        if (session.UpdatedUtc < session.CreatedUtc)
        {
            throw new ArgumentException("Session UpdatedUtc cannot precede CreatedUtc.", nameof(session));
        }
    }

    private static void ValidateMessage(PersistedMessage message)
    {
        EnsureNonEmptyGuid(message.Id, nameof(message));
        EnsureNonEmptyGuid(message.SessionId, nameof(message));
        if (message.Sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Message sequence must be non-negative.");
        }

        if (string.IsNullOrWhiteSpace(message.Role))
        {
            throw new ArgumentException("Message role is required.", nameof(message));
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
