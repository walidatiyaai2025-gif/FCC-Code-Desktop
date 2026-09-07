using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.UnitTests;

public sealed class ShellProfileTests
{
    [Fact]
    public void DefaultProfilesAreDeterministicAndWindowsBound()
    {
        var profiles = DefaultShellProfiles.All;

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, p => p.Id == "powershell" && p.Executable == "powershell.exe");
        Assert.Contains(profiles, p => p.Id == "cmd" && p.Executable == "cmd.exe");
        Assert.All(profiles, p => Assert.True(p.RequiresWindows));
    }

    [Fact]
    public void ProfileRejectsMissingIdentity()
    {
        Assert.Throws<ArgumentException>(() => new ShellProfile("", "cmd", "cmd.exe", [], true));
    }
}
