namespace FCCCodeDesktop.Fcc;

public sealed class FccStructuredAgentRuntimeOptions
{
    public int MaximumPayloadCharacters { get; init; } = 64 * 1024;

    internal void Validate()
    {
        if (MaximumPayloadCharacters < 1024 || MaximumPayloadCharacters > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPayloadCharacters),
                "Structured runtime payload bound must be between 1 KiB and 1 MiB.");
        }
    }
}
