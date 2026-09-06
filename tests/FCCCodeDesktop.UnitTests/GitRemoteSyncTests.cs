using FCCCodeDesktop.Application.Git;
using FCCCodeDesktop.Git;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitRemoteSyncTests
{
    [Fact]
    public async Task FetchUpdatesRemoteTrackingRefWithoutChangingHeadOrWorkTree()
    {
        using var workspace = new TemporaryDirectory("fccd-git-fetch");
        var fixture = await InitializeRemoteFixtureAsync(workspace);
        var headBefore = await GetHeadAsync(fixture.ClientPath);
        var bytesBefore = await File.ReadAllBytesAsync(Path.Combine(fixture.ClientPath, "shared.txt"));
        var remoteHead = await CommitAndPushAsync(fixture.SeedPath, "shared.txt", "remote-v2\n", "remote-v2");
        var service = new GitCliRemoteService();

        var result = await service.FetchAsync(fixture.ClientPath, "origin");

        Assert.Equal(GitRemoteSyncStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(GitRemoteSyncKind.Fetch, result.Kind);
        Assert.Equal(headBefore, result.PreviousHead);
        Assert.Equal(headBefore, result.CurrentHead);
        Assert.Equal(headBefore, await GetHeadAsync(fixture.ClientPath));
        Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(Path.Combine(fixture.ClientPath, "shared.txt")));
        Assert.Equal(remoteHead, await GetRefAsync(fixture.ClientPath, "refs/remotes/origin/main"));
    }

    [Fact]
    public async Task PullFastForwardUpdatesCleanBranchToFetchedHead()
    {
        using var workspace = new TemporaryDirectory("fccd-git-pull-ff");
        var fixture = await InitializeRemoteFixtureAsync(workspace);
        var headBefore = await GetHeadAsync(fixture.ClientPath);
        var remoteHead = await CommitAndPushAsync(fixture.SeedPath, "shared.txt", "remote-v2\n", "remote-v2");
        var service = new GitCliRemoteService();

        var result = await service.PullFastForwardAsync(fixture.ClientPath, "origin", "main");

        Assert.Equal(GitRemoteSyncStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(GitRemoteSyncKind.PullFastForward, result.Kind);
        Assert.Equal("main", result.CurrentBranchName);
        Assert.Equal(headBefore, result.PreviousHead);
        Assert.Equal(remoteHead, result.CurrentHead);
        Assert.Equal(remoteHead, await GetHeadAsync(fixture.ClientPath));
        Assert.Equal("remote-v2\n", await File.ReadAllTextAsync(Path.Combine(fixture.ClientPath, "shared.txt")));
        Assert.True((await new GitCliService().GetStatusAsync(fixture.ClientPath)).IsClean);
    }

    [Fact]
    public async Task PullRefusesDirtyTreeWithoutChangingHeadOrOwnerBytes()
    {
        using var workspace = new TemporaryDirectory("fccd-git-pull-dirty");
        var fixture = await InitializeRemoteFixtureAsync(workspace);
        await CommitAndPushAsync(fixture.SeedPath, "remote.txt", "remote\n", "remote-change");
        var ownerPath = Path.Combine(fixture.ClientPath, "shared.txt");
        await File.WriteAllTextAsync(ownerPath, "owner-dirty\n");
        var ownerBytes = await File.ReadAllBytesAsync(ownerPath);
        var headBefore = await GetHeadAsync(fixture.ClientPath);
        var service = new GitCliRemoteService();

        var result = await service.PullFastForwardAsync(fixture.ClientPath, "origin", "main");

        Assert.Equal(GitRemoteSyncStatus.DirtyWorkTree, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(headBefore, await GetHeadAsync(fixture.ClientPath));
        Assert.Equal(ownerBytes, await File.ReadAllBytesAsync(ownerPath));
        Assert.False(File.Exists(Path.Combine(fixture.ClientPath, "remote.txt")));
    }

    [Fact]
    public async Task PullRejectsNonFastForwardWithoutRebaseResetOrWorkTreeRewrite()
    {
        using var workspace = new TemporaryDirectory("fccd-git-pull-diverged");
        var fixture = await InitializeRemoteFixtureAsync(workspace);
        await CommitAndPushAsync(fixture.SeedPath, "remote-only.txt", "remote\n", "remote-only");

        var localOnlyPath = Path.Combine(fixture.ClientPath, "local-only.txt");
        await File.WriteAllTextAsync(localOnlyPath, "local\n");
        await AssertGitSuccessAsync(fixture.ClientPath, "add -- local-only.txt");
        await AssertGitSuccessAsync(fixture.ClientPath, "commit --quiet -m local-only");
        var localHead = await GetHeadAsync(fixture.ClientPath);
        var localBytes = await File.ReadAllBytesAsync(localOnlyPath);
        var service = new GitCliRemoteService();

        var result = await service.PullFastForwardAsync(fixture.ClientPath, "origin", "main");

        Assert.Equal(GitRemoteSyncStatus.NonFastForward, result.Status);
        Assert.Equal(localHead, await GetHeadAsync(fixture.ClientPath));
        Assert.Equal(localBytes, await File.ReadAllBytesAsync(localOnlyPath));
        Assert.False(File.Exists(Path.Combine(fixture.ClientPath, "remote-only.txt")));
        Assert.True((await new GitCliService().GetStatusAsync(fixture.ClientPath)).IsClean);
    }

    [Fact]
    public async Task PullRejectsDetachedHeadAndInvalidTargetsWithoutMutation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-pull-targets");
        var fixture = await InitializeRemoteFixtureAsync(workspace);
        var service = new GitCliRemoteService();
        var headBefore = await GetHeadAsync(fixture.ClientPath);

        var missingRemote = await service.FetchAsync(fixture.ClientPath, "missing");
        Assert.Equal(GitRemoteSyncStatus.RemoteNotFound, missingRemote.Status);

        var invalidRemote = await service.FetchAsync(fixture.ClientPath, "-unsafe");
        Assert.Equal(GitRemoteSyncStatus.InvalidRemoteName, invalidRemote.Status);

        var invalidBranch = await service.PullFastForwardAsync(fixture.ClientPath, "origin", "-unsafe");
        Assert.Equal(GitRemoteSyncStatus.InvalidRemoteBranch, invalidBranch.Status);
        Assert.Equal(headBefore, await GetHeadAsync(fixture.ClientPath));

        await AssertGitSuccessAsync(fixture.ClientPath, "checkout --quiet --detach HEAD");
        var detached = await service.PullFastForwardAsync(fixture.ClientPath, "origin", "main");
        Assert.Equal(GitRemoteSyncStatus.DetachedHead, detached.Status);
        Assert.Equal(headBefore, await GetHeadAsync(fixture.ClientPath));
    }

    [Fact]
    public async Task RemoteSyncReturnsTypedRepositoryAndUnavailableStates()
    {
        using var plain = new TemporaryDirectory("fccd-git-remote-plain");
        var service = new GitCliRemoteService();

        var nonRepository = await service.FetchAsync(plain.Path);
        Assert.Equal(GitRemoteSyncStatus.NotRepository, nonRepository.Status);

        using var bareWorkspace = new TemporaryDirectory("fccd-git-remote-bare");
        var barePath = bareWorkspace.GetPath("repository.git");
        Directory.CreateDirectory(barePath);
        await AssertGitSuccessAsync(barePath, "init --bare --quiet");

        var bare = await service.FetchAsync(barePath);
        Assert.Equal(GitRemoteSyncStatus.BareRepository, bare.Status);

        var missingGit = $"missing-git-{Guid.NewGuid():N}";
        var unavailable = new GitCliRemoteService(missingGit);
        var unavailableResult = await unavailable.FetchAsync(plain.Path);
        Assert.Equal(GitRemoteSyncStatus.GitUnavailable, unavailableResult.Status);
    }

    [Fact]
    public async Task RemoteSyncPropagatesCallerCancellationBeforeMutation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-remote-cancel");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var service = new GitCliRemoteService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.FetchAsync(workspace.Path, cancellationToken: cancellationSource.Token));
    }

    [Fact]
    public void ConstructorRejectsUnsafeRemoteOperationTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliRemoteService(operationTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliRemoteService(
                operationTimeout: GitCliRemoteService.MaximumOperationTimeout + TimeSpan.FromSeconds(1)));
    }

    private static async Task<RemoteFixture> InitializeRemoteFixtureAsync(TemporaryDirectory workspace)
    {
        var remotePath = workspace.GetPath("remote.git");
        var seedPath = workspace.GetPath("seed");
        var clientPath = workspace.GetPath("client");
        Directory.CreateDirectory(remotePath);
        Directory.CreateDirectory(seedPath);

        await AssertGitSuccessAsync(remotePath, "init --bare --quiet");
        await AssertGitSuccessAsync(seedPath, "init --quiet -b main");
        await ConfigureIdentityAsync(seedPath);
        await File.WriteAllTextAsync(Path.Combine(seedPath, "shared.txt"), "baseline\n");
        await AssertGitSuccessAsync(seedPath, "add -- shared.txt");
        await AssertGitSuccessAsync(seedPath, "commit --quiet -m baseline");
        await AssertGitSuccessAsync(seedPath, $"remote add origin \"{remotePath}\"");
        await AssertGitSuccessAsync(seedPath, "push --quiet -u origin main");

        var clone = await TestProcess.RunAsync(
            "git",
            $"clone --quiet --branch main \"{remotePath}\" \"{clientPath}\"",
            workspace.Path);
        Assert.True(clone.ExitCode == 0, clone.StandardError);
        await ConfigureIdentityAsync(clientPath);
        return new RemoteFixture(seedPath, remotePath, clientPath);
    }

    private static async Task<string> CommitAndPushAsync(
        string seedPath,
        string fileName,
        string content,
        string message)
    {
        await File.WriteAllTextAsync(Path.Combine(seedPath, fileName), content);
        await AssertGitSuccessAsync(seedPath, $"add -- \"{fileName}\"");
        await AssertGitSuccessAsync(seedPath, $"commit --quiet -m \"{message}\"");
        await AssertGitSuccessAsync(seedPath, "push --quiet origin main");
        return await GetHeadAsync(seedPath);
    }

    private static async Task ConfigureIdentityAsync(string repositoryPath)
    {
        await AssertGitSuccessAsync(repositoryPath, "config user.email fccd-tests@example.invalid");
        await AssertGitSuccessAsync(repositoryPath, "config user.name FCCD-Tests");
    }

    private static async Task AssertGitSuccessAsync(string workingDirectory, string arguments)
    {
        var result = await TestProcess.RunAsync("git", arguments, workingDirectory);
        Assert.True(result.ExitCode == 0, result.StandardError);
    }

    private static async Task<string> GetHeadAsync(string repositoryPath) =>
        await GetRefAsync(repositoryPath, "HEAD");

    private static async Task<string> GetRefAsync(string repositoryPath, string reference)
    {
        var result = await TestProcess.RunAsync("git", $"rev-parse --verify {reference}", repositoryPath);
        Assert.True(result.ExitCode == 0, result.StandardError);
        var value = result.StandardOutput.Trim();
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value;
    }

    private sealed record RemoteFixture(string SeedPath, string RemotePath, string ClientPath);
}
