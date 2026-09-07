namespace FCCCodeDesktop.Runtime;

public enum ProcessLaunchStatus
{
    Started = 0,
    UnsupportedPlatform = 1,
    InvalidWorkingDirectory = 2,
    ExecutableNotFound = 3,
    AccessDenied = 4,
    StartFailed = 5,
}

public sealed record ProcessLaunchRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?>? Environment = null);

public sealed record OwnedProcessSnapshot(
    Guid OwnershipId,
    int RootProcessId,
    DateTimeOffset StartedUtc,
    bool RootHasExited);

public sealed record OwnedProcessExit(
    Guid OwnershipId,
    int RootProcessId,
    int RootExitCode,
    DateTimeOffset StartedUtc,
    DateTimeOffset TreeExitedUtc,
    bool ForcedTerminationRequested);

public sealed record ProcessLaunchResult(
    ProcessLaunchStatus Status,
    ISupervisedProcess? Process,
    string? FailureMessage = null)
{
    public bool IsStarted => Status == ProcessLaunchStatus.Started && Process is not null;
}

/// <summary>
/// Supervises only processes started through this instance. It does not expose arbitrary PID
/// termination and does not implement graceful cancellation policy; P08-002 composes that policy
/// over the owned-tree termination primitive exposed by <see cref="ISupervisedProcess"/>.
/// </summary>
public interface IProcessSupervisor : IAsyncDisposable
{
    IReadOnlyList<OwnedProcessSnapshot> GetActiveProcesses();

    Task<ProcessLaunchResult> StartAsync(
        ProcessLaunchRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISupervisedProcess : IAsyncDisposable
{
    Guid OwnershipId { get; }

    int RootProcessId { get; }

    DateTimeOffset StartedUtc { get; }

    Task<OwnedProcessExit> Completion { get; }

    /// <summary>
    /// Immediately terminates only the OS job/process tree owned by this handle. Graceful-to-forced
    /// escalation is intentionally outside this P08-001 primitive.
    /// </summary>
    ValueTask TerminateOwnedTreeAsync(CancellationToken cancellationToken = default);
}
