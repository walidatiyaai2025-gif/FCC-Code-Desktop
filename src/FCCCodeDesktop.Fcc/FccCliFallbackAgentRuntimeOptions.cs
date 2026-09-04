namespace FCCCodeDesktop.Fcc;

public sealed class FccCliFallbackAgentRuntimeOptions
{
    public int MaximumOutputCharacters { get; init; } = 64 * 1024;

    internal void Validate()
    {
        if (MaximumOutputCharacters < 1024 || MaximumOutputCharacters > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumOutputCharacters),
                "CLI fallback output bound must be between 1 KiB and 1 MiB.");
        }
    }
}
