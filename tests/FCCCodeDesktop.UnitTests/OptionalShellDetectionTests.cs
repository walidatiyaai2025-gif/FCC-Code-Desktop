using FCCCodeDesktop.Application.Terminal;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class OptionalShellDetectionTests
{
    [Fact]
    public void InstallationRequiresFullyQualifiedPath()
    {
        Assert.Throws<ArgumentException>(
            () => new OptionalShellInstallation(
                OptionalShellKind.GitBash,
                "relative\\bash.exe",
                OptionalShellDetectionSource.StandardInstall));
    }

    [Fact]
    public void ResultSnapshotsInputAndSupportsTypedLookup()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var bashPath = Path.Combine(root, "fixture", "git", "bin", "bash.exe");
        var wslPath = Path.Combine(root, "fixture", "windows", "System32", "wsl.exe");
        var mutable = new List<OptionalShellInstallation>
        {
            new(
                OptionalShellKind.GitBash,
                bashPath,
                OptionalShellDetectionSource.StandardInstall),
            new(
                OptionalShellKind.Wsl,
                wslPath,
                OptionalShellDetectionSource.WindowsSystem),
        };

        var result = new OptionalShellDetectionResult(mutable);
        mutable.Clear();

        Assert.Equal(2, result.Installations.Count);
        Assert.True(result.IsDetected(OptionalShellKind.GitBash));
        Assert.True(result.IsDetected(OptionalShellKind.Wsl));
        Assert.Equal(
            Path.GetFullPath(bashPath),
            result.Find(OptionalShellKind.GitBash)?.ExecutablePath);
        Assert.Equal(
            OptionalShellDetectionSource.WindowsSystem,
            result.Find(OptionalShellKind.Wsl)?.Source);
    }

    [Fact]
    public void ResultReturnsNullForAbsentKind()
    {
        var result = new OptionalShellDetectionResult([]);

        Assert.False(result.IsDetected(OptionalShellKind.GitBash));
        Assert.False(result.IsDetected(OptionalShellKind.Wsl));
        Assert.Null(result.Find(OptionalShellKind.GitBash));
        Assert.Null(result.Find(OptionalShellKind.Wsl));
    }
}
