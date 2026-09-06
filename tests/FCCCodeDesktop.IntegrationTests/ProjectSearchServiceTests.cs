using System.Text;
using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Files;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class ProjectSearchServiceTests
{
    [Fact]
    public async Task ContentSearchFindsUnicodeAndSpaceContainingPathsWithoutModifyingFiles()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-content");
        var root = workspace.GetPath("مشروع search with spaces");
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "ملف مهم.txt");
        const string source = "first line\nNeedle هنا\nneedle again";
        await File.WriteAllTextAsync(sourcePath, source, Encoding.UTF8, CancellationToken.None);

        var result = await new FileSystemProjectSearchService().SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content),
            CancellationToken.None);

        Assert.Equal(2, result.Matches.Count);
        Assert.All(result.Matches, match => Assert.Equal("ملف مهم.txt", match.RelativePath));
        Assert.Equal(2, result.Matches[0].LineNumber);
        Assert.Equal(1, result.Matches[0].ColumnNumber);
        Assert.Equal(source, await File.ReadAllTextAsync(sourcePath, Encoding.UTF8, CancellationToken.None));
    }

    [Fact]
    public async Task FileNameSearchDoesNotReadFileContentAndHonorsCaseSetting()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-file-name");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(Path.Combine(root, "Feature-SPEC.BIN"), new byte[128], CancellationToken.None);
        var service = new FileSystemProjectSearchService();

        var insensitive = await service.SearchAsync(
            new ProjectSearchRequest(root, "spec.bin", ProjectSearchMode.FileName, MatchCase: false, MaximumFileBytes: 1),
            CancellationToken.None);
        var sensitive = await service.SearchAsync(
            new ProjectSearchRequest(root, "spec.bin", ProjectSearchMode.FileName, MatchCase: true, MaximumFileBytes: 1),
            CancellationToken.None);

        Assert.Single(insensitive.Matches);
        Assert.Equal("Feature-SPEC.BIN", insensitive.Matches[0].LocationLabel);
        Assert.Empty(sensitive.Matches);
    }

    [Fact]
    public async Task RegularExpressionSearchReturnsLineAndColumnAndRejectsInvalidPattern()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-regex");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, "work.cs"),
            "alpha\nISSUE 123: repair\nISSUE 999: verify",
            CancellationToken.None);
        var service = new FileSystemProjectSearchService();

        var result = await service.SearchAsync(
            new ProjectSearchRequest(root, @"ISSUE\s+\d+", ProjectSearchMode.RegularExpression),
            CancellationToken.None);

        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(2, result.Matches[0].LineNumber);
        Assert.Equal(1, result.Matches[0].ColumnNumber);
        _ = await Assert.ThrowsAsync<ProjectSearchQueryException>(() =>
            service.SearchAsync(
                new ProjectSearchRequest(root, "[unterminated", ProjectSearchMode.RegularExpression),
                CancellationToken.None));
    }

    [Fact]
    public async Task SearchSkipsGeneratedDirectoriesBinaryAndOversizedFiles()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-safety");
        var root = workspace.GetPath("project");
        var generated = Path.Combine(root, "node_modules", "dependency");
        Directory.CreateDirectory(generated);
        await File.WriteAllTextAsync(Path.Combine(generated, "ignored.txt"), "needle", CancellationToken.None);
        await File.WriteAllBytesAsync(Path.Combine(root, "binary.dat"), [0x41, 0x00, 0x42, 0x43], CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "oversized.txt"), "needle plus content beyond cap", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "small.txt"), "needle", CancellationToken.None);

        var result = await new FileSystemProjectSearchService().SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content, MaximumFileBytes: 8),
            CancellationToken.None);

        Assert.Single(result.Matches);
        Assert.Equal("small.txt", result.Matches[0].RelativePath);
        Assert.True(result.FilesSkipped >= 2);
        Assert.True(result.DirectoriesSkipped >= 1);
    }

    [Fact]
    public async Task SearchSupportsBomEncodedTextAndNeverTraversesIgnoredGitMetadata()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-encoding");
        var root = workspace.GetPath("project");
        var gitDirectory = Path.Combine(root, ".git", "objects");
        Directory.CreateDirectory(gitDirectory);
        await File.WriteAllTextAsync(Path.Combine(gitDirectory, "ignored.txt"), "needle", CancellationToken.None);
        await File.WriteAllTextAsync(
            Path.Combine(root, "utf16.txt"),
            "prefix needle suffix",
            Encoding.Unicode,
            CancellationToken.None);

        var result = await new FileSystemProjectSearchService().SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content),
            CancellationToken.None);

        Assert.Single(result.Matches);
        Assert.Equal("utf16.txt", result.Matches[0].RelativePath);
        Assert.True(result.DirectoriesSkipped >= 1);
    }

    [Fact]
    public async Task ResultAndFileCapsAreBoundedAndReported()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-bounds");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        for (var index = 0; index < 8; index++)
        {
            await File.WriteAllTextAsync(Path.Combine(root, $"match-{index:D2}.txt"), "needle", CancellationToken.None);
        }

        var service = new FileSystemProjectSearchService();
        var resultBound = await service.SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content, MaximumResults: 2, MaximumFiles: 8),
            CancellationToken.None);
        var fileBound = await service.SearchAsync(
            new ProjectSearchRequest(root, "match", ProjectSearchMode.FileName, MaximumResults: 8, MaximumFiles: 2),
            CancellationToken.None);

        Assert.Equal(2, resultBound.Matches.Count);
        Assert.True(resultBound.LimitReached);
        Assert.Equal(2, fileBound.FilesExamined);
        Assert.True(fileBound.LimitReached);
    }

    [Fact]
    public async Task TraversalDepthAndPerFileCapsProduceTypedPartialResults()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-policy-limits");
        var root = workspace.GetPath("project");
        var levelOne = Path.Combine(root, "level-one");
        var levelTwo = Path.Combine(levelOne, "level-two");
        Directory.CreateDirectory(levelTwo);
        await File.WriteAllTextAsync(
            Path.Combine(root, "many.txt"),
            "needle needle needle needle",
            CancellationToken.None);
        await File.WriteAllTextAsync(
            Path.Combine(levelTwo, "deep.txt"),
            "needle",
            CancellationToken.None);
        var policy = new WorkspaceScalePolicy(
            maximumTraversalDepth: 1,
            maximumSearchResults: 10,
            maximumSearchMatchesPerFile: 2);
        var service = new FileSystemProjectSearchService(policy);

        var result = await service.SearchAsync(
            new ProjectSearchRequest(
                root,
                "needle",
                ProjectSearchMode.Content,
                MaximumResults: 10,
                MaximumTraversalDepth: 1,
                MaximumMatchesPerFile: 2),
            CancellationToken.None);

        Assert.Equal(2, result.Matches.Count);
        Assert.True(result.LimitReached);
        Assert.True(result.LimitReasons.HasFlag(ProjectSearchLimitReason.MatchesPerFile));
        Assert.True(result.LimitReasons.HasFlag(ProjectSearchLimitReason.TraversalDepth));
        Assert.Equal(2, result.MaximumMatchesPerFile);
        Assert.Equal(1, result.MaximumTraversalDepth);
        Assert.DoesNotContain(result.Matches, match => match.RelativePath.Contains("deep.txt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WideDirectoryMaterializationIsBoundedOrderedAndStable()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-wide");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        for (var index = 7; index >= 0; index--)
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, $"match-{index:D2}.txt"),
                "fixture",
                CancellationToken.None);
        }
        var policy = new WorkspaceScalePolicy(maximumDirectoryEntries: 3);
        var service = new FileSystemProjectSearchService(policy);
        var request = new ProjectSearchRequest(root, "match", ProjectSearchMode.FileName);

        var first = await service.SearchAsync(request, CancellationToken.None);
        var second = await service.SearchAsync(request, CancellationToken.None);

        Assert.Equal(3, first.FilesExamined);
        Assert.Equal(3, first.Matches.Count);
        Assert.True(first.LimitReached);
        Assert.True(first.LimitReasons.HasFlag(ProjectSearchLimitReason.DirectoryEntries));
        Assert.Equal(
            first.Matches.Select(match => match.RelativePath),
            second.Matches.Select(match => match.RelativePath));
        Assert.Equal(
            first.Matches.OrderBy(match => match.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(match => match.RelativePath, StringComparer.Ordinal)
                .Select(match => match.RelativePath),
            first.Matches.Select(match => match.RelativePath));
    }

    [Fact]
    public async Task SharedPolicyBoundsTraversalPerFileMatchesAndReportsMetadataWithoutMutation()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-scale-policy");
        var root = workspace.GetPath("مشروع bounded search");
        var levelOne = Path.Combine(root, "level one");
        var levelTwo = Path.Combine(levelOne, "level two");
        Directory.CreateDirectory(levelTwo);
        var hotFile = Path.Combine(root, "hot.txt");
        const string hotContent = "needle needle needle";
        await File.WriteAllTextAsync(hotFile, hotContent, Encoding.UTF8, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(levelTwo, "deep.txt"), "needle", Encoding.UTF8, CancellationToken.None);
        var policy = new WorkspaceScalePolicy(
            maximumTraversalDepth: 1,
            maximumFilesPerOperation: 10,
            maximumSearchResults: 10,
            maximumSearchMatchesPerFile: 2,
            maximumSearchFileBytes: 1_024,
            maximumPreviewCharacters: 64,
            binaryProbeBytes: 128);
        var service = new FileSystemProjectSearchService(policy);

        var result = await service.SearchAsync(
            new ProjectSearchRequest(
                root,
                "needle",
                ProjectSearchMode.Content,
                MaximumResults: 10,
                MaximumFiles: 10,
                MaximumFileBytes: 1_024,
                MaximumMatchesPerFile: 2,
                MaximumTraversalDepth: 1,
                MaximumPreviewCharacters: 64),
            CancellationToken.None);

        Assert.Equal(2, result.Matches.Count);
        Assert.All(result.Matches, match => Assert.Equal("hot.txt", match.RelativePath));
        Assert.True(result.DirectoriesSkipped >= 1);
        Assert.True(result.LimitReached);
        Assert.True(result.LimitReasons.HasFlag(ProjectSearchLimitReason.MatchesPerFile));
        Assert.True(result.LimitReasons.HasFlag(ProjectSearchLimitReason.TraversalDepth));
        Assert.Equal(2, result.MaximumMatchesPerFile);
        Assert.Equal(1, result.MaximumTraversalDepth);
        Assert.Equal(64, result.MaximumPreviewCharacters);
        Assert.Equal(128, result.BinaryProbeBytes);
        Assert.Equal(hotContent, await File.ReadAllTextAsync(hotFile, Encoding.UTF8, CancellationToken.None));
    }

    [Fact]
    public async Task SearchRejectsRequestsAboveInjectedWorkspacePolicy()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-policy-rejection");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        var policy = new WorkspaceScalePolicy(
            maximumTraversalDepth: 2,
            maximumFilesPerOperation: 4,
            maximumSearchResults: 3,
            maximumSearchMatchesPerFile: 2,
            maximumSearchFileBytes: 128,
            maximumPreviewCharacters: 32);
        var service = new FileSystemProjectSearchService(policy);
        var request = new ProjectSearchRequest(
            root,
            "needle",
            ProjectSearchMode.Content,
            MaximumResults: 3,
            MaximumFiles: 4,
            MaximumFileBytes: 128,
            MaximumMatchesPerFile: 2,
            MaximumTraversalDepth: 2,
            MaximumPreviewCharacters: 32);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchAsync(request with { MaximumResults = 4 }, CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchAsync(request with { MaximumFiles = 5 }, CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchAsync(request with { MaximumFileBytes = 129 }, CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchAsync(request with { MaximumMatchesPerFile = 3 }, CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchAsync(request with { MaximumTraversalDepth = 3 }, CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchAsync(request with { MaximumPreviewCharacters = 33 }, CancellationToken.None));
    }

    [Fact]
    public async Task CancellationMissingRootAndInvalidBoundsFailExplicitly()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-errors");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        var service = new FileSystemProjectSearchService();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SearchAsync(new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content), cancellation.Token));
        _ = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            service.SearchAsync(
                new ProjectSearchRequest(Path.Combine(root, "missing"), "needle", ProjectSearchMode.Content),
                CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SearchAsync(
                new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content, MaximumResults: 0),
                CancellationToken.None));

        await File.WriteAllTextAsync(Path.Combine(root, "recovered.txt"), "needle", CancellationToken.None);
        var recovered = await service.SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content),
            CancellationToken.None);
        Assert.Single(recovered.Matches);
    }
}
