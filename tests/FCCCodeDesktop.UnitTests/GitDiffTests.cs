using FCCCodeDesktop.Application.Git;
using FCCCodeDesktop.Git;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitDiffTests
{
    [Fact]
    public async Task GetDiffAsyncSeparatesStagedAndWorkTreeChangesWithoutMutatingIndex()
    {
        using var workspace = new TemporaryDirectory("fccd-git-diff-split");
        await InitializeRepositoryAsync(workspace.Path, ("tracked.txt", "baseline\n"));

        await File.WriteAllTextAsync(workspace.GetPath("tracked.txt"), "baseline\nstaged\n");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- tracked.txt", workspace.Path)).ExitCode);
        await File.AppendAllTextAsync(workspace.GetPath("tracked.txt"), "worktree\n");

        var indexPath = workspace.GetPath(Path.Combine(".git", "index"));
        var indexBefore = await File.ReadAllBytesAsync(indexPath);
        var service = new GitCliService();

        var result = await service.GetDiffAsync(workspace.Path, "tracked.txt");
        var indexAfter = await File.ReadAllBytesAsync(indexPath);

        Assert.Equal(GitDiffQueryStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.True(result.HasChanges);
        Assert.Equal("tracked.txt", result.RepositoryRelativePath);
        Assert.Equal(indexBefore, indexAfter);

        Assert.Equal(GitDiffSectionKind.Staged, result.Staged.Kind);
        Assert.True(result.Staged.HasChanges);
        Assert.Contains("+staged", result.Staged.Patch, StringComparison.Ordinal);
        Assert.DoesNotContain("+worktree", result.Staged.Patch, StringComparison.Ordinal);

        Assert.Equal(GitDiffSectionKind.WorkTree, result.WorkTree.Kind);
        Assert.True(result.WorkTree.HasChanges);
        Assert.Contains("+worktree", result.WorkTree.Patch, StringComparison.Ordinal);
        Assert.False(result.WorkTree.IsNewFile);
    }

    [Fact]
    public async Task GetDiffAsyncPreservesUnicodeAndSpaceContainingRepositoryPaths()
    {
        using var workspace = new TemporaryDirectory("fccd-git-diff-unicode");
        const string relativePath = "folder with spaces/عربي.txt";
        await InitializeRepositoryAsync(workspace.Path, (relativePath, "baseline\n"));
        await File.AppendAllTextAsync(workspace.GetPath(relativePath), "تغيير\n");
        var service = new GitCliService();

        var result = await service.GetDiffAsync(workspace.Path, relativePath);

        Assert.Equal(GitDiffQueryStatus.Success, result.Status);
        Assert.Equal(relativePath, result.RepositoryRelativePath);
        Assert.True(result.WorkTree.HasChanges);
        Assert.Contains("عربي.txt", result.WorkTree.Patch, StringComparison.Ordinal);
        Assert.Contains("+تغيير", result.WorkTree.Patch, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDiffAsyncProducesReadOnlyAdditionViewForUntrackedFilesIncludingEmptyFiles()
    {
        using var workspace = new TemporaryDirectory("fccd-git-diff-untracked");
        await InitializeRepositoryAsync(workspace.Path, ("tracked.txt", "baseline\n"));
        var directory = workspace.GetPath("new folder");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "جديد.txt"), "hello\n");
        await File.WriteAllTextAsync(workspace.GetPath("empty.txt"), string.Empty);
        var service = new GitCliService();

        var populated = await service.GetDiffAsync(workspace.Path, "new folder/جديد.txt");
        var empty = await service.GetDiffAsync(workspace.Path, "empty.txt");

        Assert.Equal(GitDiffQueryStatus.Success, populated.Status);
        Assert.False(populated.Staged.HasChanges);
        Assert.True(populated.WorkTree.IsNewFile);
        Assert.True(populated.WorkTree.HasChanges);
        Assert.Contains("+hello", populated.WorkTree.Patch, StringComparison.Ordinal);

        Assert.Equal(GitDiffQueryStatus.Success, empty.Status);
        Assert.True(empty.WorkTree.IsNewFile);
        Assert.True(empty.WorkTree.HasChanges);
    }

    [Fact]
    public async Task GetDiffAsyncClassifiesBinaryTrackedChanges()
    {
        using var workspace = new TemporaryDirectory("fccd-git-diff-binary");
        await InitializeRepositoryAsync(workspace.Path, ("binary.bin", "baseline\0payload"));
        await File.WriteAllBytesAsync(
            workspace.GetPath("binary.bin"),
            [0x00, 0x10, 0x20, 0x30, 0x40, 0x50]);
        var service = new GitCliService();

        var result = await service.GetDiffAsync(workspace.Path, "binary.bin");

        Assert.Equal(GitDiffQueryStatus.Success, result.Status);
        Assert.True(result.WorkTree.HasChanges);
        Assert.True(result.WorkTree.IsBinary);
    }

    [Fact]
    public async Task GetDiffAsyncFailsClosedWhenPatchExceedsConfiguredMaterializationBound()
    {
        using var workspace = new TemporaryDirectory("fccd-git-diff-bound");
        await InitializeRepositoryAsync(workspace.Path, ("large.txt", "baseline\n"));
        var largeContent = string.Join(
            '\n',
            Enumerable.Range(0, 400).Select(index => $"changed-line-{index:D4}-abcdefghijklmnopqrstuvwxyz"));
        await File.WriteAllTextAsync(workspace.GetPath("large.txt"), largeContent);
        var service = new GitCliService(maxDiffCharacters: 256);

        var result = await service.GetDiffAsync(workspace.Path, "large.txt");

        Assert.Equal(GitDiffQueryStatus.TooLarge, result.Status);
        Assert.True(result.WasTruncated);
        Assert.True(result.WorkTree.WasTruncated);
        Assert.Empty(result.WorkTree.Patch);
    }

    [Fact]
    public async Task GetDiffAsyncReturnsTypedRepositoryAndGitAvailabilityStates()
    {
        using var plain = new TemporaryDirectory("fccd-git-diff-plain");
        var service = new GitCliService();

        var plainResult = await service.GetDiffAsync(plain.Path, "file.txt");

        Assert.Equal(GitDiffQueryStatus.NotRepository, plainResult.Status);

        using var bareWorkspace = new TemporaryDirectory("fccd-git-diff-bare");
        var barePath = bareWorkspace.GetPath("repository.git");
        Directory.CreateDirectory(barePath);
        Assert.Equal(0, (await TestProcess.RunAsync("git", "init --bare --quiet", barePath)).ExitCode);

        var bareResult = await service.GetDiffAsync(barePath, "file.txt");

        Assert.Equal(GitDiffQueryStatus.BareRepository, bareResult.Status);

        var unavailable = new GitCliService($"missing-git-{Guid.NewGuid():N}");
        var unavailableResult = await unavailable.GetDiffAsync(plain.Path, "file.txt");

        Assert.Equal(GitDiffQueryStatus.GitUnavailable, unavailableResult.Status);
    }

    [Fact]
    public async Task GetDiffAsyncRejectsEscapingPathsAndPropagatesCallerCancellation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-diff-safety");
        await InitializeRepositoryAsync(workspace.Path, ("tracked.txt", "baseline\n"));
        var service = new GitCliService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetDiffAsync(workspace.Path, "../outside.txt"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetDiffAsync(workspace.Path, "/absolute.txt"));

        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetDiffAsync(workspace.Path, "tracked.txt", cancellationSource.Token));
    }

    [Fact]
    public void ConstructorRejectsUnsafeDiffBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliService(diffTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliService(diffTimeout: GitCliService.MaximumDiffTimeout + TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliService(maxDiffCharacters: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliService(maxDiffCharacters: GitCliService.MaximumDiffCharacters + 1));
    }

    private static async Task InitializeRepositoryAsync(
        string rootPath,
        params (string Path, string Content)[] files)
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

        foreach (var file in files)
        {
            var filePath = Path.Combine(rootPath, file.Path);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, file.Content);
        }

        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- .", rootPath)).ExitCode);
        var commit = await TestProcess.RunAsync("git", "commit --quiet -m baseline", rootPath);
        Assert.True(commit.ExitCode == 0, commit.StandardError);
    }
}
