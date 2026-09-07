using FCCCodeDesktop.Application.Terminal;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ConPtyTerminalContractTests
{
    [Fact]
    public void TerminalSizeRequiresPositiveNativeSafeDimensions()
    {
        var size = new TerminalSize(120, 40);

        Assert.Equal(120, size.Columns);
        Assert.Equal(40, size.Rows);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerminalSize(0, 40));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerminalSize(120, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TerminalSize(TerminalSize.MaximumDimension + 1, 40));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TerminalSize(120, TerminalSize.MaximumDimension + 1));
    }

    [Fact]
    public void LaunchRequestRequiresFullyQualifiedPathsAndSnapshotsArguments()
    {
        var arguments = new List<string> { "/d", "/q", "مرحبا world" };
        var executable = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cmd.exe"));
        var workingDirectory = Path.GetFullPath(Path.GetTempPath());

        var request = new ConPtyLaunchRequest(
            executable,
            arguments,
            workingDirectory,
            new TerminalSize(100, 30));
        arguments.Add("later-mutation");

        Assert.Equal(executable, request.ExecutablePath);
        Assert.Equal(workingDirectory, request.WorkingDirectory);
        Assert.Equal(3, request.Arguments.Count);
        Assert.Equal("مرحبا world", request.Arguments[2]);
        Assert.DoesNotContain("later-mutation", request.Arguments);
        Assert.Throws<ArgumentException>(
            () => new ConPtyLaunchRequest("cmd.exe", [], workingDirectory, new TerminalSize(80, 25)));
        Assert.Throws<ArgumentException>(
            () => new ConPtyLaunchRequest(executable, [], ".", new TerminalSize(80, 25)));
    }
}
