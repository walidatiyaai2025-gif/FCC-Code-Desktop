using System.Text;
using FCCCodeDesktop.Runtime;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ProcessOutputIntegrationTests
{
    [Fact]
    public async Task ProcessCompletionDrainsBothUnicodeStreamsAndFinalPartialLines()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var workingDirectory = Directory.CreateDirectory(
            Path.Combine(directory.Path, "output fixture with spaces")).FullName;
        var taskId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var toolRunId = Guid.NewGuid();
        var processRunId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var options = new ProcessOutputOptions(
            correlation: new ProcessOutputCorrelation(
                taskId,
                agentRunId,
                toolRunId,
                processRunId,
                operationId));
        const string script =
            "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); " +
            "[Console]::Error.Write('خطأ'); [Console]::Out.Write('مرحبا 😀'); exit 9";

        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                workingDirectory,
                Output: options));
        Assert.True(launch.IsStarted);
        await using var process = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var statistics = await process.Output.Completion.WaitAsync(TimeSpan.FromSeconds(1));
        var entries = process.Output.GetSnapshot().Entries;

        Assert.Equal(9, exit.RootExitCode);
        Assert.True(statistics.IsCompleted);
        Assert.Equal(ProcessOutputStreamState.Completed, statistics.StandardOutputState);
        Assert.Equal(ProcessOutputStreamState.Completed, statistics.StandardErrorState);
        Assert.Collection(
            entries.OrderBy(static entry => entry.Source),
            entry => AssertCorrelatedEntry(
                entry,
                ProcessOutputSource.StandardOutput,
                "مرحبا 😀",
                process,
                taskId,
                agentRunId,
                toolRunId,
                processRunId,
                operationId),
            entry => AssertCorrelatedEntry(
                entry,
                ProcessOutputSource.StandardError,
                "خطأ",
                process,
                taskId,
                agentRunId,
                toolRunId,
                processRunId,
                operationId));
        Assert.Equal(2, entries.Select(static entry => entry.Sequence).Distinct().Count());
        Assert.Equal(1, entries.Min(static entry => entry.Sequence));
        Assert.Equal(2, entries.Max(static entry => entry.Sequence));
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task MalformedUtf8UsesReplacementFallbackWithoutReadFailure()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string script =
            "$stream = [Console]::OpenStandardOutput(); " +
            "$bytes = [byte[]](0x66,0x80,0x6f,0x0a); " +
            "$stream.Write($bytes, 0, $bytes.Length); $stream.Flush()";
        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                Environment.SystemDirectory));
        Assert.True(launch.IsStarted);
        await using var process = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var snapshot = process.Output.GetSnapshot();

        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(ProcessOutputSource.StandardOutput, entry.Source);
        Assert.Equal("f\uFFFDo", entry.Text);
        Assert.Equal(ProcessOutputStreamState.Completed, snapshot.Statistics.StandardOutputState);
    }

    [Fact]
    public async Task EmptyFastNonzeroProcessCompletesBothReadersWithoutHanging()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "cmd.exe",
                ["/d", "/s", "/c", "exit /b 6"],
                Environment.SystemDirectory));
        Assert.True(launch.IsStarted);
        await using var process = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var output = await process.Output.Completion.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(6, exit.RootExitCode);
        Assert.True(output.IsCompleted);
        Assert.Empty(process.Output.GetSnapshot().Entries);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task GracefulCancellationDrainsOutputWrittenAfterStopSignal()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var signalPath = Path.Combine(directory.Path, "output.stop");
        var escapedSignal = signalPath.Replace("'", "''", StringComparison.Ordinal);
        var script =
            "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); " +
            "[Console]::Out.WriteLine('ready'); [Console]::Out.Flush(); " +
            $"while (-not [IO.File]::Exists('{escapedSignal}')) {{ Start-Sleep -Milliseconds 20 }}; " +
            "[Console]::Error.Write('final after stop'); [Console]::Error.Flush(); exit 0";

        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                Environment.SystemDirectory));
        Assert.True(launch.IsStarted);
        await using var process = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);
        await WaitForEntryAsync(process.Output, static entry => entry.Text == "ready");

        var escalator = new ProcessCancellationEscalator();
        var cancellation = await escalator.CancelAsync(
            process,
            token => new ValueTask(File.WriteAllTextAsync(signalPath, "stop", token)),
            TimeSpan.FromSeconds(5));

        Assert.Equal(ProcessCancellationOutcome.GracefulExit, cancellation.Outcome);
        Assert.Contains(
            process.Output.GetSnapshot().Entries,
            static entry =>
                entry.Source == ProcessOutputSource.StandardError &&
                entry.Text == "final after stop");
        Assert.True((await process.Output.Completion).IsCompleted);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task ForcedTerminationCompletesActiveReadersAndRetainsAcceptedOutput()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string script =
            "[Console]::Out.WriteLine('started'); [Console]::Out.Flush(); " +
            "while ($true) { [Console]::Error.WriteLine('streaming'); " +
            "[Console]::Error.Flush(); Start-Sleep -Milliseconds 20 }";
        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                Environment.SystemDirectory));
        Assert.True(launch.IsStarted);
        await using var process = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);
        await WaitForEntryAsync(process.Output, static entry => entry.Text == "started");

        await process.TerminateOwnedTreeAsync();
        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var output = await process.Output.Completion.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(exit.ForcedTerminationRequested);
        Assert.True(output.IsCompleted);
        Assert.Contains(process.Output.GetSnapshot().Entries, static entry => entry.Text == "started");
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task SupervisorDisposalTerminatesTreeAndCompletesOutputDrain()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string script =
            "[Console]::Out.WriteLine('before dispose'); [Console]::Out.Flush(); " +
            "while ($true) { Start-Sleep -Milliseconds 50 }";
        var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                Environment.SystemDirectory));
        Assert.True(launch.IsStarted);
        await using var process = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        try
        {
            await WaitForEntryAsync(process.Output, static entry => entry.Text == "before dispose");
            await supervisor.DisposeAsync();

            var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            var output = await process.Output.Completion.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(exit.ForcedTerminationRequested);
            Assert.True(output.IsCompleted);
            Assert.Contains(
                process.Output.GetSnapshot().Entries,
                static entry => entry.Text == "before dispose");
            Assert.Empty(supervisor.GetActiveProcesses());
        }
        finally
        {
            await supervisor.DisposeAsync();
        }
    }

    private static async Task WaitForEntryAsync(
        IProcessOutput output,
        Func<ProcessLogEntry, bool> predicate)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var entry in output.ReadEntriesAsync(cancellation.Token))
        {
            if (predicate(entry))
            {
                return;
            }
        }

        throw new InvalidOperationException("The process output completed before the expected entry arrived.");
    }

    private static void AssertCorrelatedEntry(
        ProcessLogEntry entry,
        ProcessOutputSource source,
        string text,
        ISupervisedProcess process,
        Guid taskId,
        Guid agentRunId,
        Guid toolRunId,
        Guid processRunId,
        Guid operationId)
    {
        Assert.Equal(source, entry.Source);
        Assert.Equal(text, entry.Text);
        Assert.Equal(Encoding.UTF8.GetByteCount(text), entry.RetainedUtf8Bytes);
        Assert.Equal(process.OwnershipId, entry.Identity.OwnershipId);
        Assert.Equal(process.RootProcessId, entry.Identity.RootProcessId);
        Assert.Equal(taskId, entry.Identity.TaskId);
        Assert.Equal(agentRunId, entry.Identity.AgentRunId);
        Assert.Equal(toolRunId, entry.Identity.ToolRunId);
        Assert.Equal(processRunId, entry.Identity.ProcessRunId);
        Assert.Equal(operationId, entry.Identity.OperationId);
        Assert.True(entry.TimestampUtc >= process.StartedUtc);
        Assert.False(entry.IsTruncated);
    }
}
