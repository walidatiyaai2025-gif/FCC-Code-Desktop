using FCCCodeDesktop.Files;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class ProjectFileExplorerServiceTests
{
    private static readonly string[] ExpectedImmediateChildren =
    [
        "Alpha folder",
        "Zulu",
        "alpha.txt",
        "beta.txt",
    ];

    [Fact]
    public async Task ListsOnlyImmediateChildrenAndSortsDirectoriesBeforeFiles()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-explorer-immediate");
        var root = workspace.GetPath("project");
        var alphaDirectory = Path.Combine(root, "Alpha folder");
        var zuluDirectory = Path.Combine(root, "Zulu");
        Directory.CreateDirectory(alphaDirectory);
        Directory.CreateDirectory(zuluDirectory);
        await File.WriteAllTextAsync(Path.Combine(root, "beta.txt"), "beta", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "alpha.txt"), "alpha", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(alphaDirectory, "nested.txt"), "nested", CancellationToken.None);

        var result = await new FileSystemProjectFileExplorerService()
            .ListChildrenAsync(root, root, CancellationToken.None);

        Assert.False(result.LimitReached);
        Assert.Equal(Path.GetFullPath(root), result.ProjectRootPath);
        Assert.Equal(Path.GetFullPath(root), result.DirectoryPath);
        Assert.Equal(ExpectedImmediateChildren, result.Entries.Select(entry => entry.Name).ToArray());
        Assert.All(result.Entries.Take(2), entry => Assert.True(entry.IsDirectory));
        Assert.All(result.Entries.Skip(2), entry => Assert.False(entry.IsDirectory));
        Assert.DoesNotContain(result.Entries, entry => entry.Name == "nested.txt");
    }

    [Fact]
    public async Task SupportsNonAsciiAndSpaceContainingPathsWithoutModifyingSource()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-explorer-unicode");
        var root = workspace.GetPath("مشروع explorer with spaces");
        var directory = Path.Combine(root, "مجلد فرعي");
        Directory.CreateDirectory(directory);
        var sentinelPath = Path.Combine(root, "ملف مهم.txt");
        await File.WriteAllTextAsync(sentinelPath, "do-not-change", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(directory, "child.txt"), "child", CancellationToken.None);

        var service = new FileSystemProjectFileExplorerService();
        var rootListing = await service.ListChildrenAsync(root, root, CancellationToken.None);
        var nestedListing = await service.ListChildrenAsync(root, directory, CancellationToken.None);

        Assert.Contains(rootListing.Entries, entry => entry.Name == "مجلد فرعي" && entry.CanExpand);
        Assert.Contains(rootListing.Entries, entry => entry.Name == "ملف مهم.txt" && !entry.IsDirectory);
        Assert.Single(nestedListing.Entries);
        Assert.Equal("مجلد فرعي/child.txt", nestedListing.Entries[0].RelativePath);
        Assert.Equal("do-not-change", await File.ReadAllTextAsync(sentinelPath, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsOutsideRootAndMissingDirectoryWithoutEnumeratingOwnerData()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-explorer-boundary");
        var root = workspace.GetPath("project");
        var outside = workspace.GetPath("outside-owner-data");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        var outsideSentinel = Path.Combine(outside, "owner.txt");
        await File.WriteAllTextAsync(outsideSentinel, "owner-data", CancellationToken.None);
        var service = new FileSystemProjectFileExplorerService();

        var outsideFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ListChildrenAsync(root, outside, CancellationToken.None));
        Assert.Contains("outside", outsideFailure.Message, StringComparison.OrdinalIgnoreCase);

        var missing = Path.Combine(root, "missing");
        _ = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            service.ListChildrenAsync(root, missing, CancellationToken.None));
        Assert.Equal("owner-data", await File.ReadAllTextAsync(outsideSentinel, CancellationToken.None));
    }

    [Fact]
    public async Task DirectoryEntryCapReportsLimitAndBoundsMaterialization()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-explorer-cap");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        for (var index = 0; index < 12; index++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, $"{index:D2}.txt"),
                "fixture",
                CancellationToken.None);
        }

        var service = new FileSystemProjectFileExplorerService(maximumEntriesPerDirectory: 5);
        var result = await service.ListChildrenAsync(root, root, CancellationToken.None);

        Assert.True(result.LimitReached);
        Assert.Equal(5, result.MaximumEntries);
        Assert.Equal(5, result.Entries.Count);
    }

    [Fact]
    public async Task CancellationAndInvalidConfigurationFailExplicitly()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-explorer-cancel");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new FileSystemProjectFileExplorerService()
                .ListChildrenAsync(root, root, cancellation.Token));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FileSystemProjectFileExplorerService(maximumEntriesPerDirectory: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FileSystemProjectFileExplorerService(
                FileSystemProjectFileExplorerService.MaximumSupportedEntriesPerDirectory + 1));
    }
}
