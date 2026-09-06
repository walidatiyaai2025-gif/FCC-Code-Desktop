using FCCCodeDesktop.Application.Git;
using FCCCodeDesktop.Git;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitBranchMutationTests
{
    [Fact]
    public async Task CreateAndCheckoutCreatesUnicodeBranchAndPreservesOwnerBytes()
    {
        using var workspace = new TemporaryDirectory("fccd-git-branch-create");
        await InitializeRepositoryAsync(workspace.Path, "owner.txt");
        var ownerPath = workspace.GetPath("owner.txt");
        await File.AppendAllTextAsync(ownerPath, "owner-change\n");
        var ownerBefore = await File.ReadAllBytesAsync(ownerPath);
        var previousBranch = await GetCurrentBranchAsync(workspace.Path);
        var service = new GitCliBranchService();

        var result = await service.CreateAndCheckoutAsync(workspace.Path, "feature/تجربة");

        Assert.Equal(GitBranchMutationStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(GitBranchMutationKind.CreateAndCheckout, result.Kind);
        Assert.Equal("feature/تجربة", result.RequestedBranchName);
        Assert.Equal(previousBranch, result.PreviousBranchName);
        Assert.Equal("feature/تجربة", result.CurrentBranchName);
        Assert.Equal("feature/تجربة", await GetCurrentBranchAsync(workspace.Path));
        Assert.Equal(ownerBefore, await File.ReadAllBytesAsync(ownerPath));
    }

    [Fact]
    public async Task CheckoutExistingBranchPreservesUnrelatedDirtyOwnerChange()
    {
        using var workspace = new TemporaryDirectory("fccd-git-branch-checkout");
        await InitializeRepositoryAsync(workspace.Path, "owner.txt", "shared.txt");
        var service = new GitCliBranchService();
        var originalBranch = await GetCurrentBranchAsync(workspace.Path);

        Assert.Equal(
            GitBranchMutationStatus.Success,
            (await service.CreateAndCheckoutAsync(workspace.Path, "feature/safe")).Status);
        await File.WriteAllTextAsync(workspace.GetPath("feature-only.txt"), "feature\n");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- feature-only.txt", workspace.Path)).ExitCode);
        Assert.Equal(0, (await TestProcess.RunAsync("git", "commit --quiet -m feature", workspace.Path)).ExitCode);
        Assert.Equal(
            GitBranchMutationStatus.Success,
            (await service.CheckoutAsync(workspace.Path, originalBranch)).Status);

        var ownerPath = workspace.GetPath("owner.txt");
        await File.AppendAllTextAsync(ownerPath, "local-owner-change\n");
        var ownerBefore = await File.ReadAllBytesAsync(ownerPath);

        var result = await service.CheckoutAsync(workspace.Path, "feature/safe");

        Assert.Equal(GitBranchMutationStatus.Success, result.Status);
        Assert.Equal("feature/safe", await GetCurrentBranchAsync(workspace.Path));
        Assert.Equal(ownerBefore, await File.ReadAllBytesAsync(ownerPath));
    }

    [Fact]
    public async Task CheckoutConflictIsBlockedWithoutChangingBranchOrOwnerBytes()
    {
        using var workspace = new TemporaryDirectory("fccd-git-branch-conflict");
        await InitializeRepositoryAsync(workspace.Path, "shared.txt");
        var service = new GitCliBranchService();
        var originalBranch = await GetCurrentBranchAsync(workspace.Path);

        Assert.Equal(
            GitBranchMutationStatus.Success,
            (await service.CreateAndCheckoutAsync(workspace.Path, "feature/conflict")).Status);
        await File.WriteAllTextAsync(workspace.GetPath("shared.txt"), "feature-version\n");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- shared.txt", workspace.Path)).ExitCode);
        Assert.Equal(0, (await TestProcess.RunAsync("git", "commit --quiet -m feature-change", workspace.Path)).ExitCode);
        Assert.Equal(
            GitBranchMutationStatus.Success,
            (await service.CheckoutAsync(workspace.Path, originalBranch)).Status);

        var sharedPath = workspace.GetPath("shared.txt");
        await File.WriteAllTextAsync(sharedPath, "owner-uncommitted-version\n");
        var bytesBefore = await File.ReadAllBytesAsync(sharedPath);

        var result = await service.CheckoutAsync(workspace.Path, "feature/conflict");

        Assert.Equal(GitBranchMutationStatus.CheckoutBlocked, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(originalBranch, await GetCurrentBranchAsync(workspace.Path));
        Assert.Equal(originalBranch, result.CurrentBranchName);
        Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(sharedPath));
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
    }

    [Fact]
    public async Task CreateAndCheckoutRejectsExistingAndInvalidNamesWithoutMutation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-branch-invalid");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        var service = new GitCliBranchService();
        var originalBranch = await GetCurrentBranchAsync(workspace.Path);

        var invalid = await service.CreateAndCheckoutAsync(workspace.Path, "-unsafe");
        Assert.Equal(GitBranchMutationStatus.InvalidBranchName, invalid.Status);
        Assert.Equal(originalBranch, await GetCurrentBranchAsync(workspace.Path));

        var created = await service.CreateAndCheckoutAsync(workspace.Path, "feature/existing");
        Assert.Equal(GitBranchMutationStatus.Success, created.Status);

        var existing = await service.CreateAndCheckoutAsync(workspace.Path, "feature/existing");
        Assert.Equal(GitBranchMutationStatus.BranchAlreadyExists, existing.Status);
        Assert.Equal("feature/existing", await GetCurrentBranchAsync(workspace.Path));
    }

    [Fact]
    public async Task CheckoutMissingBranchReturnsTypedFailureWithoutMutation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-branch-missing");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        var service = new GitCliBranchService();
        var originalBranch = await GetCurrentBranchAsync(workspace.Path);

        var result = await service.CheckoutAsync(workspace.Path, "feature/missing");

        Assert.Equal(GitBranchMutationStatus.BranchNotFound, result.Status);
        Assert.Equal(originalBranch, result.PreviousBranchName);
        Assert.Equal(originalBranch, result.CurrentBranchName);
        Assert.Equal(originalBranch, await GetCurrentBranchAsync(workspace.Path));
    }

    [Fact]
    public async Task BranchMutationReturnsTypedNonRepositoryBareAndUnavailableStates()
    {
        using var plain = new TemporaryDirectory("fccd-git-branch-plain");
        var service = new GitCliBranchService();

        var nonRepository = await service.CreateAndCheckoutAsync(plain.Path, "feature/test");
        Assert.Equal(GitBranchMutationStatus.NotRepository, nonRepository.Status);

        using var bareWorkspace = new TemporaryDirectory("fccd-git-branch-bare");
        var barePath = bareWorkspace.GetPath("repository.git");
        Directory.CreateDirectory(barePath);
        Assert.Equal(0, (await TestProcess.RunAsync("git", "init --bare --quiet", barePath)).ExitCode);

        var bare = await service.CreateAndCheckoutAsync(barePath, "feature/test");
        Assert.Equal(GitBranchMutationStatus.BareRepository, bare.Status);

        var missingGit = $"missing-git-{Guid.NewGuid():N}";
        var unavailable = new GitCliBranchService(missingGit);
        var unavailableResult = await unavailable.CreateAndCheckoutAsync(plain.Path, "feature/test");
        Assert.Equal(GitBranchMutationStatus.GitUnavailable, unavailableResult.Status);
    }

    [Fact]
    public async Task BranchMutationPropagatesCallerCancellationBeforeMutation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-branch-cancel");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var service = new GitCliBranchService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateAndCheckoutAsync(
                workspace.Path,
                "feature/cancelled",
                cancellationSource.Token));
    }

    [Fact]
    public void ConstructorRejectsUnsafeOperationTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliBranchService(operationTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliBranchService(
                operationTimeout: GitCliBranchService.MaximumOperationTimeout + TimeSpan.FromSeconds(1)));
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

    private static async Task<string> GetCurrentBranchAsync(string repositoryPath)
    {
        var result = await TestProcess.RunAsync("git", "branch --show-current", repositoryPath);
        Assert.Equal(0, result.ExitCode);
        var branch = result.StandardOutput.Trim();
        Assert.False(string.IsNullOrWhiteSpace(branch));
        return branch;
    }
}
