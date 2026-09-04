using FCCCodeDesktop.Runtime;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class AgentRuntimeSupervisorTests
{
    [Fact]
    public async Task ExplicitRetryableFailureRetriesSeriallyAndPreservesLogicalIdentity()
    {
        var taskId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var runtime = new ScriptedRuntime(
            Failure(
                AgentRuntimeFailureKind.ProviderUnavailable,
                AgentRuntimeRetryability.Retryable,
                AgentRuntimeUserAction.NotRequired),
            Success());
        var supervisor = new AgentRuntimeSupervisor(
            runtime,
            new AgentRuntimeSupervisionOptions(maximumAttempts: 3));
        var request = CreateRequest(taskId, runId);

        await using var execution = await supervisor.StartAsync(request, CancellationToken.None);
        var result = await execution.Completion;
        var events = await ReadEventsAsync(execution.Events);

        Assert.Equal(AgentRuntimeTerminalState.Succeeded, result.State);
        Assert.Equal(taskId, result.TaskId);
        Assert.Equal(runId, result.RunId);
        Assert.Equal(2, runtime.StartCount);
        Assert.Equal(1, runtime.MaximumConcurrentExecutions);
        Assert.All(runtime.Requests, item => Assert.Equal(taskId, item.TaskId));
        Assert.All(runtime.Requests, item => Assert.Equal(runId, item.RunId));
        Assert.Equal(3, events.Count);
        Assert.Equal(AgentRuntimeEventKind.RuntimeStatus, events[0].Kind);
        Assert.Equal(AgentRuntimeEventKind.Retry, events[1].Kind);
        Assert.Equal(AgentRuntimeEventKind.RuntimeStatus, events[2].Kind);
        Assert.Equal(0, events[0].Sequence);
        Assert.Equal(1, events[1].Sequence);
        Assert.Equal(2, events[2].Sequence);
        Assert.Contains("attempt 2 of 3", events[1].Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownRetryabilityDoesNotInventRetryPolicy()
    {
        var runtime = new ScriptedRuntime(
            Failure(
                AgentRuntimeFailureKind.RateLimited,
                AgentRuntimeRetryability.Unknown,
                AgentRuntimeUserAction.Unknown),
            Success());
        var supervisor = new AgentRuntimeSupervisor(runtime);

        await using var execution = await supervisor.StartAsync(
            CreateRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);
        var result = await execution.Completion;

        Assert.Equal(AgentRuntimeTerminalState.Failed, result.State);
        Assert.NotNull(result.Failure);
        Assert.Equal(AgentRuntimeRetryability.Unknown, result.Failure.Retryability);
        Assert.Equal(1, runtime.StartCount);
    }

    [Fact]
    public async Task RequiredUserActionBlocksAutomaticRetryEvenWhenMarkedRetryable()
    {
        var runtime = new ScriptedRuntime(
            Failure(
                AgentRuntimeFailureKind.AuthenticationFailure,
                AgentRuntimeRetryability.Retryable,
                AgentRuntimeUserAction.Required),
            Success());
        var supervisor = new AgentRuntimeSupervisor(runtime);

        await using var execution = await supervisor.StartAsync(
            CreateRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);
        var result = await execution.Completion;

        Assert.Equal(AgentRuntimeTerminalState.Failed, result.State);
        Assert.Equal(1, runtime.StartCount);
    }

    [Fact]
    public async Task RetryAttemptsAreBoundedByConfiguredMaximum()
    {
        var retryable = Failure(
            AgentRuntimeFailureKind.ProviderBusyOrOverloaded,
            AgentRuntimeRetryability.Retryable,
            AgentRuntimeUserAction.NotRequired);
        var runtime = new ScriptedRuntime(retryable, retryable, retryable, Success());
        var supervisor = new AgentRuntimeSupervisor(
            runtime,
            new AgentRuntimeSupervisionOptions(maximumAttempts: 3));

        await using var execution = await supervisor.StartAsync(
            CreateRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);
        var result = await execution.Completion;
        var events = await ReadEventsAsync(execution.Events);

        Assert.Equal(AgentRuntimeTerminalState.Failed, result.State);
        Assert.Equal(3, runtime.StartCount);
        Assert.Equal(2, events.Count(item => item.Kind == AgentRuntimeEventKind.Retry));
        Assert.Equal(1, runtime.MaximumConcurrentExecutions);
    }

    [Fact]
    public async Task CancellationIsIdempotentStopsActiveExecutionAndSuppressesRetry()
    {
        var runtime = new ScriptedRuntime(CancelWhenRequested(), Success());
        var supervisor = new AgentRuntimeSupervisor(runtime);

        await using var execution = await supervisor.StartAsync(
            CreateRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        await execution.CancelAsync(CancellationToken.None);
        await execution.CancelAsync(CancellationToken.None);
        var result = await execution.Completion;

        Assert.Equal(AgentRuntimeTerminalState.Cancelled, result.State);
        Assert.Equal(1, runtime.StartCount);
        Assert.Single(runtime.Executions);
        Assert.Equal(1, runtime.Executions[0].CancelCalls);
        Assert.Equal(1, runtime.MaximumConcurrentExecutions);
    }

    [Fact]
    public async Task AutomaticRetryCanBeDisabledWithoutChangingRuntimeContract()
    {
        var runtime = new ScriptedRuntime(
            Failure(
                AgentRuntimeFailureKind.Timeout,
                AgentRuntimeRetryability.Retryable,
                AgentRuntimeUserAction.NotRequired),
            Success());
        var supervisor = new AgentRuntimeSupervisor(
            runtime,
            new AgentRuntimeSupervisionOptions(automaticRetryEnabled: false));

        await using var execution = await supervisor.StartAsync(
            CreateRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);
        var result = await execution.Completion;

        Assert.Equal(AgentRuntimeTerminalState.Failed, result.State);
        Assert.Equal(1, runtime.StartCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void MaximumAttemptPolicyRejectsUnsafeBounds(int maximumAttempts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AgentRuntimeSupervisionOptions(maximumAttempts));
    }

    private static AgentRuntimeRequest CreateRequest(Guid taskId, Guid runId) =>
        new(taskId, runId, "supervision fixture", Environment.CurrentDirectory);

    private static ScriptedOutcome Success() =>
        new(AgentRuntimeTerminalState.Succeeded);

    private static ScriptedOutcome CancelWhenRequested() =>
        new(AgentRuntimeTerminalState.Cancelled, completeOnCancel: true);

    private static ScriptedOutcome Failure(
        AgentRuntimeFailureKind kind,
        AgentRuntimeRetryability retryability,
        AgentRuntimeUserAction userAction) =>
        new(
            AgentRuntimeTerminalState.Failed,
            new AgentRuntimeFailure(
                kind,
                $"Fixture {kind}.",
                retryability,
                userAction,
                source: "fixture"));

    private static async Task<IReadOnlyList<AgentRuntimeEvent>> ReadEventsAsync(
        IAsyncEnumerable<AgentRuntimeEvent> events)
    {
        var output = new List<AgentRuntimeEvent>();
        await foreach (var runtimeEvent in events)
        {
            output.Add(runtimeEvent);
        }

        return output.AsReadOnly();
    }

    private sealed record ScriptedOutcome(
        AgentRuntimeTerminalState State,
        AgentRuntimeFailure? Failure = null,
        bool CompleteOnCancel = false);

    private sealed class ScriptedRuntime : IAgentRuntime
    {
        private readonly Queue<ScriptedOutcome> _outcomes;
        private int _activeExecutions;

        public ScriptedRuntime(params ScriptedOutcome[] outcomes)
        {
            _outcomes = new Queue<ScriptedOutcome>(outcomes);
            Descriptor = new AgentRuntimeDescriptor(
                "fixture.supervised",
                "Fixture supervised runtime",
                AgentRuntimeTransport.Fixture,
                new AgentRuntimeCapabilities(
                    supportsStreaming: true,
                    supportsSessions: true,
                    supportsResume: true,
                    supportsCancellation: true,
                    supportsToolActivity: true));
        }

        public AgentRuntimeDescriptor Descriptor { get; }

        public int StartCount { get; private set; }

        public int MaximumConcurrentExecutions { get; private set; }

        public List<AgentRuntimeRequest> Requests { get; } = [];

        public List<ScriptedExecution> Executions { get; } = [];

        public Task<IAgentRuntimeExecution> StartAsync(
            AgentRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_outcomes.Count == 0)
            {
                throw new InvalidOperationException("No scripted runtime outcome remains.");
            }

            StartCount++;
            Requests.Add(request);
            _activeExecutions++;
            MaximumConcurrentExecutions = Math.Max(
                MaximumConcurrentExecutions,
                _activeExecutions);

            var execution = new ScriptedExecution(
                request,
                _outcomes.Dequeue(),
                () => _activeExecutions--,
                StartCount);
            Executions.Add(execution);
            return Task.FromResult<IAgentRuntimeExecution>(execution);
        }
    }

    private sealed class ScriptedExecution : IAgentRuntimeExecution
    {
        private readonly TaskCompletionSource<AgentRuntimeResult> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action _onDisposed;
        private readonly AgentRuntimeRequest _request;
        private int _disposed;

        public ScriptedExecution(
            AgentRuntimeRequest request,
            ScriptedOutcome outcome,
            Action onDisposed,
            int attempt)
        {
            _request = request;
            _onDisposed = onDisposed;
            TaskId = request.TaskId;
            RunId = request.RunId;
            Events = EmitStatusAsync(attempt);

            if (!outcome.CompleteOnCancel)
            {
                _completion.TrySetResult(CreateResult(outcome));
            }
        }

        public Guid TaskId { get; }

        public Guid RunId { get; }

        public IAsyncEnumerable<AgentRuntimeEvent> Events { get; }

        public Task<AgentRuntimeResult> Completion => _completion.Task;

        public int CancelCalls { get; private set; }

        public ValueTask CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancelCalls++;
            _completion.TrySetResult(
                new AgentRuntimeResult(
                    TaskId,
                    RunId,
                    AgentRuntimeTerminalState.Cancelled));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _onDisposed();
            }

            return ValueTask.CompletedTask;
        }

        private AgentRuntimeResult CreateResult(ScriptedOutcome outcome) =>
            new(TaskId, RunId, outcome.State, failure: outcome.Failure);

        private static async IAsyncEnumerable<AgentRuntimeEvent> EmitStatusAsync(int attempt)
        {
            await Task.Yield();
            yield return new AgentRuntimeEvent(
                0,
                DateTimeOffset.UtcNow,
                AgentRuntimeEventKind.RuntimeStatus,
                text: $"Fixture attempt {attempt}.",
                sourceType: "fixture/runtime-status");
        }
    }
}
