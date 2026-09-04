using System.Text.Json;
using System.Threading.Channels;

namespace FCCCodeDesktop.Runtime;

/// <summary>
/// Transport-neutral supervision decorator for one logical runtime execution.
/// </summary>
/// <remarks>
/// Retry is intentionally evidence-bounded: only a terminal failure explicitly classified as
/// retryable with no required user action is eligible. Global queueing, cooldowns, provider
/// backoff, and crash/reboot recovery remain outside this P04 component.
/// </remarks>
public sealed class AgentRuntimeSupervisor : IAgentRuntime
{
    private readonly IAgentRuntime _runtime;
    private readonly AgentRuntimeSupervisionOptions _options;

    public AgentRuntimeSupervisor(
        IAgentRuntime runtime,
        AgentRuntimeSupervisionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        _options = options ?? new AgentRuntimeSupervisionOptions();
    }

    public AgentRuntimeDescriptor Descriptor => _runtime.Descriptor;

    public async Task<IAgentRuntimeExecution> StartAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var firstExecution = await _runtime
            .StartAsync(request, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            EnsureExecutionIdentity(firstExecution, request);
            return new SupervisedExecution(_runtime, request, firstExecution, _options);
        }
        catch
        {
            await firstExecution.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static void EnsureExecutionIdentity(
        IAgentRuntimeExecution execution,
        AgentRuntimeRequest request)
    {
        ArgumentNullException.ThrowIfNull(execution);
        if (execution.TaskId != request.TaskId || execution.RunId != request.RunId)
        {
            throw new InvalidOperationException(
                "The runtime returned an execution whose task/run identity does not match the request.");
        }
    }

    private sealed class SupervisedExecution : IAgentRuntimeExecution
    {
        private readonly IAgentRuntime _runtime;
        private readonly AgentRuntimeRequest _request;
        private readonly AgentRuntimeSupervisionOptions _options;
        private readonly Channel<AgentRuntimeEvent> _events;
        private readonly TaskCompletionSource<AgentRuntimeResult> _completion;
        private readonly object _activeGate = new();
        private readonly Task _pumpTask;
        private IAgentRuntimeExecution? _activeExecution;
        private long _nextSequence;
        private int _cancellationRequested;

        public SupervisedExecution(
            IAgentRuntime runtime,
            AgentRuntimeRequest request,
            IAgentRuntimeExecution firstExecution,
            AgentRuntimeSupervisionOptions options)
        {
            _runtime = runtime;
            _request = request;
            _options = options;
            TaskId = request.TaskId;
            RunId = request.RunId;
            _events = Channel.CreateUnbounded<AgentRuntimeEvent>(
                new UnboundedChannelOptions
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = false,
                    SingleWriter = true
                });
            _completion = new TaskCompletionSource<AgentRuntimeResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _activeExecution = firstExecution;
            _pumpTask = PumpAsync(firstExecution);
        }

        public Guid TaskId { get; }

        public Guid RunId { get; }

        public IAsyncEnumerable<AgentRuntimeEvent> Events => _events.Reader.ReadAllAsync();

        public Task<AgentRuntimeResult> Completion => _completion.Task;

        public async ValueTask CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_completion.Task.IsCompleted)
            {
                return;
            }

            var firstCancellationRequest = Interlocked.CompareExchange(
                ref _cancellationRequested,
                1,
                0) == 0;

            if (firstCancellationRequest)
            {
                IAgentRuntimeExecution? activeExecution;
                lock (_activeGate)
                {
                    activeExecution = _activeExecution;
                }

                if (activeExecution is not null)
                {
                    await activeExecution
                        .CancelAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            await _pumpTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completion.Task.IsCompleted)
            {
                await CancelAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await _pumpTask.ConfigureAwait(false);
        }

        private async Task PumpAsync(IAgentRuntimeExecution firstExecution)
        {
            var attempt = 1;
            var execution = firstExecution;

            try
            {
                while (true)
                {
                    SetActiveExecution(execution);
                    var forwardTask = ForwardEventsAsync(execution.Events);
                    AgentRuntimeResult result;
                    try
                    {
                        result = await execution.Completion.ConfigureAwait(false);
                        await forwardTask.ConfigureAwait(false);
                    }
                    finally
                    {
                        await execution.DisposeAsync().ConfigureAwait(false);
                        ClearActiveExecution(execution);
                    }

                    var cancellationRequested = Volatile.Read(ref _cancellationRequested) != 0;
                    if (cancellationRequested)
                    {
                        _completion.TrySetResult(
                            result.State == AgentRuntimeTerminalState.Succeeded
                                ? result
                                : new AgentRuntimeResult(
                                    TaskId,
                                    RunId,
                                    AgentRuntimeTerminalState.Cancelled,
                                    result.SessionId));
                        return;
                    }

                    if (!_options.ShouldRetry(result, attempt, cancellationRequested))
                    {
                        _completion.TrySetResult(result);
                        return;
                    }

                    attempt++;
                    WriteRetryEvent(attempt, result.Failure!);

                    execution = await _runtime
                        .StartAsync(_request, CancellationToken.None)
                        .ConfigureAwait(false);
                    try
                    {
                        EnsureExecutionIdentity(execution, _request);
                    }
                    catch
                    {
                        await execution.DisposeAsync().ConfigureAwait(false);
                        throw;
                    }
                }
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
                _events.Writer.TryComplete(exception);
                return;
            }
            finally
            {
                _events.Writer.TryComplete();
            }
        }

        private async Task ForwardEventsAsync(IAsyncEnumerable<AgentRuntimeEvent> source)
        {
            await foreach (var runtimeEvent in source.ConfigureAwait(false))
            {
                var sequence = Interlocked.Increment(ref _nextSequence) - 1;
                _events.Writer.TryWrite(
                    new AgentRuntimeEvent(
                        sequence,
                        runtimeEvent.OccurredUtc,
                        runtimeEvent.Kind,
                        runtimeEvent.Text,
                        runtimeEvent.SessionId,
                        runtimeEvent.CorrelationId,
                        runtimeEvent.SourceType,
                        runtimeEvent.PayloadJson));
            }
        }

        private void WriteRetryEvent(int nextAttempt, AgentRuntimeFailure failure)
        {
            var sequence = Interlocked.Increment(ref _nextSequence) - 1;
            var payloadJson = JsonSerializer.Serialize(
                new
                {
                    nextAttempt,
                    maximumAttempts = _options.MaximumAttempts,
                    failureKind = failure.Kind.ToString(),
                    failureSource = failure.Source
                });

            _events.Writer.TryWrite(
                new AgentRuntimeEvent(
                    sequence,
                    DateTimeOffset.UtcNow,
                    AgentRuntimeEventKind.Retry,
                    text: $"Retrying runtime attempt {nextAttempt} of {_options.MaximumAttempts}.",
                    sourceType: "fccd/runtime-supervisor/retry",
                    payloadJson: payloadJson));
        }

        private void SetActiveExecution(IAgentRuntimeExecution execution)
        {
            lock (_activeGate)
            {
                _activeExecution = execution;
            }
        }

        private void ClearActiveExecution(IAgentRuntimeExecution execution)
        {
            lock (_activeGate)
            {
                if (ReferenceEquals(_activeExecution, execution))
                {
                    _activeExecution = null;
                }
            }
        }
    }
}
