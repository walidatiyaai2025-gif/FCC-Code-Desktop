using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class SqliteDatabaseMaintenanceServiceTests
{
    [Fact]
    public async Task IntegrityCheckAndVerifiedBackupPreserveCurrentState()
    {
        using var workspace = new TemporaryDirectory("fccd p03 backup مساحة");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var updatedUtc = new DateTimeOffset(2026, 9, 4, 1, 50, 0, TimeSpan.Zero);
        await new SqliteSettingsStore(options).UpsertGlobalSettingAsync(
            new PersistedSetting("appearance.theme", "\"dark\"", updatedUtc),
            CancellationToken.None);

        var clock = new MutableTimeProvider(updatedUtc.AddMinutes(5));
        var service = new SqliteDatabaseMaintenanceService(
            options,
            new SqliteBackupOptions(workspace.GetPath("backup مساحة"), retentionCount: 3),
            clock);

        var integrity = await service.CheckIntegrityAsync(CancellationToken.None);
        var backup = await service.CreateBackupAsync(CancellationToken.None);

        Assert.True(integrity.IsHealthy);
        Assert.Equal("ok", Assert.Single(integrity.Messages), ignoreCase: true);
        Assert.True(File.Exists(backup.BackupPath));
        Assert.Equal(clock.GetUtcNow(), backup.CreatedUtc);

        var backupOptions = new SqliteDatabaseOptions(backup.BackupPath);
        var backupInitialization = await new SqliteDatabaseInitializer(backupOptions)
            .InitializeAsync(CancellationToken.None);
        var persistedTheme = await new SqliteSettingsStore(backupOptions)
            .GetGlobalSettingAsync("APPEARANCE.THEME", CancellationToken.None);

        Assert.Equal(5, backupInitialization.CurrentVersion);
        Assert.Empty(backupInitialization.AppliedVersions);
        Assert.NotNull(persistedTheme);
        Assert.Equal("\"dark\"", persistedTheme.ValueJson);
        Assert.Equal(updatedUtc, persistedTheme.UpdatedUtc);
    }

    [Fact]
    public async Task BackupRotationRetainsNewestConfiguredBackups()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-backup-rotation");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var clock = new MutableTimeProvider(
            new DateTimeOffset(2026, 9, 4, 2, 0, 0, TimeSpan.Zero));
        var service = new SqliteDatabaseMaintenanceService(
            options,
            new SqliteBackupOptions(workspace.GetPath("backups"), retentionCount: 2),
            clock);

        var first = await service.CreateBackupAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        var second = await service.CreateBackupAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        var third = await service.CreateBackupAsync(CancellationToken.None);

        var backups = await service.ListBackupsAsync(CancellationToken.None);

        Assert.Equal(2, backups.Count);
        Assert.Equal(third.BackupPath, backups[0].BackupPath);
        Assert.Equal(second.BackupPath, backups[1].BackupPath);
        Assert.False(File.Exists(first.BackupPath));
        Assert.True(File.Exists(second.BackupPath));
        Assert.True(File.Exists(third.BackupPath));
    }

    [Fact]
    public async Task CorruptSourceRefusesNewBackupAndPreservesLastVerifiedBackup()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-backup-corruption");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var clock = new MutableTimeProvider(
            new DateTimeOffset(2026, 9, 4, 2, 20, 0, TimeSpan.Zero));
        var service = new SqliteDatabaseMaintenanceService(
            options,
            new SqliteBackupOptions(workspace.GetPath("backups"), retentionCount: 2),
            clock);
        var verifiedBackup = await service.CreateBackupAsync(CancellationToken.None);

        File.WriteAllBytes(options.DatabasePath, Enumerable.Repeat((byte)0xA5, 512).ToArray());

        var integrity = await service.CheckIntegrityAsync(CancellationToken.None);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CreateBackupAsync(CancellationToken.None));
        var backups = await service.ListBackupsAsync(CancellationToken.None);

        Assert.False(integrity.IsHealthy);
        Assert.NotEmpty(integrity.Messages);
        Assert.Contains("integrity", failure.Message, StringComparison.OrdinalIgnoreCase);
        var onlyBackup = Assert.Single(backups);
        Assert.Equal(verifiedBackup.BackupPath, onlyBackup.BackupPath);
        Assert.True(File.Exists(verifiedBackup.BackupPath));
    }

    [Fact]
    public async Task UnrecognizedBackupFilesAreIgnoredByInventoryAndRotation()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-backup-inventory");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var backupDirectory = workspace.GetPath("backups");
        Directory.CreateDirectory(backupDirectory);
        var unrelatedPath = Path.Combine(backupDirectory, "state.db.not-a-timestamp.deadbeef.backup");
        File.WriteAllText(unrelatedPath, "not a managed backup");

        var clock = new MutableTimeProvider(
            new DateTimeOffset(2026, 9, 4, 2, 40, 0, TimeSpan.Zero));
        var service = new SqliteDatabaseMaintenanceService(
            options,
            new SqliteBackupOptions(backupDirectory, retentionCount: 1),
            clock);

        await service.CreateBackupAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        var latest = await service.CreateBackupAsync(CancellationToken.None);
        var backups = await service.ListBackupsAsync(CancellationToken.None);

        var onlyManagedBackup = Assert.Single(backups);
        Assert.Equal(latest.BackupPath, onlyManagedBackup.BackupPath);
        Assert.True(File.Exists(unrelatedPath));
    }

    [Fact]
    public async Task MissingDatabaseAndInvalidRetentionAreRejectedTruthfully()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-backup-validation");
        var options = new SqliteDatabaseOptions(workspace.GetPath("missing.db"));
        var service = new SqliteDatabaseMaintenanceService(options);

        var integrity = await service.CheckIntegrityAsync(CancellationToken.None);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CreateBackupAsync(CancellationToken.None));

        Assert.False(integrity.IsHealthy);
        Assert.Contains("missing", Assert.Single(integrity.Messages), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("integrity", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqliteBackupOptions(retentionCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqliteBackupOptions(retentionCount: 101));
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount)
        {
            _utcNow = _utcNow.Add(amount);
        }
    }
}
