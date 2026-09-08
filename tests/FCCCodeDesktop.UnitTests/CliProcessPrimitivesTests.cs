using System.Runtime.CompilerServices;
using FCCCodeDesktop.Runtime;
using FCCCodeDesktop.Tools;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class CliProcessPrimitivesTests
{
    [Fact]
    public void ToolProcessRequestPreservesStructuredInvocationAndValidatesResolvedExecutable()
    {
        var context = CreateProjectContext();
        var workingDirectory = Path.GetFullPath(Path.Combine(context.RootPath, "nested work"));
        var arguments = new[] { string.Empty, "quoted value", "&|<>", "العربية" };
        var environment = new[]
        {
            new KeyValuePair<string, string>("FCCD_FIXTURE", "value with spaces"),
        };
        var invocation = new FixtureInvocation(
            context,
            "fixture.execute",
            arguments,
            workingDirectory,
            environment);
        var tool = new ToolIdentity("fixture.cli", "Fixture CLI");
        var executablePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fixture tool.exe"));
        var correlation = new ToolProcessCorrelation(
            taskId: Guid.NewGuid(),
            agentRunId: Guid.NewGuid(),
            toolRunId: Guid.NewGuid(),
            processRunId: Guid.NewGuid(),
            operationId: Guid.NewGuid());

        var request = new ToolProcessRequest(tool, invocation, executablePath, correlation);

        Assert.Same(tool, request.Tool);
        Assert.Same(invocation, request.Invocation);
        Assert.Equal(executablePath, request.ExecutablePath);
        Assert.Same(correlation, request.Correlation);
        Assert.Equal(arguments, request.Invocation.Arguments);
        Assert.Equal("value with spaces", request.Invocation.Environment["FCCD_FIXTURE"]);
        Assert.Throws<ArgumentException>(() =>
            new ToolProcessRequest(tool, invocation, "relative-tool.exe"));
        Assert.Throws<ArgumentException>(() => new ToolProcessCorrelation(toolRunId: Guid.Empty));
    }

    [Fact]
    public async Task RunnerPreservesArgvEnvironmentCorrelationAndStreamsStructuredEvents()
    {
        var context = CreateProjectContext();
        var arguments = new[] { string.Empty, "two words", "\"quoted\"", "&|<>", "مرحبا" };
        var environment = new[]
        {
            new KeyValuePair<string, string>("FCCD_ONE", "1"),
            new KeyValuePair<string, string>("FCCD_TWO", "two words"),
        };
        var invocation = new FixtureInvocation(
            context,
            "fixture.stream",
            arguments,
            context.RootPath,
            environment);
        var tool = new ToolIdentity("fixture.cli", "Fixture CLI");
        var executablePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fixture.exe"));
        var correlation = new ToolProcessCorrelation(
            taskId: Guid.NewGuid(),
            agentRunId: Guid.NewGuid(),
            toolRunId: Guid.NewGuid(),
            processRunId: Guid.NewGuid(),
            operationId: Guid.NewGuid());
        var request = new ToolProcessRequest(tool, invocation, executablePath, correlation);
        var ownershipId = Guid.NewGuid();
        const int processId = 4242;
        var startedUtc = new DateTimeOffset(2026, 9, 8, 2, 0, 0, TimeSpan.Zero);
        var statistics = CreateStatistics(acceptedEntries: 2, retainedEntries: 2);
        var output = new FixtureProcessOutput(
            new[]
            {
                new ProcessLogEntry(
                    new ProcessOutputIdentity(ownershipId, processId, null, null, null, null, null),
                    1,
                    startedUtc.AddMilliseconds(10),
                    ProcessOutputSource.StandardOutput,
                    "hello",
                    5,
                    false,
                    0),
                new ProcessLogEntry(
                    new ProcessOutputIdentity(ownershipId, processId, null, null, null, null, null),
                    2,
                    startedUtc.AddMilliseconds(20),
                    ProcessOutputSource.StandardError,
                    "warning",
                    7,
                    true,
                    3),
            },
            statistics);
        var supervisedProcess = new FixtureSupervisedProcess(
            ownershipId,
            processId,
            startedUtc,
            output,
            new OwnedProcessExit(
                ownershipId,
                processId,
                0,
                startedUtc,
                startedUtc.AddSeconds(1),
                false));
        var supervisor = new FixtureProcessSupervisor(
            new ProcessLaunchResult(ProcessLaunchStatus.Started, supervisedProcess));
        var runner = new ExternalToolProcessRunner(supervisor);

        var events = await CollectAsync(runner.ExecuteAsync(request));

        Assert.NotNull(supervisor.LastRequest);
        Assert.Equal(executablePath, supervisor.LastRequest!.FileName);
        Assert.Equal(arguments, supervisor.LastRequest.Arguments);
        Assert.Equal(context.RootPath, supervisor.LastRequest.WorkingDirectory);
        Assert.NotNull(supervisor.LastRequest.Environment);
        Assert.Equal("1", supervisor.LastRequest.Environment!["FCCD_ONE"]);
        Assert.Equal("two words", supervisor.LastRequest.Environment["FCCD_TWO"]);
        Assert.NotNull(supervisor.LastRequest.Output?.Correlation);
        Assert.Equal(correlation.TaskId, supervisor.LastRequest.Output!.Correlation!.TaskId);
        Assert.Equal(correlation.AgentRunId, supervisor.LastRequest.Output.Correlation.AgentRunId);
        Assert.Equal(correlation.ToolRunId, supervisor.LastRequest.Output.Correlation.ToolRunId);
        Assert.Equal(correlation.ProcessRunId, supervisor.LastRequest.Output.Correlation.ProcessRunId);
        Assert.Equal(correlation.OperationId, supervisor.LastRequest.Output.Correlation.OperationId);

        Assert.Collection(
            events,
            item =>
            {
                var started = Assert.IsType<ToolProcessStartedEvent>(item);
                Assert.Equal(tool, started.Tool);
                Assert.Equal("fixture.stream", started.Operation);
                Assert.Equal(ownershipId, started.OwnershipId);
                Assert.Equal(processId, started.ProcessId);
                Assert.Equal(startedUtc, started.StartedUtc);
            },
            item =>
            {
                var outputEvent = Assert.IsType<ToolProcessOutputEvent>(item);
                Assert.Equal(ToolProcessOutputSource.StandardOutput, outputEvent.Source);
                Assert.Equal("hello", outputEvent.Text);
                Assert.False(outputEvent.IsTruncated);
            },
            item =>
            {
                var outputEvent = Assert.IsType<ToolProcessOutputEvent>(item);
                Assert.Equal(ToolProcessOutputSource.StandardError, outputEvent.Source);
                Assert.Equal("warning", outputEvent.Text);
                Assert.True(outputEvent.IsTruncated);
                Assert.Equal(3, outputEvent.TruncatedCharacters);
            },
            item =>
            {
                var resultEvent = Assert.IsType<ToolResultEvent>(item);
                var result = Assert.IsType<ToolProcessResult>(resultEvent.Result);
                Assert.Equal(ToolResultStatus.Succeeded, result.Status);
                Assert.Equal(ToolProcessLaunchStatus.Started, result.LaunchStatus);
                Assert.Equal(0, result.ExitCode);
                Assert.False(result.ForcedTerminationRequested);
                Assert.Equal(2, result.Output.AcceptedEntries);
                Assert.Equal(2, result.Output.RetainedEntries);
                Assert.True(result.Output.IsCompleted);
            });

        Assert.True(supervisedProcess.Disposed);
    }

    [Fact]
    public async Task RunnerClassifiesNonZeroExitAsFailureWithoutLosingExitCode()
    {
        var request = CreateRequest("fixture.fail");
        var ownershipId = Guid.NewGuid();
        var startedUtc = DateTimeOffset.UtcNow;
        var output = new FixtureProcessOutput(Array.Empty<ProcessLogEntry>(), CreateStatistics());
        var process = new FixtureSupervisedProcess(
            ownershipId,
            5151,
            startedUtc,
            output,
            new OwnedProcessExit(
                ownershipId,
                5151,
                7,
                startedUtc,
                startedUtc.AddMilliseconds(50),
                false));
        var runner = new ExternalToolProcessRunner(
            new FixtureProcessSupervisor(
                new ProcessLaunchResult(ProcessLaunchStatus.Started, process)));

        var events = await CollectAsync(runner.ExecuteAsync(request));
        var resultEvent = Assert.IsType<ToolResultEvent>(events[^1]);
        var result = Assert.IsType<ToolProcessResult>(resultEvent.Result);

        Assert.Equal(ToolResultStatus.Failed, result.Status);
        Assert.Equal(ToolProcessLaunchStatus.Started, result.LaunchStatus);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal("The tool process exited with a non-zero exit code.", result.Summary);
    }

    [Fact]
    public async Task LaunchFailureIsStructuredAndDoesNotCopyRawFailureMessage()
    {
        const string secret = "SUPER_SECRET_VALUE";
        var supervisor = new FixtureProcessSupervisor(
            new ProcessLaunchResult(
                ProcessLaunchStatus.StartFailed,
                null,
                $"provider failure leaked {secret}"));
        var runner = new ExternalToolProcessRunner(supervisor);

        var events = await CollectAsync(runner.ExecuteAsync(CreateRequest("fixture.launch-failure")));
        var resultEvent = Assert.IsType<ToolResultEvent>(Assert.Single(events));
        var result = Assert.IsType<ToolProcessResult>(resultEvent.Result);

        Assert.Equal(ToolResultStatus.Failed, result.Status);
        Assert.Equal(ToolProcessLaunchStatus.StartFailed, result.LaunchStatus);
        Assert.Null(result.ExitCode);
        Assert.DoesNotContain(secret, result.Summary, StringComparison.Ordinal);
        Assert.Equal("The tool process could not be started.", result.Summary);
        Assert.Null(supervisor.LastStartedProcess);
    }

    [Fact]
    public async Task CancellationTerminatesOwnedTreeDisposesHandleAndPropagates()
    {
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new BlockingProcessOutput(readStarted);
        var process = new CancellableSupervisedProcess(output);
        var supervisor = new FixtureProcessSupervisor(
            new ProcessLaunchResult(ProcessLaunchStatus.Started, process));
        var runner = new ExternalToolProcessRunner(supervisor);
        using var cancellation = new CancellationTokenSource();

        var collectionTask = CollectAsync(
            runner.ExecuteAsync(CreateRequest("fixture.cancel"), cancellation.Token));
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await collectionTask);
        Assert.True(process.TerminateRequested);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task PreCancelledExecutionDoesNotReachProcessSupervisor()
    {
        var supervisor = new FixtureProcessSupervisor(
            new ProcessLaunchResult(ProcessLaunchStatus.StartFailed, null));
        var runner = new ExternalToolProcessRunner(supervisor);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await CollectAsync(
                runner.ExecuteAsync(CreateRequest("fixture.pre-cancel"), cancellation.Token));
        });

        Assert.Null(supervisor.LastRequest);
    }

    private static ToolProcessRequest CreateRequest(string operation)
    {
        var context = CreateProjectContext();
        var invocation = new FixtureInvocation(context, operation);
        return new ToolProcessRequest(
            new ToolIdentity("fixture.cli", "Fixture CLI"),
            invocation,
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fixture.exe")));
    }

    private static ProjectContext CreateProjectContext() =>
        new(
            Guid.NewGuid(),
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"fccd-p09-007-{Guid.NewGuid():N}")));

    private static ProcessOutputStatistics CreateStatistics(
        long acceptedEntries = 0,
        int retainedEntries = 0)
    {
        return new ProcessOutputStatistics(
            AcceptedEntries: acceptedEntries,
            AcceptedUtf8Bytes: acceptedEntries * 8,
            RetainedEntries: retainedEntries,
            RetainedUtf8Bytes: retainedEntries * 8L,
            EvictedEntries: 0,
            EvictedUtf8Bytes: 0,
            TruncatedEntries: 0,
            TruncatedCharacters: 0,
            DroppedDeliveryEntries: 0,
            DroppedDeliveryUtf8Bytes: 0,
            StandardOutputState: ProcessOutputStreamState.Completed,
            StandardErrorState: ProcessOutputStreamState.Completed,
            IsCompleted: true);
    }

    private static async Task<List<ToolEvent>> CollectAsync(IAsyncEnumerable<ToolEvent> source)
    {
        var events = new List<ToolEvent>();
        await foreach (var item in source)
        {
            events.Add(item);
        }

        return events;
    }

    private sealed record FixtureInvocation : StructuredToolInvocation
    {
        public FixtureInvocation(
            ProjectContext project,
            string operation,
            IEnumerable<string>? arguments = null,
            string? workingDirectory = null,
            IEnumerable<KeyValuePair<string, string>>? environment = null)
            : base(project, operation, arguments, workingDirectory, environment)
        {
        }
    }

    private sealed class FixtureProcessSupervisor : IProcessSupervisor
    {
        private readonly ProcessLaunchResult _launchResult;

        public FixtureProcessSupervisor(ProcessLaunchResult launchResult)
        {
            _launchResult = launchResult;
            LastStartedProcess = launchResult.Process;
        }

        public ProcessLaunchRequest? LastRequest { get; private set; }

        public ISupervisedProcess? LastStartedProcess { get; }

        public IReadOnlyList<OwnedProcessSnapshot> GetActiveProcesses() => Array.Empty<OwnedProcessSnapshot>();

        public Task<ProcessLaunchResult> StartAsync(
            ProcessLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(_launchResult);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixtureProcessOutput : IProcessOutput
    {
        private readonly IReadOnlyList<ProcessLogEntry> _entries;
        private readonly ProcessOutputStatistics _statistics;

        public FixtureProcessOutput(
            IReadOnlyList<ProcessLogEntry> entries,
            ProcessOutputStatistics statistics)
        {
            _entries = entries;
            _statistics = statistics;
        }

        public ProcessOutputPolicy Policy => ProcessOutputPolicy.Default;

        public Task<ProcessOutputStatistics> Completion => Task.FromResult(_statistics);

        public ProcessOutputSnapshot GetSnapshot() => new(_entries, _statistics);

        public async IAsyncEnumerable<ProcessLogEntry> ReadEntriesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var entry in _entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return entry;
            }
        }
    }

    private sealed class FixtureSupervisedProcess : ISupervisedProcess
    {
        public FixtureSupervisedProcess(
            Guid ownershipId,
            int rootProcessId,
            DateTimeOffset startedUtc,
            IProcessOutput output,
            OwnedProcessExit exit)
        {
            OwnershipId = ownershipId;
            RootProcessId = rootProcessId;
            StartedUtc = startedUtc;
            Output = output;
            Completion = Task.FromResult(exit);
        }

        public Guid OwnershipId { get; }

        public int RootProcessId { get; }

        public DateTimeOffset StartedUtc { get; }

        public IProcessOutput Output { get; }

        public Task<OwnedProcessExit> Completion { get; }

        public bool Disposed { get; private set; }

        public ValueTask TerminateOwnedTreeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingProcessOutput : IProcessOutput
    {
        private readonly TaskCompletionSource _readStarted;

        public BlockingProcessOutput(TaskCompletionSource readStarted)
        {
            _readStarted = readStarted;
        }

        public ProcessOutputPolicy Policy => ProcessOutputPolicy.Default;

        public Task<ProcessOutputStatistics> Completion => Task.FromResult(CreateStatistics());

        public ProcessOutputSnapshot GetSnapshot() =>
            new(Array.Empty<ProcessLogEntry>(), CreateStatistics());

        public async IAsyncEnumerable<ProcessLogEntry> ReadEntriesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }
    }

    private sealed class CancellableSupervisedProcess : ISupervisedProcess
    {
        private readonly TaskCompletionSource<OwnedProcessExit> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellableSupervisedProcess(IProcessOutput output)
        {
            Output = output;
            OwnershipId = Guid.NewGuid();
            RootProcessId = 6161;
            StartedUtc = DateTimeOffset.UtcNow;
        }

        public Guid OwnershipId { get; }

        public int RootProcessId { get; }

        public DateTimeOffset StartedUtc { get; }

        public IProcessOutput Output { get; }

        public Task<OwnedProcessExit> Completion => _completion.Task;

        public bool TerminateRequested { get; private set; }

        public bool Disposed { get; private set; }

        public ValueTask TerminateOwnedTreeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TerminateRequested = true;
            _completion.TrySetResult(
                new OwnedProcessExit(
                    OwnershipId,
                    RootProcessId,
                    1,
                    StartedUtc,
                    DateTimeOffset.UtcNow,
                    true));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
