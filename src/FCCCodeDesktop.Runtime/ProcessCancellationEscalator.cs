namespace FCCCodeDesktop.Runtime;

/// <summary>
/// Bounded cancellation policy for owned process trees. A caller may provide the process-specific
/// graceful signal (for example Ctrl+C/terminal input in a later phase). If the owned tree remains
/// alive after the configured grace window, only the existing owned-tree termination primitive is
/// used for escalation.
/// </summary>
public sealed class ProcessCancellationEscalator : IProcessCancellationEscalator
{
    public static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan MaximumGracePeriod = TimeSpan.FromSeconds(30);

    private const int MaximumFailureMessageCharacters = 2_048;

    public async Task<ProcessCancellationResult> CancelAsync(
        ISupervisedProcess process,
        ProcessGracefulStopRequest? requestGracefulStop = null,
        TimeSpan? gracePeriod = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(process);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedGracePeriod = gracePeriod ?? DefaultGracePeriod;
        if (resolvedGracePeriod <= TimeSpan.Zero || resolvedGracePeriod > MaximumGracePeriod)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gracePeriod),
                gracePeriod,
                $"Process cancellation grace period must be greater than zero and no more than {MaximumGracePeriod.TotalSeconds} seconds.");
        }

        if (process.Completion.IsCompleted)
        {
            var completedExit = await process.Completion.ConfigureAwait(false);
            return new ProcessCancellationResult(
                ProcessCancellationOutcome.AlreadyCompleted,
                GracefulStopRequestStatus.NotProvided,
                completedExit,
                resolvedGracePeriod);
        }

        if (requestGracefulStop is null)
        {
            return await ForceOwnedTreeAsync(
                process,
                GracefulStopRequestStatus.NotProvided,
                resolvedGracePeriod,
                null).ConfigureAwait(false);
        }

        using var gracefulDeadline = new CancellationTokenSource(resolvedGracePeriod);
        var gracefulRequest = ObserveGracefulRequestAsync(requestGracefulStop, gracefulDeadline.Token);
        var graceDelay = Task.Delay(resolvedGracePeriod, CancellationToken.None);

        while (true)
        {
            var winner = await Task.WhenAny(process.Completion, gracefulRequest, graceDelay).ConfigureAwait(false);
            if (ReferenceEquals(winner, process.Completion))
            {
                await gracefulDeadline.CancelAsync().ConfigureAwait(false);
                var gracefulExit = await process.Completion.ConfigureAwait(false);
                var requestResult = gracefulRequest.IsCompletedSuccessfully
                    ? gracefulRequest.Result
                    : GracefulRequestObservation.Completed;

                return new ProcessCancellationResult(
                    ProcessCancellationOutcome.GracefulExit,
                    requestResult.Status,
                    gracefulExit,
                    resolvedGracePeriod,
                    requestResult.FailureMessage);
            }

            if (ReferenceEquals(winner, gracefulRequest))
            {
                var requestResult = await gracefulRequest.ConfigureAwait(false);
                if (requestResult.Status is GracefulStopRequestStatus.Failed or GracefulStopRequestStatus.TimedOut)
                {
                    return await ForceOwnedTreeAsync(
                        process,
                        requestResult.Status,
                        resolvedGracePeriod,
                        requestResult.FailureMessage).ConfigureAwait(false);
                }

                var exitOrDeadline = await Task.WhenAny(process.Completion, graceDelay).ConfigureAwait(false);
                if (ReferenceEquals(exitOrDeadline, process.Completion))
                {
                    var gracefulExit = await process.Completion.ConfigureAwait(false);
                    return new ProcessCancellationResult(
                        ProcessCancellationOutcome.GracefulExit,
                        requestResult.Status,
                        gracefulExit,
                        resolvedGracePeriod,
                        requestResult.FailureMessage);
                }

                return await ForceOwnedTreeAsync(
                    process,
                    requestResult.Status,
                    resolvedGracePeriod,
                    requestResult.FailureMessage).ConfigureAwait(false);
            }

            await gracefulDeadline.CancelAsync().ConfigureAwait(false);
            var timedRequest = gracefulRequest.IsCompleted
                ? await gracefulRequest.ConfigureAwait(false)
                : GracefulRequestObservation.TimedOut;

            return await ForceOwnedTreeAsync(
                process,
                timedRequest.Status == GracefulStopRequestStatus.Completed
                    ? GracefulStopRequestStatus.Completed
                    : GracefulStopRequestStatus.TimedOut,
                resolvedGracePeriod,
                timedRequest.FailureMessage).ConfigureAwait(false);
        }
    }

    private static async Task<ProcessCancellationResult> ForceOwnedTreeAsync(
        ISupervisedProcess process,
        GracefulStopRequestStatus gracefulStatus,
        TimeSpan gracePeriod,
        string? gracefulFailureMessage)
    {
        // Once cancellation begins, cleanup is intentionally non-abandonable. A caller token may
        // reject the operation before it starts, but it cannot interrupt owned-tree cleanup and
        // leave an orphan after escalation has begun.
        await process.TerminateOwnedTreeAsync(CancellationToken.None).ConfigureAwait(false);
        var exit = await process.Completion.ConfigureAwait(false);
        var outcome = exit.ForcedTerminationRequested
            ? ProcessCancellationOutcome.ForcedExit
            : ProcessCancellationOutcome.GracefulExit;

        return new ProcessCancellationResult(
            outcome,
            gracefulStatus,
            exit,
            gracePeriod,
            gracefulFailureMessage);
    }

    private static async Task<GracefulRequestObservation> ObserveGracefulRequestAsync(
        ProcessGracefulStopRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await request(cancellationToken).ConfigureAwait(false);
            return GracefulRequestObservation.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return GracefulRequestObservation.TimedOut;
        }
        catch (Exception exception)
        {
            return new GracefulRequestObservation(
                GracefulStopRequestStatus.Failed,
                BoundFailureMessage(exception.Message));
        }
    }

    private static string BoundFailureMessage(string message) =>
        message.Length <= MaximumFailureMessageCharacters
            ? message
            : message[..MaximumFailureMessageCharacters];

    private sealed record GracefulRequestObservation(
        GracefulStopRequestStatus Status,
        string? FailureMessage)
    {
        public static GracefulRequestObservation Completed { get; } =
            new(GracefulStopRequestStatus.Completed, null);

        public static GracefulRequestObservation TimedOut { get; } =
            new(GracefulStopRequestStatus.TimedOut, null);
    }
}
