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

`TaskExecutionState` owns the legal transition matrix and rejects illegal or overlapping starts. P05-006 owns the user-facing stop/cancel/retry actions; P05-005 only establishes the lifecycle states and durable execution identity needed by that later task.

A new logical task receives a new task ID and run ID. The task is attached to exactly the active persisted session. Runtime execution and terminal result identities must match the prepared task/run identity or the task fails closed.

## Durable journal

The state machine writes through `IExecutionJournalStore` / `SqliteExecutionJournalStore`:

- task identity and current state;
- agent-run identity and current/terminal state;
- ordered task/agent/tool event journal rows.

Only normalized, task-safe event metadata is journaled. Raw provider `PayloadJson` is deliberately not written into the task journal.

Completed assistant text is persisted through `SessionWorkspaceState.AppendMessageAsync` so conversation history survives process restart. Runtime session identity is rebound through the existing P05-004 session persistence path.

## Cross-session safety

The active local session is captured when a task starts. If the active session changes while the runtime is emitting events, the owned execution is cancelled and the task fails closed rather than allowing output to be written into a different session.

Only one task may be active in the workspace at a time.

## Conversation event sequencing

Each underlying runtime execution owns its own source event sequence. `ConversationSequencedAgentRuntime` validates source contiguity per execution and projects a monotonic conversation-facing sequence across successive logical tasks. This preserves the fail-closed ordering invariant already enforced by `StreamingConversationState` without changing the transport-neutral P04 runtime contract.

## Runtime availability

Production startup uses `FccEnvironmentDiscoveryService`. When `fcc-claude` is discovered, the structured runtime is composed as:

`ConversationSequencedAgentRuntime(AgentRuntimeSupervisor(FccStructuredAgentRuntime))`.

If the executable is unavailable, task start fails before a task identity is created and the Tasks surface exposes the discovery reason. Cloud fixtures do not pretend that the owner's installed FCC/provider environment is available.

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

The gate includes static contract checks, negative mutation fixtures, and an executable Windows/WPF + temporary-SQLite fixture. Controlled fixture runtimes cover successful consecutive tasks, durable assistant history, one-active-task rejection, classified failure, source-sequence corruption, runtime-unavailable rejection, and production surface construction.

All runtime events used by this gate are `SELF_TEST_ONLY` fixtures. They are not provider-backed `REAL_TARGET` evidence and do not satisfy `OWNER-P04-008-REAL-TARGET`.

## Explicit exclusions

This task does not close or implement:

- `FCCD-P05-006` — user Stop/cancel/retry UX;
- `FCCD-P05-007` — Markdown/code/diff rendering;
- `FCCD-P05-008` — measured conversation virtualization/performance closure;
- P04-008 real-target acceptance.
