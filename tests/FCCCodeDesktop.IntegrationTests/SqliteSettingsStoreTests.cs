using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class SqliteSettingsStoreTests
{
    [Fact]
    public async Task GlobalSettingsSurviveStoreRecreationWithCaseInsensitiveLookupAndDeterministicOrdering()
    {
        using var workspace = new TemporaryDirectory("fccd p03 settings مساحة");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 1, 0, 0, TimeSpan.Zero);
        var writer = new SqliteSettingsStore(options);
        await writer.UpsertGlobalSettingAsync(
            new PersistedSetting(
                "workspace.layout",
                """{"leftWidth":288.5,"label":"مساحة"}""",
                createdUtc.AddMinutes(1)),
            CancellationToken.None);
        await writer.UpsertGlobalSettingAsync(
            new PersistedSetting("Appearance.Theme", "\"dark\"", createdUtc),
            CancellationToken.None);

        var reader = new SqliteSettingsStore(options);
        var theme = await reader.GetGlobalSettingAsync("appearance.theme", CancellationToken.None);
        var settings = await reader.ListGlobalSettingsAsync(CancellationToken.None);

        Assert.NotNull(theme);
        Assert.Equal("Appearance.Theme", theme.Key);
        Assert.Equal("\"dark\"", theme.ValueJson);
        Assert.Equal(createdUtc, theme.UpdatedUtc);

        Assert.Equal(2, settings.Count);
        Assert.Equal("Appearance.Theme", settings[0].Key);
        Assert.Equal("workspace.layout", settings[1].Key);
        Assert.Contains("مساحة", settings[1].ValueJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProjectSettingsAreIsolatedPerProjectAndSurviveStoreRecreation()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-settings-projects");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 1, 20, 0, TimeSpan.Zero);
        var firstProjectId = await CreateProjectAsync(options, workspace, "first", createdUtc);
        var secondProjectId = await CreateProjectAsync(options, workspace, "second", createdUtc.AddSeconds(1));

        var writer = new SqliteSettingsStore(options);
        await writer.UpsertProjectSettingAsync(
            firstProjectId,
            new PersistedSetting(
                "workspace.panels",
                """{"bottomHeight":220,"side":"left"}""",
                createdUtc),
            CancellationToken.None);
        await writer.UpsertProjectSettingAsync(
            secondProjectId,
            new PersistedSetting(
                "workspace.panels",
                """{"bottomHeight":340,"side":"right","title":"إعدادات"}""",
                createdUtc.AddMinutes(1)),
            CancellationToken.None);

        var reader = new SqliteSettingsStore(options);
        var first = await reader.GetProjectSettingAsync(
            firstProjectId,
            "WORKSPACE.PANELS",
            CancellationToken.None);
        var second = await reader.GetProjectSettingAsync(
            secondProjectId,
            "workspace.panels",
            CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Contains("220", first.ValueJson, StringComparison.Ordinal);
        Assert.DoesNotContain("إعدادات", first.ValueJson, StringComparison.Ordinal);
        Assert.Contains("إعدادات", second.ValueJson, StringComparison.Ordinal);
        Assert.Single(await reader.ListProjectSettingsAsync(firstProjectId, CancellationToken.None));
        Assert.Single(await reader.ListProjectSettingsAsync(secondProjectId, CancellationToken.None));
    }

    [Fact]
    public async Task GlobalAndProjectScopesCanReuseKeysAndCaseInsensitiveUpsertsReplaceValues()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-settings-scopes");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 1, 40, 0, TimeSpan.Zero);
        var projectId = await CreateProjectAsync(options, workspace, "scope", createdUtc);
        var store = new SqliteSettingsStore(options);

        await store.UpsertGlobalSettingAsync(
            new PersistedSetting("Appearance.Theme", "\"dark\"", createdUtc),
            CancellationToken.None);
        await store.UpsertGlobalSettingAsync(
            new PersistedSetting("appearance.theme", "\"light\"", createdUtc.AddMinutes(1)),
            CancellationToken.None);
        await store.UpsertProjectSettingAsync(
            projectId,
            new PersistedSetting("Appearance.Theme", "\"system\"", createdUtc),
            CancellationToken.None);
        await store.UpsertProjectSettingAsync(
            projectId,
            new PersistedSetting("appearance.theme", "\"dark\"", createdUtc.AddMinutes(2)),
            CancellationToken.None);

        var globalSettings = await store.ListGlobalSettingsAsync(CancellationToken.None);
        var projectSettings = await store.ListProjectSettingsAsync(projectId, CancellationToken.None);

        var global = Assert.Single(globalSettings);
        var project = Assert.Single(projectSettings);
        Assert.Equal("\"light\"", global.ValueJson);
        Assert.Equal(createdUtc.AddMinutes(1), global.UpdatedUtc);
        Assert.Equal("\"dark\"", project.ValueJson);
        Assert.Equal(createdUtc.AddMinutes(2), project.UpdatedUtc);
    }

    [Fact]
    public async Task SettingsCanBeDeletedAndProjectDeletionCascadesWorkspaceSettings()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-settings-delete");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 2, 0, 0, TimeSpan.Zero);
        var projectId = await CreateProjectAsync(options, workspace, "cascade", createdUtc);
        var store = new SqliteSettingsStore(options);

        await store.UpsertGlobalSettingAsync(
            new PersistedSetting("window.maximized", "true", createdUtc),
            CancellationToken.None);
        await store.UpsertProjectSettingAsync(
            projectId,
            new PersistedSetting("editor.wordWrap", "false", createdUtc),
            CancellationToken.None);

        Assert.True(await store.DeleteGlobalSettingAsync("WINDOW.MAXIMIZED", CancellationToken.None));
        Assert.False(await store.DeleteGlobalSettingAsync("window.maximized", CancellationToken.None));
        Assert.Empty(await store.ListGlobalSettingsAsync(CancellationToken.None));

        await DeleteProjectAsync(options, projectId);

        Assert.Empty(await store.ListProjectSettingsAsync(projectId, CancellationToken.None));
        Assert.False(
            await store.DeleteProjectSettingAsync(
                projectId,
                "editor.wordWrap",
                CancellationToken.None));
    }

    [Fact]
    public async Task InvalidSettingsAndOrphanProjectSettingsAreRejectedWithoutPersistence()
    {
        using var workspace = new TemporaryDirectory("fccd-p03-settings-validation");
        var options = new SqliteDatabaseOptions(workspace.GetPath("state.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        var createdUtc = new DateTimeOffset(2026, 9, 4, 2, 20, 0, TimeSpan.Zero);
        var store = new SqliteSettingsStore(options);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.UpsertGlobalSettingAsync(
                new PersistedSetting("   ", "true", createdUtc),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.UpsertGlobalSettingAsync(
                new PersistedSetting("appearance.theme", "{not-json}", createdUtc),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.UpsertProjectSettingAsync(
                Guid.Empty,
                new PersistedSetting("workspace.layout", "{}", createdUtc),
                CancellationToken.None));

        var orphanFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpsertProjectSettingAsync(
                Guid.NewGuid(),
                new PersistedSetting("workspace.layout", "{}", createdUtc),
                CancellationToken.None));
        Assert.Contains("persist project setting", orphanFailure.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(await store.ListGlobalSettingsAsync(CancellationToken.None));
    }

    private static async Task<Guid> CreateProjectAsync(
        SqliteDatabaseOptions options,
        TemporaryDirectory workspace,
        string name,
        DateTimeOffset createdUtc)
    {
        var projectId = Guid.NewGuid();
        await new SqliteConversationStateStore(options).UpsertProjectAsync(
            new PersistedProject(
                projectId,
                workspace.GetPath($"project-{name}-{projectId:D}"),
                $"Settings {name}",
                createdUtc,
                createdUtc),
            CancellationToken.None);
        return projectId;
    }

    private static async Task DeleteProjectAsync(SqliteDatabaseOptions options, Guid projectId)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);

        await using (var foreignKeys = connection.CreateCommand())
        {
            foreignKeys.CommandText = "PRAGMA foreign_keys = ON;";
            await foreignKeys.ExecuteNonQueryAsync(CancellationToken.None);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Projects WHERE Id = $projectId;";
        command.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
        Assert.Equal(1, await command.ExecuteNonQueryAsync(CancellationToken.None));
    }
}
