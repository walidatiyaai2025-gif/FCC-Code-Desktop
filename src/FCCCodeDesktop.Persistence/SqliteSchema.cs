using System.Collections.ObjectModel;

namespace FCCCodeDesktop.Persistence;

internal static class SqliteSchema
{
    public const int BaselineVersion = 4;

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
                """),
            new SqliteMigration(
                3,
                "create_tasks_execution_journal",
                """
                CREATE TABLE Tasks (
                    Id TEXT NOT NULL PRIMARY KEY,
                    SessionId TEXT NOT NULL,
                    State TEXT NOT NULL,
                    Summary TEXT NULL,
                    CreatedUtc TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL,
                    CONSTRAINT FK_Tasks_Sessions
                        FOREIGN KEY (SessionId) REFERENCES Sessions(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_Tasks_State_NotEmpty CHECK (length(trim(State)) > 0)
                );

                CREATE INDEX IX_Tasks_SessionId_UpdatedUtc
                    ON Tasks(SessionId, UpdatedUtc DESC);

                CREATE TABLE AgentRuns (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TaskId TEXT NOT NULL,
                    RuntimeKind TEXT NOT NULL,
                    State TEXT NOT NULL,
                    StartedUtc TEXT NOT NULL,
                    CompletedUtc TEXT NULL,
                    CONSTRAINT UQ_AgentRuns_Id_TaskId UNIQUE (Id, TaskId),
                    CONSTRAINT FK_AgentRuns_Tasks
                        FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_AgentRuns_RuntimeKind_NotEmpty CHECK (length(trim(RuntimeKind)) > 0),
                    CONSTRAINT CK_AgentRuns_State_NotEmpty CHECK (length(trim(State)) > 0)
                );

                CREATE INDEX IX_AgentRuns_TaskId_StartedUtc
                    ON AgentRuns(TaskId, StartedUtc);

                CREATE TABLE ToolRuns (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TaskId TEXT NOT NULL,
                    AgentRunId TEXT NULL,
                    ToolKind TEXT NOT NULL,
                    Operation TEXT NOT NULL,
                    State TEXT NOT NULL,
                    StartedUtc TEXT NOT NULL,
                    CompletedUtc TEXT NULL,
                    CONSTRAINT UQ_ToolRuns_Id_TaskId UNIQUE (Id, TaskId),
                    CONSTRAINT FK_ToolRuns_Tasks
                        FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_ToolRuns_AgentRuns
                        FOREIGN KEY (AgentRunId, TaskId) REFERENCES AgentRuns(Id, TaskId),
                    CONSTRAINT CK_ToolRuns_ToolKind_NotEmpty CHECK (length(trim(ToolKind)) > 0),
                    CONSTRAINT CK_ToolRuns_Operation_NotEmpty CHECK (length(trim(Operation)) > 0),
                    CONSTRAINT CK_ToolRuns_State_NotEmpty CHECK (length(trim(State)) > 0)
                );

                CREATE INDEX IX_ToolRuns_TaskId_StartedUtc
                    ON ToolRuns(TaskId, StartedUtc);

                CREATE TABLE ProcessRuns (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TaskId TEXT NOT NULL,
                    AgentRunId TEXT NULL,
                    ToolRunId TEXT NULL,
                    OperationId TEXT NOT NULL,
                    Executable TEXT NOT NULL,
                    ArgumentsSanitized TEXT NOT NULL,
                    WorkingDirectory TEXT NOT NULL,
                    ProcessId INTEGER NULL,
                    State TEXT NOT NULL,
                    StartedUtc TEXT NOT NULL,
                    CompletedUtc TEXT NULL,
                    ExitCode INTEGER NULL,
                    CONSTRAINT UQ_ProcessRuns_Id_TaskId UNIQUE (Id, TaskId),
                    CONSTRAINT FK_ProcessRuns_Tasks
                        FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_ProcessRuns_AgentRuns
                        FOREIGN KEY (AgentRunId, TaskId) REFERENCES AgentRuns(Id, TaskId),
                    CONSTRAINT FK_ProcessRuns_ToolRuns
                        FOREIGN KEY (ToolRunId, TaskId) REFERENCES ToolRuns(Id, TaskId),
                    CONSTRAINT CK_ProcessRuns_OperationId_NotEmpty CHECK (length(trim(OperationId)) > 0),
                    CONSTRAINT CK_ProcessRuns_Executable_NotEmpty CHECK (length(trim(Executable)) > 0),
                    CONSTRAINT CK_ProcessRuns_WorkingDirectory_NotEmpty CHECK (length(trim(WorkingDirectory)) > 0),
                    CONSTRAINT CK_ProcessRuns_ProcessId_Positive CHECK (ProcessId IS NULL OR ProcessId > 0),
                    CONSTRAINT CK_ProcessRuns_State_NotEmpty CHECK (length(trim(State)) > 0)
                );

                CREATE INDEX IX_ProcessRuns_TaskId_StartedUtc
                    ON ProcessRuns(TaskId, StartedUtc);

                CREATE TABLE TaskEvents (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TaskId TEXT NOT NULL,
                    Sequence INTEGER NOT NULL,
                    Category TEXT NOT NULL,
                    EventType TEXT NOT NULL,
                    AgentRunId TEXT NULL,
                    ToolRunId TEXT NULL,
                    ProcessRunId TEXT NULL,
                    DataJson TEXT NULL,
                    OccurredUtc TEXT NOT NULL,
                    CONSTRAINT FK_TaskEvents_Tasks
                        FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_TaskEvents_AgentRuns
                        FOREIGN KEY (AgentRunId, TaskId) REFERENCES AgentRuns(Id, TaskId),
                    CONSTRAINT FK_TaskEvents_ToolRuns
                        FOREIGN KEY (ToolRunId, TaskId) REFERENCES ToolRuns(Id, TaskId),
                    CONSTRAINT FK_TaskEvents_ProcessRuns
                        FOREIGN KEY (ProcessRunId, TaskId) REFERENCES ProcessRuns(Id, TaskId),
                    CONSTRAINT UQ_TaskEvents_TaskId_Sequence UNIQUE (TaskId, Sequence),
                    CONSTRAINT CK_TaskEvents_Sequence_NonNegative CHECK (Sequence >= 0),
                    CONSTRAINT CK_TaskEvents_Category CHECK (Category IN ('TASK', 'AGENT', 'TOOL', 'PROCESS')),
                    CONSTRAINT CK_TaskEvents_EventType_NotEmpty CHECK (length(trim(EventType)) > 0)
                );

                CREATE INDEX IX_TaskEvents_TaskId_OccurredUtc
                    ON TaskEvents(TaskId, OccurredUtc);
                """),
            new SqliteMigration(
                4,
                "create_queue_items",
                """
                CREATE TABLE QueueItems (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TaskId TEXT NOT NULL UNIQUE,
                    OrderKey INTEGER NOT NULL,
                    State TEXT NOT NULL,
                    EnqueuedUtc TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL,
                    CONSTRAINT FK_QueueItems_Tasks
                        FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_QueueItems_OrderKey_NonNegative CHECK (OrderKey >= 0),
                    CONSTRAINT CK_QueueItems_State_NotEmpty CHECK (length(trim(State)) > 0)
                );

                CREATE INDEX IX_QueueItems_State_Order
                    ON QueueItems(State, OrderKey, EnqueuedUtc, Id);
                """)
        ]);
}
