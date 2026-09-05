# Stop / cancel / retry UX

`FCCD-P05-006` adds explicit user controls over the durable task lifecycle established by P05-005. The feature remains a UI/domain layer over the existing `IAgentRuntimeExecution` cancellation contract; it does not introduce a second runtime supervisor or provider-specific control path.

## Stop contract

A task can be stopped only while its owned runtime execution is in `Running` state. The first valid Stop action transitions the logical task to `StopRequested`, records that intent in the durable task/event journal when persistence is available, and invokes `CancelAsync` only on the currently owned `IAgentRuntimeExecution`.

Repeated Stop while `StopRequested` is idempotent and does not issue another cancellation call. The UI remains in the truthful `Stopping` state until the runtime reaches a terminal result. A cancellation-call failure is surfaced with bounded diagnostics and a sanitized `StopRequestFailed` journal marker; the UI does not invent a `Cancelled` result.

## Manual retry contract

Manual Retry is available only after a `Failed` or `Cancelled` run has completely settled. Retry:

- preserves the existing logical `TaskId`;
- preserves the task's owning persisted session;
- preserves the exact in-memory original prompt for the same application lifetime;
- creates a fresh `RunId` and increments `Attempt`;
- continues the durable task-event sequence from the highest persisted sequence;
- records `ManualRetryStarting` before runtime handoff;
- does not insert another user message for the original prompt;
- rejects retry if the user is no longer in the task's owning session.

Crash/reboot reconstruction of retryable in-memory prompt/control state is intentionally not claimed here; startup reconciliation remains owned by P15.

## UI

`TaskExecutionSurface` exposes semantic WPF Stop and Retry buttons. Their enabled state comes from `TaskExecutionState.CanStop` / `CanRetry`, and both controls have automation names and explanatory tooltips. Control errors are surfaced through the existing bounded task failure diagnostic surface.

## Safety and ownership boundaries

- P04 retains runtime cancellation/supervision mechanics and REAL_TARGET runtime acceptance.
- P05-005 retains the underlying lifecycle, persistence ordering, execution identity, and one-active/settling-task invariants.
- P05-007 owns Markdown/code/diff rendering.
- P05-008 owns long-conversation virtualization/performance closure.
- P14 owns the global execution queue/cooldown/rate-limit coordination.
- P15 owns crash/restart reconciliation.
- No raw provider payload is persisted by P05-006.
- No provider 429, owner Windows run, or other REAL_TARGET result is manufactured by this feature.

## Permanent cloud validation

```powershell
.\tools\ui\validate-task-controls.ps1 -RunFixtures -RequireRuntime
```

The executable fixture uses controlled local runtime implementations with a temporary SQLite database. It verifies idempotent owned cancellation, durable Stop intent, terminal cancellation, same-task/new-run manual retry, exact prompt reuse, no duplicated durable user message, contiguous journal sequencing, cross-session retry rejection, and production WPF control construction. The Windows CI policy rejects removal of this gate.
