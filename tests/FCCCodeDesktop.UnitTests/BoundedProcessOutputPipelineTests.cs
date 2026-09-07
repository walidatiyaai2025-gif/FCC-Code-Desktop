using System.Globalization;
using System.Text;
using FCCCodeDesktop.Runtime;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class BoundedProcessOutputPipelineTests
{
    [Fact]
    public async Task FramesSplitAndMultipleLinesAcrossBothSourcesWithDeterministicSequences()
    {
        await using var pipeline = CreatePipeline();

        await pipeline.WriteAsync(ProcessOutputSource.StandardOutput, "one\r".AsMemory());
        await pipeline.WriteAsync(ProcessOutputSource.StandardError, "error\npart".AsMemory());
        await pipeline.WriteAsync(ProcessOutputSource.StandardOutput, "\ntwo\n".AsMemory());
        pipeline.CompleteSource(ProcessOutputSource.StandardOutput);
        pipeline.CompleteSource(ProcessOutputSource.StandardError);

        var completed = await pipeline.Completion;
        var snapshot = pipeline.GetSnapshot();

        Assert.True(completed.IsCompleted);
        Assert.Equal(ProcessOutputStreamState.Completed, completed.StandardOutputState);
        Assert.Equal(ProcessOutputStreamState.Completed, completed.StandardErrorState);
        Assert.Collection(
            snapshot.Entries,
            entry => AssertEntry(entry, 1, ProcessOutputSource.StandardOutput, "one"),
            entry => AssertEntry(entry, 2, ProcessOutputSource.StandardError, "error"),
            entry => AssertEntry(entry, 3, ProcessOutputSource.StandardOutput, "two"),
            entry => AssertEntry(entry, 4, ProcessOutputSource.StandardError, "part"));
    }

    [Fact]
    public async Task HandlesCrlfLfLoneCrFinalPartialUnicodeArabicAndEmoji()
    {
        await using var pipeline = CreatePipeline();

        await pipeline.WriteAsync(ProcessOutputSource.StandardOutput, "العربية\r".AsMemory());
        await pipeline.WriteAsync(
            ProcessOutputSource.StandardOutput,
            "\nemoji 😀\nsolo\rfinal".AsMemory());
        pipeline.CompleteSource(ProcessOutputSource.StandardOutput);
        pipeline.CompleteSource(ProcessOutputSource.StandardError);

        var snapshot = pipeline.GetSnapshot();
        var expected = new[] { "العربية", "emoji 😀", "solo", "final" };

        Assert.Equal(expected, snapshot.Entries.Select(static entry => entry.Text));
        Assert.All(snapshot.Entries, static entry =>
        {
            Assert.Equal(ProcessOutputSource.StandardOutput, entry.Source);
            Assert.Equal(Encoding.UTF8.GetByteCount(entry.Text), entry.RetainedUtf8Bytes);
            Assert.False(entry.IsTruncated);
        });
    }

    [Fact]
    public async Task ConcurrentWritersPreserveSourceOrderAndUniqueGlobalSequence()
    {
        var policy = new ProcessOutputPolicy(
            maximumRetainedEntries: 256,
            maximumRetainedUtf8Bytes: 32_768,
            maximumEntryCharacters: 64,
            maximumEntryUtf8Bytes: 256,
            maximumPartialLineCharacters: 64,
            maximumPendingDeliveryEntries: 256,
            readBufferCharacters: 16);
        await using var pipeline = CreatePipeline(policy);
        using var start = new ManualResetEventSlim(initialState: false);

        var stdout = Task.Run(async () =>
        {
            start.Wait();
            for (var index = 0; index < 100; index++)
            {
                await pipeline.WriteAsync(
                    ProcessOutputSource.StandardOutput,
                    string.Concat(index.ToString(CultureInfo.InvariantCulture), "\n").AsMemory());
                await Task.Yield();
            }
        });
        var stderr = Task.Run(async () =>
        {
            start.Wait();
            for (var index = 0; index < 100; index++)
            {
                await pipeline.WriteAsync(
                    ProcessOutputSource.StandardError,
                    string.Concat(index.ToString(CultureInfo.InvariantCulture), "\n").AsMemory());
                await Task.Yield();
            }
        });

        start.Set();
        await Task.WhenAll(stdout, stderr);
        pipeline.CompleteSource(ProcessOutputSource.StandardOutput);
        pipeline.CompleteSource(ProcessOutputSource.StandardError);
        var entries = pipeline.GetSnapshot().Entries;

        Assert.Equal(200, entries.Count);
        Assert.Equal(
            Enumerable.Range(1, 200).Select(static value => (long)value),
            entries.Select(static entry => entry.Sequence));
        Assert.Equal(
            Enumerable.Range(0, 100).Select(static value => value.ToString(CultureInfo.InvariantCulture)),
            entries
                .Where(static entry => entry.Source == ProcessOutputSource.StandardOutput)
                .Select(static entry => entry.Text));
        Assert.Equal(
            Enumerable.Range(0, 100).Select(static value => value.ToString(CultureInfo.InvariantCulture)),
            entries
                .Where(static entry => entry.Source == ProcessOutputSource.StandardError)
                .Select(static entry => entry.Text));
    }

    [Fact]
    public async Task EmptyStreamsCompleteWithoutEntriesOrLossClaims()
    {
        await using var pipeline = CreatePipeline();

        pipeline.CompleteSource(ProcessOutputSource.StandardOutput);
        pipeline.CompleteSource(ProcessOutputSource.StandardError);
        var statistics = await pipeline.Completion;

        Assert.Equal(0, statistics.AcceptedEntries);
        Assert.Equal(0, statistics.EvictedEntries);
        Assert.Equal(0, statistics.TruncatedEntries);
        Assert.Equal(0, statistics.DroppedDeliveryEntries);
        Assert.Empty(pipeline.GetSnapshot().Entries);
    }

    private static BoundedProcessOutputPipeline CreatePipeline(ProcessOutputPolicy? policy = null)
    {
        var correlation = new ProcessOutputCorrelation(
            taskId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            processRunId: Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var identity = new ProcessOutputIdentity(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            42,
            correlation.TaskId,
            correlation.AgentRunId,
            correlation.ToolRunId,
            correlation.ProcessRunId,
            correlation.OperationId);
        return new BoundedProcessOutputPipeline(identity, policy);
    }

    private static void AssertEntry(
        ProcessLogEntry entry,
        long sequence,
        ProcessOutputSource source,
        string text)
    {
        Assert.Equal(sequence, entry.Sequence);
        Assert.Equal(source, entry.Source);
        Assert.Equal(text, entry.Text);
        Assert.False(entry.IsTruncated);
        Assert.Equal(0, entry.TruncatedCharacters);
        Assert.NotEqual(Guid.Empty, entry.Identity.OwnershipId);
        Assert.Equal(42, entry.Identity.RootProcessId);
    }
}
