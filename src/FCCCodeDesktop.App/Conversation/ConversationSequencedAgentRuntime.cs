using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.App.Conversation;

/// <summary>
/// Keeps conversation-facing runtime event sequences monotonic across logical task executions
/// while independently verifying that each source execution emits a contiguous sequence.
/// </summary>
public sealed class ConversationSequencedAgentRuntime : IAgentRuntime
{
    private readonly IAgentRuntime _inner;
    private long _nextPresentationSequence;

    public ConversationSequencedAgentRuntime(IAgentRuntime inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public AgentRuntimeDescriptor Descriptor => _inner.Descriptor;

    public async Task<IAgentRuntimeExecution> StartAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var execution = await _inner.StartAsync(request, cancellationToken).ConfigureAwait(false);
        return new SequencedExecution(this, execution);
    }

    private long NextPresentationSequence() =>
        Interlocked.Increment(ref _nextPresentationSequence) - 1;

    private sealed class SequencedExecution : IAgentRuntimeExecution
    {
        private readonly ConversationSequencedAgentRuntime _owner;
        private readonly IAgentRuntimeExecution _inner;

        public SequencedExecution(ConversationSequencedAgentRuntime owner, IAgentRuntimeExecution inner)
        {
            _owner = owner;
            _inner = inner;
        }

        public Guid TaskId => _inner.TaskId;

        public Guid RunId => _inner.RunId;

        public IAsyncEnumerable<AgentRuntimeEvent> Events => ProjectEventsAsync();

        public Task<AgentRuntimeResult> Completion => _inner.Completion;

        public ValueTask CancelAsync(CancellationToken cancellationToken = default) =>
            _inner.CancelAsync(cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();

        private async IAsyncEnumerable<AgentRuntimeEvent> ProjectEventsAsync()
        {
            long? priorSourceSequence = null;
            await foreach (var runtimeEvent in _inner.Events.ConfigureAwait(false))
            {
                if (priorSourceSequence is long prior)
                {
                    var expected = checked(prior + 1);
                    if (runtimeEvent.Sequence != expected)
                    {
                        throw new InvalidOperationException(
                            $"Source runtime event sequence must remain contiguous. Expected {expected}, received {runtimeEvent.Sequence}.");
                    }
                }

                priorSourceSequence = runtimeEvent.Sequence;
                yield return new AgentRuntimeEvent(
                    _owner.NextPresentationSequence(),
                    runtimeEvent.OccurredUtc,
                    runtimeEvent.Kind,
                    runtimeEvent.Text,
                    runtimeEvent.SessionId,
                    runtimeEvent.CorrelationId,
                    runtimeEvent.SourceType,
                    runtimeEvent.PayloadJson);
            }
        }
    }
}
