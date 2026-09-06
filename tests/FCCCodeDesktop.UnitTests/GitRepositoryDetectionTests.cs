using FCCCodeDesktop.Application.Git;
using FCCCodeDesktop.Git;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitRepositoryDetectionTests
{
    [Fact]
    public async Task DetectRepositoryAsyncReturnsNotRepositoryForOrdinaryDirectory()
    {
        using var workspace = new TemporaryDirectory("fccd-git-plain");
        var service = new GitCliService();

        var result = await service.DetectRepositoryAsync(workspace.Path);

        Assert.Equal(GitRepositoryDetectionStatus.NotRepository, result.Status);
        Assert.Null(result.Repository);
        Assert.True(result.GitAvailable);
    }

    [Fact]
    public async Task DetectRepositoryAsyncDetectsWorkTreeFromNestedUnicodePathWithoutMutation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-worktree");
        var init = await TestProcess.RunAsync("git", "init --quiet", workspace.Path);
        Assert.Equal(0, init.ExitCode);

        var nestedPath = workspace.GetPath(Path.Combine("folder with spaces", "عربي"));
        Directory.CreateDirectory(nestedPath);
        var sentinelPath = workspace.GetPath("owner-sentinel.txt");
        await File.WriteAllTextAsync(sentinelPath, "do-not-change");
        var snapshotBefore = Snapshot(workspace.Path);

        var service = new GitCliService();
        var result = await service.DetectRepositoryAsync(nestedPath);

        Assert.Equal(GitRepositoryDetectionStatus.Repository, result.Status);
        var repository = result.Repository;
        Assert.NotNull(repository);
        Assert.Equal(GitRepositoryKind.WorkTree, repository!.Kind);
        AssertPathEqual(workspace.Path, repository.RepositoryRootPath);
        AssertPathEqual(Path.Combine(workspace.Path, ".git"), repository.GitDirectoryPath);
        AssertPathEqual(nestedPath, repository.ProbePath);
        Assert.Equal("do-not-change", await File.ReadAllTextAsync(sentinelPath));
        Assert.Equal(snapshotBefore, Snapshot(workspace.Path));
    }

    [Fact]
    public async Task DetectRepositoryAsyncDetectsBareRepository()
    {
        using var workspace = new TemporaryDirectory("fccd-git-bare");
        var barePath = workspace.GetPath("repository.git");
        Directory.CreateDirectory(barePath);
        var init = await TestProcess.RunAsync("git", "init --bare --quiet", barePath);
        Assert.Equal(0, init.ExitCode);

        var service = new GitCliService();
        var result = await service.DetectRepositoryAsync(barePath);

        Assert.Equal(GitRepositoryDetectionStatus.Repository, result.Status);
        var repository = result.Repository;
        Assert.NotNull(repository);
        Assert.Equal(GitRepositoryKind.Bare, repository!.Kind);
        AssertPathEqual(barePath, repository.RepositoryRootPath);
        AssertPathEqual(barePath, repository.GitDirectoryPath);
    }

    [Fact]
    public async Task DetectRepositoryAsyncReturnsGitUnavailableWhenExecutableCannotStart()
    {
        using var workspace = new TemporaryDirectory("fccd-git-unavailable");
        var service = new GitCliService($"missing-git-{Guid.NewGuid():N}");

        var result = await service.DetectRepositoryAsync(workspace.Path);

        Assert.Equal(GitRepositoryDetectionStatus.GitUnavailable, result.Status);
        Assert.Null(result.Repository);
        Assert.False(result.GitAvailable);
    }

    [Fact]
    public async Task DetectRepositoryAsyncThrowsForMissingDirectory()
    {
        using var workspace = new TemporaryDirectory("fccd-git-missing");
        var missingPath = workspace.GetPath("missing");
        var service = new GitCliService();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => service.DetectRepositoryAsync(missingPath));
    }

    [Fact]
    public async Task DetectRepositoryAsyncPropagatesCallerCancellation()
    {
        using var workspace = new TemporaryDirectory("fccd-git-cancel");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var service = new GitCliService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.DetectRepositoryAsync(workspace.Path, cancellationSource.Token));
    }

    [Fact]
    public void ConstructorRejectsUnsafeProbeTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GitCliService(probeTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliService(probeTimeout: GitCliService.MaximumProbeTimeout + TimeSpan.FromSeconds(1)));
    }

    private static string[] Snapshot(string rootPath) =>
        Directory.EnumerateFileSystemEntries(rootPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(rootPath, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

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
