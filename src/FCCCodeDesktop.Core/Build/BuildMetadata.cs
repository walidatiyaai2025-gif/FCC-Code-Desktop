using System.Reflection;
using System.Runtime.Versioning;

namespace FCCCodeDesktop.Core.Build;

public enum BuildChannel
{
    Development,
    Production,
}

public sealed record BuildMetadata
{
    public const string UnknownGitCommit = "unknown";

    public BuildMetadata(
        string productName,
        string productVersion,
        string informationalVersion,
        BuildChannel channel,
        string gitCommit,
        string configuration,
        string targetFramework,
        string repositoryUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(productVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(informationalVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitCommit);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);

        ValidateProductVersion(productVersion, channel);
        ValidateGitCommit(gitCommit, channel);

        if (!informationalVersion.StartsWith(productVersion + "+", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Informational version must start with the product version followed by source provenance.",
                nameof(informationalVersion));
        }

        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var repositoryUri)
            || repositoryUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Repository URL must be an absolute HTTPS URL.", nameof(repositoryUrl));
        }

        ProductName = productName;
        ProductVersion = productVersion;
        InformationalVersion = informationalVersion;
        Channel = channel;
        GitCommit = gitCommit;
        Configuration = configuration;
        TargetFramework = targetFramework;
        RepositoryUrl = repositoryUrl;
    }

    public string ProductName { get; }

    public string ProductVersion { get; }

    public string InformationalVersion { get; }

    public BuildChannel Channel { get; }

    public string GitCommit { get; }

    public string Configuration { get; }

    public string TargetFramework { get; }

    public string RepositoryUrl { get; }

    public bool IsPublicRelease => Channel == BuildChannel.Production;

    public bool HasSourceProvenance => !string.Equals(GitCommit, UnknownGitCommit, StringComparison.Ordinal);

    private static void ValidateProductVersion(string productVersion, BuildChannel channel)
    {
        var coreVersion = productVersion.Split(new[] { '-', '+' }, 2)[0];
        if (!Version.TryParse(coreVersion, out var parsedVersion) || parsedVersion.Build < 0)
        {
            throw new ArgumentException("Product version must contain a three-part numeric version core.", nameof(productVersion));
        }

        if (channel == BuildChannel.Production && productVersion.Contains('-', StringComparison.Ordinal))
        {
            throw new ArgumentException("Production builds cannot carry a prerelease suffix.", nameof(productVersion));
        }
    }

    private static void ValidateGitCommit(string gitCommit, BuildChannel channel)
    {
        if (string.Equals(gitCommit, UnknownGitCommit, StringComparison.Ordinal))
        {
            if (channel == BuildChannel.Production)
            {
                throw new ArgumentException("Production builds require exact source provenance.", nameof(gitCommit));
            }

            return;
        }

        if ((gitCommit.Length != 40 && gitCommit.Length != 64) || !gitCommit.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Git commit must be a 40- or 64-character hexadecimal object ID.", nameof(gitCommit));
        }
    }
}

public interface IBuildMetadataService
{
    BuildMetadata Current { get; }
}

public sealed class AssemblyBuildMetadataService : IBuildMetadataService
{
    private const string GitCommitKey = "FccGitCommit";
    private const string BuildChannelKey = "FccBuildChannel";
    private const string BuildConfigurationKey = "FccBuildConfiguration";
    private const string ProductVersionKey = "FccProductVersion";
    private const string RepositoryUrlKey = "FccRepositoryUrl";

    private readonly BuildMetadata current;

    public AssemblyBuildMetadataService()
        : this(typeof(AssemblyBuildMetadataService).Assembly)
    {
    }

    public AssemblyBuildMetadataService(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        current = Read(assembly);
    }

    public BuildMetadata Current => current;

    public static BuildMetadata Read(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var productName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
            ?? throw new InvalidOperationException("Assembly product metadata is missing.");
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? throw new InvalidOperationException("Assembly informational version is missing.");
        var targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
            ?? throw new InvalidOperationException("Assembly target framework metadata is missing.");
        var productVersion = GetRequiredMetadata(assembly, ProductVersionKey);
        var gitCommit = GetRequiredMetadata(assembly, GitCommitKey);
        var configuration = GetRequiredMetadata(assembly, BuildConfigurationKey);
        var repositoryUrl = GetRequiredMetadata(assembly, RepositoryUrlKey);
        var channelText = GetRequiredMetadata(assembly, BuildChannelKey);

        if (!Enum.TryParse<BuildChannel>(channelText, ignoreCase: false, out var channel))
        {
            throw new InvalidOperationException($"Unsupported build channel '{channelText}'.");
        }

        return new BuildMetadata(
            productName,
            productVersion,
            informationalVersion,
            channel,
            gitCommit,
            configuration,
            targetFramework,
            repositoryUrl);
    }

    private static string GetRequiredMetadata(Assembly assembly, string key)
    {
        var matches = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].Value))
        {
            throw new InvalidOperationException($"Assembly metadata '{key}' must exist exactly once with a value.");
        }

        return matches[0].Value!;
    }
}
