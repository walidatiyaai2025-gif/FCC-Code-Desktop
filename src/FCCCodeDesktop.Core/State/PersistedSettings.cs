namespace FCCCodeDesktop.Core.State;

public sealed record PersistedSetting(
    string Key,
    string ValueJson,
    DateTimeOffset UpdatedUtc);
