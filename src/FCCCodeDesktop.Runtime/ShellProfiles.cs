namespace FCCCodeDesktop.Runtime;

/// <summary>
/// Immutable command shell launch profiles used by terminal hosts.
/// The profile only describes a launch contract; it does not start processes.
/// </summary>
public sealed record ShellProfile
{
    public ShellProfile(
        string id,
        string displayName,
        string executable,
        IReadOnlyList<string>? arguments,
        bool requiresWindows)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Profile id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(executable))
        {
            throw new ArgumentException("Executable is required.", nameof(executable));
        }

        Id = id;
        DisplayName = displayName;
        Executable = executable;
        Arguments = arguments is null
            ? Array.Empty<string>()
            : Array.AsReadOnly(arguments.ToArray());
        RequiresWindows = requiresWindows;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Executable { get; }
    public IReadOnlyList<string> Arguments { get; }
    public bool RequiresWindows { get; }
}

public static class DefaultShellProfiles
{
    public static IReadOnlyList<ShellProfile> WindowsProfiles { get; } =
        Array.AsReadOnly<ShellProfile>(
        [
            new("powershell", "PowerShell", "powershell.exe", ["-NoLogo"], true),
            new("cmd", "Command Prompt", "cmd.exe", Array.Empty<string>(), true),
        ]);

    public static IReadOnlyList<ShellProfile> All => WindowsProfiles;
}
