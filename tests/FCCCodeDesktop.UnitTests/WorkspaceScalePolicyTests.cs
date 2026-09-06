using FCCCodeDesktop.Application.Projects;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class WorkspaceScalePolicyTests
{
    [Fact]
    public void DefaultsAreFiniteAndContainCanonicalGeneratedDirectories()
    {
        var policy = new WorkspaceScalePolicy();

        Assert.Equal(2_048, policy.MaximumDirectoryEntries);
        Assert.Equal(64, policy.MaximumTraversalDepth);
        Assert.Equal(20_000, policy.MaximumFilesPerOperation);
        Assert.Equal(500, policy.MaximumSearchResults);
        Assert.Equal(100, policy.MaximumSearchMatchesPerFile);
        Assert.Equal(8L * 1024 * 1024, policy.MaximumTextFileBytes);
        Assert.Equal(4L * 1024 * 1024, policy.MaximumSearchFileBytes);
        Assert.Equal(240, policy.MaximumPreviewCharacters);
        Assert.Equal(4_096, policy.BinaryProbeBytes);
        Assert.True(policy.ShouldExcludeDirectory(".GIT"));
        Assert.True(policy.ShouldExcludeDirectory("node_modules"));
        Assert.True(policy.ShouldExcludeDirectory("library"));
        Assert.False(policy.ShouldExcludeDirectory("source"));
    }

    [Fact]
    public void CustomLimitsAndExclusionsAreImmutableCaseInsensitiveAndCannotRemoveBuiltIns()
    {
        var sourceExclusions = new List<string> { "vendor", "Generated", "VENDOR" };
        var policy = new WorkspaceScalePolicy(
            maximumDirectoryEntries: 7,
            maximumTraversalDepth: 3,
            maximumFilesPerOperation: 11,
            maximumSearchResults: 5,
            maximumSearchMatchesPerFile: 2,
            maximumTextFileBytes: 1_024,
            maximumSearchFileBytes: 512,
            maximumPreviewCharacters: 64,
            binaryProbeBytes: 128,
            excludedDirectoryNames: sourceExclusions);
        sourceExclusions.Add("later");

        Assert.Equal(7, policy.MaximumDirectoryEntries);
        Assert.Equal(3, policy.MaximumTraversalDepth);
        Assert.Equal(11, policy.MaximumFilesPerOperation);
        Assert.Equal(5, policy.MaximumSearchResults);
        Assert.Equal(2, policy.MaximumSearchMatchesPerFile);
        Assert.Equal(1_024, policy.MaximumTextFileBytes);
        Assert.Equal(512, policy.MaximumSearchFileBytes);
        Assert.Equal(64, policy.MaximumPreviewCharacters);
        Assert.Equal(128, policy.BinaryProbeBytes);
        Assert.Contains("vendor", policy.ExcludedDirectoryNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("generated", policy.ExcludedDirectoryNames, StringComparer.OrdinalIgnoreCase);
        Assert.True(policy.ShouldExcludeDirectory(".git"));
        Assert.True(policy.ShouldExcludeDirectory("node_modules"));
        Assert.True(policy.ShouldExcludeDirectory("generated"));
        Assert.False(policy.ShouldExcludeDirectory("later"));
    }

    [Fact]
    public void EmptyCustomExclusionsStillPreserveCanonicalSafetyExclusions()
    {
        var policy = new WorkspaceScalePolicy(excludedDirectoryNames: []);

        Assert.True(policy.ShouldExcludeDirectory(".git"));
        Assert.True(policy.ShouldExcludeDirectory("node_modules"));
        Assert.True(policy.ShouldExcludeDirectory("bin"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(WorkspaceScalePolicy.MaximumSupportedDirectoryEntries + 1)]
    public void RejectsInvalidDirectoryEntryLimit(int value)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkspaceScalePolicy(maximumDirectoryEntries: value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(WorkspaceScalePolicy.MaximumSupportedTraversalDepth + 1)]
    public void RejectsInvalidTraversalDepth(int value)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkspaceScalePolicy(maximumTraversalDepth: value));
    }

    [Fact]
    public void RejectsEveryInvalidBoundAndUnsafeExclusion()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceScalePolicy(maximumFilesPerOperation: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceScalePolicy(maximumSearchResults: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceScalePolicy(maximumSearchMatchesPerFile: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceScalePolicy(maximumTextFileBytes: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceScalePolicy(maximumSearchFileBytes: 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkspaceScalePolicy(maximumPreviewCharacters: WorkspaceScalePolicy.MinimumPreviewCharacters - 1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkspaceScalePolicy(binaryProbeBytes: WorkspaceScalePolicy.MinimumBinaryProbeBytes - 1));
        _ = Assert.Throws<ArgumentException>(() =>
            new WorkspaceScalePolicy(excludedDirectoryNames: [".."]));
        _ = Assert.Throws<ArgumentException>(() =>
            new WorkspaceScalePolicy(excludedDirectoryNames: ["nested/path"]));
        _ = Assert.Throws<ArgumentException>(() =>
            new WorkspaceScalePolicy(excludedDirectoryNames: [" "]));
    }
}
