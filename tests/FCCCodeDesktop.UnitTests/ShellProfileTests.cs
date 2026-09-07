using FCCCodeDesktop.Runtime;
using Xunit;

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

    [Fact]
    public void ProfileSnapshotsCallerArguments()
    {
        var source = new List<string> { "-NoLogo" };
        var profile = new ShellProfile("powershell", "PowerShell", "powershell.exe", source, true);

        source[0] = "-EncodedCommand";
        source.Add("malicious-change");

        Assert.Equal(["-NoLogo"], profile.Arguments);
    }
}
