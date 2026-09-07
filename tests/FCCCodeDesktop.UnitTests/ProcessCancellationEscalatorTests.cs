using FCCCodeDesktop.Runtime;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ProcessCancellationEscalatorTests
{
    [Fact]
    public async Task GracefulRequestCompletesOwnedTreeWithoutForcedTermination()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var signalPath = Path.Combine(directory.Path, "graceful.stop");
        var escapedSignal = signalPath.Replace("'", "''", StringComparison.Ordinal);
        var script =
            $"while (-not [System.IO.File]::Exists('{escapedSignal}')) {{ Start-Sleep -Milliseconds 50 }}; exit 0";

        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                Environment.SystemDirectory));
        Assert.True(launch.IsStarted);
        await using var owned = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        var escalator = new ProcessCancellationEscalator();
        var result = await escalator.CancelAsync(
            owned,
            async cancellationToken =>
            {
                await File.WriteAllTextAsync(signalPath, "stop", cancellationToken);
            },
            TimeSpan.FromSeconds(5));

        Assert.Equal(ProcessCancellationOutcome.GracefulExit, result.Outcome);
        Assert.Equal(GracefulStopRequestStatus.Completed, result.GracefulRequestStatus);
        Assert.False(result.ForcedTerminationRequested);
        Assert.Equal(0, result.Exit.RootExitCode);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task GracePeriodExpiryForcesOnlyOwnedTree()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30"],
                Environment.SystemDirectory));
        Assert.True(launch.IsStarted);
        await using var owned = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        var escalator = new ProcessCancellationEscalator();
        var started = DateTimeOffset.UtcNow;
        var result = await escalator.CancelAsync(
            owned,
            static _ => ValueTask.CompletedTask,
            TimeSpan.FromMilliseconds(250));

        Assert.Equal(ProcessCancellationOutcome.ForcedExit, result.Outcome);
        Assert.Equal(GracefulStopRequestStatus.Completed, result.GracefulRequestStatus);
        Assert.True(result.ForcedTerminationRequested);
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(10));
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task GracefulRequestFailureFallsBackToForcedOwnedCleanup()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30"],
                Environment.SystemDirectory));
        Assert.True(launch.IsStarted);
        await using var owned = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        var escalator = new ProcessCancellationEscalator();
        var result = await escalator.CancelAsync(
            owned,
            static _ => ValueTask.FromException(new InvalidOperationException("fixture graceful failure")),
            TimeSpan.FromSeconds(2));

        Assert.Equal(ProcessCancellationOutcome.ForcedExit, result.Outcome);
        Assert.Equal(GracefulStopRequestStatus.Failed, result.GracefulRequestStatus);
        Assert.Contains("fixture graceful failure", result.GracefulFailureMessage, StringComparison.Ordinal);
        Assert.True(result.ForcedTerminationRequested);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task MissingGracefulRequestForcesImmediatelyWithoutWaitingGraceWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30"],
                Environment.SystemDirectory));
        Assert.True(launch.IsStarted);
        await using var owned = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        var escalator = new ProcessCancellationEscalator();
        var started = DateTimeOffset.UtcNow;
        var result = await escalator.CancelAsync(
            owned,
            requestGracefulStop: null,
            gracePeriod: TimeSpan.FromSeconds(10));

        Assert.Equal(ProcessCancellationOutcome.ForcedExit, result.Outcome);
        Assert.Equal(GracefulStopRequestStatus.NotProvided, result.GracefulRequestStatus);
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(5));
        Assert.True(result.ForcedTerminationRequested);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task PreCancelledOperationDoesNotMutateOwnedProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30"],
                Environment.SystemDirectory));
        Assert.True(launch.IsStarted);
        await using var owned = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var escalator = new ProcessCancellationEscalator();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => escalator.CancelAsync(
                owned,
                static _ => ValueTask.CompletedTask,
                TimeSpan.FromSeconds(1),
                cancellation.Token));

        Assert.False(owned.Completion.IsCompleted);
        Assert.Single(supervisor.GetActiveProcesses());

        await owned.TerminateOwnedTreeAsync();
        var cleanup = await owned.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(cleanup.ForcedTerminationRequested);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task AlreadyCompletedProcessReturnsWithoutGracefulOrForcedMutation()
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
                ["/d", "/s", "/c", "exit /b 4"],
                directory.Path));
        Assert.True(launch.IsStarted);
        await using var owned = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);
        var completed = await owned.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(completed.ForcedTerminationRequested);

        var gracefulInvoked = false;
        var escalator = new ProcessCancellationEscalator();
        var result = await escalator.CancelAsync(
            owned,
            _ =>
            {
                gracefulInvoked = true;
                return ValueTask.CompletedTask;
            });

        Assert.Equal(ProcessCancellationOutcome.AlreadyCompleted, result.Outcome);
        Assert.Equal(4, result.Exit.RootExitCode);
        Assert.False(result.ForcedTerminationRequested);
        Assert.False(gracefulInvoked);
    }

    [Fact]
    public async Task GracePeriodSafetyBoundsRejectInvalidValuesBeforeMutation()
    {
        var fake = new IncompleteSupervisedProcess();
        var escalator = new ProcessCancellationEscalator();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => escalator.CancelAsync(fake, gracePeriod: TimeSpan.Zero));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => escalator.CancelAsync(
                fake,
                gracePeriod: ProcessCancellationEscalator.MaximumGracePeriod + TimeSpan.FromMilliseconds(1)));

        Assert.False(fake.TerminateCalled);
    }

    private sealed class IncompleteSupervisedProcess : ISupervisedProcess
    {
        private readonly TaskCompletionSource<OwnedProcessExit> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Guid OwnershipId { get; } = Guid.NewGuid();

        public int RootProcessId => 42;

        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;

        public Task<OwnedProcessExit> Completion => _completion.Task;

        public bool TerminateCalled { get; private set; }

        public ValueTask TerminateOwnedTreeAsync(CancellationToken cancellationToken = default)
        {
            TerminateCalled = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
