using FCCCodeDesktop.Application.Git;
using FCCCodeDesktop.Git;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitIndexMutationTests
{
    [Fact]
    public async Task StageAsyncStagesOnlyRequestedPathsAndPreservesWorkTreeBytes()
    {
        using var workspace = new TemporaryDirectory("fccd-git-stage-selected");
        await InitializeRepositoryAsync(workspace.Path, "selected.txt", "owner.txt");
        await File.AppendAllTextAsync(workspace.GetPath("selected.txt"), "selected-change\n");
        await File.AppendAllTextAsync(workspace.GetPath("owner.txt"), "owner-change\n");

        var unicodeDirectory = workspace.GetPath("folder with spaces");
        Directory.CreateDirectory(unicodeDirectory);
        var unicodePath = Path.Combine(unicodeDirectory, "عربي.txt");
        await File.WriteAllTextAsync(unicodePath, "unicode-change\n");

        var selectedBefore = await File.ReadAllBytesAsync(workspace.GetPath("selected.txt"));
        var ownerBefore = await File.ReadAllBytesAsync(workspace.GetPath("owner.txt"));
        var unicodeBefore = await File.ReadAllBytesAsync(unicodePath);
        var service = new GitCliIndexService();

        var result = await service.StageAsync(
            workspace.Path,
            ["selected.txt", "folder with spaces/عربي.txt"]);
        var status = await new GitCliService().GetStatusAsync(workspace.Path);

        Assert.Equal(GitIndexMutationStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(GitIndexMutationKind.Stage, result.Kind);
        Assert.Equal(2, result.RequestedPaths.Count);
        Assert.Equal(2, result.EffectivePaths.Count);
        Assert.Null(result.FailureMessage);

        var files = status.Files.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        Assert.Equal(GitFileChangeKind.Modified, files["selected.txt"].IndexChange);
        Assert.Equal(GitFileChangeKind.None, files["selected.txt"].WorkTreeChange);
        Assert.Equal(GitFileChangeKind.None, files["owner.txt"].IndexChange);
        Assert.Equal(GitFileChangeKind.Modified, files["owner.txt"].WorkTreeChange);
        Assert.Equal(GitFileChangeKind.Added, files["folder with spaces/عربي.txt"].IndexChange);
        Assert.Equal(GitFileChangeKind.None, files["folder with spaces/عربي.txt"].WorkTreeChange);

        Assert.Equal(selectedBefore, await File.ReadAllBytesAsync(workspace.GetPath("selected.txt")));
        Assert.Equal(ownerBefore, await File.ReadAllBytesAsync(workspace.GetPath("owner.txt")));
        Assert.Equal(unicodeBefore, await File.ReadAllBytesAsync(unicodePath));
    }

    [Fact]
    public async Task UnstageAsyncRestoresIndexOnlyAndPreservesModifiedWorkTree()
    {
        using var workspace = new TemporaryDirectory("fccd-git-unstage-modified");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        await File.AppendAllTextAsync(workspace.GetPath("tracked.txt"), "owner-change\n");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- tracked.txt", workspace.Path)).ExitCode);
        var workTreeBefore = await File.ReadAllBytesAsync(workspace.GetPath("tracked.txt"));
        var service = new GitCliIndexService();

        var result = await service.UnstageAsync(workspace.Path, ["tracked.txt"]);
        var status = await new GitCliService().GetStatusAsync(workspace.Path);

        Assert.Equal(GitIndexMutationStatus.Success, result.Status);
        var tracked = Assert.Single(status.Files);
        Assert.Equal("tracked.txt", tracked.Path);
        Assert.Equal(GitFileChangeKind.None, tracked.IndexChange);
        Assert.Equal(GitFileChangeKind.Modified, tracked.WorkTreeChange);
        Assert.Equal(workTreeBefore, await File.ReadAllBytesAsync(workspace.GetPath("tracked.txt")));
    }

    [Fact]
    public async Task StageAndUnstageDeletionNeverRecreatesDeletedWorkTreeFile()
    {
        using var workspace = new TemporaryDirectory("fccd-git-stage-delete");
        await InitializeRepositoryAsync(workspace.Path, "deleted.txt");
        var deletedPath = workspace.GetPath("deleted.txt");
        File.Delete(deletedPath);
        var service = new GitCliIndexService();

        var stage = await service.StageAsync(workspace.Path, ["deleted.txt"]);
        var stagedStatus = await new GitCliService().GetStatusAsync(workspace.Path);

        Assert.Equal(GitIndexMutationStatus.Success, stage.Status);
        Assert.False(File.Exists(deletedPath));
        var staged = Assert.Single(stagedStatus.Files);
        Assert.Equal(GitFileChangeKind.Deleted, staged.IndexChange);
        Assert.Equal(GitFileChangeKind.None, staged.WorkTreeChange);

        var unstage = await service.UnstageAsync(workspace.Path, ["deleted.txt"]);
        var unstagedStatus = await new GitCliService().GetStatusAsync(workspace.Path);

        Assert.Equal(GitIndexMutationStatus.Success, unstage.Status);
        Assert.False(File.Exists(deletedPath));
        var unstaged = Assert.Single(unstagedStatus.Files);
        Assert.Equal(GitFileChangeKind.None, unstaged.IndexChange);
        Assert.Equal(GitFileChangeKind.Deleted, unstaged.WorkTreeChange);
    }

    [Fact]
    public async Task RenameSelectionExpandsBothSidesForAtomicStageAndUnstage()
    {
        using var workspace = new TemporaryDirectory("fccd-git-stage-rename");
        await InitializeRepositoryAsync(workspace.Path, "rename-source.txt");
        var move = await TestProcess.RunAsync(
            "git",
            "mv -- rename-source.txt renamed-target.txt",
            workspace.Path);
        Assert.Equal(0, move.ExitCode);
        var targetBytes = await File.ReadAllBytesAsync(workspace.GetPath("renamed-target.txt"));
        var service = new GitCliIndexService();

        var unstage = await service.UnstageAsync(workspace.Path, ["renamed-target.txt"]);
        var unstagedStatus = await new GitCliService().GetStatusAsync(workspace.Path);

        Assert.Equal(GitIndexMutationStatus.Success, unstage.Status);
        Assert.Contains("rename-source.txt", unstage.EffectivePaths);
        Assert.Contains("renamed-target.txt", unstage.EffectivePaths);
        Assert.DoesNotContain(unstagedStatus.Files, static entry => entry.IsStaged);
        Assert.False(File.Exists(workspace.GetPath("rename-source.txt")));
        Assert.True(File.Exists(workspace.GetPath("renamed-target.txt")));
        Assert.Equal(targetBytes, await File.ReadAllBytesAsync(workspace.GetPath("renamed-target.txt")));

        var stage = await service.StageAsync(workspace.Path, unstage.EffectivePaths);
        var restagedStatus = await new GitCliService().GetStatusAsync(workspace.Path);

        Assert.Equal(GitIndexMutationStatus.Success, stage.Status);
        Assert.Contains("rename-source.txt", stage.EffectivePaths);
        Assert.Contains("renamed-target.txt", stage.EffectivePaths);
        var renamed = Assert.Single(restagedStatus.Files);
        Assert.True(renamed.IsStaged);
        Assert.Equal(GitFileChangeKind.Renamed, renamed.IndexChange);
        Assert.Equal("rename-source.txt", renamed.OriginalPath);
        Assert.Equal(targetBytes, await File.ReadAllBytesAsync(workspace.GetPath("renamed-target.txt")));
    }

    [Fact]
    public async Task UnstageAsyncSupportsUnbornRepositoryWithoutDeletingWorkTreeFile()
    {
        using var workspace = new TemporaryDirectory("fccd-git-unstage-unborn");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "init --quiet", workspace.Path)).ExitCode);
        var newFile = workspace.GetPath("new file.txt");
        await File.WriteAllTextAsync(newFile, "new-content\n");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- \"new file.txt\"", workspace.Path)).ExitCode);
        var before = await File.ReadAllBytesAsync(newFile);
        var service = new GitCliIndexService();

        var result = await service.UnstageAsync(workspace.Path, ["new file.txt"]);
        var status = await new GitCliService().GetStatusAsync(workspace.Path);

        Assert.Equal(GitIndexMutationStatus.Success, result.Status);
        Assert.True(File.Exists(newFile));
        Assert.Equal(before, await File.ReadAllBytesAsync(newFile));
        var entry = Assert.Single(status.Files);
        Assert.True(entry.IsUntracked);
        Assert.False(entry.IsStaged);
    }

    [Fact]
    public async Task MutationReturnsTypedNonRepositoryBareAndUnavailableStates()
    {
        using var plain = new TemporaryDirectory("fccd-git-mutation-plain");
        var service = new GitCliIndexService();

        var nonRepository = await service.StageAsync(plain.Path, ["file.txt"]);
        Assert.Equal(GitIndexMutationStatus.NotRepository, nonRepository.Status);

        using var bareWorkspace = new TemporaryDirectory("fccd-git-mutation-bare");
        var barePath = bareWorkspace.GetPath("repository.git");
        Directory.CreateDirectory(barePath);
        Assert.Equal(0, (await TestProcess.RunAsync("git", "init --bare --quiet", barePath)).ExitCode);

        var bare = await service.StageAsync(barePath, ["file.txt"]);
        Assert.Equal(GitIndexMutationStatus.BareRepository, bare.Status);

        var missingGit = $"missing-git-{Guid.NewGuid():N}";
        var unavailable = new GitCliIndexService(missingGit);
        var unavailableResult = await unavailable.StageAsync(plain.Path, ["file.txt"]);
        Assert.Equal(GitIndexMutationStatus.GitUnavailable, unavailableResult.Status);
    }

    [Fact]
    public async Task MutationRejectsUnsafePathsetsBeforeInvokingGit()
    {
        using var workspace = new TemporaryDirectory("fccd-git-mutation-paths");
        var service = new GitCliIndexService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.StageAsync(workspace.Path, Array.Empty<string>()));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.StageAsync(workspace.Path, ["../escape.txt"]));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.StageAsync(workspace.Path, [".git/index"]));
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.StageAsync(
                workspace.Path,
                Enumerable.Range(0, GitCliIndexService.MaximumMutationPaths + 1)
                    .Select(static index => $"file-{index}.txt")
                    .ToArray()));
    }

    [Fact]
    public async Task MutationPropagatesCallerCancellation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-mutation-cancel");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var service = new GitCliIndexService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.StageAsync(workspace.Path, ["file.txt"], cancellationSource.Token));
    }

    [Fact]
    public void ConstructorRejectsUnsafeMutationTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliIndexService(mutationTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliIndexService(
                mutationTimeout: GitCliIndexService.MaximumMutationTimeout + TimeSpan.FromSeconds(1)));
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
}
