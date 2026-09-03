using System.Globalization;
using System.Text.Json;
using FCCCodeDesktop.Application.Persistence;
using FCCCodeDesktop.Core.State;
using Microsoft.Data.Sqlite;

namespace FCCCodeDesktop.Persistence;

public sealed class SqliteSettingsStore : ISettingsStore
{
    private readonly SqliteDatabaseOptions _options;

    public SqliteSettingsStore(SqliteDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task UpsertGlobalSettingAsync(
        PersistedSetting setting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ValidateSetting(setting);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO GlobalSettings (Key, ValueJson, UpdatedUtc)
            VALUES ($key, $valueJson, $updatedUtc)
            ON CONFLICT(Key) DO UPDATE SET
                ValueJson = excluded.ValueJson,
                UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$key", NormalizeKey(setting.Key));
        command.Parameters.AddWithValue("$valueJson", setting.ValueJson);
        command.Parameters.AddWithValue("$updatedUtc", FormatTimestamp(setting.UpdatedUtc));

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException(
                $"SQLite could not persist global setting '{NormalizeKey(setting.Key)}'.",
                exception);
        }
    }

    public async Task<PersistedSetting?> GetGlobalSettingAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Key, ValueJson, UpdatedUtc
            FROM GlobalSettings
            WHERE Key = $key;
            """;
        command.Parameters.AddWithValue("$key", normalizedKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadSetting(reader)
            : null;
    }

    public async Task<IReadOnlyList<PersistedSetting>> ListGlobalSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Key, ValueJson, UpdatedUtc
            FROM GlobalSettings
            ORDER BY Key COLLATE NOCASE ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var settings = new List<PersistedSetting>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            settings.Add(ReadSetting(reader));
        }

        return settings.AsReadOnly();
    }

    public async Task<bool> DeleteGlobalSettingAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeKey(key);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM GlobalSettings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", normalizedKey);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task UpsertProjectSettingAsync(
        Guid projectId,
        PersistedSetting setting,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(projectId, nameof(projectId));
        ArgumentNullException.ThrowIfNull(setting);
        ValidateSetting(setting);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ProjectSettings (ProjectId, Key, ValueJson, UpdatedUtc)
            VALUES ($projectId, $key, $valueJson, $updatedUtc)
            ON CONFLICT(ProjectId, Key) DO UPDATE SET
                ValueJson = excluded.ValueJson,
                UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$projectId", FormatGuid(projectId));
        command.Parameters.AddWithValue("$key", NormalizeKey(setting.Key));
        command.Parameters.AddWithValue("$valueJson", setting.ValueJson);
        command.Parameters.AddWithValue("$updatedUtc", FormatTimestamp(setting.UpdatedUtc));

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException(
                $"SQLite could not persist project setting '{NormalizeKey(setting.Key)}' for project '{projectId:D}'.",
                exception);
        }
    }

    public async Task<PersistedSetting?> GetProjectSettingAsync(
        Guid projectId,
        string key,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(projectId, nameof(projectId));
        var normalizedKey = NormalizeKey(key);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Key, ValueJson, UpdatedUtc
            FROM ProjectSettings
            WHERE ProjectId = $projectId AND Key = $key;
            """;
        command.Parameters.AddWithValue("$projectId", FormatGuid(projectId));
        command.Parameters.AddWithValue("$key", normalizedKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadSetting(reader)
            : null;
    }

    public async Task<IReadOnlyList<PersistedSetting>> ListProjectSettingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(projectId, nameof(projectId));

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Key, ValueJson, UpdatedUtc
            FROM ProjectSettings
            WHERE ProjectId = $projectId
            ORDER BY Key COLLATE NOCASE ASC;
            """;
        command.Parameters.AddWithValue("$projectId", FormatGuid(projectId));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var settings = new List<PersistedSetting>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            settings.Add(ReadSetting(reader));
        }

        return settings.AsReadOnly();
    }

    public async Task<bool> DeleteProjectSettingAsync(
        Guid projectId,
        string key,
        CancellationToken cancellationToken = default)
    {
        EnsureNonEmptyGuid(projectId, nameof(projectId));
        var normalizedKey = NormalizeKey(key);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM ProjectSettings WHERE ProjectId = $projectId AND Key = $key;";
        command.Parameters.AddWithValue("$projectId", FormatGuid(projectId));
        command.Parameters.AddWithValue("$key", normalizedKey);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
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

    private static PersistedSetting ReadSetting(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            ParseTimestamp(reader.GetString(2)));

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Setting key is required.", nameof(key));
        }

        return key.Trim();
    }

    private static void ValidateSetting(PersistedSetting setting)
    {
        _ = NormalizeKey(setting.Key);

        try
        {
            using var _ = JsonDocument.Parse(setting.ValueJson);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Setting value must contain valid JSON.", nameof(setting), exception);
        }
    }

    private static void EnsureNonEmptyGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must not be empty.", parameterName);
        }
    }

    private static string FormatGuid(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
