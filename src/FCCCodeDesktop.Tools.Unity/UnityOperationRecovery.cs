namespace FCCCodeDesktop.Tools.Unity;

/// <summary>
/// Durable non-path process identity captured for a Unity automation operation.
/// The checkpoint intentionally stores no executable, project path, argv, environment, stdout or stderr.
/// </summary>
public sealed record UnityOperationRecoveryCheckpoint
{
    public UnityOperationRecoveryCheckpoint(
        Guid operationId,
        Guid projectId,
        Guid processRunId,
        UnityRecoveryCheckpointState state,
        DateTimeOffset startedUtc,
        DateTimeOffset updatedUtc)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Unity recovery operation identity cannot be empty.", nameof(operationId));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Unity recovery project identity cannot be empty.", nameof(projectId));
        }

        if (processRunId == Guid.Empty)
        {
            throw new ArgumentException("Unity recovery process-run identity cannot be empty.", nameof(processRunId));
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        startedUtc = startedUtc.ToUniversalTime();
        updatedUtc = updatedUtc.ToUniversalTime();
        if (updatedUtc < startedUtc)
        {
            throw new ArgumentException("Unity recovery checkpoint update time cannot precede its start time.", nameof(updatedUtc));
        }

        OperationId = operationId;
        ProjectId = projectId;
        ProcessRunId = processRunId;
        State = state;
        StartedUtc = startedUtc;
        UpdatedUtc = updatedUtc;
    }

    public Guid OperationId { get; }

    public Guid ProjectId { get; }

    public Guid ProcessRunId { get; }

    public UnityRecoveryCheckpointState State { get; }

    public DateTimeOffset StartedUtc { get; }

    public DateTimeOffset UpdatedUtc { get; }
}

public enum UnityRecoveryCheckpointState
{
    Running = 1,
    CancellationRequested = 2,
}

public enum UnityObservedProcessState
{
    NotObserved = 1,
    OwnedRunning = 2,
    OwnedExited = 3,
    ForeignOrMismatchedIdentity = 4,
}

public enum UnityObservedEvidenceState
{
    None = 1,
    FreshCorrelatedTerminal = 2,
    StaleOrMismatched = 3,
}

public enum UnityObservedLeaseState
{
    HeldByOperation = 1,
    Released = 2,
    Unknown = 3,
}

public sealed record UnityOperationRecoveryObservation
{
    public UnityOperationRecoveryObservation(
        Guid operationId,
        Guid projectId,
        Guid? observedProcessRunId,
        UnityObservedProcessState processState,
        UnityObservedEvidenceState evidenceState,
        UnityObservedLeaseState leaseState,
        DateTimeOffset observedUtc)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Observed Unity operation identity cannot be empty.", nameof(operationId));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Observed Unity project identity cannot be empty.", nameof(projectId));
        }

        if (observedProcessRunId == Guid.Empty)
        {
            throw new ArgumentException("Observed Unity process-run identity cannot be empty when present.", nameof(observedProcessRunId));
        }

        if (!Enum.IsDefined(processState))
        {
            throw new ArgumentOutOfRangeException(nameof(processState));
        }

        if (!Enum.IsDefined(evidenceState))
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceState));
        }

        if (!Enum.IsDefined(leaseState))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseState));
        }

        if (processState is UnityObservedProcessState.OwnedRunning or UnityObservedProcessState.OwnedExited && observedProcessRunId is null)
        {
            throw new ArgumentException("An observed owned Unity process requires a process-run identity.", nameof(observedProcessRunId));
        }

        OperationId = operationId;
        ProjectId = projectId;
        ObservedProcessRunId = observedProcessRunId;
        ProcessState = processState;
        EvidenceState = evidenceState;
        LeaseState = leaseState;
        ObservedUtc = observedUtc.ToUniversalTime();
    }

    public Guid OperationId { get; }

    public Guid ProjectId { get; }

    public Guid? ObservedProcessRunId { get; }

    public UnityObservedProcessState ProcessState { get; }

    public UnityObservedEvidenceState EvidenceState { get; }

    public UnityObservedLeaseState LeaseState { get; }

    public DateTimeOffset ObservedUtc { get; }
}

public enum UnityRecoveryDecisionStatus
{
    ContinueOwnedOperation = 1,
    CancelOwnedOperation = 2,
    ReconcileTerminalEvidence = 3,
    RetryFromFreshInvocation = 4,
    BlockedUnsafeOwnership = 5,
    BlockedInvariantViolation = 6,
}

public enum UnityRecoveryAction
{
    WaitForOwnedExit = 1,
    RequestOwnedCancellation = 2,
    ReleaseOwnedProjectLease = 3,
    RequireFreshCorrelatedEvidence = 4,
    ReconcileTerminalEvidence = 5,
    RetryFromFreshInvocation = 6,
    PreserveForeignProcess = 7,
    InvestigateOwnership = 8,
}

public sealed record UnityOperationRecoveryDecision(
    UnityRecoveryDecisionStatus Status,
    IReadOnlyList<UnityRecoveryAction> Actions,
    bool MayTerminateObservedProcess,
    bool MayRetry,
    string Code,
    string Summary);

/// <summary>
/// Fail-closed recovery policy for Unity automation after cancellation, timeout, application restart,
/// or an interrupted observation window. It never authorizes termination unless the durable process-run
/// identity matches the checkpoint, and it never treats stale/mismatched artifacts as successful evidence.
/// </summary>
public static class UnityOperationRecoveryPolicy
{
    public static UnityOperationRecoveryDecision Evaluate(
        UnityOperationRecoveryCheckpoint checkpoint,
        UnityOperationRecoveryObservation observation)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(observation);

        if (checkpoint.OperationId != observation.OperationId || checkpoint.ProjectId != observation.ProjectId)
        {
            return BlockedOwnership("unity.recovery.identity-mismatch", "Recovery observation does not belong to the checkpointed Unity operation/project.");
        }

        if (observation.ObservedUtc < checkpoint.StartedUtc)
        {
            return BlockedInvariant("unity.recovery.time-regression", "Recovery observation predates the checkpointed Unity operation.");
        }

        if (observation.ProcessState == UnityObservedProcessState.ForeignOrMismatchedIdentity ||
            (observation.ObservedProcessRunId is Guid observedId && observedId != checkpoint.ProcessRunId))
        {
            return BlockedOwnership("unity.recovery.foreign-process", "Observed process identity is not owned by the checkpointed Unity operation; it must be preserved.");
        }

        if (observation.ProcessState == UnityObservedProcessState.OwnedRunning)
        {
            if (observation.LeaseState != UnityObservedLeaseState.HeldByOperation)
            {
                return BlockedInvariant("unity.recovery.running-without-lease", "Owned Unity process is still running but the same-project lease is not confirmed held.");
            }

            if (checkpoint.State == UnityRecoveryCheckpointState.CancellationRequested)
            {
                return Decision(
                    UnityRecoveryDecisionStatus.CancelOwnedOperation,
                    [UnityRecoveryAction.RequestOwnedCancellation, UnityRecoveryAction.WaitForOwnedExit],
                    mayTerminateObservedProcess: true,
                    mayRetry: false,
                    "unity.recovery.cancel-owned",
                    "Cancellation may target only the checkpointed owned Unity process tree, then wait for exit before releasing the project lease.");
            }

            return Decision(
                UnityRecoveryDecisionStatus.ContinueOwnedOperation,
                [UnityRecoveryAction.WaitForOwnedExit],
                mayTerminateObservedProcess: false,
                mayRetry: false,
                "unity.recovery.owned-running",
                "The checkpointed Unity process remains active and retains its same-project lease.");
        }

        if (observation.LeaseState == UnityObservedLeaseState.Unknown)
        {
            return BlockedInvariant("unity.recovery.lease-unknown", "Project lease disposition is unknown; retry could overlap an existing Unity operation.");
        }

        if (observation.EvidenceState == UnityObservedEvidenceState.FreshCorrelatedTerminal)
        {
            var actions = observation.LeaseState == UnityObservedLeaseState.HeldByOperation
                ? new[] { UnityRecoveryAction.ReleaseOwnedProjectLease, UnityRecoveryAction.ReconcileTerminalEvidence }
                : new[] { UnityRecoveryAction.ReconcileTerminalEvidence };

            return Decision(
                UnityRecoveryDecisionStatus.ReconcileTerminalEvidence,
                actions,
                mayTerminateObservedProcess: false,
                mayRetry: false,
                "unity.recovery.reconcile-terminal",
                "Fresh correlated terminal evidence is available; release any remaining owned lease before reconciliation.");
        }

        var retryActions = new List<UnityRecoveryAction>(4);
        if (observation.LeaseState == UnityObservedLeaseState.HeldByOperation)
        {
            retryActions.Add(UnityRecoveryAction.ReleaseOwnedProjectLease);
        }

        retryActions.Add(UnityRecoveryAction.RequireFreshCorrelatedEvidence);
        retryActions.Add(UnityRecoveryAction.RetryFromFreshInvocation);

        return Decision(
            UnityRecoveryDecisionStatus.RetryFromFreshInvocation,
            retryActions,
            mayTerminateObservedProcess: false,
            mayRetry: true,
            observation.EvidenceState == UnityObservedEvidenceState.StaleOrMismatched
                ? "unity.recovery.reject-stale-retry"
                : "unity.recovery.no-terminal-retry",
            "No trustworthy terminal evidence remains after the owned process ended/disappeared; stale evidence is rejected and any retry must use a fresh invocation identity.");
    }

    private static UnityOperationRecoveryDecision BlockedOwnership(string code, string summary) =>
        Decision(
            UnityRecoveryDecisionStatus.BlockedUnsafeOwnership,
            [UnityRecoveryAction.PreserveForeignProcess, UnityRecoveryAction.InvestigateOwnership],
            mayTerminateObservedProcess: false,
            mayRetry: false,
            code,
            summary);

    private static UnityOperationRecoveryDecision BlockedInvariant(string code, string summary) =>
        Decision(
            UnityRecoveryDecisionStatus.BlockedInvariantViolation,
            [UnityRecoveryAction.InvestigateOwnership],
            mayTerminateObservedProcess: false,
            mayRetry: false,
            code,
            summary);

    private static UnityOperationRecoveryDecision Decision(
        UnityRecoveryDecisionStatus status,
        IReadOnlyList<UnityRecoveryAction> actions,
        bool mayTerminateObservedProcess,
        bool mayRetry,
        string code,
        string summary)
    {
        return new UnityOperationRecoveryDecision(status, actions, mayTerminateObservedProcess, mayRetry, code, summary);
    }
}
