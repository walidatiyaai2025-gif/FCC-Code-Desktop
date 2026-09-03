using FCCCodeDesktop.Core.Build;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class BuildMetadataTests
{
    private const string RepositoryUrl = "https://github.com/walidatiyaai2025-gif/FCC-Code-Desktop";
    private const string ValidGitCommit = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void AssemblyServiceReadsCentralBuildMetadata()
    {
        var metadata = new AssemblyBuildMetadataService().Current;

        Assert.Equal("FCC Code Desktop", metadata.ProductName);
        Assert.Equal("1.0.0-dev", metadata.ProductVersion);
        Assert.Equal(BuildChannel.Development, metadata.Channel);
        Assert.False(metadata.IsPublicRelease);
        Assert.Equal(RepositoryUrl, metadata.RepositoryUrl);
        Assert.Contains(".NETCoreApp,Version=v10.0", metadata.TargetFramework, StringComparison.Ordinal);
        Assert.StartsWith(metadata.ProductVersion + "+", metadata.InformationalVersion, StringComparison.Ordinal);

        var githubSha = Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (!string.IsNullOrWhiteSpace(githubSha))
        {
            Assert.Equal(githubSha, metadata.GitCommit);
            Assert.True(metadata.HasSourceProvenance);
        }
    }

    [Fact]
    public void DevelopmentMetadataAllowsExplicitUnknownSource()
    {
        var metadata = new BuildMetadata(
            "FCC Code Desktop",
            "1.0.0-dev",
            "1.0.0-dev+unknown",
            BuildChannel.Development,
            BuildMetadata.UnknownGitCommit,
            "Debug",
            ".NETCoreApp,Version=v10.0",
            RepositoryUrl);

        Assert.False(metadata.HasSourceProvenance);
        Assert.False(metadata.IsPublicRelease);
    }

    [Fact]
    public void ProductionMetadataRequiresSourceProvenance()
    {
        Assert.Throws<ArgumentException>(() => new BuildMetadata(
            "FCC Code Desktop",
            "1.0.0",
            "1.0.0+unknown",
            BuildChannel.Production,
            BuildMetadata.UnknownGitCommit,
            "Release",
            ".NETCoreApp,Version=v10.0",
            RepositoryUrl));
    }

    [Fact]
    public void MetadataRejectsMalformedGitCommit()
    {
        Assert.Throws<ArgumentException>(() => new BuildMetadata(
            "FCC Code Desktop",
            "1.0.0-dev",
            "1.0.0-dev+not-a-sha",
            BuildChannel.Development,
            "not-a-sha",
            "Release",
            ".NETCoreApp,Version=v10.0",
            RepositoryUrl));
    }

    [Fact]
    public void ProductionMetadataRejectsPrereleaseVersion()
    {
        Assert.Throws<ArgumentException>(() => new BuildMetadata(
            "FCC Code Desktop",
            "1.0.0-dev",
            "1.0.0-dev+" + ValidGitCommit,
            BuildChannel.Production,
            ValidGitCommit,
            "Release",
            ".NETCoreApp,Version=v10.0",
            RepositoryUrl));
    }

    [Fact]
    public void ProductionMetadataAcceptsExactVersionAndCommit()
    {
        var metadata = new BuildMetadata(
            "FCC Code Desktop",
            "1.0.0",
            "1.0.0+" + ValidGitCommit,
            BuildChannel.Production,
            ValidGitCommit,
            "Release",
            ".NETCoreApp,Version=v10.0",
            RepositoryUrl);

        Assert.True(metadata.IsPublicRelease);
        Assert.True(metadata.HasSourceProvenance);
    }
}
