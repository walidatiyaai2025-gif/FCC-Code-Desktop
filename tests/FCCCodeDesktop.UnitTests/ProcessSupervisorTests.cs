using System.Diagnostics;
using System.Globalization;
using FCCCodeDesktop.Runtime;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ProcessSupervisorTests
{
    [Fact]
    public async Task TracksOwnedRootUntilNaturalExit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        await using var supervisor = new ProcessSupervisor();

        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "cmd.exe",
                ["/d", "/s", "/c", "ping 127.0.0.1 -n 2 > nul & exit /b 7"],
                directory.Path));

        Assert.Equal(ProcessLaunchStatus.Started, launch.Status);
        await using var owned = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);
        var snapshot = Assert.Single(supervisor.GetActiveProcesses());
        Assert.Equal(owned.OwnershipId, snapshot.OwnershipId);
        Assert.Equal(owned.RootProcessId, snapshot.RootProcessId);

        var result = await owned.Completion.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(7, result.RootExitCode);
        Assert.False(result.ForcedTerminationRequested);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task OwnedTreeTerminationKillsDescendantButPreservesUnownedProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var childPidFile = Path.Combine(directory.Path, "child.pid");
        var escapedPidFile = childPidFile.Replace("'", "''", StringComparison.Ordinal);
        var script =
            "$child = Start-Process -FilePath 'cmd.exe' " +
            "-ArgumentList @('/d','/s','/c','ping 127.0.0.1 -n 30 > nul') -PassThru; " +
            $"[System.IO.File]::WriteAllText('{escapedPidFile}', [string]$child.Id); exit 0";

        await using var supervisor = new ProcessSupervisor();
        // Keep the process CWD outside the disposable evidence directory. The PID file still
        // proves descendant identity, while fixture cleanup no longer depends on Windows console
        // support-process directory-handle rundown after the owned job has reached zero processes.
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                Environment.SystemDirectory));
        Assert.Equal(ProcessLaunchStatus.Started, launch.Status);
        await using var owned = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        await WaitUntilAsync(() => File.Exists(childPidFile), TimeSpan.FromSeconds(10));
        var childPidText = await File.ReadAllTextAsync(childPidFile);
        var childPid = int.Parse(childPidText.Trim(), CultureInfo.InvariantCulture);
        await WaitUntilAsync(() => IsProcessRunning(childPid), TimeSpan.FromSeconds(5));

        using var unowned = StartUnownedSentinel(Environment.SystemDirectory);
        try
        {
            await Task.Delay(200);
            Assert.False(owned.Completion.IsCompleted);
            Assert.False(unowned.HasExited);

            await owned.TerminateOwnedTreeAsync();
            var result = await owned.Completion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(result.ForcedTerminationRequested);
            await WaitUntilAsync(() => !IsProcessRunning(childPid), TimeSpan.FromSeconds(5));
            Assert.False(unowned.HasExited);
            Assert.Empty(supervisor.GetActiveProcesses());
        }
        finally
        {
            KillIfRunning(unowned);
        }
    }

    [Fact]
    public async Task SupervisorDisposalCleansOnlyItsOwnedTree()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "cmd.exe",
                ["/d", "/s", "/c", "ping 127.0.0.1 -n 30 > nul"],
                directory.Path));
        Assert.Equal(ProcessLaunchStatus.Started, launch.Status);
        var owned = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);
        var rootPid = owned.RootProcessId;

        using var unowned = StartUnownedSentinel(Environment.SystemDirectory);
        try
        {
            Assert.True(IsProcessRunning(rootPid));
            Assert.False(unowned.HasExited);

            await supervisor.DisposeAsync();

            await WaitUntilAsync(() => !IsProcessRunning(rootPid), TimeSpan.FromSeconds(5));
            Assert.False(unowned.HasExited);
            await owned.DisposeAsync();
        }
        finally
        {
            KillIfRunning(unowned);
            await supervisor.DisposeAsync();
        }
    }

    [Fact]
    public async Task MissingExecutableReturnsTypedFailureWithoutOwnershipLeak()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        await using var supervisor = new ProcessSupervisor();
        var missing = Path.Combine(directory.Path, $"missing-{Guid.NewGuid():N}.exe");

        var result = await supervisor.StartAsync(
            new ProcessLaunchRequest(missing, [], directory.Path));

        Assert.Equal(ProcessLaunchStatus.ExecutableNotFound, result.Status);
        Assert.Null(result.Process);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task PreCancelledLaunchDoesNotStartOrRegisterProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        await using var supervisor = new ProcessSupervisor();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => supervisor.StartAsync(
                new ProcessLaunchRequest(
                    "cmd.exe",
                    ["/d", "/s", "/c", "exit /b 0"],
                    directory.Path),
                cancellation.Token));

        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task ArgumentSafetyBoundRejectsOversizedLaunchBeforeMutation()
    {
        using var directory = new TemporaryDirectory();
        await using var supervisor = new ProcessSupervisor();
        var arguments = Enumerable.Repeat("x", ProcessSupervisor.MaximumArgumentCount + 1).ToArray();

        await Assert.ThrowsAsync<ArgumentException>(
            () => supervisor.StartAsync(
                new ProcessLaunchRequest("cmd.exe", arguments, directory.Path)));

        Assert.Empty(supervisor.GetActiveProcesses());
    }

    private static Process StartUnownedSentinel(string workingDirectory)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/s");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("ping 127.0.0.1 -n 30 > nul");

        var process = Process.Start(startInfo);
        return process ?? throw new InvalidOperationException("Failed to start unowned sentinel process.");
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch (InvalidOperationException)
        {
            // Process completed concurrently with test cleanup.
        }
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(predicate(), "Timed out waiting for the expected process-test condition.");
    }
}
