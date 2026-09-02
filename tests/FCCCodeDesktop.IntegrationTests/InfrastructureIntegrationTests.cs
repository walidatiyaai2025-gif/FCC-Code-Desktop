using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class InfrastructureIntegrationTests
{
    [Fact]
    public async Task DotNetProcessUsesPinnedSdkFromDisposableUnicodeWorkspace()
    {
        using var workspace = new TemporaryDirectory("fccd integration مساحة");
        File.Copy(
            Path.Combine(FindRepositoryRoot(), "global.json"),
            workspace.GetPath("global.json"));

        var result = await TestProcess.RunAsync(
            "dotnet",
            "--version",
            workspace.Path,
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("10.0.400", result.StandardOutput.Trim());
    }

    [Fact]
    public async Task InvalidDotNetArgumentReturnsNonZeroWithoutTouchingOwnerData()
    {
        using var workspace = new TemporaryDirectory("fccd-integration-negative");
        File.Copy(
            Path.Combine(FindRepositoryRoot(), "global.json"),
            workspace.GetPath("global.json"));

        var result = await TestProcess.RunAsync(
            "dotnet",
            "--fccd-invalid-option",
            workspace.Path,
            CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
        Assert.True(Directory.Exists(workspace.Path));
    }

    [Fact]
    public async Task CancelledProcessIsTerminatedAndWorkspaceRemainsReusable()
    {
        using var workspace = new TemporaryDirectory("fccd-integration-cancel");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TestProcess.RunAsync(
                "pwsh",
                "-NoProfile -Command \"Start-Sleep -Seconds 30\"",
                workspace.Path,
                cancellation.Token));

        var recoveryFile = workspace.GetPath(Path.Combine("recovery space", "بعد.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(recoveryFile)!);
        File.WriteAllText(recoveryFile, "recovered", System.Text.Encoding.UTF8);

        Assert.Equal("recovered", File.ReadAllText(recoveryFile, System.Text.Encoding.UTF8));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
                File.Exists(Path.Combine(current.FullName, "FCCCodeDesktop.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the FCC Code Desktop repository root from the test output directory.");
    }
}
