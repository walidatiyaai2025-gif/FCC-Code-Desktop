using FCCCodeDesktop.Application.Git;
using FCCCodeDesktop.Git;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitChangeProvenanceTests
{
    [Fact]
    public async Task CleanBaselineClassifiesLaterUnicodeChangeAsCreatedSinceBaseline()
    {
        using var workspace = new TemporaryDirectory("fccd-git-provenance-clean");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        var service = new GitChangeProvenanceService();

        var capture = await service.CaptureBaselineAsync(workspace.Path);
        Assert.True(capture.IsSuccess);
        Assert.NotNull(capture.Baseline);
        Assert.True(capture.Baseline.WasClean);

        var folder = workspace.GetPath("folder with spaces");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "عربي.txt"), "new-change\n");

        var result = await service.CompareAsync(workspace.Path, capture.Baseline);

        Assert.True(result.IsSuccess);
        Assert.False(result.HasPreExistingOverlap);
        Assert.True(result.HasNewChanges);
        var change = Assert.Single(result.CurrentChanges);
        Assert.Equal("folder with spaces/عربي.txt", change.Path);
        Assert.Equal(GitChangeProvenanceOrigin.CreatedSinceBaseline, change.Origin);
        Assert.Empty(change.BaselineMatches);
        Assert.Empty(result.ResolvedPreExistingChanges);
    }

    [Fact]
    public async Task DirtyBaselineKeepsOwnerPathPreExistingAfterAdditionalEdits()
    {
        using var workspace = new TemporaryDirectory("fccd-git-provenance-overlap");
        await InitializeRepositoryAsync(workspace.Path, "owner.txt");
        var ownerPath = workspace.GetPath("owner.txt");
        await File.AppendAllTextAsync(ownerPath, "owner-pre-existing\n");
        var bytesBeforeCapture = await File.ReadAllBytesAsync(ownerPath);
        var service = new GitChangeProvenanceService();

        var capture = await service.CaptureBaselineAsync(workspace.Path);
        Assert.True(capture.IsSuccess);
        Assert.NotNull(capture.Baseline);
        Assert.False(capture.Baseline.WasClean);
        Assert.Equal(bytesBeforeCapture, await File.ReadAllBytesAsync(ownerPath));

        await File.AppendAllTextAsync(ownerPath, "later-overlap\n");
        await File.WriteAllTextAsync(workspace.GetPath("new-agent-path.txt"), "new\n");
        var ownerBytesBeforeCompare = await File.ReadAllBytesAsync(ownerPath);

        var result = await service.CompareAsync(workspace.Path, capture.Baseline);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasPreExistingOverlap);
        Assert.True(result.HasNewChanges);
        var ownerChange = result.CurrentChanges.Single(change => change.Path == "owner.txt");
        Assert.Equal(GitChangeProvenanceOrigin.PreExistingDirty, ownerChange.Origin);
        var baselineMatch = Assert.Single(ownerChange.BaselineMatches);
        Assert.Equal("owner.txt", baselineMatch.Path);
        Assert.Equal(GitFileChangeKind.Modified, baselineMatch.WorkTreeChange);
        var newChange = result.CurrentChanges.Single(change => change.Path == "new-agent-path.txt");
        Assert.Equal(GitChangeProvenanceOrigin.CreatedSinceBaseline, newChange.Origin);
        Assert.Equal(ownerBytesBeforeCompare, await File.ReadAllBytesAsync(ownerPath));
    }

    [Fact]
    public async Task ResolvedPreExistingChangeIsReportedWithoutCurrentOwnershipClaim()
    {
        using var workspace = new TemporaryDirectory("fccd-git-provenance-resolved");
        await InitializeRepositoryAsync(workspace.Path, "owner.txt");
        await File.AppendAllTextAsync(workspace.GetPath("owner.txt"), "pre-existing\n");
        var service = new GitChangeProvenanceService();
        var capture = await service.CaptureBaselineAsync(workspace.Path);
        Assert.True(capture.IsSuccess);
        Assert.NotNull(capture.Baseline);

        await AssertGitSuccessAsync(workspace.Path, "restore -- owner.txt");
        var result = await service.CompareAsync(workspace.Path, capture.Baseline);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.CurrentChanges);
        var resolved = Assert.Single(result.ResolvedPreExistingChanges);
        Assert.Equal("owner.txt", resolved.Path);
        Assert.Equal(GitFileChangeKind.Modified, resolved.WorkTreeChange);
    }

    [Fact]
    public async Task RenameAliasesRemainPreExistingWhenStatusShapeChanges()
    {
        using var workspace = new TemporaryDirectory("fccd-git-provenance-rename");
        await InitializeRepositoryAsync(workspace.Path, "owner.txt");
        await AssertGitSuccessAsync(workspace.Path, "mv -- owner.txt renamed.txt");
        var service = new GitChangeProvenanceService();

        var capture = await service.CaptureBaselineAsync(workspace.Path);
        Assert.True(capture.IsSuccess);
        Assert.NotNull(capture.Baseline);
        var renameBaseline = Assert.Single(capture.Baseline.Entries);
        Assert.Equal("renamed.txt", renameBaseline.Path);
        Assert.Equal("owner.txt", renameBaseline.OriginalPath);
        Assert.Equal(GitFileChangeKind.Renamed, renameBaseline.IndexChange);

        await AssertGitSuccessAsync(workspace.Path, "reset --quiet HEAD -- .");
        var result = await service.CompareAsync(workspace.Path, capture.Baseline);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.CurrentChanges.Count);
        Assert.All(
            result.CurrentChanges,
            change => Assert.Equal(GitChangeProvenanceOrigin.PreExistingDirty, change.Origin));
        Assert.Contains(result.CurrentChanges, change => change.Path == "owner.txt");
        Assert.Contains(result.CurrentChanges, change => change.Path == "renamed.txt");
        Assert.Empty(result.ResolvedPreExistingChanges);
    }

    [Fact]
    public async Task CompareRejectsBaselineCapturedFromDifferentRepository()
    {
        using var first = new TemporaryDirectory("fccd-git-provenance-first");
        using var second = new TemporaryDirectory("fccd-git-provenance-second");
        await InitializeRepositoryAsync(first.Path, "first.txt");
        await InitializeRepositoryAsync(second.Path, "second.txt");
        await File.AppendAllTextAsync(first.GetPath("first.txt"), "dirty\n");
        var service = new GitChangeProvenanceService();
        var capture = await service.CaptureBaselineAsync(first.Path);
        Assert.True(capture.IsSuccess);
        Assert.NotNull(capture.Baseline);

        var result = await service.CompareAsync(second.Path, capture.Baseline);

        Assert.Equal(GitChangeProvenanceQueryStatus.BaselineRepositoryMismatch, result.Status);
        Assert.Empty(result.CurrentChanges);
        Assert.Empty(result.ResolvedPreExistingChanges);
    }

    [Fact]
    public async Task CaptureReturnsTypedRepositoryFailureStates()
    {
        using var plain = new TemporaryDirectory("fccd-git-provenance-plain");
        var service = new GitChangeProvenanceService();

        var nonRepository = await service.CaptureBaselineAsync(plain.Path);
        Assert.Equal(GitChangeProvenanceQueryStatus.NotRepository, nonRepository.Status);

        using var bareWorkspace = new TemporaryDirectory("fccd-git-provenance-bare");
        var barePath = bareWorkspace.GetPath("repository.git");
        Directory.CreateDirectory(barePath);
        await AssertGitSuccessAsync(barePath, "init --bare --quiet");
        var bare = await service.CaptureBaselineAsync(barePath);
        Assert.Equal(GitChangeProvenanceQueryStatus.BareRepository, bare.Status);

        var missingGit = $"missing-git-{Guid.NewGuid():N}";
        var unavailableService = new GitChangeProvenanceService(new GitCliService(missingGit));
        var unavailable = await unavailableService.CaptureBaselineAsync(plain.Path);
        Assert.Equal(GitChangeProvenanceQueryStatus.GitUnavailable, unavailable.Status);
    }

    [Fact]
    public async Task DirtyPathBoundFailsClosedInsteadOfDroppingProvenance()
    {
        using var workspace = new TemporaryDirectory("fccd-git-provenance-bound");
        await InitializeRepositoryAsync(workspace.Path, "tracked.txt");
        await File.WriteAllTextAsync(workspace.GetPath("one.txt"), "one\n");
        await File.WriteAllTextAsync(workspace.GetPath("two.txt"), "two\n");
        var service = new GitChangeProvenanceService(maximumDirtyPaths: 1);

        var capture = await service.CaptureBaselineAsync(workspace.Path);

        Assert.Equal(GitChangeProvenanceQueryStatus.TooManyChanges, capture.Status);
        Assert.Null(capture.Baseline);
    }

    [Fact]
    public async Task CapturePropagatesCallerCancellationBeforeQuery()
    {
        using var workspace = new TemporaryDirectory("fccd-git-provenance-cancel");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var service = new GitChangeProvenanceService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CaptureBaselineAsync(workspace.Path, cancellationSource.Token));
    }

    [Fact]
    public void ConstructorRejectsUnsafeDirtyPathBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitChangeProvenanceService(maximumDirtyPaths: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitChangeProvenanceService(
                maximumDirtyPaths: GitChangeProvenanceService.MaximumDirtyPaths + 1));
    }

    private static async Task InitializeRepositoryAsync(string rootPath, params string[] trackedFiles)
    {
        await AssertGitSuccessAsync(rootPath, "init --quiet");
        await AssertGitSuccessAsync(rootPath, "config user.email fccd-tests@example.invalid");
        await AssertGitSuccessAsync(rootPath, "config user.name FCCD-Tests");

        foreach (var trackedFile in trackedFiles)
        {
            await File.WriteAllTextAsync(Path.Combine(rootPath, trackedFile), $"baseline:{trackedFile}\n");
        }

        await AssertGitSuccessAsync(rootPath, "add -- .");
        await AssertGitSuccessAsync(rootPath, "commit --quiet -m baseline");
    }

    private static async Task AssertGitSuccessAsync(string workingDirectory, string arguments)
    {
        var result = await TestProcess.RunAsync("git", arguments, workingDirectory);
        Assert.True(result.ExitCode == 0, result.StandardError);
    }
}
