using System.Runtime.CompilerServices;
using FCCCodeDesktop.Runtime;
using FCCCodeDesktop.Tools;

namespace FCCCodeDesktop.Application;

/// <summary>
/// Application-owned orchestration that executes provider-neutral tool-process contracts
/// through the proven Runtime owned-process supervisor and bounded-output pipeline.
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
