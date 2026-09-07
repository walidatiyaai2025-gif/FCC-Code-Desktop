using System.Buffers;
using System.Text;
using System.Threading.Channels;

namespace FCCCodeDesktop.Runtime;

/// <summary>
/// Concurrent, bounded line framing and delivery for one owned process. Writers never wait for a
/// consumer; a full delivery channel is accounted as loss while pipe drainage continues.
/// </summary>
public sealed class BoundedProcessOutputPipeline : IProcessOutput, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly ProcessOutputIdentity _identity;
    private readonly TimeProvider _timeProvider;
    private readonly Queue<ProcessLogEntry> _history;
    private readonly Channel<ProcessLogEntry> _delivery;
    private readonly LineState _standardOutput;
    private readonly LineState _standardError;
    private readonly TaskCompletionSource<ProcessOutputStatistics> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private long _nextSequence;
    private long _acceptedEntries;
    private long _acceptedUtf8Bytes;
    private long _retainedUtf8Bytes;
    private long _evictedEntries;
    private long _evictedUtf8Bytes;
    private long _truncatedEntries;
    private long _truncatedCharacters;
    private long _droppedDeliveryEntries;
    private long _droppedDeliveryUtf8Bytes;
    private bool _disposed;

    public BoundedProcessOutputPipeline(
        ProcessOutputIdentity identity,
        ProcessOutputPolicy? policy = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateIdentity(identity);

        _identity = identity;
        Policy = policy ?? ProcessOutputPolicy.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _history = new Queue<ProcessLogEntry>(
            Math.Min(Policy.MaximumRetainedEntries, ProcessOutputPolicy.DefaultMaximumRetainedEntries));
        _delivery = Channel.CreateBounded<ProcessLogEntry>(
            new BoundedChannelOptions(Policy.MaximumPendingDeliveryEntries)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
        _standardOutput = new LineState(Policy.MaximumPartialLineCharacters);
        _standardError = new LineState(Policy.MaximumPartialLineCharacters);
    }

    public ProcessOutputPolicy Policy { get; }

    public Task<ProcessOutputStatistics> Completion => _completion.Task;

    public ValueTask WriteAsync(
        ProcessOutputSource source,
        ReadOnlyMemory<char> characters,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSource(source);

        if (characters.IsEmpty)
        {
            lock (_gate)
            {
                ThrowIfNotWritable(GetLineState(source), source);
            }

            return ValueTask.CompletedTask;
        }

        lock (_gate)
        {
            var state = GetLineState(source);
            ThrowIfNotWritable(state, source);
            AcceptCharacters(state, source, characters.Span);
        }

        return ValueTask.CompletedTask;
    }

    public void CompleteSource(ProcessOutputSource source, bool readFailed = false)
    {
        ValidateSource(source);

        lock (_gate)
        {
            var state = GetLineState(source);
            if (state.State != ProcessOutputStreamState.Active)
            {
                return;
            }

            EmitFinalPartialLine(state, source);
            state.State = readFailed
                ? ProcessOutputStreamState.ReadFailed
                : ProcessOutputStreamState.Completed;
            TryCompletePipeline();
        }
    }

    public ProcessOutputSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new ProcessOutputSnapshot(
                _history.ToArray(),
                CreateStatistics(IsComplete));
        }
    }

    public IAsyncEnumerable<ProcessLogEntry> ReadEntriesAsync(
        CancellationToken cancellationToken = default) =>
        _delivery.Reader.ReadAllAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            CompleteActiveSource(_standardOutput, ProcessOutputSource.StandardOutput);
            CompleteActiveSource(_standardError, ProcessOutputSource.StandardError);
            TryCompletePipeline();
        }

        return ValueTask.CompletedTask;
    }

    private bool IsComplete =>
        _standardOutput.State != ProcessOutputStreamState.Active &&
        _standardError.State != ProcessOutputStreamState.Active;

    private void AcceptCharacters(
        LineState state,
        ProcessOutputSource source,
        ReadOnlySpan<char> characters)
    {
        foreach (var character in characters)
        {
            if (character == '\n')
            {
                if (state.PendingCarriageReturn)
                {
                    state.PendingCarriageReturn = false;
                }
                else
                {
                    EmitLine(state, source);
                }

                continue;
            }

            if (character == '\r')
            {
                EmitLine(state, source);
                state.PendingCarriageReturn = true;
                continue;
            }

            state.PendingCarriageReturn = false;
            if (state.Length < state.Buffer.Length)
            {
                state.Buffer[state.Length++] = character;
            }
            else
            {
                state.DiscardedCharacters = SaturatingIncrement(state.DiscardedCharacters);
            }
        }
    }

    private void EmitFinalPartialLine(LineState state, ProcessOutputSource source)
    {
        state.PendingCarriageReturn = false;
        if (state.Length != 0 || state.DiscardedCharacters != 0)
        {
            EmitLine(state, source);
        }
    }

    private void EmitLine(LineState state, ProcessOutputSource source)
    {
        var characterLimit = Math.Min(state.Length, Policy.MaximumEntryCharacters);
        if (characterLimit < state.Length &&
            characterLimit > 0 &&
            char.IsHighSurrogate(state.Buffer[characterLimit - 1]) &&
            char.IsLowSurrogate(state.Buffer[characterLimit]))
        {
            characterLimit--;
        }

        var retainedCharacters = FindUtf8PrefixLength(
            state.Buffer.AsSpan(0, characterLimit),
            Policy.MaximumEntryUtf8Bytes,
            out var retainedUtf8Bytes);
        var truncatedCharacters = SaturatingAdd(
            state.DiscardedCharacters,
            state.Length - retainedCharacters);
        var entry = new ProcessLogEntry(
            _identity,
            NextSequence(),
            _timeProvider.GetUtcNow(),
            source,
            new string(state.Buffer, 0, retainedCharacters),
            retainedUtf8Bytes,
            truncatedCharacters != 0,
            truncatedCharacters);

        _acceptedEntries = SaturatingIncrement(_acceptedEntries);
        _acceptedUtf8Bytes = SaturatingAdd(_acceptedUtf8Bytes, retainedUtf8Bytes);
        if (entry.IsTruncated)
        {
            _truncatedEntries = SaturatingIncrement(_truncatedEntries);
            _truncatedCharacters = SaturatingAdd(_truncatedCharacters, truncatedCharacters);
        }

        Retain(entry);
        Deliver(entry);
        state.ResetLine();
    }

    private void Retain(ProcessLogEntry entry)
    {
        while (_history.Count >= Policy.MaximumRetainedEntries ||
               _retainedUtf8Bytes + entry.RetainedUtf8Bytes > Policy.MaximumRetainedUtf8Bytes)
        {
            var evicted = _history.Dequeue();
            _retainedUtf8Bytes -= evicted.RetainedUtf8Bytes;
            _evictedEntries = SaturatingIncrement(_evictedEntries);
            _evictedUtf8Bytes = SaturatingAdd(_evictedUtf8Bytes, evicted.RetainedUtf8Bytes);
        }

        _history.Enqueue(entry);
        _retainedUtf8Bytes += entry.RetainedUtf8Bytes;
    }

    private void Deliver(ProcessLogEntry entry)
    {
        if (_delivery.Writer.TryWrite(entry))
        {
            return;
        }

        _droppedDeliveryEntries = SaturatingIncrement(_droppedDeliveryEntries);
        _droppedDeliveryUtf8Bytes = SaturatingAdd(
            _droppedDeliveryUtf8Bytes,
            entry.RetainedUtf8Bytes);
    }

    private void CompleteActiveSource(LineState state, ProcessOutputSource source)
    {
        if (state.State == ProcessOutputStreamState.Active)
        {
            EmitFinalPartialLine(state, source);
            state.State = ProcessOutputStreamState.Completed;
        }
    }

    private void TryCompletePipeline()
    {
        if (!IsComplete)
        {
            return;
        }

        _delivery.Writer.TryComplete();
        _completion.TrySetResult(CreateStatistics(isCompleted: true));
    }

    private ProcessOutputStatistics CreateStatistics(bool isCompleted) =>
        new(
            _acceptedEntries,
            _acceptedUtf8Bytes,
            _history.Count,
            _retainedUtf8Bytes,
            _evictedEntries,
            _evictedUtf8Bytes,
            _truncatedEntries,
            _truncatedCharacters,
            _droppedDeliveryEntries,
            _droppedDeliveryUtf8Bytes,
            _standardOutput.State,
            _standardError.State,
            isCompleted);

    private LineState GetLineState(ProcessOutputSource source) =>
        source == ProcessOutputSource.StandardOutput ? _standardOutput : _standardError;

    private void ThrowIfNotWritable(LineState state, ProcessOutputSource source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (state.State != ProcessOutputStreamState.Active)
        {
            throw new InvalidOperationException($"The {source} stream is already complete.");
        }
    }

    private long NextSequence()
    {
        if (_nextSequence == long.MaxValue)
        {
            throw new InvalidOperationException("The process-output sequence space is exhausted.");
        }

        return ++_nextSequence;
    }

    private static int FindUtf8PrefixLength(
        ReadOnlySpan<char> characters,
        int maximumUtf8Bytes,
        out int utf8Bytes)
    {
        var characterIndex = 0;
        utf8Bytes = 0;

        while (characterIndex < characters.Length)
        {
            var status = Rune.DecodeFromUtf16(
                characters[characterIndex..],
                out var rune,
                out var consumedCharacters);
            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumedCharacters = 1;
            }

            if (utf8Bytes + rune.Utf8SequenceLength > maximumUtf8Bytes)
            {
                break;
            }

            utf8Bytes += rune.Utf8SequenceLength;
            characterIndex += consumedCharacters;
        }

        return characterIndex;
    }

    private static void ValidateIdentity(ProcessOutputIdentity identity)
    {
        if (identity.OwnershipId == Guid.Empty)
        {
            throw new ArgumentException("Process output requires a non-empty ownership ID.", nameof(identity));
        }

        if (identity.RootProcessId <= 0)
        {
            throw new ArgumentException("Process output requires a positive root process ID.", nameof(identity));
        }

        foreach (var optionalIdentity in new[]
                 {
                     identity.TaskId,
                     identity.AgentRunId,
                     identity.ToolRunId,
                     identity.ProcessRunId,
                     identity.OperationId,
                 })
        {
            if (optionalIdentity == Guid.Empty)
            {
                throw new ArgumentException(
                    "Optional process-output identities cannot be empty.",
                    nameof(identity));
            }
        }
    }

    private static void ValidateSource(ProcessOutputSource source)
    {
        if (source is not ProcessOutputSource.StandardOutput and not ProcessOutputSource.StandardError)
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown process-output source.");
        }
    }

    private static long SaturatingIncrement(long value) =>
        value == long.MaxValue ? long.MaxValue : value + 1;

    private static long SaturatingAdd(long value, long increment)
    {
        if (increment <= 0)
        {
            return value;
        }

        return value > long.MaxValue - increment ? long.MaxValue : value + increment;
    }

    private sealed class LineState
    {
        public LineState(int maximumCharacters)
        {
            Buffer = new char[maximumCharacters];
        }

        public char[] Buffer { get; }

        public int Length { get; set; }

        public long DiscardedCharacters { get; set; }

        public bool PendingCarriageReturn { get; set; }

        public ProcessOutputStreamState State { get; set; }

        public void ResetLine()
        {
            Length = 0;
            DiscardedCharacters = 0;
        }
    }
}
