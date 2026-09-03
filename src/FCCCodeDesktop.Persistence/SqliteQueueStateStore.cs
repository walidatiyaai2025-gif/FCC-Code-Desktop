using System.Globalization;
using FCCCodeDesktop.Application.Persistence;
using FCCCodeDesktop.Core.State;
using Microsoft.Data.Sqlite;

namespace FCCCodeDesktop.Persistence;

public sealed class SqliteQueueStateStore : IQueueStateStore
{
    private readonly SqliteDatabaseOptions _options;

    public SqliteQueueStateStore(SqliteDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task UpsertQueueItemAsync(
        PersistedQueueItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ValidateQueueItem(item);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO QueueItems (Id, TaskId, OrderKey, State, EnqueuedUtc, UpdatedUtc)
            VALUES ($id, $taskId, $orderKey, $state, $enqueuedUtc, $updatedUtc)
            ON CONFLICT(Id) DO UPDATE SET
                OrderKey = excluded.OrderKey,
                State = excluded.State,
                UpdatedUtc = excluded.UpdatedUtc
            WHERE QueueItems.TaskId = excluded.TaskId
              AND QueueItems.EnqueuedUtc = excluded.EnqueuedUtc;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(item.Id));
        command.Parameters.AddWithValue("$taskId", FormatGuid(item.TaskId));
        command.Parameters.AddWithValue("$orderKey", item.OrderKey);
        command.Parameters.AddWithValue("$state", item.State.Trim());
        command.Parameters.AddWithValue("$enqueuedUtc", FormatTimestamp(item.EnqueuedUtc));
        command.Parameters.AddWithValue("$updatedUtc", FormatTimestamp(item.UpdatedUtc));

        try
        {
            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (affectedRows != 1)
            {
                throw new InvalidOperationException(
                    $"Queue item '{item.Id:D}' cannot change its task identity or original enqueue timestamp.");
            }
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException(
                $"SQLite could not persist queue item '{item.Id:D}' for task '{item.TaskId:D}'.",
                exception);
        }
    }

    public async Task<PersistedQueueItem?> GetQueueItemAsync(
        Guid queueItemId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(queueItemId, nameof(queueItemId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskId, OrderKey, State, EnqueuedUtc, UpdatedUtc
            FROM QueueItems
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", FormatGuid(queueItemId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadQueueItem(reader)
            : null;
    }

    public async Task<PersistedQueueItem?> GetQueueItemByTaskIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(taskId, nameof(taskId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskId, OrderKey, State, EnqueuedUtc, UpdatedUtc
            FROM QueueItems
            WHERE TaskId = $taskId;
            """;
        command.Parameters.AddWithValue("$taskId", FormatGuid(taskId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadQueueItem(reader)
            : null;
    }

    public async Task<IReadOnlyList<PersistedQueueItem>> ListQueueItemsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TaskId, OrderKey, State, EnqueuedUtc, UpdatedUtc
            FROM QueueItems
            ORDER BY OrderKey ASC, EnqueuedUtc ASC, Id ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<PersistedQueueItem>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(ReadQueueItem(reader));
        }

        return items.AsReadOnly();
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
                timeoutCommand.CommandText =
                    $"PRAGMA busy_timeout = {busyTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture)};";
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

    private static PersistedQueueItem ReadQueueItem(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetInt64(2),
            reader.GetString(3),
            ParseTimestamp(reader.GetString(4)),
            ParseTimestamp(reader.GetString(5)));

    private static string FormatGuid(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void ValidateQueueItem(PersistedQueueItem item)
    {
        EnsureNonEmptyGuid(item.Id, nameof(item));
        EnsureNonEmptyGuid(item.TaskId, nameof(item));
        if (item.OrderKey < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(item), "Queue order key must be non-negative.");
        }

        if (string.IsNullOrWhiteSpace(item.State))
        {
            throw new ArgumentException("Queue item state is required.", nameof(item));
        }

        if (item.UpdatedUtc < item.EnqueuedUtc)
        {
            throw new ArgumentException("Queue item UpdatedUtc cannot precede EnqueuedUtc.", nameof(item));
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
