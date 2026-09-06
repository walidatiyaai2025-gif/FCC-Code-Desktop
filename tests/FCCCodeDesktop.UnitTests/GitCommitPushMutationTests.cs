using FCCCodeDesktop.Application.Git;
using FCCCodeDesktop.Git;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitCommitPushMutationTests
{
    [Fact]
    public async Task CommitUsesOnlyStagedIndexAndPreservesUnstagedOwnerBytes()
    {
        using var workspace = new TemporaryDirectory("fccd-git-commit");
        await InitializeRepositoryAsync(workspace.Path, "staged.txt", "owner.txt");
        var stagedPath = workspace.GetPath("staged.txt");
        var ownerPath = workspace.GetPath("owner.txt");
        await File.WriteAllTextAsync(stagedPath, "staged-change\n");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- staged.txt", workspace.Path)).ExitCode);
        await File.WriteAllTextAsync(ownerPath, "owner-unstaged-change\n");
        var ownerBefore = await File.ReadAllBytesAsync(ownerPath);
        var headBefore = await ReadHeadAsync(workspace.Path);
        var service = new GitCliCommitPushService();

        var result = await service.CommitAsync(workspace.Path, "bounded commit");

        Assert.Equal(GitCommitPushStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(GitCommitPushKind.Commit, result.Kind);
        Assert.NotEqual(headBefore, result.CommitSha);
        Assert.Equal(result.CommitSha, await ReadHeadAsync(workspace.Path));
        Assert.Equal(ownerBefore, await File.ReadAllBytesAsync(ownerPath));
        Assert.Equal(1, (await TestProcess.RunAsync("git", "diff --quiet -- owner.txt", workspace.Path)).ExitCode);
        Assert.Equal(0, (await TestProcess.RunAsync("git", "diff --cached --quiet --exit-code", workspace.Path)).ExitCode);
        var committedText = await TestProcess.RunAsync("git", "show HEAD:staged.txt", workspace.Path);
        Assert.Equal(0, committedText.ExitCode);
        Assert.Equal("staged-change", committedText.StandardOutput.Trim());
    }

    [Fact]
    public async Task CommitWithoutStagedChangesReturnsTypedFailureWithoutMovingHead()
    {
        using var workspace = new TemporaryDirectory("fccd-git-commit-empty");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        await File.WriteAllTextAsync(workspace.GetPath("tracked.txt"), "unstaged-only\n");
        var headBefore = await ReadHeadAsync(workspace.Path);
        var service = new GitCliCommitPushService();

        var result = await service.CommitAsync(workspace.Path, "must not commit unstaged work");

        Assert.Equal(GitCommitPushStatus.NothingStaged, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(headBefore, await ReadHeadAsync(workspace.Path));
        Assert.Equal("unstaged-only\n", await File.ReadAllTextAsync(workspace.GetPath("tracked.txt")));
    }

    [Fact]
    public async Task CommitRejectsInvalidMessageBeforeMutation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-commit-message");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        await File.WriteAllTextAsync(workspace.GetPath("tracked.txt"), "staged\n");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- tracked.txt", workspace.Path)).ExitCode);
        var headBefore = await ReadHeadAsync(workspace.Path);
        var service = new GitCliCommitPushService();

        var result = await service.CommitAsync(workspace.Path, "   ");

        Assert.Equal(GitCommitPushStatus.InvalidCommitMessage, result.Status);
        Assert.Equal(headBefore, await ReadHeadAsync(workspace.Path));
    }

    [Fact]
    public async Task PushPublishesCurrentBranchToConfiguredBareRemoteWithoutForce()
    {
        using var workspace = new TemporaryDirectory("fccd-git-push");
        using var remoteWorkspace = new TemporaryDirectory("fccd-git-push-remote");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        var barePath = await InitializeBareRemoteAsync(remoteWorkspace.Path);
        await AddRemoteAsync(workspace.Path, barePath);
        var branch = await ReadCurrentBranchAsync(workspace.Path);
        var localHead = await ReadHeadAsync(workspace.Path);
        var service = new GitCliCommitPushService();

        var result = await service.PushAsync(workspace.Path, "origin");

        Assert.Equal(GitCommitPushStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(GitCommitPushKind.Push, result.Kind);
        Assert.Equal(branch, result.CurrentBranchName);
        Assert.Equal(localHead, result.CommitSha);
        Assert.Equal("origin", result.RemoteName);
        Assert.Equal(localHead, await ReadBareBranchHeadAsync(barePath, branch));
    }

    [Fact]
    public async Task PushRejectsNonFastForwardAndPreservesBothHeads()
    {
        using var workspace = new TemporaryDirectory("fccd-git-push-reject");
        using var remoteWorkspace = new TemporaryDirectory("fccd-git-push-reject-remote");
        using var peerWorkspace = new TemporaryDirectory("fccd-git-push-reject-peer");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        var barePath = await InitializeBareRemoteAsync(remoteWorkspace.Path);
        await AddRemoteAsync(workspace.Path, barePath);
        var branch = await ReadCurrentBranchAsync(workspace.Path);
        var service = new GitCliCommitPushService();
        Assert.Equal(GitCommitPushStatus.Success, (await service.PushAsync(workspace.Path)).Status);

        var peerPath = peerWorkspace.GetPath("peer");
        var clone = await TestProcess.RunAsync(
            "git",
            $"clone --quiet \"{NormalizeCommandPath(barePath)}\" \"{NormalizeCommandPath(peerPath)}\"",
            peerWorkspace.Path);
        Assert.True(clone.ExitCode == 0, clone.StandardError);
        await ConfigureIdentityAsync(peerPath);
        await File.WriteAllTextAsync(Path.Combine(peerPath, "peer.txt"), "peer\n");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- peer.txt", peerPath)).ExitCode);
        Assert.Equal(0, (await TestProcess.RunAsync("git", "commit --quiet -m peer", peerPath)).ExitCode);
        Assert.Equal(0, (await TestProcess.RunAsync("git", "push --quiet origin HEAD", peerPath)).ExitCode);
        var remoteHeadBefore = await ReadBareBranchHeadAsync(barePath, branch);

        await File.WriteAllTextAsync(workspace.GetPath("local.txt"), "local\n");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- local.txt", workspace.Path)).ExitCode);
        Assert.Equal(0, (await TestProcess.RunAsync("git", "commit --quiet -m local", workspace.Path)).ExitCode);
        var localHeadBefore = await ReadHeadAsync(workspace.Path);

        var result = await service.PushAsync(workspace.Path);

        Assert.Equal(GitCommitPushStatus.PushRejected, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(localHeadBefore, await ReadHeadAsync(workspace.Path));
        Assert.Equal(remoteHeadBefore, await ReadBareBranchHeadAsync(barePath, branch));
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
    }

    [Fact]
    public async Task PushReturnsTypedMissingRemoteAndDetachedHeadStates()
    {
        using var workspace = new TemporaryDirectory("fccd-git-push-states");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        var service = new GitCliCommitPushService();

        var missing = await service.PushAsync(workspace.Path, "missing");
        Assert.Equal(GitCommitPushStatus.RemoteNotFound, missing.Status);

        Assert.Equal(0, (await TestProcess.RunAsync("git", "checkout --detach --quiet", workspace.Path)).ExitCode);
        var detached = await service.PushAsync(workspace.Path, "origin");
        Assert.Equal(GitCommitPushStatus.DetachedHead, detached.Status);
    }

    [Fact]
    public async Task CommitPushReturnsTypedRepositoryAndUnavailableStates()
    {
        using var plain = new TemporaryDirectory("fccd-git-publish-plain");
        var service = new GitCliCommitPushService();
        Assert.Equal(
            GitCommitPushStatus.NotRepository,
            (await service.CommitAsync(plain.Path, "message")).Status);

        using var bareWorkspace = new TemporaryDirectory("fccd-git-publish-bare");
        var barePath = await InitializeBareRemoteAsync(bareWorkspace.Path);
        Assert.Equal(
            GitCommitPushStatus.BareRepository,
            (await service.PushAsync(barePath)).Status);

        var missingGit = $"missing-git-{Guid.NewGuid():N}";
        var unavailable = new GitCliCommitPushService(missingGit);
        Assert.Equal(
            GitCommitPushStatus.GitUnavailable,
            (await unavailable.CommitAsync(plain.Path, "message")).Status);
    }

    [Fact]
    public async Task CommitPushPropagatesCallerCancellationBeforeMutation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-publish-cancel");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var service = new GitCliCommitPushService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CommitAsync(workspace.Path, "cancelled", cancellationSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.PushAsync(workspace.Path, cancellationToken: cancellationSource.Token));
    }

    [Fact]
    public void ConstructorRejectsUnsafeOperationTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliCommitPushService(operationTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliCommitPushService(
                operationTimeout: GitCliCommitPushService.MaximumOperationTimeout + TimeSpan.FromSeconds(1)));
    }

    private static async Task InitializeRepositoryAsync(string rootPath, params string[] trackedFiles)
    {
        Assert.Equal(0, (await TestProcess.RunAsync("git", "init --quiet", rootPath)).ExitCode);
        await ConfigureIdentityAsync(rootPath);
        foreach (var trackedFile in trackedFiles)
        {
            await File.WriteAllTextAsync(Path.Combine(rootPath, trackedFile), $"baseline:{trackedFile}\n");
        }

        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- .", rootPath)).ExitCode);
        var commit = await TestProcess.RunAsync("git", "commit --quiet -m baseline", rootPath);
        Assert.True(commit.ExitCode == 0, commit.StandardError);
    }

    private static async Task ConfigureIdentityAsync(string repositoryPath)
    {
        Assert.Equal(
            0,
            (await TestProcess.RunAsync("git", "config user.email fccd-tests@example.invalid", repositoryPath)).ExitCode);
        Assert.Equal(
            0,
            (await TestProcess.RunAsync("git", "config user.name FCCD-Tests", repositoryPath)).ExitCode);
    }

    private static async Task<string> InitializeBareRemoteAsync(string rootPath)
    {
        var barePath = Path.Combine(rootPath, "remote.git");
        Directory.CreateDirectory(barePath);
        var init = await TestProcess.RunAsync("git", "init --bare --quiet", barePath);
        Assert.True(init.ExitCode == 0, init.StandardError);
        return barePath;
    }

    private static async Task AddRemoteAsync(string repositoryPath, string barePath)
    {
        var result = await TestProcess.RunAsync(
            "git",
            $"remote add origin \"{NormalizeCommandPath(barePath)}\"",
            repositoryPath);
        Assert.True(result.ExitCode == 0, result.StandardError);
    }

    private static async Task<string> ReadHeadAsync(string repositoryPath)
    {
        var result = await TestProcess.RunAsync("git", "rev-parse --verify HEAD", repositoryPath);
        Assert.True(result.ExitCode == 0, result.StandardError);
        return result.StandardOutput.Trim();
    }

    private static async Task<string> ReadCurrentBranchAsync(string repositoryPath)
    {
        var head = (await File.ReadAllTextAsync(Path.Combine(repositoryPath, ".git", "HEAD"))).Trim();
        const string prefix = "ref: refs/heads/";
        Assert.StartsWith(prefix, head, StringComparison.Ordinal);
        return head[prefix.Length..];
    }

    private static async Task<string> ReadBareBranchHeadAsync(string barePath, string branchName)
    {
        var result = await TestProcess.RunAsync("git", $"rev-parse refs/heads/{branchName}", barePath);
        Assert.True(result.ExitCode == 0, result.StandardError);
        return result.StandardOutput.Trim();
    }

    private static string NormalizeCommandPath(string path) => path.Replace('\\', '/');
}
