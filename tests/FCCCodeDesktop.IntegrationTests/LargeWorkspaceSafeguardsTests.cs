using System.Diagnostics;
using System.Text;
using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Files;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class LargeWorkspaceSafeguardsTests
{
    [Fact]
    public async Task SyntheticLargeTreeIsBoundedResponsiveAndStableAfterCancellation()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-large-tree");
        var root = workspace.GetPath("مشروع synthetic tree with spaces");
        Directory.CreateDirectory(root);
        for (var directoryIndex = 0; directoryIndex < 24; directoryIndex++)
        {
            var directory = Path.Combine(root, $"source-{directoryIndex:D2}");
            Directory.CreateDirectory(directory);
            for (var fileIndex = 0; fileIndex < 20; fileIndex++)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(directory, $"ملف-{fileIndex:D2}.txt"),
                    $"needle {directoryIndex:D2}-{fileIndex:D2}",
                    Encoding.UTF8,
                    CancellationToken.None);
            }
        }

        var generatedDirectory = Path.Combine(root, "node_modules", "dependency");
        Directory.CreateDirectory(generatedDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(generatedDirectory, "ignored.txt"),
            "needle",
            Encoding.UTF8,
            CancellationToken.None);
        var sentinelPath = Path.Combine(root, "source-00", "ملف-00.txt");
        var sentinelContent = await File.ReadAllTextAsync(sentinelPath, Encoding.UTF8, CancellationToken.None);
        var sentinelWriteTime = File.GetLastWriteTimeUtc(sentinelPath);
        var service = new FileSystemProjectSearchService();

        using (var cancellation = new CancellationTokenSource())
        {
            var cancelledSearch = service.SearchAsync(
                new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content),
                cancellation.Token);
            cancellation.Cancel();
            Assert.True(cancellation.IsCancellationRequested);
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledSearch);
        }

        var request = new ProjectSearchRequest(
            root,
            ".txt",
            ProjectSearchMode.FileName,
            MaximumResults: 500,
            MaximumFiles: 250);
        var stopwatch = Stopwatch.StartNew();
        var first = await service.SearchAsync(request, CancellationToken.None);
        var firstDuration = stopwatch.Elapsed;
        stopwatch.Restart();
        var second = await service.SearchAsync(request, CancellationToken.None);
        var secondDuration = stopwatch.Elapsed;

        Assert.Equal(250, first.FilesExamined);
        Assert.Equal(250, first.Matches.Count);
        Assert.True(first.LimitReached);
        Assert.True(first.LimitReasons.HasFlag(ProjectSearchLimitReason.Files));
        Assert.Equal(
            first.Matches.Select(match => match.RelativePath),
            second.Matches.Select(match => match.RelativePath));
        Assert.True(firstDuration < TimeSpan.FromSeconds(30), $"First bounded search took {firstDuration}.");
        Assert.True(secondDuration < TimeSpan.FromSeconds(30), $"Second bounded search took {secondDuration}.");
        Assert.Equal(sentinelContent, await File.ReadAllTextAsync(sentinelPath, Encoding.UTF8, CancellationToken.None));
        Assert.Equal(sentinelWriteTime, File.GetLastWriteTimeUtc(sentinelPath));
    }

    [Fact]
    public async Task LockedFileIsSkippedAndOperationRecoversWithoutMutation()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-locked-file");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "locked.txt");
        const string source = "needle remains unchanged";
        await File.WriteAllTextAsync(sourcePath, source, Encoding.UTF8, CancellationToken.None);
        var searchService = new FileSystemProjectSearchService();
        var fileService = new FileSystemProjectFileService();

        await using (var exclusiveLock = new FileStream(
                         sourcePath,
                         FileMode.Open,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         bufferSize: 4_096,
                         useAsync: true))
        {
            var skipped = await searchService.SearchAsync(
                new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content),
                CancellationToken.None);

            Assert.Empty(skipped.Matches);
            Assert.True(skipped.FilesSkipped >= 1);
            _ = await Assert.ThrowsAsync<IOException>(() =>
                fileService.InspectAsync(root, sourcePath, CancellationToken.None));
        }

        var recoveredInspection = await fileService.InspectAsync(root, sourcePath, CancellationToken.None);
        var recoveredSearch = await searchService.SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content),
            CancellationToken.None);

        Assert.Equal(ProjectFileContentKind.Text, recoveredInspection.ContentKind);
        Assert.Single(recoveredSearch.Matches);
        Assert.Equal(source, await File.ReadAllTextAsync(sourcePath, Encoding.UTF8, CancellationToken.None));
    }

    [Fact]
    public async Task ReparsePointDoesNotEscapeProjectRootWhenSupported()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-reparse");
        var root = workspace.GetPath("project");
        var outside = workspace.GetPath("outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        var outsideFile = Path.Combine(outside, "sentinel.txt");
        const string outsideContent = "needle outside root";
        await File.WriteAllTextAsync(outsideFile, outsideContent, Encoding.UTF8, CancellationToken.None);
        var linkPath = Path.Combine(root, "linked-outside");

        try
        {
            _ = Directory.CreateSymbolicLink(linkPath, outside);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
                                          or IOException
                                          or PlatformNotSupportedException)
        {
            return;
        }

        var explorer = new FileSystemProjectFileExplorerService();
        var listing = await explorer.ListChildrenAsync(root, root, CancellationToken.None);
        var linkEntry = Assert.Single(listing.Entries);
        Assert.True(linkEntry.IsReparsePoint);
        Assert.Equal(ProjectFileTraversalRestriction.ReparsePoint, linkEntry.TraversalRestriction);
        Assert.False(linkEntry.CanExpand);
        _ = await Assert.ThrowsAsync<IOException>(() =>
            explorer.ListChildrenAsync(root, linkPath, CancellationToken.None));

        var search = await new FileSystemProjectSearchService().SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content),
            CancellationToken.None);
        Assert.Empty(search.Matches);
        Assert.True(search.DirectoriesSkipped >= 1);

        _ = await Assert.ThrowsAsync<IOException>(() =>
            new FileSystemProjectFileService().InspectAsync(
                root,
                Path.Combine(linkPath, "sentinel.txt"),
                CancellationToken.None));
        Assert.Equal(outsideContent, await File.ReadAllTextAsync(outsideFile, Encoding.UTF8, CancellationToken.None));
    }
}
