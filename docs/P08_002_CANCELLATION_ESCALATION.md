# P08-002 — Graceful → forced cancellation escalation

## Scope

`FCCD-P08-002` composes a bounded cancellation policy over the owned process-tree primitive delivered by P08-001. It does not add terminal emulation, ConPTY, log streaming, shell profiles, remote process control, arbitrary PID termination, or later-phase tool behavior.

## Contract

`IProcessCancellationEscalator.CancelAsync` receives an already-owned `ISupervisedProcess`, an optional caller-specific graceful-stop request, and a bounded grace period.

The cancellation result records:

- whether the process was already complete, exited gracefully, or required forced termination;
- whether a graceful request was absent, completed, failed, or timed out;
- the canonical `OwnedProcessExit` from P08-001;
- the configured grace period;
- bounded diagnostic text for graceful-request failures.

## Safety invariants

- Pre-cancelled caller tokens fail before any cancellation mutation.
- Once cancellation has begun, owned-tree cleanup is non-abandonable: a caller cannot interrupt escalation halfway and leave an owned orphan.
- If no graceful mechanism exists, escalation uses the existing owned-tree termination primitive immediately rather than pretending a graceful signal was sent.
- If the graceful request fails, times out, or the owned tree remains alive through the grace period, only `ISupervisedProcess.TerminateOwnedTreeAsync` is used for forced cleanup.
- The cancellation layer never accepts an arbitrary PID and cannot terminate an unowned process.
- Grace periods must be greater than zero and no greater than 30 seconds.
- Graceful-request failure text is bounded before crossing the contract boundary.

The graceful callback is intentionally transport-agnostic. P08-004/P08-007 may later supply terminal-specific Ctrl+C/input behavior without coupling P08-002 to ConPTY. Other runtimes may supply their own bounded cooperative stop request.

## Cloud validation

Windows real-process tests cover:

- cooperative graceful exit through a real supervised PowerShell process and filesystem signal;
- grace-period expiry followed by forced owned-tree cleanup;
- graceful-request failure followed by forced cleanup;
- immediate owned-tree termination when no graceful mechanism is available;
- pre-cancelled operation leaving the owned process running until explicit fixture cleanup;
- already-completed process idempotence;
- grace-period safety bounds.

The existing P08-001 tests remain authoritative for Job Object ownership isolation and unowned-process preservation. Permanent Windows Release CI remains the task integration gate.

## Non-claims

This task does not close P08, authorize P09/P13, implement bounded logs/ConPTY/shell profiles/interactive terminal UX, satisfy either queued owner-only obligation, or imply `VERIFIED_FINAL_COMPLETE=true`.
