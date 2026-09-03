namespace FCCCodeDesktop.Persistence;

internal static class SqliteSchema
{
    public const int BaselineVersion = 1;

    public static IReadOnlyList<SqliteMigration> BaselineMigrations { get; } =
        Array.AsReadOnly(
        [
            new SqliteMigration(
                BaselineVersion,
                "bootstrap_schema_migrations",
                """
                CREATE TABLE IF NOT EXISTS SchemaMigrations (
                    Version INTEGER NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL UNIQUE,
                    Checksum TEXT NOT NULL,
                    AppliedUtc TEXT NOT NULL
                );
                """)
        ]);
}
