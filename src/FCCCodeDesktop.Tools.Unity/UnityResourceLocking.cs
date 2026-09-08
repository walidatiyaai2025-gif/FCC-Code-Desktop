using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using FCCCodeDesktop.Tools;

namespace FCCCodeDesktop.Tools.Unity;

/// <summary>
/// Unity-specific resource-lock declaration layered on the provider-neutral P09 lock contract.
/// Every invocation reserves both the logical Unity process slot for its project identity and the
/// physical Unity project root, preventing duplicate launches without globally serializing unrelated projects.
/// </summary>
public interface IUnityResourceLockProvider : IExternalToolResourceLockProvider
{
    IReadOnlyCollection<ToolResourceLockKey> GetResourceLockKeys(UnityCliInvocation invocation);
}

/// <summary>
/// Produces deterministic, non-path-disclosing lock keys for Unity invocations.
/// </summary>
public sealed class UnityResourceLockProvider : IUnityResourceLockProvider
{
    private const string ProcessKeyPrefix = "unity:process:";
    private const string ProjectKeyPrefix = "unity:project:";

    public IReadOnlyCollection<ToolResourceLockKey> GetResourceLockKeys(UnityCliInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var keys = new[]
        {
            new ToolResourceLockKey(ProcessKeyPrefix + invocation.Project.ProjectId.ToString("N")),
            new ToolResourceLockKey(ProjectKeyPrefix + ComputeProjectPathFingerprint(invocation.Project.RootPath)),
        };

        return new ReadOnlyCollection<ToolResourceLockKey>(keys);
    }

    public IReadOnlyCollection<ToolResourceLockKey> GetResourceLockKeys(ToolInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        if (invocation is not UnityCliInvocation unityInvocation)
        {
            throw new ArgumentException(
                $"Unity resource locking requires a {nameof(UnityCliInvocation)}.",
                nameof(invocation));
        }

        return GetResourceLockKeys(unityInvocation);
    }

    private static string ComputeProjectPathFingerprint(string projectRoot)
    {
        var canonicalPath = CanonicalizeProjectRoot(projectRoot);
        var caseFoldedPath = canonicalPath.ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(caseFoldedPath);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string CanonicalizeProjectRoot(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var fullPath = Path.GetFullPath(projectRoot);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root) || fullPath.Length <= root.Length)
        {
            return fullPath;
        }

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

/// <summary>
/// Acquires Unity process/project locks through the shared P09 lock manager. The lease must span the
/// complete external process lifetime so cancellation, failure and normal completion all release the same resources.
/// </summary>
public sealed class UnityResourceLockCoordinator
{
    private readonly IToolResourceLockManager _lockManager;
    private readonly IUnityResourceLockProvider _lockProvider;

    public UnityResourceLockCoordinator(
        IToolResourceLockManager lockManager,
        IUnityResourceLockProvider lockProvider)
    {
        _lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
        _lockProvider = lockProvider ?? throw new ArgumentNullException(nameof(lockProvider));
    }

    public ValueTask<ToolResourceLockLease> AcquireAsync(
        UnityCliInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return _lockManager.AcquireAsync(_lockProvider.GetResourceLockKeys(invocation), cancellationToken);
    }
}
