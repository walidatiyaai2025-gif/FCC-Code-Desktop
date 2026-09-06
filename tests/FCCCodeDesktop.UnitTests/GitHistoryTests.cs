using System.Text;
using FCCCodeDesktop.Application.Git;
using FCCCodeDesktop.Git;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitHistoryTests
{
    [Fact]
    public async Task HistoryReturnsNewestFirstUnicodeMetadataAndStableCursor()
    {
        using var workspace = new TemporaryDirectory("fccd-git-history-page");
        await InitializeRepositoryAsync(workspace.Path);

        await File.WriteAllTextAsync(workspace.GetPath("second.txt"), "second\n");
        await StageAllAndCommitAsync(workspace.Path, "رسالة ثانية");
        await File.AppendAllTextAsync(workspace.GetPath("base.txt"), "third\n");
        await StageAllAndCommitAsync(workspace.Path, "third");

        var service = new GitCliHistoryService();
        var firstPage = await service.GetHistoryAsync(
            workspace.Path,
            new GitHistoryQuery(MaxCount: 2));

        Assert.Equal(GitHistoryStatus.Success, firstPage.Status);
        Assert.True(firstPage.IsSuccess);
        Assert.True(firstPage.HasMore);
        Assert.Equal(2, firstPage.Commits.Count);
        Assert.Equal("third", firstPage.Commits[0].Subject);
        Assert.Equal("رسالة ثانية", firstPage.Commits[1].Subject);
        Assert.Single(firstPage.Commits[0].ParentShas);
        Assert.Equal(firstPage.Commits[1].Sha, firstPage.Commits[0].ParentShas[0]);
        Assert.Equal(firstPage.Commits[1].Sha, firstPage.NextCursorSha);
        Assert.Equal("FCCD-Tests", firstPage.Commits[0].AuthorName);
        Assert.Equal("fccd-tests@example.invalid", firstPage.Commits[0].AuthorEmail);
        Assert.NotEqual(default, firstPage.Commits[0].AuthorDate);

        var secondPage = await service.GetHistoryAsync(
            workspace.Path,
            new GitHistoryQuery(MaxCount: 2, BeforeCommitSha: firstPage.NextCursorSha));

        Assert.Equal(GitHistoryStatus.Success, secondPage.Status);
        Assert.False(secondPage.HasMore);
        Assert.Single(secondPage.Commits);
        Assert.Equal("baseline", secondPage.Commits[0].Subject);
    }

    [Fact]
    public async Task HistoryPathFilterUsesLiteralPathspec()
    {
        using var workspace = new TemporaryDirectory("fccd-git-history-path");
        await InitializeRepositoryAsync(workspace.Path, "literal[1].txt", "literal1.txt");

        await File.AppendAllTextAsync(workspace.GetPath("literal1.txt"), "similar-only\n");
        await StageAllAndCommitAsync(workspace.Path, "similar");
        await File.AppendAllTextAsync(workspace.GetPath("literal[1].txt"), "literal-only\n");
        await StageAllAndCommitAsync(workspace.Path, "literal");

        var service = new GitCliHistoryService();
        var result = await service.GetHistoryAsync(
            workspace.Path,
            new GitHistoryQuery(MaxCount: 10, RelativePath: "literal[1].txt"));

        Assert.Equal(GitHistoryStatus.Success, result.Status);
        Assert.Equal(2, result.Commits.Count);
        Assert.Equal("literal", result.Commits[0].Subject);
        Assert.Equal("baseline", result.Commits[1].Subject);
        Assert.DoesNotContain(result.Commits, static commit => commit.Subject == "similar");
    }

    [Fact]
    public async Task HistorySupportsBareRepositoryWithoutMutation()
    {
        using var source = new TemporaryDirectory("fccd-git-history-bare-source");
        await InitializeRepositoryAsync(source.Path);
        await File.AppendAllTextAsync(source.GetPath("base.txt"), "second\n");
        await StageAllAndCommitAsync(source.Path, "second");

        using var bareWorkspace = new TemporaryDirectory("fccd-git-history-bare");
        var barePath = bareWorkspace.GetPath("repository.git");
        var clone = await TestProcess.RunAsync(
            "git",
            $"clone --bare --quiet . \"{barePath}\"",
            source.Path);
        Assert.True(clone.ExitCode == 0, clone.StandardError);

        var service = new GitCliHistoryService();
        var result = await service.GetHistoryAsync(barePath, new GitHistoryQuery(MaxCount: 10));

        Assert.Equal(GitHistoryStatus.Success, result.Status);
        Assert.Equal(2, result.Commits.Count);
        Assert.Equal("second", result.Commits[0].Subject);
        Assert.Equal("baseline", result.Commits[1].Subject);
    }

    [Fact]
    public async Task HistoryReturnsTypedEmptyNonRepositoryAndUnavailableStates()
    {
        using var plain = new TemporaryDirectory("fccd-git-history-plain");
        var service = new GitCliHistoryService();

        var nonRepository = await service.GetHistoryAsync(plain.Path);
        Assert.Equal(GitHistoryStatus.NotRepository, nonRepository.Status);

        using var empty = new TemporaryDirectory("fccd-git-history-empty");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "init --quiet", empty.Path)).ExitCode);
        var emptyResult = await service.GetHistoryAsync(empty.Path);
        Assert.Equal(GitHistoryStatus.EmptyRepository, emptyResult.Status);
        Assert.True(emptyResult.IsSuccess);
        Assert.Empty(emptyResult.Commits);

        var missingGit = new GitCliHistoryService($"missing-git-{Guid.NewGuid():N}");
        var unavailable = await missingGit.GetHistoryAsync(plain.Path);
        Assert.Equal(GitHistoryStatus.GitUnavailable, unavailable.Status);
    }

    [Fact]
    public async Task HistoryRejectsUnsafeOrInvalidQueries()
    {
        using var workspace = new TemporaryDirectory("fccd-git-history-invalid");
        await InitializeRepositoryAsync(workspace.Path);
        var service = new GitCliHistoryService();

        Assert.Equal(
            GitHistoryStatus.InvalidQuery,
            (await service.GetHistoryAsync(workspace.Path, new GitHistoryQuery(MaxCount: 0))).Status);
        Assert.Equal(
            GitHistoryStatus.InvalidQuery,
            (await service.GetHistoryAsync(
                workspace.Path,
                new GitHistoryQuery(RelativePath: "../escape.txt"))).Status);
        Assert.Equal(
            GitHistoryStatus.InvalidQuery,
            (await service.GetHistoryAsync(
                workspace.Path,
                new GitHistoryQuery(RelativePath: ".git/config"))).Status);
        Assert.Equal(
            GitHistoryStatus.InvalidQuery,
            (await service.GetHistoryAsync(
                workspace.Path,
                new GitHistoryQuery(BeforeCommitSha: "not-a-full-object-id"))).Status);
        Assert.Equal(
            GitHistoryStatus.InvalidQuery,
            (await service.GetHistoryAsync(
                workspace.Path,
                new GitHistoryQuery(BeforeCommitSha: new string('f', 40)))).Status);
    }

    [Fact]
    public async Task HistoryOutputLimitReturnsTypedTooLarge()
    {
        using var workspace = new TemporaryDirectory("fccd-git-history-limit");
        await InitializeRepositoryAsync(workspace.Path);
        var service = new GitCliHistoryService(maximumOutputCharacters: 32);

        var result = await service.GetHistoryAsync(workspace.Path);

        Assert.Equal(GitHistoryStatus.TooLarge, result.Status);
        Assert.Empty(result.Commits);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
    }

    [Fact]
    public async Task HistoryPreservesDirtyWorkTreeAndIndexBytes()
    {
        using var workspace = new TemporaryDirectory("fccd-git-history-readonly");
        await InitializeRepositoryAsync(workspace.Path);
        var trackedPath = workspace.GetPath("base.txt");

        await File.AppendAllTextAsync(trackedPath, "staged-owner-change\n");
        Assert.Equal(0, (await TestProcess.RunAsync("git", "add -- base.txt", workspace.Path)).ExitCode);
        await File.AppendAllTextAsync(trackedPath, "unstaged-owner-change\n");

        var ownerBytesBefore = await File.ReadAllBytesAsync(trackedPath);
        var indexPath = workspace.GetPath(".git/index");
        var indexBytesBefore = await File.ReadAllBytesAsync(indexPath);
        var service = new GitCliHistoryService();

        var result = await service.GetHistoryAsync(workspace.Path);

        Assert.Equal(GitHistoryStatus.Success, result.Status);
        Assert.Equal(ownerBytesBefore, await File.ReadAllBytesAsync(trackedPath));
        Assert.Equal(indexBytesBefore, await File.ReadAllBytesAsync(indexPath));
    }

    [Fact]
    public async Task HistoryPropagatesCallerCancellationBeforeExecution()
    {
        using var workspace = new TemporaryDirectory("fccd-git-history-cancel");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var service = new GitCliHistoryService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetHistoryAsync(
                workspace.Path,
                cancellationToken: cancellationSource.Token));
    }

    [Fact]
    public void ConstructorRejectsUnsafeTimeoutAndOutputBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliHistoryService(operationTimeout: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliHistoryService(
                operationTimeout: GitCliHistoryService.MaximumOperationTimeout + TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliHistoryService(maximumOutputCharacters: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCliHistoryService(
                maximumOutputCharacters: GitCliHistoryService.MaximumOutputCharacters + 1));
    }

    private static async Task InitializeRepositoryAsync(string rootPath, params string[] additionalFiles)
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

        await File.WriteAllTextAsync(Path.Combine(rootPath, "base.txt"), "baseline\n");
        foreach (var additionalFile in additionalFiles)
        {
            await File.WriteAllTextAsync(
                Path.Combine(rootPath, additionalFile),
                $"baseline:{additionalFile}\n");
        }

        await StageAllAndCommitAsync(rootPath, "baseline");
    }

    private static async Task StageAllAndCommitAsync(string repositoryPath, string subject)
    {
        var add = await TestProcess.RunAsync("git", "add -- .", repositoryPath);
        Assert.True(add.ExitCode == 0, add.StandardError);

        var messagePath = Path.Combine(repositoryPath, ".git", "fccd-history-message.txt");
        await File.WriteAllTextAsync(messagePath, subject + Environment.NewLine, new UTF8Encoding(false));
        try
        {
            var commit = await TestProcess.RunAsync(
                "git",
                "commit --quiet --file .git/fccd-history-message.txt",
                repositoryPath);
            Assert.True(commit.ExitCode == 0, commit.StandardError);
        }
        finally
        {
            File.Delete(messagePath);
        }
    }
}
