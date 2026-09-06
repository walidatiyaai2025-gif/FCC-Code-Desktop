using FCCCodeDesktop.Application.Git;
using FCCCodeDesktop.Git;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitIntegrationConflictScenarioTests
{
    [Fact]
    public async Task CleanWorkflowPullsStagesCommitsPushesAndFinishesClean()
    {
        using var workspace = new TemporaryDirectory("fccd-git-integration-clean");
        var fixture = await InitializeRemoteFixtureAsync(workspace);
        var remoteHead = await CommitAndPushSeedAsync(
            fixture.SeedPath,
            "remote.txt",
            "remote-change\n",
            "remote-change");

        var remoteService = new GitCliRemoteService();
        var pull = await remoteService.PullFastForwardAsync(fixture.ClientPath, "origin", "main");
        Assert.Equal(GitRemoteSyncStatus.Success, pull.Status);
        Assert.Equal(remoteHead, await ReadHeadAsync(fixture.ClientPath));

        var localPath = Path.Combine(fixture.ClientPath, "agent-output.txt");
        await File.WriteAllTextAsync(localPath, "agent-output\n");
        var indexService = new GitCliIndexService();
        var stage = await indexService.StageAsync(fixture.ClientPath, ["agent-output.txt"]);
        Assert.True(stage.IsSuccess);

        var publishService = new GitCliCommitPushService();
        var commit = await publishService.CommitAsync(fixture.ClientPath, "integration workflow");
        Assert.True(commit.IsSuccess);
        var push = await publishService.PushAsync(fixture.ClientPath, "origin");
        Assert.True(push.IsSuccess);

        var localHead = await ReadHeadAsync(fixture.ClientPath);
        Assert.Equal(localHead, await ReadBareBranchHeadAsync(fixture.RemotePath, "main"));
        Assert.True((await new GitCliService().GetStatusAsync(fixture.ClientPath)).IsClean);
    }

    [Fact]
    public async Task DirtyCheckoutRefusalPreservesOwnerBytesAndProvenance()
    {
        using var workspace = new TemporaryDirectory("fccd-git-integration-dirty-checkout");
        await InitializeRepositoryAsync(workspace.Path, "shared.txt");
        var branchService = new GitCliBranchService();
        var mainBranch = await ReadCurrentBranchAsync(workspace.Path);

        Assert.True((await branchService.CreateAndCheckoutAsync(workspace.Path, "feature/conflict")).IsSuccess);
        var sharedPath = workspace.GetPath("shared.txt");
        await File.WriteAllTextAsync(sharedPath, "feature-version\n");
        await AssertGitSuccessAsync(workspace.Path, "add -- shared.txt");
        await AssertGitSuccessAsync(workspace.Path, "commit --quiet -m feature-version");
        Assert.True((await branchService.CheckoutAsync(workspace.Path, mainBranch)).IsSuccess);

        await File.WriteAllTextAsync(sharedPath, "owner-pre-existing-dirty\n");
        var ownerBytes = await File.ReadAllBytesAsync(sharedPath);
        var provenanceService = new GitChangeProvenanceService();
        var capture = await provenanceService.CaptureBaselineAsync(workspace.Path);
        Assert.True(capture.IsSuccess);
        Assert.NotNull(capture.Baseline);
        Assert.False(capture.Baseline.WasClean);

        var checkout = await branchService.CheckoutAsync(workspace.Path, "feature/conflict");

        Assert.Equal(GitBranchMutationStatus.CheckoutBlocked, checkout.Status);
        Assert.Equal(mainBranch, await ReadCurrentBranchAsync(workspace.Path));
        Assert.Equal(ownerBytes, await File.ReadAllBytesAsync(sharedPath));

        var provenance = await provenanceService.CompareAsync(workspace.Path, capture.Baseline);
        Assert.True(provenance.IsSuccess);
        Assert.True(provenance.HasPreExistingOverlap);
        var ownerChange = Assert.Single(provenance.CurrentChanges);
        Assert.Equal("shared.txt", ownerChange.Path);
        Assert.Equal(GitChangeProvenanceOrigin.PreExistingDirty, ownerChange.Origin);
    }

    [Fact]
    public async Task RealMergeConflictRemainsVisibleAndSafetyPolicyCannotSilentlyEraseIt()
    {
        using var workspace = new TemporaryDirectory("fccd-git-integration-merge-conflict");
        await InitializeRepositoryAsync(workspace.Path, "shared.txt");
        var branchService = new GitCliBranchService();
        var mainBranch = await ReadCurrentBranchAsync(workspace.Path);

        Assert.True((await branchService.CreateAndCheckoutAsync(workspace.Path, "feature/conflict")).IsSuccess);
        var sharedPath = workspace.GetPath("shared.txt");
        await File.WriteAllTextAsync(sharedPath, "feature-line\n");
        await AssertGitSuccessAsync(workspace.Path, "add -- shared.txt");
        await AssertGitSuccessAsync(workspace.Path, "commit --quiet -m feature-conflict");
        Assert.True((await branchService.CheckoutAsync(workspace.Path, mainBranch)).IsSuccess);

        await File.WriteAllTextAsync(sharedPath, "main-line\n");
        await AssertGitSuccessAsync(workspace.Path, "add -- shared.txt");
        await AssertGitSuccessAsync(workspace.Path, "commit --quiet -m main-conflict");

        var merge = await TestProcess.RunAsync("git", "merge --no-edit feature/conflict", workspace.Path);
        Assert.NotEqual(0, merge.ExitCode);
        var conflictBytes = await File.ReadAllBytesAsync(sharedPath);

        var status = await new GitCliService().GetStatusAsync(workspace.Path);

        Assert.True(status.IsSuccess);
        Assert.False(status.IsClean);
        var conflicted = Assert.Single(status.Files, entry => entry.Path == "shared.txt");
        Assert.True(conflicted.IsConflicted);
        Assert.Equal(conflictBytes, await File.ReadAllBytesAsync(sharedPath));

        string[][] destructiveCommands =
        [
            ["reset", "--hard", "HEAD"],
            ["clean", "-fdx"],
            ["checkout", "--force", mainBranch],
            ["switch", "--discard-changes", mainBranch],
        ];
        foreach (var command in destructiveCommands)
        {
            Assert.False(GitCommandSafetyPolicy.Evaluate(command).IsAllowed);
            Assert.Throws<InvalidOperationException>(() => GitCommandSafetyPolicy.EnsureAllowed(command));
        }

        Assert.Equal(conflictBytes, await File.ReadAllBytesAsync(sharedPath));
    }

    [Fact]
    public async Task DivergedRemoteRefusesPullAndPushWithoutMovingEitherHead()
    {
        using var workspace = new TemporaryDirectory("fccd-git-integration-diverged");
        var fixture = await InitializeRemoteFixtureAsync(workspace);

        await File.WriteAllTextAsync(Path.Combine(fixture.ClientPath, "local.txt"), "local\n");
        await AssertGitSuccessAsync(fixture.ClientPath, "add -- local.txt");
        await AssertGitSuccessAsync(fixture.ClientPath, "commit --quiet -m local-only");
        var localHead = await ReadHeadAsync(fixture.ClientPath);
        var localBytes = await File.ReadAllBytesAsync(Path.Combine(fixture.ClientPath, "local.txt"));

        var remoteHead = await CommitAndPushSeedAsync(
            fixture.SeedPath,
            "remote.txt",
            "remote\n",
            "remote-only");

        var pull = await new GitCliRemoteService().PullFastForwardAsync(
            fixture.ClientPath,
            "origin",
            "main");
        Assert.Equal(GitRemoteSyncStatus.NonFastForward, pull.Status);
        Assert.Equal(localHead, await ReadHeadAsync(fixture.ClientPath));
        Assert.Equal(localBytes, await File.ReadAllBytesAsync(Path.Combine(fixture.ClientPath, "local.txt")));

        var push = await new GitCliCommitPushService().PushAsync(fixture.ClientPath, "origin");
        Assert.Equal(GitCommitPushStatus.PushRejected, push.Status);
        Assert.Equal(localHead, await ReadHeadAsync(fixture.ClientPath));
        Assert.Equal(remoteHead, await ReadBareBranchHeadAsync(fixture.RemotePath, "main"));
        Assert.Equal(localBytes, await File.ReadAllBytesAsync(Path.Combine(fixture.ClientPath, "local.txt")));
        Assert.True((await new GitCliService().GetStatusAsync(fixture.ClientPath)).IsClean);
    }

    private static async Task InitializeRepositoryAsync(string repositoryPath, params string[] trackedFiles)
    {
        await AssertGitSuccessAsync(repositoryPath, "init --quiet -b main");
        await ConfigureIdentityAsync(repositoryPath);
        foreach (var file in trackedFiles)
        {
            await File.WriteAllTextAsync(Path.Combine(repositoryPath, file), $"baseline:{file}\n");
        }

        await AssertGitSuccessAsync(repositoryPath, "add -- .");
        await AssertGitSuccessAsync(repositoryPath, "commit --quiet -m baseline");
    }

    private static async Task<RemoteFixture> InitializeRemoteFixtureAsync(TemporaryDirectory workspace)
    {
        var remotePath = workspace.GetPath("remote.git");
        var seedPath = workspace.GetPath("seed");
        var clientPath = workspace.GetPath("client");
        Directory.CreateDirectory(remotePath);
        Directory.CreateDirectory(seedPath);

        await AssertGitSuccessAsync(remotePath, "init --bare --quiet");
        await InitializeRepositoryAsync(seedPath, "shared.txt");
        await AssertGitSuccessAsync(seedPath, $"remote add origin \"{NormalizeCommandPath(remotePath)}\"");
        await AssertGitSuccessAsync(seedPath, "push --quiet -u origin main");

        var clone = await TestProcess.RunAsync(
            "git",
            $"clone --quiet --branch main \"{NormalizeCommandPath(remotePath)}\" \"{NormalizeCommandPath(clientPath)}\"",
            workspace.Path);
        Assert.True(clone.ExitCode == 0, clone.StandardError);
        await ConfigureIdentityAsync(clientPath);
        return new RemoteFixture(seedPath, remotePath, clientPath);
    }

    private static async Task<string> CommitAndPushSeedAsync(
        string seedPath,
        string fileName,
        string content,
        string message)
    {
        await File.WriteAllTextAsync(Path.Combine(seedPath, fileName), content);
        await AssertGitSuccessAsync(seedPath, $"add -- \"{fileName}\"");
        await AssertGitSuccessAsync(seedPath, $"commit --quiet -m \"{message}\"");
        await AssertGitSuccessAsync(seedPath, "push --quiet origin main");
        return await ReadHeadAsync(seedPath);
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

    private sealed record RemoteFixture(string SeedPath, string RemotePath, string ClientPath);
}
