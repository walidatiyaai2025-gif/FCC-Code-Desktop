using System.Globalization;
using FCCCodeDesktop.Runtime;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ProcessOutputStressTests
{
    private const int SingleSourceLineCount = 3_000;
    private const int DualSourceLineCount = 5_000;

    [Theory]
    [InlineData(ProcessOutputSource.StandardOutput)]
    [InlineData(ProcessOutputSource.StandardError)]
    public async Task HighVolumeSingleSourceCompletesWithExactCountAndBoundedLatestHistory(
        ProcessOutputSource source)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var writer = source == ProcessOutputSource.StandardOutput
            ? "[Console]::Out"
            : "[Console]::Error";
        var script = string.Concat(
            "for ($i = 0; $i -lt ",
            SingleSourceLineCount.ToString(CultureInfo.InvariantCulture),
            "; $i++) { ",
            writer,
            ".WriteLine(('line-' + $i)) }");
        var policy = CreateStressPolicy();

        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                Environment.SystemDirectory,
                Output: new ProcessOutputOptions(policy)));
        Assert.True(launch.IsStarted);
        await using var process = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        var snapshot = process.Output.GetSnapshot();

        Assert.Equal(0, exit.RootExitCode);
        Assert.Equal(SingleSourceLineCount, snapshot.Statistics.AcceptedEntries);
        Assert.InRange(snapshot.Statistics.RetainedEntries, 1, policy.MaximumRetainedEntries);
        Assert.InRange(
            snapshot.Statistics.RetainedUtf8Bytes,
            1,
            policy.MaximumRetainedUtf8Bytes);
        Assert.Equal(
            SingleSourceLineCount - policy.MaximumPendingDeliveryEntries,
            snapshot.Statistics.DroppedDeliveryEntries);
        Assert.All(snapshot.Entries, entry => Assert.Equal(source, entry.Source));
        Assert.Equal(
            string.Concat("line-", (SingleSourceLineCount - 1).ToString(CultureInfo.InvariantCulture)),
            snapshot.Entries[^1].Text);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task AlternatingHighVolumeBothStreamsCannotDeadlockOnStoppedConsumer()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var script = string.Concat(
            "for ($i = 0; $i -lt ",
            DualSourceLineCount.ToString(CultureInfo.InvariantCulture),
            "; $i++) { [Console]::Out.WriteLine(('out-' + $i)); ",
            "[Console]::Error.WriteLine(('error-' + $i)) }");
        var policy = CreateStressPolicy();

        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                Environment.SystemDirectory,
                Output: new ProcessOutputOptions(policy)));
        Assert.True(launch.IsStarted);
        await using var process = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        var snapshot = process.Output.GetSnapshot();

        Assert.Equal(0, exit.RootExitCode);
        Assert.Equal(DualSourceLineCount * 2L, snapshot.Statistics.AcceptedEntries);
        Assert.Equal(
            (DualSourceLineCount * 2L) - policy.MaximumPendingDeliveryEntries,
            snapshot.Statistics.DroppedDeliveryEntries);
        Assert.InRange(snapshot.Statistics.RetainedEntries, 1, policy.MaximumRetainedEntries);
        Assert.InRange(
            snapshot.Statistics.RetainedUtf8Bytes,
            1,
            policy.MaximumRetainedUtf8Bytes);
        Assert.Contains(
            snapshot.Entries,
            static entry => entry.Source == ProcessOutputSource.StandardOutput);
        Assert.Contains(
            snapshot.Entries,
            static entry => entry.Source == ProcessOutputSource.StandardError);
        Assert.True(snapshot.Statistics.EvictedEntries > 0);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task ForcedStopDuringUnthrottledOutputRemainsBoundedAndCompletesReaders()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var policy = CreateStressPolicy();
        const string script =
            "$i = 0; while ($true) { [Console]::Out.WriteLine(('hot-' + $i)); $i++ }";
        await using var supervisor = new ProcessSupervisor();
        var launch = await supervisor.StartAsync(
            new ProcessLaunchRequest(
                "pwsh.exe",
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                Environment.SystemDirectory,
                Output: new ProcessOutputOptions(policy)));
        Assert.True(launch.IsStarted);
        await using var process = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);
        await WaitForFirstEntryAsync(process.Output);

        await process.TerminateOwnedTreeAsync();
        var exit = await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        var snapshot = process.Output.GetSnapshot();

        Assert.True(exit.ForcedTerminationRequested);
        Assert.True(snapshot.Statistics.AcceptedEntries > 0);
        Assert.InRange(snapshot.Statistics.RetainedEntries, 1, policy.MaximumRetainedEntries);
        Assert.InRange(
            snapshot.Statistics.RetainedUtf8Bytes,
            1,
            policy.MaximumRetainedUtf8Bytes);
        Assert.True(snapshot.Statistics.IsCompleted);
        Assert.Empty(supervisor.GetActiveProcesses());
    }

    [Fact]
    public async Task RepeatedStartsAndStopsLeaveNoOwnedProcessOrReaderLeak()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using var supervisor = new ProcessSupervisor();
        for (var index = 0; index < 5; index++)
        {
            var expected = string.Concat("run-", index.ToString(CultureInfo.InvariantCulture));
            var launch = await supervisor.StartAsync(
                new ProcessLaunchRequest(
                    "cmd.exe",
                    ["/d", "/s", "/c", string.Concat("echo ", expected)],
                    Environment.SystemDirectory));
            Assert.True(launch.IsStarted);
            await using var process = Assert.IsAssignableFrom<ISupervisedProcess>(launch.Process);

            await process.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(expected, Assert.Single(process.Output.GetSnapshot().Entries).Text);
            Assert.True((await process.Output.Completion).IsCompleted);
            Assert.Empty(supervisor.GetActiveProcesses());
        }
    }

    private static ProcessOutputPolicy CreateStressPolicy() =>
        new(
            maximumRetainedEntries: 128,
            maximumRetainedUtf8Bytes: 16 * 1024,
            maximumEntryCharacters: 128,
            maximumEntryUtf8Bytes: 512,
            maximumPartialLineCharacters: 128,
            maximumPendingDeliveryEntries: 4,
            readBufferCharacters: 64);

    private static async Task WaitForFirstEntryAsync(IProcessOutput output)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var _ in output.ReadEntriesAsync(cancellation.Token))
        {
            return;
        }

        throw new InvalidOperationException("The hot output process completed without one entry.");
    }
}
