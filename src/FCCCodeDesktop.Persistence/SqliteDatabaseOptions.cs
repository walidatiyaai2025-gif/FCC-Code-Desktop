namespace FCCCodeDesktop.Persistence;

public sealed record SqliteDatabaseOptions
{
    public static readonly TimeSpan DefaultBusyTimeout = TimeSpan.FromSeconds(5);

    public SqliteDatabaseOptions(string databasePath, TimeSpan? busyTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        DatabasePath = Path.GetFullPath(databasePath);
        BusyTimeout = busyTimeout ?? DefaultBusyTimeout;

        if (BusyTimeout < TimeSpan.Zero || BusyTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(
                nameof(busyTimeout),
                BusyTimeout,
                "SQLite busy timeout must be between zero and five minutes.");
        }
    }

    public string DatabasePath { get; }

    public TimeSpan BusyTimeout { get; }
}
