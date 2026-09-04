using System.Runtime.CompilerServices;
using FCCCodeDesktop.Runtime;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class AgentRuntimeContractTests
{
    [Fact]
    public void RequestPreservesPromptAndNormalizesOptionalSessionIdentity()
    {
        var taskId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        const string prompt = "  keep prompt whitespace مساحة  ";

        var request = new AgentRuntimeRequest(
            taskId,
            runId,
            prompt,
            "  C:\\workspace\\مساحة  ",
            "  session-123  ");

        Assert.Equal(taskId, request.TaskId);
        Assert.Equal(runId, request.RunId);
        Assert.Equal(prompt, request.Prompt);
        Assert.Equal("C:\\workspace\\مساحة", request.WorkingDirectory);
        Assert.Equal("session-123", request.ResumeSessionId);
    }

    [Fact]
    public void RequestRejectsMissingRequiredIdentityAndContent()
    {
        var taskId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeRequest(Guid.Empty, runId, "prompt", "C:\\workspace"));
        Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeRequest(taskId, Guid.Empty, "prompt", "C:\\workspace"));
        Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeRequest(taskId, runId, "   ", "C:\\workspace"));
        Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeRequest(taskId, runId, "prompt", "   "));
    }

    [Fact]
    public void ResumeCapabilityRequiresSessionIdentityCapability()
    {
        Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeCapabilities(
                supportsStreaming: true,
                supportsSessions: false,
                supportsResume: true,
                supportsCancellation: true,
                supportsToolActivity: true));
    }

    [Fact]
    public void DescriptorRequiresExplicitTransportAndPreservesCapabilities()
    {
        var capabilities = new AgentRuntimeCapabilities(
            supportsStreaming: true,
            supportsSessions: true,
            supportsResume: true,
            supportsCancellation: true,
            supportsToolActivity: true);
        var descriptor = new AgentRuntimeDescriptor(
            "fcc.structured",
            "FCC structured runtime",
            AgentRuntimeTransport.StructuredProcess,
            capabilities,
            "2.1.251");

        Assert.Equal("fcc.structured", descriptor.RuntimeId);
        Assert.Equal(AgentRuntimeTransport.StructuredProcess, descriptor.Transport);
        Assert.Same(capabilities, descriptor.Capabilities);
        Assert.Equal("2.1.251", descriptor.Version);

        Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeDescriptor(
                "fcc",
                "FCC",
                AgentRuntimeTransport.Unknown,
                capabilities));
    }

    [Fact]
    public void UnknownEventRetainsUpstreamTypeAndSanitizedPayload()
    {
        var occurred = new DateTimeOffset(2026, 9, 4, 4, 0, 0, TimeSpan.FromHours(3));
        var runtimeEvent = new AgentRuntimeEvent(
            7,
            occurred,
            AgentRuntimeEventKind.Unknown,
            text: "opaque",
            sourceType: "future/runtime_event",
            payloadJson: "{\"safe\":true}");

        Assert.Equal(7, runtimeEvent.Sequence);
        Assert.Equal(TimeSpan.Zero, runtimeEvent.OccurredUtc.Offset);
        Assert.Equal("future/runtime_event", runtimeEvent.SourceType);
        Assert.Equal("{\"safe\":true}", runtimeEvent.PayloadJson);

        Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeEvent(0, occurred, AgentRuntimeEventKind.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AgentRuntimeEvent(-1, occurred, AgentRuntimeEventKind.RuntimeStatus));
    }

    [Fact]
    public void FailureAndTerminalResultPreserveEvidenceUncertainty()
    {
        var taskId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var failure = new AgentRuntimeFailure(
            AgentRuntimeFailureKind.ProviderUnavailable,
            "Provider returned an unavailable response.",
            statusCode: 503,
            source: "system/api_retry");
        var result = new AgentRuntimeResult(
            taskId,
            runId,
            AgentRuntimeTerminalState.Failed,
            "session-abc",
            failure);

        Assert.Equal(AgentRuntimeRetryability.Unknown, failure.Retryability);
        Assert.Equal(AgentRuntimeUserAction.Unknown, failure.UserAction);
        Assert.Equal(503, failure.StatusCode);
        Assert.Equal("system/api_retry", failure.Source);
        Assert.Equal(failure, result.Failure);
        Assert.Equal("session-abc", result.SessionId);

        Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeResult(
                taskId,
                runId,
                AgentRuntimeTerminalState.Failed));
        Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeResult(
                taskId,
                runId,
                AgentRuntimeTerminalState.Succeeded,
                failure: failure));
    }

    [Fact]
    public async Task RuntimeExecutionStreamsThenCompletesAndExposesCancellationSeam()
    {
        var taskId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var runtime = new FixtureRuntime(taskId, runId);
        var request = new AgentRuntimeRequest(
            taskId,
            runId,
            "reply with fixture marker",
            "C:\\workspace");

        await using var execution = await runtime.StartAsync(request, CancellationToken.None);
        var events = new List<AgentRuntimeEvent>();
        await foreach (var runtimeEvent in execution.Events)
        {
            events.Add(runtimeEvent);
        }

        var result = await execution.Completion;
        await execution.CancelAsync(CancellationToken.None);

        Assert.Equal(taskId, execution.TaskId);
        Assert.Equal(runId, execution.RunId);
        Assert.Equal(2, events.Count);
        Assert.Equal(AgentRuntimeEventKind.SessionIdentified, events[0].Kind);
        Assert.Equal(AgentRuntimeEventKind.AssistantTextDelta, events[1].Kind);
        Assert.Equal(AgentRuntimeTerminalState.Succeeded, result.State);
        Assert.Equal("fixture-session", result.SessionId);
        Assert.True(((FixtureExecution)execution).CancellationRequested);
    }

    private sealed class FixtureRuntime : IAgentRuntime
    {
        private readonly Guid _taskId;
        private readonly Guid _runId;

        public FixtureRuntime(Guid taskId, Guid runId)
        {
            _taskId = taskId;
            _runId = runId;
        }

        public AgentRuntimeDescriptor Descriptor { get; } = new(
            "fixture",
            "Fixture runtime",
            AgentRuntimeTransport.Fixture,
            new AgentRuntimeCapabilities(
                supportsStreaming: true,
                supportsSessions: true,
                supportsResume: true,
                supportsCancellation: true,
                supportsToolActivity: true));

        public Task<IAgentRuntimeExecution> StartAsync(
            AgentRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(_taskId, request.TaskId);
            Assert.Equal(_runId, request.RunId);
            return Task.FromResult<IAgentRuntimeExecution>(new FixtureExecution(_taskId, _runId));
        }
    }

    private sealed class FixtureExecution : IAgentRuntimeExecution
    {
        public FixtureExecution(Guid taskId, Guid runId)
        {
            TaskId = taskId;
            RunId = runId;
            Completion = Task.FromResult(
                new AgentRuntimeResult(
                    taskId,
                    runId,
                    AgentRuntimeTerminalState.Succeeded,
                    "fixture-session"));
        }

        public Guid TaskId { get; }

        public Guid RunId { get; }

        public IAsyncEnumerable<AgentRuntimeEvent> Events => CreateEvents();

        public Task<AgentRuntimeResult> Completion { get; }

        public bool CancellationRequested { get; private set; }

        public ValueTask CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancellationRequested = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static async IAsyncEnumerable<AgentRuntimeEvent> CreateEvents(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new AgentRuntimeEvent(
                0,
                DateTimeOffset.UtcNow,
                AgentRuntimeEventKind.SessionIdentified,
                sessionId: "fixture-session",
                sourceType: "system/init");

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new AgentRuntimeEvent(
                1,
                DateTimeOffset.UtcNow,
                AgentRuntimeEventKind.AssistantTextDelta,
                text: "fixture marker",
                sourceType: "assistant/delta");
        }
    }
}
