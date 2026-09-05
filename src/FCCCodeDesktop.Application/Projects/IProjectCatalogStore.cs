using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.Application.Projects;

public interface IProjectCatalogStore
{
    Task<PersistedProject?> FindProjectByRootPathAsync(
        string rootPath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersistedProject>> ListRecentProjectsAsync(
        int maximumCount,
        CancellationToken cancellationToken = default);

    Task UpsertProjectAsync(
        PersistedProject project,
        CancellationToken cancellationToken = default);
}
