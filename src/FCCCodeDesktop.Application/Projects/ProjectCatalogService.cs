using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.Application.Projects;

public sealed class ProjectCatalogService
{
    public const int DefaultRecentProjectCount = 20;
    public const int MaximumRecentProjectCount = 100;

    private readonly IProjectCatalogStore _store;
    private readonly IProjectDirectoryProbe _directoryProbe;
    private readonly TimeProvider _timeProvider;

    public ProjectCatalogService(
        IProjectCatalogStore store,
        IProjectDirectoryProbe directoryProbe,
        TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _directoryProbe = directoryProbe ?? throw new ArgumentNullException(nameof(directoryProbe));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PersistedProject> OpenProjectAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(rootPath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRootPath = _directoryProbe.NormalizeRootPath(rootPath);
        if (!_directoryProbe.DirectoryExists(normalizedRootPath))
        {
            throw new DirectoryNotFoundException($"Project folder does not exist: {normalizedRootPath}");
        }

        var displayName = _directoryProbe.GetDisplayName(normalizedRootPath);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("Project display name could not be resolved from the selected folder.");
        }

        var existing = await _store
            .FindProjectByRootPathAsync(normalizedRootPath, cancellationToken)
            .ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        PersistedProject project;
        if (existing is null)
        {
            project = new PersistedProject(
                Guid.NewGuid(),
                normalizedRootPath,
                displayName,
                now,
                now);
        }
        else
        {
            var updatedUtc = now > existing.UpdatedUtc ? now : existing.UpdatedUtc;
            project = existing with
            {
                RootPath = normalizedRootPath,
                DisplayName = displayName,
                UpdatedUtc = updatedUtc,
            };
        }

        await _store.UpsertProjectAsync(project, cancellationToken).ConfigureAwait(false);
        return project;
    }

    public Task<IReadOnlyList<PersistedProject>> ListRecentProjectsAsync(
        int maximumCount = DefaultRecentProjectCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount < 1 || maximumCount > MaximumRecentProjectCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount),
                maximumCount,
                $"Recent project count must be between 1 and {MaximumRecentProjectCount}.");
        }

        return _store.ListRecentProjectsAsync(maximumCount, cancellationToken);
    }
}
