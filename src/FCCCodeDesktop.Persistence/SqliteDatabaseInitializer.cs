using System.Globalization;
using Microsoft.Data.Sqlite;

namespace FCCCodeDesktop.Persistence;

public sealed class SqliteDatabaseInitializer
{
    private readonly SqliteDatabaseOptions _options;
    private readonly IReadOnlyList<SqliteMigration> _migrations;

    public SqliteDatabaseInitializer(
        SqliteDatabaseOptions options,
        IEnumerable<SqliteMigration>? additionalMigrations = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _migrations = BuildMigrationPlan(additionalMigrations);
    }

    public async Task<SqliteInitializationResult> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDatabaseDirectoryExists();

        await using var connection = new SqliteConnection(BuildConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var appliedMigrations = await ReadAppliedMigrationsAsync(connection, cancellationToken).ConfigureAwait(false);
        ValidateAppliedMigrations(appliedMigrations);

        var newlyAppliedVersions = new List<int>();
        foreach (var migration in _migrations)
        {
            if (appliedMigrations.ContainsKey(migration.Version))
            {
                continue;
            }

            await ApplyMigrationAsync(connection, migration, cancellationToken).ConfigureAwait(false);
            newlyAppliedVersions.Add(migration.Version);
        }

        return new SqliteInitializationResult(
            _options.DatabasePath,
            _migrations[^1].Version,
            Array.AsReadOnly(newlyAppliedVersions.ToArray()));
    }

    private static IReadOnlyList<SqliteMigration> BuildMigrationPlan(
        IEnumerable<SqliteMigration>? additionalMigrations)
    {
        var migrations = new List<SqliteMigration>(SqliteSchema.BaselineMigrations);
        if (additionalMigrations is not null)
        {
            migrations.AddRange(additionalMigrations);
        }

        migrations.Sort(static (left, right) => left.Version.CompareTo(right.Version));

        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < migrations.Count; index++)
        {
            var migration = migrations[index];
            var expectedVersion = index + 1;

            if (migration.Version != expectedVersion)
            {
                throw new ArgumentException(
                    $"SQLite migrations must be contiguous starting at version 1. Expected version {expectedVersion}, found {migration.Version}.",
                    nameof(additionalMigrations));
            }

            if (!names.Add(migration.Name))
            {
                throw new ArgumentException(
                    $"SQLite migration name '{migration.Name}' is duplicated.",
                    nameof(additionalMigrations));
            }
        }

        return migrations.AsReadOnly();
    }

    private string BuildConnectionString()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        return builder.ToString();
    }

    private void EnsureDatabaseDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_options.DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private async Task ConfigureConnectionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var foreignKeysCommand = connection.CreateCommand())
        {
            foreignKeysCommand.CommandText = "PRAGMA foreign_keys = ON;";
            await foreignKeysCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var busyTimeoutMilliseconds = checked((int)_options.BusyTimeout.TotalMilliseconds);
        await using var busyTimeoutCommand = connection.CreateCommand();
        busyTimeoutCommand.CommandText = $"PRAGMA busy_timeout = {busyTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture)};";
        await busyTimeoutCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<int, AppliedMigration>> ReadAppliedMigrationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var tableCommand = connection.CreateCommand())
        {
            tableCommand.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'SchemaMigrations';";

            var tableCount = Convert.ToInt32(
                await tableCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                CultureInfo.InvariantCulture);

            if (tableCount == 0)
            {
                return [];
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Version, Name, Checksum FROM SchemaMigrations ORDER BY Version;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var appliedMigrations = new Dictionary<int, AppliedMigration>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var applied = new AppliedMigration(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2));

            if (!appliedMigrations.TryAdd(applied.Version, applied))
            {
                throw new InvalidOperationException(
                    $"SQLite migration ledger contains duplicate version {applied.Version}.");
            }
        }

        return appliedMigrations;
    }

    private void ValidateAppliedMigrations(IReadOnlyDictionary<int, AppliedMigration> appliedMigrations)
    {
        if (appliedMigrations.Count == 0)
        {
            return;
        }

        var highestAppliedVersion = appliedMigrations.Keys.Max();
        if (highestAppliedVersion > _migrations[^1].Version)
        {
            throw new InvalidOperationException(
                $"Database schema version {highestAppliedVersion} is newer than the application supports ({_migrations[^1].Version}).");
        }

        for (var version = 1; version <= highestAppliedVersion; version++)
        {
            if (!appliedMigrations.TryGetValue(version, out var applied))
            {
                throw new InvalidOperationException(
                    $"SQLite migration ledger is missing applied version {version} before version {highestAppliedVersion}.");
            }

            var expected = _migrations[version - 1];
            if (!string.Equals(applied.Name, expected.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"SQLite migration {version} name mismatch. Database='{applied.Name}', application='{expected.Name}'.");
            }

            if (!string.Equals(applied.Checksum, expected.Checksum, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"SQLite migration {version} checksum mismatch. An applied migration must never be rewritten.");
            }
        }
    }

    private static async Task ApplyMigrationAsync(
        SqliteConnection connection,
        SqliteMigration migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await using (var migrationCommand = connection.CreateCommand())
            {
                migrationCommand.Transaction = transaction;
                migrationCommand.CommandText = migration.Sql;
                await migrationCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var ledgerCommand = connection.CreateCommand())
            {
                ledgerCommand.Transaction = transaction;
                ledgerCommand.CommandText =
                    """
                    INSERT INTO SchemaMigrations (Version, Name, Checksum, AppliedUtc)
                    VALUES ($version, $name, $checksum, $appliedUtc);
                    """;
                ledgerCommand.Parameters.AddWithValue("$version", migration.Version);
                ledgerCommand.Parameters.AddWithValue("$name", migration.Name);
                ledgerCommand.Parameters.AddWithValue("$checksum", migration.Checksum);
                ledgerCommand.Parameters.AddWithValue(
                    "$appliedUtc",
                    DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));

                await ledgerCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException(
                $"SQLite migration {migration.Version} ('{migration.Name}') failed. The migration transaction was not committed.",
                exception);
        }
    }

    private sealed record AppliedMigration(int Version, string Name, string Checksum);
}
