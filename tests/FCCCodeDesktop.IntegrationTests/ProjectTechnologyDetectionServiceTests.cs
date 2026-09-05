using FCCCodeDesktop.Files;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class ProjectTechnologyDetectionServiceTests
{
    [Fact]
    public async Task DetectsMixedTechnologyMarkersDeterministicallyWithoutModifyingSource()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-tech مساحة scan");
        var root = workspace.GetPath("مشروع mixed with spaces");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
        Directory.CreateDirectory(Path.Combine(root, "src", "nested"));

        await File.WriteAllTextAsync(Path.Combine(root, "Workspace.sln"), string.Empty, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "package.json"), "{}", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "pyproject.toml"), "[project]", CancellationToken.None);
        await File.WriteAllTextAsync(
            Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"),
            "m_EditorVersion: 6000.5.8f1",
            CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "scene.blend"), "blend-sentinel", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "src", "nested", "Cargo.toml"), "[package]", CancellationToken.None);
        var sentinelPath = Path.Combine(root, "owner-source.txt");
        await File.WriteAllTextAsync(sentinelPath, "do-not-change", CancellationToken.None);

        var service = new FileSystemProjectTechnologyDetectionService();
        var result = await service.DetectAsync(root, CancellationToken.None);
        var ids = result.Technologies.Select(technology => technology.Id).ToArray();

        Assert.Equal(Path.GetFullPath(root), result.RootPath);
        Assert.Contains("blender", ids);
        Assert.Contains("dotnet", ids);
        Assert.Contains("nodejs", ids);
        Assert.Contains("python", ids);
        Assert.Contains("rust", ids);
        Assert.Contains("unity", ids);
        Assert.False(result.LimitReached);
        Assert.Equal("do-not-change", await File.ReadAllTextAsync(sentinelPath, CancellationToken.None));
        Assert.Equal(
            result.Technologies.OrderBy(technology => technology.DisplayName, StringComparer.OrdinalIgnoreCase),
            result.Technologies);
    }

    [Fact]
    public async Task GeneratedDirectoriesAndReparseSensitiveBoundariesAreIgnored()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-tech-ignore");
        var root = workspace.GetPath("project");
        var nodeModules = Path.Combine(root, "node_modules");
        var bin = Path.Combine(root, "bin");
        var obj = Path.Combine(root, "obj");
        Directory.CreateDirectory(nodeModules);
        Directory.CreateDirectory(bin);
        Directory.CreateDirectory(obj);
        await File.WriteAllTextAsync(Path.Combine(nodeModules, "package.json"), "{}", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(bin, "Ignored.csproj"), string.Empty, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(obj, "CMakeLists.txt"), string.Empty, CancellationToken.None);

        var service = new FileSystemProjectTechnologyDetectionService();
        var result = await service.DetectAsync(root, CancellationToken.None);

        Assert.Empty(result.Technologies);
        Assert.True(result.SkippedPaths >= 3);
    }

    [Fact]
    public async Task EntryCapStopsTraversalAndReportsLimitWithoutLaunchingAnything()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-tech-limit");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        for (var index = 0; index < 10; index++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, $"{index:D2}.txt"),
                "fixture",
                CancellationToken.None);
        }

        await File.WriteAllTextAsync(Path.Combine(root, "zz-after-cap.csproj"), string.Empty, CancellationToken.None);

        var service = new FileSystemProjectTechnologyDetectionService(maximumDepth: 2, maximumEntries: 5);
        var result = await service.DetectAsync(root, CancellationToken.None);

        Assert.True(result.LimitReached);
        Assert.Equal(5, result.EntriesExamined);
        Assert.DoesNotContain(result.Technologies, technology => technology.Id == "dotnet");
    }

    [Fact]
    public async Task DetectsRepresentativeAdditionalToolchainsFromMarkers()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-tech-toolchains");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "pom.xml"), "<project />", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "go.mod"), "module example", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "composer.json"), "{}", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "CMakeLists.txt"), "project(example)", CancellationToken.None);

        var result = await new FileSystemProjectTechnologyDetectionService()
            .DetectAsync(root, CancellationToken.None);
        var ids = result.Technologies.Select(technology => technology.Id).ToArray();

        Assert.Contains("java", ids);
        Assert.Contains("go", ids);
        Assert.Contains("php", ids);
        Assert.Contains("cpp", ids);
        Assert.All(result.Technologies, technology => Assert.False(string.IsNullOrWhiteSpace(technology.Toolchain)));
    }

    [Fact]
    public async Task MissingRootAndCancellationFailExplicitly()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-tech-negative");
        var missingRoot = workspace.GetPath("missing");
        var service = new FileSystemProjectTechnologyDetectionService();

        var missing = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            service.DetectAsync(missingRoot, CancellationToken.None));
        Assert.Contains("does not exist", missing.Message, StringComparison.OrdinalIgnoreCase);

        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DetectAsync(root, cancellation.Token));
    }

    [Fact]
    public void ConstructorRejectsUnboundedScanConfiguration()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FileSystemProjectTechnologyDetectionService(maximumDepth: -1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FileSystemProjectTechnologyDetectionService(
                maximumEntries: FileSystemProjectTechnologyDetectionService.MaximumSupportedEntries + 1));
    }
}
