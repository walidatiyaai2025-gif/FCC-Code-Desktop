# P05-005 — Explicit task state machine

## Scope

`FCCD-P05-005` owns the application-level lifecycle for one logical coding task inside the active persisted session. It composes the already-integrated P03 execution journal, P04 runtime abstraction, and P05 conversation/session surfaces without redefining those lower-level contracts.

Production flow:

`ConversationComposer` → `MainWindow` submission boundary → `TaskExecutionState` → `IAgentRuntime` → normalized runtime events → `StreamingConversationState` + durable P03 journal/session history.

## Lifecycle

The explicit lifecycle is:

- `Idle`
- `Starting`
- `Running`
- `StopRequested`
- `Succeeded`
- `Failed`
- `Cancelled`

`TaskExecutionState` owns the legal transition matrix and rejects illegal or overlapping starts. A new task is also rejected while the previous execution pump or owned runtime execution is still settling, even if a terminal state has already been projected. P05-006 owns the user-facing stop/cancel/retry actions; P05-005 only establishes the lifecycle states and durable execution identity needed by that later task.

A new logical task receives a new task ID and run ID. The task is attached to exactly the active persisted session. Runtime execution and terminal result identities must match the prepared task/run identity or the task fails closed.

If task startup fails after a runtime execution has been created but before ownership transfers to the execution pump, the execution is cancelled when needed and disposed before the startup failure is reconciled. Startup cancellation is represented as `Cancelled`, not silently converted into success.

## Durable journal

The state machine writes through `IExecutionJournalStore` / `SqliteExecutionJournalStore`:

- task identity and current state;
- agent-run identity and current/terminal state;
- ordered task/agent/tool event journal rows.

Only normalized, task-safe event metadata is journaled. Raw provider `PayloadJson` is deliberately not written into the task journal.

Terminal task persistence is fail-closed: the durable terminal task/agent/event state is written before the corresponding terminal UI transition. If terminal persistence fails, the task is reconciled as failed rather than displaying a successful terminal state that the journal did not durably record. Cancellation/failure recovery persists the task row before dependent agent-run state so recovery remains foreign-key safe even when the initial starting write was interrupted.

Completed assistant text is persisted through `SessionWorkspaceState.AppendMessageAsync` so conversation history survives process restart. Runtime session identity is rebound through the existing P05-004 session persistence path. Failure diagnostics shown by the task state are bounded.

## Cross-session safety

The active local session is captured when a task starts. If the active session changes while the runtime is emitting events, the owned execution is cancelled and the task fails closed rather than allowing output to be written into a different session.

Only one task may be active or settling in the workspace at a time.

## Conversation event sequencing

Each underlying runtime execution owns a zero-based source event sequence. `ConversationSequencedAgentRuntime` rejects a source sequence that does not start at zero, validates contiguity for each execution, and projects a monotonic conversation-facing sequence across successive logical tasks. This preserves the fail-closed ordering invariant already enforced by `StreamingConversationState` without changing the transport-neutral P04 runtime contract.

## Runtime availability

Production startup uses `FccEnvironmentDiscoveryService`. When `fcc-claude` is discovered, the structured runtime is composed as:

`ConversationSequencedAgentRuntime(AgentRuntimeSupervisor(FccStructuredAgentRuntime))`.

If the executable is unavailable, task start fails before a task identity is created and the Tasks surface exposes the discovery reason. P04 remains the authority for health/version compatibility evidence and the owner-deferred full real-target runtime contract. Cloud fixtures do not pretend that the owner's installed FCC/provider environment is available.

## UI

`TaskExecutionSurface` is wired into the existing Tasks workspace section and presents:

- lifecycle state;
- runtime availability;
- task ID;
- run ID;
- attempt count;
- bounded failure diagnostics.

It uses only the semantic design-system resources established in P02.

## Permanent cloud validation

Windows CI runs:

```powershell
.\tools\ui\validate-task-state-machine.ps1 -RunFixtures -RequireRuntime
```

The gate includes static contract checks, negative mutation fixtures, and an executable Windows/WPF + temporary-SQLite fixture. Controlled fixture runtimes cover consecutive successful tasks, durable journal and assistant history, active/settling task rejection, bounded classified failures, startup identity mismatch cleanup, zero-origin and contiguous source-sequence enforcement, runtime-unavailable rejection, and production surface construction.

All runtime events used by this gate are `SELF_TEST_ONLY` fixtures. They are not provider-backed `REAL_TARGET` evidence and do not satisfy `OWNER-P04-008-REAL-TARGET`.

## Explicit exclusions

This task does not close or implement:

- `FCCD-P05-006` — user Stop/cancel/retry UX;
- `FCCD-P05-007` — Markdown/code/diff rendering;
- `FCCD-P05-008` — measured conversation virtualization/performance closure;
- P04-008 real-target acceptance;
- P15 crash/reboot reconciliation of interrupted work.
