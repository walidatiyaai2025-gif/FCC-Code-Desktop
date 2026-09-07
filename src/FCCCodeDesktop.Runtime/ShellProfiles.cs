namespace FCCCodeDesktop.Runtime;

/// <summary>
/// Immutable command shell launch profiles used by terminal hosts.
/// The profile only describes a launch contract; it does not start processes.
/// </summary>
public sealed record ShellProfile(
    string Id,
    string DisplayName,
    string Executable,
    IReadOnlyList<string> Arguments,
    bool RequiresWindows)
{
    public ShellProfile
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new ArgumentException("Profile id is required.", nameof(Id));
        if (string.IsNullOrWhiteSpace(DisplayName))
            throw new ArgumentException("Display name is required.", nameof(DisplayName));
        if (string.IsNullOrWhiteSpace(Executable))
            throw new ArgumentException("Executable is required.", nameof(Executable));
        Arguments ??= Array.Empty<string>();
    }
}

public static class DefaultShellProfiles
{
    public static IReadOnlyList<ShellProfile> WindowsProfiles { get; } =
    [
        new("powershell", "PowerShell", "powershell.exe", ["-NoLogo"], true),
        new("cmd", "Command Prompt", "cmd.exe", Array.Empty<string>(), true),
    ];

    public static IReadOnlyList<ShellProfile> All => WindowsProfiles;
}
