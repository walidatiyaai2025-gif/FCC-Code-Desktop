# FCCD-P05-006 — Cloud implementation evidence

**Task:** `FCCD-P05-006 — Stop/cancel/retry UX`  
**Phase:** P05  
**Evidence class:** `SELF_TEST_ONLY / CLOUD`  
**Status:** `IMPLEMENTED — exact PR-head CI, normal merge, exact-main verification, and canonical reconciliation are still required before CLOSED`.

## Implemented cloud scope

- production Stop and Retry controls on `TaskExecutionSurface`;
- `CanStop` / `CanRetry` state derived from the durable P05-005 task lifecycle;
- idempotent Stop while cancellation is already requested;
- cancellation targeted only at the owned `IAgentRuntimeExecution`;
- durable `StopRequested` task/journal projection with bounded failure diagnostics;
- sanitized `StopRequestFailed` marker when cancellation invocation itself fails;
- manual retry restricted to fully settled `Failed` / `Cancelled` runs;
- same logical task identity with a fresh run identity and incremented attempt;
- exact original prompt reuse for same-process manual retry;
- durable journal sequence continuation across retry;
- no duplicate user-message persistence on retry;
- owning-session guard before retry;
- semantic/accessible WPF control composition.

## Permanent validation

```powershell
.\tools\ui\validate-task-controls.ps1 -RunFixtures -RequireRuntime
```

Static and negative fixtures protect cancellation ownership, retry task/run identity, session ownership, durable journal markers, control enablement bindings, semantic resources, and removal of placeholder behavior.

The executable Windows/WPF fixture uses a controlled local runtime and temporary SQLite database to validate:

- Running → StopRequested → Cancelled state flow;
- repeated Stop issues exactly one runtime cancellation call;
- durable cancelled task state and exactly one `StopRequested` event;
- retry only after the prior run is fully settled;
- same `TaskId`, new `RunId`, incremented `Attempt`;
- exact prompt reuse and no duplicated durable user message;
- `ManualRetryStarting` journal evidence and contiguous durable event sequence;
- cross-session retry rejection without another runtime launch;
- production Stop/Retry controls construct under WPF.

## Safety / owner-last boundary

This evidence does **not** claim a real provider cancellation, real FCC target execution, provider 429 behavior, owner-machine acceptance, P04 closure, P05 phase closure, or release eligibility.

`OWNER-P04-008-REAL-TARGET` remains the existing genuine release-blocking owner-only obligation. P05-006 introduces no new owner-only requirement: all feature-local code, tests, fixtures, CI policy, documentation, and repair work are cloud-actionable.

## Closure rule

Do not mark `FCCD-P05-006` `CLOSED` from this file alone. Closure requires:

1. exact implementation PR-head Windows CI `SUCCESS`;
2. normal merge into canonical `main`;
3. exact merge-SHA Windows CI `SUCCESS`;
4. integrated reconciliation of `docs/TASK_LEDGER.md`, `CURRENT_PHASE.md`, and task evidence;
5. reconciliation PR CI, normal merge, and exact resulting `main` verification.
