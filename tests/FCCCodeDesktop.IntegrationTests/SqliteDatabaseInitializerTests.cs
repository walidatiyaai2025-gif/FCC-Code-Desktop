using System.Globalization;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class SqliteDatabaseInitializerTests
{
    [Fact]
    public async Task InitializeCreatesUnicodePathDatabaseAndAppliesBaselineMigration()
    {
        using var workspace = new TemporaryDirectory("fccd p03 sqlite مساحة");
        var databasePath = workspace.GetPath(Path.Combine("state data", "fcc desktop.db"));
        var initializer = new SqliteDatabaseInitializer(new SqliteDatabaseOptions(databasePath));

        var result = await initializer.InitializeAsync(CancellationToken.None);

        Assert.Equal(Path.GetFullPath(databasePath), result.DatabasePath);
        Assert.Equal(1, result.CurrentVersion);
        Assert.Equal([1], result.AppliedVersions);
        Assert.True(File.Exists(databasePath));
        Assert.True(await TableExistsAsync(databasePath, "SchemaMigrations"));
        Assert.Equal(1, await CountAppliedMigrationsAsync(databasePath));

        var baseline = await ReadAppliedMigrationAsync(databasePath, 1);
        Assert.Equal("bootstrap_schema_migrations", baseline.Name);
        Assert.Equal(64, baseline.Checksum.Length);
        Assert.True(
            DateTimeOffset.TryParse(
                baseline.AppliedUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _));
    }

    [Fact]
    public async Task InitializeIsIdempotentAndDoesNotReapplyCompletedMigration()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-sqlite-idempotent");
        var databasePath = workspace.GetPath("state.db");
        var initializer = new SqliteDatabaseInitializer(new SqliteDatabaseOptions(databasePath));

        var first = await initializer.InitializeAsync(CancellationToken.None);
        var second = await initializer.InitializeAsync(CancellationToken.None);

        Assert.Equal([1], first.AppliedVersions);
        Assert.Empty(second.AppliedVersions);
        Assert.Equal(1, second.CurrentVersion);
        Assert.Equal(1, await CountAppliedMigrationsAsync(databasePath));
    }

    [Fact]
    public async Task FailedMigrationRollsBackAndCorrectedMigrationCanRecover()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-sqlite-recovery");
        var databasePath = workspace.GetPath("state.db");
        var options = new SqliteDatabaseOptions(databasePath);
        var brokenMigration = new SqliteMigration(
            2,
            "create_migration_probe",
            """
            CREATE TABLE MigrationProbe (Id INTEGER NOT NULL PRIMARY KEY);
            THIS IS NOT VALID SQLITE;
            """);

        var brokenInitializer = new SqliteDatabaseInitializer(options, [brokenMigration]);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            brokenInitializer.InitializeAsync(CancellationToken.None));

        Assert.Contains("migration 2", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await TableExistsAsync(databasePath, "MigrationProbe"));
        Assert.Equal(1, await CountAppliedMigrationsAsync(databasePath));

        var correctedMigration = new SqliteMigration(
            2,
            "create_migration_probe",
            "CREATE TABLE MigrationProbe (Id INTEGER NOT NULL PRIMARY KEY);");
        var recoveredInitializer = new SqliteDatabaseInitializer(options, [correctedMigration]);

        var recovered = await recoveredInitializer.InitializeAsync(CancellationToken.None);

        Assert.Equal([2], recovered.AppliedVersions);
        Assert.Equal(2, recovered.CurrentVersion);
        Assert.True(await TableExistsAsync(databasePath, "MigrationProbe"));
        Assert.Equal(2, await CountAppliedMigrationsAsync(databasePath));
    }

    [Fact]
    public async Task AppliedMigrationChecksumDriftIsRejectedBeforeNewSqlRuns()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-sqlite-drift");
        var databasePath = workspace.GetPath("state.db");
        var options = new SqliteDatabaseOptions(databasePath);
        var originalMigration = new SqliteMigration(
            2,
            "create_probe",
            "CREATE TABLE Probe (Id INTEGER NOT NULL PRIMARY KEY);");

        await new SqliteDatabaseInitializer(options, [originalMigration])
            .InitializeAsync(CancellationToken.None);

        var rewrittenMigration = new SqliteMigration(
            2,
            "create_probe",
            "CREATE TABLE Probe (Id INTEGER NOT NULL PRIMARY KEY, Name TEXT NULL);");
        var driftedInitializer = new SqliteDatabaseInitializer(options, [rewrittenMigration]);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driftedInitializer.InitializeAsync(CancellationToken.None));

        Assert.Contains("checksum mismatch", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await CountAppliedMigrationsAsync(databasePath));
    }

    [Fact]
    public void MigrationPlanRejectsVersionGapsBeforeTouchingDisk()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-sqlite-gap");
        var databasePath = workspace.GetPath("state.db");
        var invalidMigration = new SqliteMigration(3, "skipped_version", "SELECT 1;");

        var failure = Assert.Throws<ArgumentException>(() =>
            new SqliteDatabaseInitializer(new SqliteDatabaseOptions(databasePath), [invalidMigration]));

        Assert.Contains("Expected version 2", failure.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(databasePath));
    }

    private static async Task<bool> TableExistsAsync(string databasePath, string tableName)
    {
        await using var connection = await OpenConnectionAsync(databasePath);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.AddWithValue("$tableName", tableName);

        var count = Convert.ToInt32(
            await command.ExecuteScalarAsync(CancellationToken.None),
            CultureInfo.InvariantCulture);
        return count == 1;
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

    private static async Task<AppliedMigrationRow> ReadAppliedMigrationAsync(
        string databasePath,
        int version)
    {
        await using var connection = await OpenConnectionAsync(databasePath);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Name, Checksum, AppliedUtc FROM SchemaMigrations WHERE Version = $version;";
        command.Parameters.AddWithValue("$version", version);

        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        Assert.True(await reader.ReadAsync(CancellationToken.None));

        return new AppliedMigrationRow(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2));
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        return connection;
    }

    private sealed record AppliedMigrationRow(string Name, string Checksum, string AppliedUtc);
}
