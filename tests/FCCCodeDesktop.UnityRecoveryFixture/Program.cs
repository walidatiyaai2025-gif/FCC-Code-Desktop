using FCCCodeDesktop.Tools.Unity;

var operationId = Guid.Parse("8df87916-b12d-4fd0-8509-fbe620c9a6a9");
var projectId = Guid.Parse("6c61c932-f892-4a45-99d4-e4ed195205e3");
var processRunId = Guid.Parse("d7edc314-a97d-44cc-a0ec-ea89074c65fd");
var startedUtc = new DateTimeOffset(2026, 9, 8, 18, 0, 0, TimeSpan.Zero);

var running = new UnityOperationRecoveryCheckpoint(
    operationId,
    projectId,
    processRunId,
    UnityRecoveryCheckpointState.Running,
    startedUtc,
    startedUtc.AddSeconds(2));
var cancelRequested = new UnityOperationRecoveryCheckpoint(
    operationId,
    projectId,
    processRunId,
    UnityRecoveryCheckpointState.CancellationRequested,
    startedUtc,
    startedUtc.AddSeconds(3));

var ownedRunning = Observe(
    processRunId,
    UnityObservedProcessState.OwnedRunning,
    UnityObservedEvidenceState.None,
    UnityObservedLeaseState.HeldByOperation);
var continueDecision = UnityOperationRecoveryPolicy.Evaluate(running, ownedRunning);
Expect(continueDecision.Status == UnityRecoveryDecisionStatus.ContinueOwnedOperation, "owned running operation continues");
Expect(!continueDecision.MayTerminateObservedProcess, "running operation is not terminated without cancellation request");
Expect(!continueDecision.MayRetry, "same-project operation cannot overlap while owned process runs");

var cancelDecision = UnityOperationRecoveryPolicy.Evaluate(cancelRequested, ownedRunning);
Expect(cancelDecision.Status == UnityRecoveryDecisionStatus.CancelOwnedOperation, "cancellation request targets owned operation");
Expect(cancelDecision.MayTerminateObservedProcess, "owned cancellation may terminate only matching process tree");
Expect(cancelDecision.Actions.Contains(UnityRecoveryAction.WaitForOwnedExit), "cancellation waits for owned tree exit before lease release");
Expect(!cancelDecision.MayRetry, "retry cannot begin before cancellation completes");

var foreignObservation = Observe(
    Guid.Parse("e1a90b96-e413-4e51-ad04-496576ba5158"),
    UnityObservedProcessState.ForeignOrMismatchedIdentity,
    UnityObservedEvidenceState.None,
    UnityObservedLeaseState.HeldByOperation);
var foreignDecision = UnityOperationRecoveryPolicy.Evaluate(cancelRequested, foreignObservation);
Expect(foreignDecision.Status == UnityRecoveryDecisionStatus.BlockedUnsafeOwnership, "foreign process is blocked from cancellation");
Expect(!foreignDecision.MayTerminateObservedProcess, "foreign process is never killed");
Expect(foreignDecision.Actions.Contains(UnityRecoveryAction.PreserveForeignProcess), "foreign process preservation is explicit");

var staleAfterExit = Observe(
    processRunId,
    UnityObservedProcessState.OwnedExited,
    UnityObservedEvidenceState.StaleOrMismatched,
    UnityObservedLeaseState.Released);
var retryDecision = UnityOperationRecoveryPolicy.Evaluate(running, staleAfterExit);
Expect(retryDecision.Status == UnityRecoveryDecisionStatus.RetryFromFreshInvocation, "stale terminal evidence cannot close operation");
Expect(retryDecision.MayRetry, "retry allowed only after owned exit and lease release");
Expect(retryDecision.Actions.Contains(UnityRecoveryAction.RequireFreshCorrelatedEvidence), "retry requires fresh correlation");
Expect(!retryDecision.MayTerminateObservedProcess, "no process termination after owned exit");

var freshAfterExitLeaseHeld = Observe(
    processRunId,
    UnityObservedProcessState.OwnedExited,
    UnityObservedEvidenceState.FreshCorrelatedTerminal,
    UnityObservedLeaseState.HeldByOperation);
var reconcileDecision = UnityOperationRecoveryPolicy.Evaluate(running, freshAfterExitLeaseHeld);
Expect(reconcileDecision.Status == UnityRecoveryDecisionStatus.ReconcileTerminalEvidence, "fresh terminal evidence reconciles");
Expect(reconcileDecision.Actions.SequenceEqual(
    [UnityRecoveryAction.ReleaseOwnedProjectLease, UnityRecoveryAction.ReconcileTerminalEvidence]),
    "owned project lease released before terminal reconciliation");
Expect(!reconcileDecision.MayRetry, "terminal reconciliation is not a duplicate retry");

var unknownLease = Observe(
    processRunId,
    UnityObservedProcessState.OwnedExited,
    UnityObservedEvidenceState.None,
    UnityObservedLeaseState.Unknown);
var unknownLeaseDecision = UnityOperationRecoveryPolicy.Evaluate(running, unknownLease);
Expect(unknownLeaseDecision.Status == UnityRecoveryDecisionStatus.BlockedInvariantViolation, "unknown lease blocks unsafe retry");
Expect(!unknownLeaseDecision.MayRetry, "unknown lease cannot overlap same project");

var wrongOperation = new UnityOperationRecoveryObservation(
    Guid.Parse("c54c87ad-f88b-48f7-a983-d25c124a9d30"),
    projectId,
    processRunId,
    UnityObservedProcessState.OwnedExited,
    UnityObservedEvidenceState.FreshCorrelatedTerminal,
    UnityObservedLeaseState.Released,
    startedUtc.AddMinutes(1));
var wrongOperationDecision = UnityOperationRecoveryPolicy.Evaluate(running, wrongOperation);
Expect(wrongOperationDecision.Status == UnityRecoveryDecisionStatus.BlockedUnsafeOwnership, "mismatched operation evidence rejected");
Expect(!wrongOperationDecision.MayRetry, "identity mismatch cannot silently retry");

Console.WriteLine("P10-012 Unity cancellation/recovery fixture PASS");
return 0;

UnityOperationRecoveryObservation Observe(
    Guid? observedProcessRunId,
    UnityObservedProcessState processState,
    UnityObservedEvidenceState evidenceState,
    UnityObservedLeaseState leaseState)
{
    return new UnityOperationRecoveryObservation(
        operationId,
        projectId,
        observedProcessRunId,
        processState,
        evidenceState,
        leaseState,
        startedUtc.AddMinutes(1));
}

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"P10-012 assertion failed: {message}");
    }
}
