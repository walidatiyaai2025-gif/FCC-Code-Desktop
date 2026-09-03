namespace FCCCodeDesktop.Core.State;

public sealed record PersistedProject(
    Guid Id,
    string RootPath,
    string DisplayName,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record PersistedSession(
    Guid Id,
    Guid ProjectId,
    string? RuntimeSessionId,
    string Title,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record PersistedMessage(
    Guid Id,
    Guid SessionId,
    long Sequence,
    string Role,
    string Content,
    DateTimeOffset CreatedUtc);
