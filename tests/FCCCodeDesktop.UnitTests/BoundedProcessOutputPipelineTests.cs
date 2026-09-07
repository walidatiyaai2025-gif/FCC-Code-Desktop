using System.Globalization;
using System.Text;
using FCCCodeDesktop.Runtime;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class BoundedProcessOutputPipelineTests
{
    private static readonly string[] AllDeliveryFixtureLines = ["a", "b", "c", "d", "e"];
    private static readonly string[] PendingDeliveryFixtureLines = ["a", "b"];

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

    [Fact]
    public async Task RetainedEntryAndByteBoundsKeepLatestWithExactEvictionAccounting()
    {
        var policy = new ProcessOutputPolicy(
            maximumRetainedEntries: 3,
            maximumRetainedUtf8Bytes: 6,
            maximumEntryCharacters: 6,
            maximumEntryUtf8Bytes: 6,
            maximumPartialLineCharacters: 6,
            maximumPendingDeliveryEntries: 8,
            readBufferCharacters: 4);
        await using var pipeline = CreatePipeline(policy);

        await pipeline.WriteAsync(ProcessOutputSource.StandardOutput, "aa\nbbb\ncccc\n".AsMemory());
        pipeline.CompleteSource(ProcessOutputSource.StandardOutput);
        pipeline.CompleteSource(ProcessOutputSource.StandardError);
        var snapshot = pipeline.GetSnapshot();

        var retained = Assert.Single(snapshot.Entries);
        Assert.Equal("cccc", retained.Text);
        Assert.Equal(4, retained.RetainedUtf8Bytes);
        Assert.Equal(3, snapshot.Statistics.AcceptedEntries);
        Assert.Equal(9, snapshot.Statistics.AcceptedUtf8Bytes);
        Assert.Equal(2, snapshot.Statistics.EvictedEntries);
        Assert.Equal(5, snapshot.Statistics.EvictedUtf8Bytes);
        Assert.Equal(1, snapshot.Statistics.RetainedEntries);
        Assert.Equal(4, snapshot.Statistics.RetainedUtf8Bytes);
    }

    [Fact]
    public async Task VeryLongLineUsesFixedPartialBufferAndReportsExactCharacterTruncation()
    {
        var policy = new ProcessOutputPolicy(
            maximumRetainedEntries: 2,
            maximumRetainedUtf8Bytes: 8,
            maximumEntryCharacters: 8,
            maximumEntryUtf8Bytes: 8,
            maximumPartialLineCharacters: 8,
            maximumPendingDeliveryEntries: 2,
            readBufferCharacters: 4);
        await using var pipeline = CreatePipeline(policy);

        await pipeline.WriteAsync(ProcessOutputSource.StandardError, new string('x', 10_000).AsMemory());
        pipeline.CompleteSource(ProcessOutputSource.StandardOutput);
        pipeline.CompleteSource(ProcessOutputSource.StandardError);
        var snapshot = pipeline.GetSnapshot();

        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(new string('x', 8), entry.Text);
        Assert.Equal(8, entry.RetainedUtf8Bytes);
        Assert.True(entry.IsTruncated);
        Assert.Equal(9_992, entry.TruncatedCharacters);
        Assert.Equal(1, snapshot.Statistics.TruncatedEntries);
        Assert.Equal(9_992, snapshot.Statistics.TruncatedCharacters);
    }

    [Fact]
    public async Task Utf8ByteBoundNeverSplitsNonBmpTextAndCountsDiscardedUtf16Characters()
    {
        var policy = new ProcessOutputPolicy(
            maximumRetainedEntries: 2,
            maximumRetainedUtf8Bytes: 8,
            maximumEntryCharacters: 8,
            maximumEntryUtf8Bytes: 5,
            maximumPartialLineCharacters: 8,
            maximumPendingDeliveryEntries: 2,
            readBufferCharacters: 4);
        await using var pipeline = CreatePipeline(policy);

        await pipeline.WriteAsync(ProcessOutputSource.StandardOutput, "a😀ب\n".AsMemory());
        pipeline.CompleteSource(ProcessOutputSource.StandardOutput);
        pipeline.CompleteSource(ProcessOutputSource.StandardError);
        var entry = Assert.Single(pipeline.GetSnapshot().Entries);

        Assert.Equal("a😀", entry.Text);
        Assert.Equal(5, entry.RetainedUtf8Bytes);
        Assert.True(entry.IsTruncated);
        Assert.Equal(1, entry.TruncatedCharacters);
        Assert.Equal(Encoding.UTF8.GetByteCount(entry.Text), entry.RetainedUtf8Bytes);
    }

    [Fact]
    public async Task FullDeliveryQueueDropsOnlyNotificationsAndReportsExactLoss()
    {
        var policy = new ProcessOutputPolicy(
            maximumRetainedEntries: 8,
            maximumRetainedUtf8Bytes: 64,
            maximumEntryCharacters: 8,
            maximumEntryUtf8Bytes: 8,
            maximumPartialLineCharacters: 8,
            maximumPendingDeliveryEntries: 2,
            readBufferCharacters: 4);
        await using var pipeline = CreatePipeline(policy);

        await pipeline.WriteAsync(ProcessOutputSource.StandardOutput, "a\nb\nc\nd\ne\n".AsMemory());
        pipeline.CompleteSource(ProcessOutputSource.StandardOutput);
        pipeline.CompleteSource(ProcessOutputSource.StandardError);

        var delivered = new List<ProcessLogEntry>();
        await foreach (var entry in pipeline.ReadEntriesAsync())
        {
            delivered.Add(entry);
        }

        var snapshot = pipeline.GetSnapshot();
        Assert.Equal(AllDeliveryFixtureLines, snapshot.Entries.Select(static entry => entry.Text));
        Assert.Equal(PendingDeliveryFixtureLines, delivered.Select(static entry => entry.Text));
        Assert.Equal(3, snapshot.Statistics.DroppedDeliveryEntries);
        Assert.Equal(3, snapshot.Statistics.DroppedDeliveryUtf8Bytes);
        Assert.Equal(0, snapshot.Statistics.EvictedEntries);
    }

    [Fact]
    public async Task CancelledWriteDoesNotMutateAndPipelineRecovers()
    {
        await using var pipeline = CreatePipeline();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => pipeline.WriteAsync(
                    ProcessOutputSource.StandardOutput,
                    "rejected\n".AsMemory(),
                    cancellation.Token)
                .AsTask());

        await pipeline.WriteAsync(ProcessOutputSource.StandardError, "accepted\n".AsMemory());
        pipeline.CompleteSource(ProcessOutputSource.StandardOutput);
        pipeline.CompleteSource(ProcessOutputSource.StandardError);

        var entry = Assert.Single(pipeline.GetSnapshot().Entries);
        Assert.Equal("accepted", entry.Text);
        Assert.Equal(ProcessOutputSource.StandardError, entry.Source);
    }

    [Fact]
    public async Task ReadFailureIsTypedAndStillFlushesFinalPartialLine()
    {
        await using var pipeline = CreatePipeline();

        await pipeline.WriteAsync(ProcessOutputSource.StandardError, "before failure".AsMemory());
        pipeline.CompleteSource(ProcessOutputSource.StandardOutput);
        pipeline.CompleteSource(ProcessOutputSource.StandardError, readFailed: true);
        var statistics = await pipeline.Completion;

        var entry = Assert.Single(pipeline.GetSnapshot().Entries);
        Assert.Equal("before failure", entry.Text);
        Assert.Equal(ProcessOutputStreamState.ReadFailed, statistics.StandardErrorState);
        Assert.True(statistics.IsCompleted);
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
