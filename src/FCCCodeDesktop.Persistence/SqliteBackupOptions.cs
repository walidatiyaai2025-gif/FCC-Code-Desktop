namespace FCCCodeDesktop.Persistence;

public sealed record SqliteBackupOptions
{
    public const int DefaultRetentionCount = 5;

    public SqliteBackupOptions(
        string? backupDirectory = null,
        int retentionCount = DefaultRetentionCount)
    {
        if (retentionCount < 1 || retentionCount > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retentionCount),
                retentionCount,
                "SQLite backup retention must be between 1 and 100 backups.");
        }

        BackupDirectory = string.IsNullOrWhiteSpace(backupDirectory)
            ? null
            : Path.GetFullPath(backupDirectory);
        RetentionCount = retentionCount;
    }

    public string? BackupDirectory { get; }

    public int RetentionCount { get; }
}
