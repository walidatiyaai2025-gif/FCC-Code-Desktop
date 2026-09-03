namespace FCCCodeDesktop.Persistence;

public sealed record SqliteInitializationResult(
    string DatabasePath,
    int CurrentVersion,
    IReadOnlyList<int> AppliedVersions);
