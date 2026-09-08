namespace FCCCodeDesktop.Tools;

/// <summary>
/// Stable, provider-neutral identity for an external resource that must not be used concurrently.
/// Keys are intentionally opaque to the gateway so adapters can model project, editor, device or
/// other tool-specific exclusivity without coupling the core gateway to a provider.
/// </summary>
public sealed record ToolResourceLockKey
{
    public ToolResourceLockKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Tool resource lock keys must not contain leading or trailing whitespace.",
                nameof(value));
        }

        if (value.Contains('\0'))
        {
            throw new ArgumentException("Tool resource lock keys must not contain NUL characters.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

/// <summary>
/// Adapter-side declaration of the resource locks required by a concrete invocation.
/// Adapters that require no exclusivity still implement this contract and return an empty set,
/// making the absence of locks an explicit decision rather than an orchestration assumption.
/// </summary>
public interface IExternalToolResourceLockProvider
{
    IReadOnlyCollection<ToolResourceLockKey> GetResourceLockKeys(ToolInvocation invocation);
}

/// <summary>
/// Process-local coordinator for exclusive external-tool resources.
/// </summary>
public interface IToolResourceLockManager
{
    ValueTask<ToolResourceLockLease> AcquireAsync(
        IEnumerable<ToolResourceLockKey> resourceKeys,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Acquires one or more resource keys atomically from the caller's perspective. Keys are
/// de-duplicated and acquired in a deterministic order, preventing lock-order inversions between
/// independent adapters. Keys are compared case-insensitively because the product target and the
/// most common locked resources (Windows paths/tool identities) are case-insensitive.
/// </summary>
public sealed class ToolResourceLockManager : IToolResourceLockManager
{
    private readonly object _sync = new();
    private readonly Dictionary<string, LockEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<ToolResourceLockLease> AcquireAsync(
        IEnumerable<ToolResourceLockKey> resourceKeys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceKeys);

        var keys = NormalizeKeys(resourceKeys);
        if (keys.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ToolResourceLockLease.Empty;
        }

        var acquired = new List<AcquiredEntry>(keys.Count);

        try
        {
            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = AddReference(key.Value);

                try
                {
                    await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    acquired.Add(new AcquiredEntry(key.Value, entry));
                }
                catch
                {
                    RemoveReference(key.Value, entry, releaseSemaphore: false);
                    throw;
                }
            }

            return new ToolResourceLockLease(
                keys,
                () => ReleaseAcquired(acquired));
        }
        catch
        {
            ReleaseAcquired(acquired);
            throw;
        }
    }

    private static IReadOnlyList<ToolResourceLockKey> NormalizeKeys(
        IEnumerable<ToolResourceLockKey> resourceKeys)
    {
        var distinct = new SortedDictionary<string, ToolResourceLockKey>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in resourceKeys)
        {
            if (key is null)
            {
                throw new ArgumentException("Tool resource lock sets must not contain null keys.", nameof(resourceKeys));
            }

            distinct.TryAdd(key.Value, key);
        }

        return Array.AsReadOnly(distinct.Values.ToArray());
    }

    private LockEntry AddReference(string key)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new LockEntry();
                _entries.Add(key, entry);
            }

            checked
            {
                entry.ReferenceCount++;
            }

            return entry;
        }
    }

    private void RemoveReference(string key, LockEntry entry, bool releaseSemaphore)
    {
        if (releaseSemaphore)
        {
            entry.Semaphore.Release();
        }

        lock (_sync)
        {
            if (entry.ReferenceCount <= 0)
            {
                throw new InvalidOperationException("Tool resource lock reference accounting became inconsistent.");
            }

            entry.ReferenceCount--;
            if (entry.ReferenceCount != 0)
            {
                return;
            }

            if (_entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
            {
                _entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private void ReleaseAcquired(List<AcquiredEntry> acquired)
    {
        for (var index = acquired.Count - 1; index >= 0; index--)
        {
            var item = acquired[index];
            RemoveReference(item.Key, item.Entry, releaseSemaphore: true);
        }

        acquired.Clear();
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }

    private sealed record AcquiredEntry(string Key, LockEntry Entry);
}

/// <summary>
/// Idempotent lease over a deterministic set of external-tool resource locks.
/// </summary>
public sealed class ToolResourceLockLease : IDisposable, IAsyncDisposable
{
    private Action? _release;

    internal ToolResourceLockLease(IReadOnlyList<ToolResourceLockKey> keys, Action? release)
    {
        Keys = keys;
        _release = release;
    }

    internal static ToolResourceLockLease Empty { get; } =
        new(Array.Empty<ToolResourceLockKey>(), release: null);

    public IReadOnlyList<ToolResourceLockKey> Keys { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref _release, null)?.Invoke();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Resolves adapter-declared locks and acquires them through the shared lock manager. This keeps
/// lock resolution on the adapter boundary while lock ownership remains centralized and testable.
/// </summary>
public sealed class ExternalToolResourceLockCoordinator
{
    private readonly IToolResourceLockManager _lockManager;

    public ExternalToolResourceLockCoordinator(IToolResourceLockManager lockManager)
    {
        _lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
    }

    public ValueTask<ToolResourceLockLease> AcquireAsync(
        IExternalToolAdapter adapter,
        ToolInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(invocation);

        if (adapter is not IExternalToolResourceLockProvider lockProvider)
        {
            throw new InvalidOperationException(
                $"External tool adapter '{adapter.GetType().FullName}' must explicitly declare resource locks.");
        }

        var keys = lockProvider.GetResourceLockKeys(invocation)
            ?? throw new InvalidOperationException("External tool adapter returned a null resource-lock set.");

        return _lockManager.AcquireAsync(keys, cancellationToken);
    }
}
