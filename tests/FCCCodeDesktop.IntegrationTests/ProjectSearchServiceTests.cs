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
    public async Task SearchUsesCentralPolicyForDepthPerFilePreviewAndTypedMetadata()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-scale-policy");
        var root = workspace.GetPath("project with scale policy");
        var visibleDirectory = Path.Combine(root, "source");
        var blockedDirectory = Path.Combine(visibleDirectory, "deeper");
        var generatedDirectory = Path.Combine(root, "vendor");
        Directory.CreateDirectory(blockedDirectory);
        Directory.CreateDirectory(generatedDirectory);
        await File.WriteAllTextAsync(Path.Combine(root, "many.txt"), "needle needle needle needle", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(visibleDirectory, "visible.txt"), "needle", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(blockedDirectory, "blocked.txt"), "needle", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(generatedDirectory, "generated.txt"), "needle", CancellationToken.None);

        var policy = new WorkspaceScalePolicy(
            maximumTraversalDepth: 1,
            maximumFilesPerOperation: 10,
            maximumSearchResults: 10,
            maximumSearchMatchesPerFile: 2,
            maximumSearchFileBytes: 1_024,
            maximumPreviewCharacters: 32,
            binaryProbeBytes: 64,
            excludedDirectoryNames: ["vendor"]);
        var result = await new FileSystemProjectSearchService(policy).SearchAsync(
            new ProjectSearchRequest(
                root,
                "needle",
                ProjectSearchMode.Content,
                MaximumResults: 10,
                MaximumFiles: 10,
                MaximumFileBytes: 1_024),
            CancellationToken.None);

        Assert.Equal(3, result.Matches.Count);
        Assert.Equal(2, result.Matches.Count(match => match.RelativePath == "many.txt"));
        Assert.Single(result.Matches, match => match.RelativePath == "source/visible.txt");
        Assert.DoesNotContain(result.Matches, match => match.RelativePath.Contains("blocked.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Matches, match => match.RelativePath.Contains("generated.txt", StringComparison.Ordinal));
        Assert.True(result.LimitReached);
        Assert.True(result.DirectoriesSkipped >= 2);
        Assert.Equal(1, result.MaximumTraversalDepth);
        Assert.Equal(2, result.MaximumMatchesPerFile);
        Assert.Equal(32, result.MaximumPreviewCharacters);
        Assert.Equal(64, result.BinaryProbeBytes);
        Assert.All(result.Matches, match => Assert.True(match.Preview.Length <= 32));
    }

    [Fact]
    public async Task SearchRejectsRequestLimitsAboveInjectedPolicy()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-search-policy-ceiling");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        var policy = new WorkspaceScalePolicy(
            maximumTraversalDepth: 2,
            maximumFilesPerOperation: 3,
            maximumSearchResults: 2,
            maximumSearchMatchesPerFile: 1,
            maximumSearchFileBytes: 128,
            maximumPreviewCharacters: 32,
            binaryProbeBytes: 64);
        var service = new FileSystemProjectSearchService(policy);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content, MaximumResults: 3, MaximumFiles: 3, MaximumFileBytes: 128),
            CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content, MaximumResults: 2, MaximumFiles: 4, MaximumFileBytes: 128),
            CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content, MaximumResults: 2, MaximumFiles: 3, MaximumFileBytes: 129),
            CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content, MaximumResults: 2, MaximumFiles: 3, MaximumFileBytes: 128, MaximumTraversalDepth: 3),
            CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content, MaximumResults: 2, MaximumFiles: 3, MaximumFileBytes: 128, MaximumMatchesPerFile: 2),
            CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync(
            new ProjectSearchRequest(root, "needle", ProjectSearchMode.Content, MaximumResults: 2, MaximumFiles: 3, MaximumFileBytes: 128, MaximumPreviewCharacters: 33),
            CancellationToken.None));
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
    }
}
