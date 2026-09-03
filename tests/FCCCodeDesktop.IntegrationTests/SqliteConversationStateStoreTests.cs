using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class SqliteConversationStateStoreTests
{
    [Fact]
    public async Task ProjectSessionAndMessagesSurviveStoreRecreationWithDeterministicOrdering()
    {
        using var workspace = new TemporaryDirectory("fccd p03 state مساحة");
        var databasePath = workspace.GetPath("state.db");
        var options = new SqliteDatabaseOptions(databasePath);
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var createdUtc = new DateTimeOffset(2026, 9, 3, 17, 0, 0, TimeSpan.Zero);
        var project = new PersistedProject(
            projectId,
            workspace.GetPath("مشروع source"),
            "مشروع تجريبي",
            createdUtc,
            createdUtc);
        var session = new PersistedSession(
            sessionId,
            projectId,
            "fcc-session-001",
            "جلسة الاستمرار",
            createdUtc.AddMinutes(1),
            createdUtc.AddMinutes(1));

        var writer = new SqliteConversationStateStore(options);
        await writer.UpsertProjectAsync(project, CancellationToken.None);
        await writer.UpsertSessionAsync(session, CancellationToken.None);
        await writer.AppendMessageAsync(
            new PersistedMessage(
                Guid.NewGuid(),
                sessionId,
                1,
                "assistant",
                "second",
                createdUtc.AddMinutes(3)),
            CancellationToken.None);
        await writer.AppendMessageAsync(
            new PersistedMessage(
                Guid.NewGuid(),
                sessionId,
                0,
                "user",
                "الأول",
                createdUtc.AddMinutes(2)),
            CancellationToken.None);

        var reader = new SqliteConversationStateStore(options);
        var persistedProject = await reader.GetProjectAsync(projectId, CancellationToken.None);
        var persistedSession = await reader.GetSessionAsync(sessionId, CancellationToken.None);
        var sessions = await reader.ListSessionsAsync(projectId, CancellationToken.None);
        var messages = await reader.ListMessagesAsync(sessionId, CancellationToken.None);

        Assert.NotNull(persistedProject);
        Assert.Equal(projectId, persistedProject.Id);
        Assert.Equal(Path.GetFullPath(project.RootPath), persistedProject.RootPath);
        Assert.Equal(project.DisplayName, persistedProject.DisplayName);
        Assert.Equal(createdUtc, persistedProject.CreatedUtc);

        Assert.NotNull(persistedSession);
        Assert.Equal(sessionId, persistedSession.Id);
        Assert.Equal(projectId, persistedSession.ProjectId);
        Assert.Equal("fcc-session-001", persistedSession.RuntimeSessionId);
        Assert.Equal("جلسة الاستمرار", persistedSession.Title);
        Assert.Equal(createdUtc.AddMinutes(3), persistedSession.UpdatedUtc);

        Assert.Single(sessions);
        Assert.Equal(sessionId, sessions[0].Id);
        Assert.Equal(2, messages.Count);
        Assert.Equal(0, messages[0].Sequence);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("الأول", messages[0].Content);
        Assert.Equal(1, messages[1].Sequence);
        Assert.Equal("assistant", messages[1].Role);
        Assert.Equal("second", messages[1].Content);
    }

    [Fact]
    public async Task UpsertsPreserveCreatedUtcAndUpdateMutableProjectSessionFields()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-state-upsert");
        var databasePath = workspace.GetPath("state.db");
        var options = new SqliteDatabaseOptions(databasePath);
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var store = new SqliteConversationStateStore(options);

        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var createdUtc = new DateTimeOffset(2026, 9, 3, 18, 0, 0, TimeSpan.Zero);
        var rootPath = workspace.GetPath("project");

        await store.UpsertProjectAsync(
            new PersistedProject(projectId, rootPath, "Original", createdUtc, createdUtc),
            CancellationToken.None);
        await store.UpsertProjectAsync(
            new PersistedProject(projectId, rootPath, "Renamed", createdUtc.AddHours(1), createdUtc.AddHours(2)),
            CancellationToken.None);

        await store.UpsertSessionAsync(
            new PersistedSession(
                sessionId,
                projectId,
                null,
                "Initial",
                createdUtc.AddMinutes(1),
                createdUtc.AddMinutes(1)),
            CancellationToken.None);
        await store.UpsertSessionAsync(
            new PersistedSession(
                sessionId,
                projectId,
                "fcc-session-resume",
                "Updated",
                createdUtc.AddHours(1),
                createdUtc.AddHours(3)),
            CancellationToken.None);

        var project = await store.GetProjectAsync(projectId, CancellationToken.None);
        var session = await store.GetSessionAsync(sessionId, CancellationToken.None);

        Assert.NotNull(project);
        Assert.Equal("Renamed", project.DisplayName);
        Assert.Equal(createdUtc, project.CreatedUtc);
        Assert.Equal(createdUtc.AddHours(2), project.UpdatedUtc);

        Assert.NotNull(session);
        Assert.Equal("Updated", session.Title);
        Assert.Equal("fcc-session-resume", session.RuntimeSessionId);
        Assert.Equal(createdUtc.AddMinutes(1), session.CreatedUtc);
        Assert.Equal(createdUtc.AddHours(3), session.UpdatedUtc);
    }

    [Fact]
    public async Task DuplicateMessageSequenceIsRejectedWithoutAddingAnotherMessage()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-state-message-duplicate");
        var databasePath = workspace.GetPath("state.db");
        var options = new SqliteDatabaseOptions(databasePath);
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var store = new SqliteConversationStateStore(options);

        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var createdUtc = new DateTimeOffset(2026, 9, 3, 19, 0, 0, TimeSpan.Zero);
        await store.UpsertProjectAsync(
            new PersistedProject(projectId, workspace.GetPath("project"), "Project", createdUtc, createdUtc),
            CancellationToken.None);
        await store.UpsertSessionAsync(
            new PersistedSession(sessionId, projectId, null, "Session", createdUtc, createdUtc),
            CancellationToken.None);

        await store.AppendMessageAsync(
            new PersistedMessage(Guid.NewGuid(), sessionId, 0, "user", "first", createdUtc.AddMinutes(1)),
            CancellationToken.None);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendMessageAsync(
                new PersistedMessage(Guid.NewGuid(), sessionId, 0, "assistant", "duplicate", createdUtc.AddMinutes(2)),
                CancellationToken.None));

        Assert.Contains("append message", failure.Message, StringComparison.OrdinalIgnoreCase);
        var messages = await store.ListMessagesAsync(sessionId, CancellationToken.None);
        var session = await store.GetSessionAsync(sessionId, CancellationToken.None);
        Assert.Single(messages);
        Assert.NotNull(session);
        Assert.Equal(createdUtc.AddMinutes(1), session.UpdatedUtc);
    }

    [Fact]
    public async Task ForeignKeysRejectOrphanSessionAndMessage()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-state-fk");
        var databasePath = workspace.GetPath("state.db");
        var options = new SqliteDatabaseOptions(databasePath);
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var store = new SqliteConversationStateStore(options);
        var createdUtc = new DateTimeOffset(2026, 9, 3, 20, 0, 0, TimeSpan.Zero);

        var sessionFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpsertSessionAsync(
                new PersistedSession(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "Orphan",
                    createdUtc,
                    createdUtc),
                CancellationToken.None));
        Assert.Contains("persist session", sessionFailure.Message, StringComparison.OrdinalIgnoreCase);

        var messageFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendMessageAsync(
                new PersistedMessage(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    0,
                    "user",
                    "orphan",
                    createdUtc),
                CancellationToken.None));
        Assert.Contains("append message", messageFailure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateProjectRootPathIsRejectedCaseInsensitively()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-state-root-unique");
        var databasePath = workspace.GetPath("state.db");
        var options = new SqliteDatabaseOptions(databasePath);
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var store = new SqliteConversationStateStore(options);
        var createdUtc = new DateTimeOffset(2026, 9, 3, 21, 0, 0, TimeSpan.Zero);
        var rootPath = workspace.GetPath("ProjectRoot");

        await store.UpsertProjectAsync(
            new PersistedProject(Guid.NewGuid(), rootPath, "First", createdUtc, createdUtc),
            CancellationToken.None);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpsertProjectAsync(
                new PersistedProject(
                    Guid.NewGuid(),
                    rootPath.ToUpperInvariant(),
                    "Second",
                    createdUtc,
                    createdUtc),
                CancellationToken.None));

        Assert.Contains("persist project", failure.Message, StringComparison.OrdinalIgnoreCase);
    }
}
