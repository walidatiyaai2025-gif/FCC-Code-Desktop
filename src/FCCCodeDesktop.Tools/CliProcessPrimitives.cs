using System.Runtime.CompilerServices;
using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.Tools;

/// <summary>
/// Stable launch classification exposed by the Tool Gateway without leaking process-supervisor implementation types.
/// </summary>
public enum ToolProcessLaunchStatus
{
    Started = 0,
    UnsupportedPlatform = 1,
    InvalidWorkingDirectory = 2,
    ExecutableNotFound = 3,
    AccessDenied = 4,
    StartFailed = 5,
}

public enum ToolProcessOutputSource
{
    StandardOutput = 0,
    StandardError = 1,
}

/// <summary>
/// Optional durable correlation identities carried into the bounded process-output pipeline.
/// </summary>
public sealed record ToolProcessCorrelation
{
    public ToolProcessCorrelation(
        Guid? taskId = null,
        Guid? agentRunId = null,
        Guid? toolRunId = null,
        Guid? processRunId = null,
        Guid? operationId = null)
    {
        ValidateOptionalIdentity(taskId, nameof(taskId));
        ValidateOptionalIdentity(agentRunId, nameof(agentRunId));
        ValidateOptionalIdentity(toolRunId, nameof(toolRunId));
        ValidateOptionalIdentity(processRunId, nameof(processRunId));
        ValidateOptionalIdentity(operationId, nameof(operationId));

        TaskId = taskId;
        AgentRunId = agentRunId;
        ToolRunId = toolRunId;
        ProcessRunId = processRunId;
        OperationId = operationId;
    }

    public Guid? TaskId { get; }

    public Guid? AgentRunId { get; }

    public Guid? ToolRunId { get; }

    public Guid? ProcessRunId { get; }

    public Guid? OperationId { get; }

    private static void ValidateOptionalIdentity(Guid? identity, string parameterName)
    {
        if (identity == Guid.Empty)
        {
            throw new ArgumentException("An optional tool-process identity cannot be empty.", parameterName);
        }
    }
}

/// <summary>
/// Binds a structured tool invocation to one resolved executable without flattening argv into a shell command.
/// </summary>
public sealed record ToolProcessRequest
{
    public ToolProcessRequest(
        ToolIdentity tool,
        StructuredToolInvocation invocation,
        string executablePath,
        ToolProcessCorrelation? correlation = null)
    {
        Tool = tool ?? throw new ArgumentNullException(nameof(tool));
        Invocation = invocation ?? throw new ArgumentNullException(nameof(invocation));
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException(
                "The tool executable path must be fully qualified.",
                nameof(executablePath));
        }

        if (executablePath.Contains('\0'))
        {
            throw new ArgumentException(
                "The tool executable path must not contain NUL characters.",
                nameof(executablePath));
        }

        ExecutablePath = Path.GetFullPath(executablePath);
        Correlation = correlation;
    }

    public ToolIdentity Tool { get; }

    public StructuredToolInvocation Invocation { get; }

    public string ExecutablePath { get; }

    public ToolProcessCorrelation? Correlation { get; }
}

public sealed record ToolProcessStartedEvent : ToolEvent
{
    public ToolProcessStartedEvent(
        ToolIdentity tool,
        string operation,
        Guid ownershipId,
        int processId,
        DateTimeOffset startedUtc)
    {
        Tool = tool ?? throw new ArgumentNullException(nameof(tool));
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (ownershipId == Guid.Empty)
        {
            throw new ArgumentException("Process ownership identity cannot be empty.", nameof(ownershipId));
        }

        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process ID must be positive.");
        }

        Operation = operation;
        OwnershipId = ownershipId;
        ProcessId = processId;
        StartedUtc = startedUtc.ToUniversalTime();
    }

    public ToolIdentity Tool { get; }

    public string Operation { get; }

    public Guid OwnershipId { get; }

    public int ProcessId { get; }

    public DateTimeOffset StartedUtc { get; }
}

public sealed record ToolProcessOutputEvent : ToolEvent
{
    public ToolProcessOutputEvent(
        ToolIdentity tool,
        string operation,
        long sequence,
        DateTimeOffset timestampUtc,
        ToolProcessOutputSource source,
        string text,
        bool isTruncated,
        long truncatedCharacters)
    {
        Tool = tool ?? throw new ArgumentNullException(nameof(tool));
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Output sequence must be positive.");
        }

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "A concrete output source is required.");
        }

        ArgumentNullException.ThrowIfNull(text);
        if (truncatedCharacters < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(truncatedCharacters),
                truncatedCharacters,
                "Truncated character count cannot be negative.");
        }

        Operation = operation;
        Sequence = sequence;
        TimestampUtc = timestampUtc.ToUniversalTime();
        Source = source;
        Text = text;
        IsTruncated = isTruncated;
        TruncatedCharacters = truncatedCharacters;
    }

    public ToolIdentity Tool { get; }

    public string Operation { get; }

    public long Sequence { get; }

    public DateTimeOffset TimestampUtc { get; }

    public ToolProcessOutputSource Source { get; }

    public string Text { get; }

    public bool IsTruncated { get; }

    public long TruncatedCharacters { get; }
}

public sealed record ToolProcessOutputSummary(
    long AcceptedEntries,
    long AcceptedUtf8Bytes,
    int RetainedEntries,
    long RetainedUtf8Bytes,
    long EvictedEntries,
    long EvictedUtf8Bytes,
    long TruncatedEntries,
    long TruncatedCharacters,
    long DroppedDeliveryEntries,
    long DroppedDeliveryUtf8Bytes,
    bool IsCompleted);

/// <summary>
/// Stable terminal result for a process-backed tool operation. Raw OS exception/launch messages are intentionally absent.
/// </summary>
public sealed record ToolProcessResult : ToolResult
{
    public ToolProcessResult(
        ToolIdentity tool,
        string operation,
        ToolResultStatus status,
        ToolProcessLaunchStatus launchStatus,
        int? exitCode,
        bool forcedTerminationRequested,
        ToolProcessOutputSummary output,
        string summary)
        : base(status, summary)
    {
        Tool = tool ?? throw new ArgumentNullException(nameof(tool));
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (!Enum.IsDefined(launchStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(launchStatus),
                launchStatus,
                "A concrete process launch status is required.");
        }

        Output = output ?? throw new ArgumentNullException(nameof(output));

        if (launchStatus == ToolProcessLaunchStatus.Started && exitCode is null)
        {
            throw new ArgumentException("A started process result requires an exit code.", nameof(exitCode));
        }

        if (launchStatus != ToolProcessLaunchStatus.Started && exitCode is not null)
        {
            throw new ArgumentException("A failed process launch cannot report an exit code.", nameof(exitCode));
        }

        if (status == ToolResultStatus.Succeeded &&
            (launchStatus != ToolProcessLaunchStatus.Started || exitCode != 0))
        {
            throw new ArgumentException(
                "A successful process result requires a started process with exit code zero.",
                nameof(status));
        }

        Operation = operation;
        LaunchStatus = launchStatus;
        ExitCode = exitCode;
        ForcedTerminationRequested = forcedTerminationRequested;
    }

    public ToolIdentity Tool { get; }

    public string Operation { get; }

    public ToolProcessLaunchStatus LaunchStatus { get; }

    public int? ExitCode { get; }

    public bool ForcedTerminationRequested { get; }

    public ToolProcessOutputSummary Output { get; }
}

/// <summary>
/// Executes structured external-tool invocations through the already-proven owned process supervisor.
/// </summary>
public interface IExternalToolProcessRunner
{
    IAsyncEnumerable<ToolEvent> ExecuteAsync(
        ToolProcessRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provider-neutral process runner used by first-class adapters. It never invokes a command shell,
/// owns only processes launched through <see cref="IProcessSupervisor"/>, and propagates cancellation
/// after forcing cleanup of the owned tree.
/// </summary>
public sealed class ExternalToolProcessRunner : IExternalToolProcessRunner
{
    private readonly IProcessSupervisor _processSupervisor;

    public ExternalToolProcessRunner(IProcessSupervisor processSupervisor)
    {
        _processSupervisor = processSupervisor ?? throw new ArgumentNullException(nameof(processSupervisor));
    }

    public async IAsyncEnumerable<ToolEvent> ExecuteAsync(
        ToolProcessRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var invocation = request.Invocation;
        var launchRequest = new ProcessLaunchRequest(
            request.ExecutablePath,
            invocation.Arguments,
            invocation.WorkingDirectory,
            SnapshotEnvironment(invocation.Environment),
            new ProcessOutputOptions(
                correlation: CreateProcessCorrelation(request.Correlation)));

        var launchResult = await _processSupervisor
            .StartAsync(launchRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!launchResult.IsStarted)
        {
            yield return new ToolResultEvent(
                CreateLaunchFailureResult(request, launchResult.Status));
            yield break;
        }

        await using var process = launchResult.Process!;
        yield return new ToolProcessStartedEvent(
            request.Tool,
            invocation.Operation,
            process.OwnershipId,
            process.RootProcessId,
            process.StartedUtc);

        try
        {
            await foreach (var entry in process.Output
                .ReadEntriesAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return new ToolProcessOutputEvent(
                    request.Tool,
                    invocation.Operation,
                    entry.Sequence,
                    entry.TimestampUtc,
                    MapOutputSource(entry.Source),
                    entry.Text,
                    entry.IsTruncated,
                    entry.TruncatedCharacters);
            }

            var exit = await process.Completion
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            var statistics = process.Output.GetSnapshot().Statistics;
            yield return new ToolResultEvent(
                CreateExitResult(request, exit, statistics));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await process
                .TerminateOwnedTreeAsync(CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }

    private static IReadOnlyDictionary<string, string?>? SnapshotEnvironment(
        IReadOnlyDictionary<string, string> environment)
    {
        if (environment.Count == 0)
        {
            return null;
        }

        return environment.ToDictionary(
            static pair => pair.Key,
            static pair => (string?)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static ProcessOutputCorrelation? CreateProcessCorrelation(ToolProcessCorrelation? correlation)
    {
        if (correlation is null)
        {
            return null;
        }

        return new ProcessOutputCorrelation(
            correlation.TaskId,
            correlation.AgentRunId,
            correlation.ToolRunId,
            correlation.ProcessRunId,
            correlation.OperationId);
    }

    private static ToolProcessOutputSource MapOutputSource(ProcessOutputSource source) =>
        source switch
        {
            ProcessOutputSource.StandardOutput => ToolProcessOutputSource.StandardOutput,
            ProcessOutputSource.StandardError => ToolProcessOutputSource.StandardError,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown process output source."),
        };

    private static ToolProcessLaunchStatus MapLaunchStatus(ProcessLaunchStatus status) =>
        status switch
        {
            ProcessLaunchStatus.Started => ToolProcessLaunchStatus.Started,
            ProcessLaunchStatus.UnsupportedPlatform => ToolProcessLaunchStatus.UnsupportedPlatform,
            ProcessLaunchStatus.InvalidWorkingDirectory => ToolProcessLaunchStatus.InvalidWorkingDirectory,
            ProcessLaunchStatus.ExecutableNotFound => ToolProcessLaunchStatus.ExecutableNotFound,
            ProcessLaunchStatus.AccessDenied => ToolProcessLaunchStatus.AccessDenied,
            ProcessLaunchStatus.StartFailed => ToolProcessLaunchStatus.StartFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown process launch status."),
        };

    private static ToolProcessResult CreateLaunchFailureResult(
        ToolProcessRequest request,
        ProcessLaunchStatus status)
    {
        return new ToolProcessResult(
            request.Tool,
            request.Invocation.Operation,
            ToolResultStatus.Failed,
            MapLaunchStatus(status),
            exitCode: null,
            forcedTerminationRequested: false,
            CreateEmptyOutputSummary(),
            "The tool process could not be started.");
    }

    private static ToolProcessResult CreateExitResult(
        ToolProcessRequest request,
        OwnedProcessExit exit,
        ProcessOutputStatistics statistics)
    {
        var status = exit.RootExitCode == 0
            ? ToolResultStatus.Succeeded
            : ToolResultStatus.Failed;
        var summary = exit.RootExitCode == 0
            ? "The tool process completed successfully."
            : "The tool process exited with a non-zero exit code.";

        return new ToolProcessResult(
            request.Tool,
            request.Invocation.Operation,
            status,
            ToolProcessLaunchStatus.Started,
            exit.RootExitCode,
            exit.ForcedTerminationRequested,
            CreateOutputSummary(statistics),
            summary);
    }

    private static ToolProcessOutputSummary CreateOutputSummary(ProcessOutputStatistics statistics)
    {
        return new ToolProcessOutputSummary(
            statistics.AcceptedEntries,
            statistics.AcceptedUtf8Bytes,
            statistics.RetainedEntries,
            statistics.RetainedUtf8Bytes,
            statistics.EvictedEntries,
            statistics.EvictedUtf8Bytes,
            statistics.TruncatedEntries,
            statistics.TruncatedCharacters,
            statistics.DroppedDeliveryEntries,
            statistics.DroppedDeliveryUtf8Bytes,
            statistics.IsCompleted);
    }

    private static ToolProcessOutputSummary CreateEmptyOutputSummary() =>
        new(
            AcceptedEntries: 0,
            AcceptedUtf8Bytes: 0,
            RetainedEntries: 0,
            RetainedUtf8Bytes: 0,
            EvictedEntries: 0,
            EvictedUtf8Bytes: 0,
            TruncatedEntries: 0,
            TruncatedCharacters: 0,
            DroppedDeliveryEntries: 0,
            DroppedDeliveryUtf8Bytes: 0,
            IsCompleted: true);
}
