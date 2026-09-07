namespace FCCCodeDesktop.Runtime;

public enum ProcessCancellationOutcome
{
    AlreadyCompleted = 0,
    GracefulExit = 1,
    ForcedExit = 2,
}

public enum GracefulStopRequestStatus
{
    NotProvided = 0,
    Completed = 1,
    Failed = 2,
    TimedOut = 3,
}

public sealed record ProcessCancellationResult(
    ProcessCancellationOutcome Outcome,
    GracefulStopRequestStatus GracefulRequestStatus,
    OwnedProcessExit Exit,
    TimeSpan GracePeriod,
    string? GracefulFailureMessage = null)
{
    public bool ForcedTerminationRequested => Exit.ForcedTerminationRequested;
}

/// <summary>
/// Sends the caller-specific graceful stop signal. Implementations should only transmit the
/// graceful request; they must not force-kill the process tree or wait indefinitely for exit.
/// </summary>
public delegate ValueTask ProcessGracefulStopRequest(CancellationToken cancellationToken);

/// <summary>
/// Applies bounded graceful-to-forced cancellation to an already-owned supervised process tree.
/// </summary>
public interface IProcessCancellationEscalator
{
    Task<ProcessCancellationResult> CancelAsync(
        ISupervisedProcess process,
        ProcessGracefulStopRequest? requestGracefulStop = null,
        TimeSpan? gracePeriod = null,
        CancellationToken cancellationToken = default);
}
