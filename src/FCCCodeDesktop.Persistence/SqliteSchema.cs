using System.Collections.ObjectModel;

namespace FCCCodeDesktop.Persistence;

internal static class SqliteSchema
{
    public const int BaselineVersion = 2;

    public static ReadOnlyCollection<SqliteMigration> BaselineMigrations { get; } =
        Array.AsReadOnly(
        [
            new SqliteMigration(
                1,
                "bootstrap_schema_migrations",
                """
                CREATE TABLE IF NOT EXISTS SchemaMigrations (
                    Version INTEGER NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL UNIQUE,
                    Checksum TEXT NOT NULL,
                    AppliedUtc TEXT NOT NULL
                );
                """),
            new SqliteMigration(
                2,
                "create_projects_sessions_messages",
                """
                CREATE TABLE Projects (
                    Id TEXT NOT NULL PRIMARY KEY,
                    RootPath TEXT NOT NULL,
                    DisplayName TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL,
                    CONSTRAINT CK_Projects_RootPath_NotEmpty CHECK (length(trim(RootPath)) > 0),
                    CONSTRAINT CK_Projects_DisplayName_NotEmpty CHECK (length(trim(DisplayName)) > 0)
                );

                CREATE UNIQUE INDEX UX_Projects_RootPath
                    ON Projects(RootPath COLLATE NOCASE);

                CREATE TABLE Sessions (
                    Id TEXT NOT NULL PRIMARY KEY,
                    ProjectId TEXT NOT NULL,
                    RuntimeSessionId TEXT NULL,
                    Title TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL,
                    CONSTRAINT FK_Sessions_Projects
                        FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_Sessions_Title_NotEmpty CHECK (length(trim(Title)) > 0)
                );

                CREATE INDEX IX_Sessions_ProjectId_UpdatedUtc
                    ON Sessions(ProjectId, UpdatedUtc DESC);

                CREATE TABLE Messages (
                    Id TEXT NOT NULL PRIMARY KEY,
                    SessionId TEXT NOT NULL,
                    Sequence INTEGER NOT NULL,
                    Role TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    CONSTRAINT FK_Messages_Sessions
                        FOREIGN KEY (SessionId) REFERENCES Sessions(Id) ON DELETE CASCADE,
                    CONSTRAINT UQ_Messages_SessionId_Sequence UNIQUE (SessionId, Sequence),
                    CONSTRAINT CK_Messages_Sequence_NonNegative CHECK (Sequence >= 0),
                    CONSTRAINT CK_Messages_Role_NotEmpty CHECK (length(trim(Role)) > 0)
                );

                CREATE INDEX IX_Messages_SessionId_CreatedUtc
                    ON Messages(SessionId, CreatedUtc);
                """)
        ]);
}
