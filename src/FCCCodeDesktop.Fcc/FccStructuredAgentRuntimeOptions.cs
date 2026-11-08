namespace FCCCodeDesktop.Fcc;

public sealed class FccStructuredAgentRuntimeOptions
{
    public int MaximumPayloadCharacters { get; init; } = 64 * 1024;
    public bool AcceptFileEdits { get; init; }
    public string? Model { get; init; }
    public Func<FccToolPermissionRequest, CancellationToken, Task<bool>>? PermissionHandler { get; init; }
    public string? AppendSystemPrompt { get; init; }
    public TimeSpan FirstResponseTimeout { get; init; } = TimeSpan.FromMinutes(2);

    internal void Validate()
    {
        if (FirstResponseTimeout <= TimeSpan.Zero || FirstResponseTimeout > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(nameof(FirstResponseTimeout));
        }
        if (Model is not null && (string.IsNullOrWhiteSpace(Model) || Model.Length > 256 || Model.Any(char.IsControl)))
        {
            throw new ArgumentException("A valid FCC model identifier is required.", nameof(Model));
        }
        if (AppendSystemPrompt?.Length > 16 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(AppendSystemPrompt));
        }
        if (MaximumPayloadCharacters < 1024 || MaximumPayloadCharacters > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPayloadCharacters),
                "Structured runtime payload bound must be between 1 KiB and 1 MiB.");
        }
    }
}
