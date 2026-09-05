using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Files;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class ProjectCatalogServiceTests
{
    [Fact]
    public async Task OpenProjectPersistsAndReopenReusesIdentityAndRefreshesRecency()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-projects مساحة work");
        var databasePath = workspace.GetPath("state.db");
        var firstRoot = workspace.GetPath("مشروع أول with spaces");
        var secondRoot = workspace.GetPath("second-project");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);

        var options = new SqliteDatabaseOptions(databasePath);
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 9, 5, 20, 30, 0, TimeSpan.Zero));
        var store = new SqliteProjectCatalogStore(options);
        var service = new ProjectCatalogService(store, new SystemProjectDirectoryProbe(), clock);

        var firstOpen = await service.OpenProjectAsync(firstRoot, CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var secondOpen = await service.OpenProjectAsync(secondRoot, CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var reopened = await service.OpenProjectAsync(firstRoot, CancellationToken.None);
        var recent = await service.ListRecentProjectsAsync(20, CancellationToken.None);

        Assert.Equal(firstOpen.Id, reopened.Id);
        Assert.Equal(firstOpen.CreatedUtc, reopened.CreatedUtc);
        Assert.Equal(clock.UtcNow, reopened.UpdatedUtc);
        Assert.Equal(Path.GetFullPath(firstRoot), reopened.RootPath);
        Assert.Equal(new DirectoryInfo(firstRoot).Name, reopened.DisplayName);
        Assert.Equal(2, recent.Count);
        Assert.Equal(reopened.Id, recent[0].Id);
        Assert.Equal(secondOpen.Id, recent[1].Id);

        var recreatedStore = new SqliteProjectCatalogStore(options);
        var persisted = await recreatedStore.FindProjectByRootPathAsync(firstRoot, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(reopened.Id, persisted.Id);
        Assert.Equal(reopened.UpdatedUtc, persisted.UpdatedUtc);
    }

    [Fact]
    public async Task OpenProjectSupportsGitAndNonGitFoldersWithoutTouchingSourceContent()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-git-nongit");
        var databasePath = workspace.GetPath("state.db");
        var nonGitRoot = workspace.GetPath("plain folder");
        var gitRoot = workspace.GetPath("git folder");
        Directory.CreateDirectory(nonGitRoot);
        Directory.CreateDirectory(gitRoot);
        Directory.CreateDirectory(Path.Combine(gitRoot, ".git"));
        var sentinelPath = Path.Combine(nonGitRoot, "sentinel.txt");
        await File.WriteAllTextAsync(sentinelPath, "do-not-change", CancellationToken.None);

        var options = new SqliteDatabaseOptions(databasePath);
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var service = new ProjectCatalogService(
            new SqliteProjectCatalogStore(options),
            new SystemProjectDirectoryProbe(),
            new MutableTimeProvider(new DateTimeOffset(2026, 9, 5, 21, 0, 0, TimeSpan.Zero)));

        var plain = await service.OpenProjectAsync(nonGitRoot, CancellationToken.None);
        var git = await service.OpenProjectAsync(gitRoot, CancellationToken.None);

        Assert.Equal(Path.GetFullPath(nonGitRoot), plain.RootPath);
        Assert.Equal(Path.GetFullPath(gitRoot), git.RootPath);
        Assert.Equal("do-not-change", await File.ReadAllTextAsync(sentinelPath, CancellationToken.None));
        Assert.True(Directory.Exists(Path.Combine(gitRoot, ".git")));
    }

    [Fact]
    public async Task MissingFolderIsRejectedAndNotAddedToRecentProjects()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-missing-project");
        var databasePath = workspace.GetPath("state.db");
        var missingRoot = workspace.GetPath("missing");
        var options = new SqliteDatabaseOptions(databasePath);
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var service = new ProjectCatalogService(
            new SqliteProjectCatalogStore(options),
            new SystemProjectDirectoryProbe());

        var failure = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            service.OpenProjectAsync(missingRoot, CancellationToken.None));
        Assert.Contains("does not exist", failure.Message, StringComparison.OrdinalIgnoreCase);

        var recent = await service.ListRecentProjectsAsync(20, CancellationToken.None);
        Assert.Empty(recent);
    }

    [Fact]
    public async Task RecentProjectLimitIsValidatedAndAppliedDeterministically()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-recent-limit");
        var databasePath = workspace.GetPath("state.db");
        var options = new SqliteDatabaseOptions(databasePath);
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 9, 5, 22, 0, 0, TimeSpan.Zero));
        var service = new ProjectCatalogService(
            new SqliteProjectCatalogStore(options),
            new SystemProjectDirectoryProbe(),
            clock);

        for (var index = 0; index < 3; index++)
        {
            var root = workspace.GetPath($"project-{index}");
            Directory.CreateDirectory(root);
            _ = await service.OpenProjectAsync(root, CancellationToken.None);
            clock.UtcNow = clock.UtcNow.AddMinutes(1);
        }

        var recent = await service.ListRecentProjectsAsync(2, CancellationToken.None);
        Assert.Equal(2, recent.Count);
        Assert.Equal("project-2", recent[0].DisplayName);
        Assert.Equal("project-1", recent[1].DisplayName);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.ListRecentProjectsAsync(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.ListRecentProjectsAsync(ProjectCatalogService.MaximumRecentProjectCount + 1));
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; set; }

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
