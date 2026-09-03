using System.Globalization;
using FCCCodeDesktop.Application.Persistence;
using Microsoft.Data.Sqlite;

namespace FCCCodeDesktop.Persistence;

public sealed class SqliteDatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private const string BackupTimestampFormat = "yyyyMMdd'T'HHmmss'.'fffffff'Z'";
    private const int BackupTimestampLength = 24;
    private const string BackupSuffix = ".backup";

    private readonly SqliteDatabaseOptions _databaseOptions;
    private readonly SqliteBackupOptions _backupOptions;
    private readonly TimeProvider _timeProvider;

    public SqliteDatabaseMaintenanceService(
        SqliteDatabaseOptions databaseOptions,
        SqliteBackupOptions? backupOptions = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(databaseOptions);

        _databaseOptions = databaseOptions;
        _backupOptions = backupOptions ?? new SqliteBackupOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<DatabaseIntegrityReport> CheckIntegrityAsync(
        CancellationToken cancellationToken = default) =>
        CheckIntegrityAsync(_databaseOptions.DatabasePath, cancellationToken);

    public async Task<DatabaseBackupArtifact> CreateBackupAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sourceIntegrity = await CheckIntegrityAsync(cancellationToken).ConfigureAwait(false);
        if (!sourceIntegrity.IsHealthy)
        {
            throw new InvalidDataException(
                "SQLite source database integrity check failed; backup creation was refused.");
        }

        var backupDirectory = GetBackupDirectory();
        Directory.CreateDirectory(backupDirectory);

        var createdUtc = _timeProvider.GetUtcNow();
        var finalPath = BuildBackupPath(backupDirectory, createdUtc, Guid.NewGuid());
        var temporaryPath = finalPath + ".tmp";

        try
        {
            await CopyDatabaseAsync(temporaryPath, cancellationToken).ConfigureAwait(false);

            var backupIntegrity = await CheckIntegrityAsync(temporaryPath, cancellationToken)
                .ConfigureAwait(false);
            if (!backupIntegrity.IsHealthy)
            {
                throw new InvalidDataException(
                    "SQLite backup integrity verification failed; the backup was not published.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath, overwrite: false);

            RotateBackups(backupDirectory, cancellationToken);
            return new DatabaseBackupArtifact(finalPath, createdUtc);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    public Task<IReadOnlyList<DatabaseBackupArtifact>> ListBackupsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var backupDirectory = GetBackupDirectory();
        if (!Directory.Exists(backupDirectory))
        {
            return Task.FromResult<IReadOnlyList<DatabaseBackupArtifact>>(
                Array.Empty<DatabaseBackupArtifact>());
        }

        return Task.FromResult<IReadOnlyList<DatabaseBackupArtifact>>(
            ReadBackupArtifacts(backupDirectory).AsReadOnly());
    }

    private async Task<DatabaseIntegrityReport> CheckIntegrityAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(databasePath))
        {
            return new DatabaseIntegrityReport(
                false,
                Array.AsReadOnly(["Database file is missing."]));
        }

        try
        {
            await using var connection = await OpenConnectionAsync(
                databasePath,
                SqliteOpenMode.ReadOnly,
                cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            var messages = new List<string>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                messages.Add(reader.GetString(0));
            }

            var isHealthy = messages.Count == 1
                && string.Equals(messages[0], "ok", StringComparison.OrdinalIgnoreCase);
            if (messages.Count == 0)
            {
                messages.Add("SQLite integrity check returned no result.");
            }

            return new DatabaseIntegrityReport(isHealthy, messages.AsReadOnly());
        }
        catch (SqliteException exception)
        {
            return new DatabaseIntegrityReport(
                false,
                Array.AsReadOnly(
                [
                    $"SQLite integrity check could not complete (error code {exception.SqliteErrorCode.ToString(CultureInfo.InvariantCulture)})."
                ]));
        }
    }

    private async Task CopyDatabaseAsync(
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var source = await OpenConnectionAsync(
            _databaseOptions.DatabasePath,
            SqliteOpenMode.ReadWrite,
            cancellationToken).ConfigureAwait(false);
        await using var destination = await OpenConnectionAsync(
            destinationPath,
            SqliteOpenMode.ReadWriteCreate,
            cancellationToken).ConfigureAwait(false);

        source.BackupDatabase(destination);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        string databasePath,
        SqliteOpenMode mode,
        CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = mode,
            Pooling = false
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var busyTimeoutMilliseconds = checked((int)_databaseOptions.BusyTimeout.TotalMilliseconds);
            await using var timeoutCommand = connection.CreateCommand();
            timeoutCommand.CommandText =
                $"PRAGMA busy_timeout = {busyTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture)};";
            await timeoutCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private string GetBackupDirectory()
    {
        if (_backupOptions.BackupDirectory is not null)
        {
            return _backupOptions.BackupDirectory;
        }

        var databaseDirectory = Path.GetDirectoryName(_databaseOptions.DatabasePath)
            ?? Directory.GetCurrentDirectory();
        return Path.Combine(databaseDirectory, "backups");
    }

    private string BuildBackupPath(
        string backupDirectory,
        DateTimeOffset createdUtc,
        Guid uniqueId)
    {
        var databaseFileName = Path.GetFileName(_databaseOptions.DatabasePath);
        var timestamp = createdUtc.ToUniversalTime()
            .ToString(BackupTimestampFormat, CultureInfo.InvariantCulture);
        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"{databaseFileName}.{timestamp}.{uniqueId:N}{BackupSuffix}");
        return Path.Combine(backupDirectory, fileName);
    }

    private List<DatabaseBackupArtifact> ReadBackupArtifacts(string backupDirectory)
    {
        var databaseFileName = Path.GetFileName(_databaseOptions.DatabasePath);
        var prefix = databaseFileName + ".";
        var artifacts = new List<DatabaseBackupArtifact>();

        foreach (var path in Directory.EnumerateFiles(backupDirectory, $"{databaseFileName}.*{BackupSuffix}"))
        {
            var fileName = Path.GetFileName(path);
            if (!TryParseBackupTimestamp(fileName, prefix, out var createdUtc))
            {
                continue;
            }

            artifacts.Add(new DatabaseBackupArtifact(Path.GetFullPath(path), createdUtc));
        }

        artifacts.Sort(
            static (left, right) =>
            {
                var timeComparison = right.CreatedUtc.CompareTo(left.CreatedUtc);
                return timeComparison != 0
                    ? timeComparison
                    : string.Compare(left.BackupPath, right.BackupPath, StringComparison.Ordinal);
            });
        return artifacts;
    }

    private static bool TryParseBackupTimestamp(
        string fileName,
        string prefix,
        out DateTimeOffset createdUtc)
    {
        createdUtc = default;
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payloadLength = fileName.Length - prefix.Length - BackupSuffix.Length;
        if (payloadLength <= BackupTimestampLength)
        {
            return false;
        }

        var payload = fileName.AsSpan(prefix.Length, payloadLength);
        if (payload.Length <= BackupTimestampLength || payload[BackupTimestampLength] != '.')
        {
            return false;
        }

        return DateTimeOffset.TryParseExact(
            payload[..BackupTimestampLength],
            BackupTimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out createdUtc);
    }

    private void RotateBackups(string backupDirectory, CancellationToken cancellationToken)
    {
        var artifacts = ReadBackupArtifacts(backupDirectory);
        for (var index = _backupOptions.RetentionCount; index < artifacts.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(artifacts[index].BackupPath);
        }
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        if (!File.Exists(temporaryPath))
        {
            return;
        }

        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
            // Best-effort cleanup. The unverified temporary file never participates in rotation.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup. The unverified temporary file never participates in rotation.
        }
    }
}
