using FCCCodeDesktop.Application.Git;
using FCCCodeDesktop.Git;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitStatusTests
{
    [Fact]
    public async Task GetStatusAsyncReportsCleanRepositoryFromNestedPath()
    {
        using var workspace = new TemporaryDirectory("fccd-git-status-clean");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        var nested = workspace.GetPath(Path.Combine("src", "nested"));
        Directory.CreateDirectory(nested);
        var service = new GitCliService();

        var result = await service.GetStatusAsync(nested);

        Assert.Equal(GitStatusQueryStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.True(result.IsClean);
        Assert.Empty(result.Files);
        AssertPathEqual(workspace.Path, result.RepositoryRootPath!);
    }

    [Fact]
    public async Task GetStatusAsyncReportsStagedWorkTreeRenameDeleteAndUnicodeUntrackedChanges()
    {
        using var workspace = new TemporaryDirectory("fccd-git-status-mixed");
        await InitializeRepositoryAsync(
            workspace.Path,
            "tracked.txt",
            "rename-source.txt",
            "deleted.txt");

        await File.AppendAllTextAsync(workspace.GetPath("tracked.txt"), "worktree-change\n");
        File.Delete(workspace.GetPath("deleted.txt"));

        var rename = await TestProcess.RunAsync(
            "git",
            "mv -- rename-source.txt renamed-target.txt",
            workspace.Path);
        Assert.Equal(0, rename.ExitCode);

        await File.WriteAllTextAsync(workspace.GetPath("staged.txt"), "staged\n");
        var add = await TestProcess.RunAsync("git", "add -- staged.txt", workspace.Path);
        Assert.Equal(0, add.ExitCode);

        var unicodeDirectory = workspace.GetPath("folder with spaces");
        Directory.CreateDirectory(unicodeDirectory);
        await File.WriteAllTextAsync(Path.Combine(unicodeDirectory, "عربي.txt"), "untracked\n");

        var service = new GitCliService();
        var result = await service.GetStatusAsync(workspace.Path);

        Assert.Equal(GitStatusQueryStatus.Success, result.Status);
        Assert.False(result.IsClean);
        AssertPathEqual(workspace.Path, result.RepositoryRootPath!);

        var files = result.Files.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        Assert.Equal(5, files.Count);

        AssertEntry(
            files["tracked.txt"],
            GitFileChangeKind.None,
            GitFileChangeKind.Modified);
        AssertEntry(
            files["deleted.txt"],
            GitFileChangeKind.None,
            GitFileChangeKind.Deleted);
        AssertEntry(
            files["staged.txt"],
            GitFileChangeKind.Added,
            GitFileChangeKind.None);

        var renamed = files["renamed-target.txt"];
        AssertEntry(renamed, GitFileChangeKind.Renamed, GitFileChangeKind.None);
        Assert.Equal("rename-source.txt", renamed.OriginalPath);
        Assert.True(renamed.IsStaged);

        var untracked = files["folder with spaces/عربي.txt"];
        AssertEntry(untracked, GitFileChangeKind.None, GitFileChangeKind.Untracked);
        Assert.True(untracked.IsUntracked);
        Assert.True(untracked.HasWorkTreeChange);
    }

    [Fact]
    public async Task GetStatusAsyncDoesNotRefreshOrRewriteGitIndex()
    {
        using var workspace = new TemporaryDirectory("fccd-git-status-readonly");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        await File.AppendAllTextAsync(workspace.GetPath("tracked.txt"), "owner-change\n");

        var indexPath = workspace.GetPath(Path.Combine(".git", "index"));
        var indexBefore = await File.ReadAllBytesAsync(indexPath);
        var service = new GitCliService();

        var result = await service.GetStatusAsync(workspace.Path);
        var indexAfter = await File.ReadAllBytesAsync(indexPath);

        Assert.Equal(GitStatusQueryStatus.Success, result.Status);
        Assert.Equal(indexBefore, indexAfter);
        Assert.Single(result.Files);
        Assert.Equal("tracked.txt", result.Files[0].Path);
    }

    [Fact]
    public async Task GetStatusAsyncReturnsTypedNonRepositoryBareAndUnavailableStates()
    {
        using var plain = new TemporaryDirectory("fccd-git-status-plain");
        var service = new GitCliService();

        var plainResult = await service.GetStatusAsync(plain.Path);

        Assert.Equal(GitStatusQueryStatus.NotRepository, plainResult.Status);
        Assert.Empty(plainResult.Files);
        Assert.Null(plainResult.RepositoryRootPath);

        using var bareWorkspace = new TemporaryDirectory("fccd-git-status-bare");
        var barePath = bareWorkspace.GetPath("repository.git");
        Directory.CreateDirectory(barePath);
        var initBare = await TestProcess.RunAsync("git", "init --bare --quiet", barePath);
        Assert.Equal(0, initBare.ExitCode);

        var bareResult = await service.GetStatusAsync(barePath);

        Assert.Equal(GitStatusQueryStatus.BareRepository, bareResult.Status);
        Assert.Empty(bareResult.Files);
        AssertPathEqual(barePath, bareResult.RepositoryRootPath!);

        var unavailable = new GitCliService($"missing-git-{Guid.NewGuid():N}");
        var unavailableResult = await unavailable.GetStatusAsync(plain.Path);

        Assert.Equal(GitStatusQueryStatus.GitUnavailable, unavailableResult.Status);
        Assert.Empty(unavailableResult.Files);
    }

    [Fact]
    public async Task GetStatusAsyncPropagatesCallerCancellation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-status-cancel");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var service = new GitCliService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetStatusAsync(workspace.Path, cancellationSource.Token));
    }

    [Fact]
    public void ConstructorRejectsUnsafeStatusTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliService(statusTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliService(
                statusTimeout: GitCliService.MaximumStatusTimeout + TimeSpan.FromSeconds(1)));
    }

    private static async Task InitializeRepositoryAsync(string rootPath, params string[] trackedFiles)
    {
        Assert.Equal(0, (await TestProcess.RunAsync("git", "init --quiet", rootPath)).ExitCode);
        Assert.Equal(
            0,
            (await TestProcess.RunAsync(
                "git",
                "config user.email fccd-tests@example.invalid",
                rootPath)).ExitCode);
        Assert.Equal(
            0,
            (await TestProcess.RunAsync(
                "git",
                "config user.name FCCD-Tests",
                rootPath)).ExitCode);

        foreach (var trackedFile in trackedFiles)
        {
            await File.WriteAllTextAsync(Path.Combine(rootPath, trackedFile), $"baseline:{trackedFile}\n");
        }

        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- .", rootPath)).ExitCode);
        var commit = await TestProcess.RunAsync("git", "commit --quiet -m baseline", rootPath);
        Assert.True(commit.ExitCode == 0, commit.StandardError);
    }

    private static void AssertEntry(
        GitFileStatusEntry entry,
        GitFileChangeKind indexChange,
        GitFileChangeKind workTreeChange)
    {
        Assert.Equal(indexChange, entry.IndexChange);
        Assert.Equal(workTreeChange, entry.WorkTreeChange);
    }

    private static void AssertPathEqual(string expected, string actual)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var expectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected));
        var actualPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(actual));
        Assert.True(
            string.Equals(expectedPath, actualPath, comparison),
            $"Expected path '{expectedPath}' but found '{actualPath}'.");
    }
}
