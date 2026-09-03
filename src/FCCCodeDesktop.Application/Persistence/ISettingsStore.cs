using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.Application.Persistence;

public interface ISettingsStore
{
    Task UpsertGlobalSettingAsync(
        PersistedSetting setting,
        CancellationToken cancellationToken = default);

    Task<PersistedSetting?> GetGlobalSettingAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersistedSetting>> ListGlobalSettingsAsync(
        CancellationToken cancellationToken = default);

    Task<bool> DeleteGlobalSettingAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task UpsertProjectSettingAsync(
        Guid projectId,
        PersistedSetting setting,
        CancellationToken cancellationToken = default);

    Task<PersistedSetting?> GetProjectSettingAsync(
        Guid projectId,
        string key,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersistedSetting>> ListProjectSettingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteProjectSettingAsync(
        Guid projectId,
        string key,
        CancellationToken cancellationToken = default);
}
