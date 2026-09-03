# P03-003 — Task / Agent / Tool / Process Event Journal

## Scope

`FCCD-P03-003` establishes durable task execution identity, lifecycle snapshots, and an append-only task event journal. It extends the P03 SQLite baseline through migration v3 without implementing queue coordination, runtime execution, tool invocation, process supervision, task transition policy, backup rotation, or startup recovery orchestration.

## Ownership

- `FCCCodeDesktop.Core.State` owns persistence-neutral records for tasks, agent runs, tool runs, process runs, and journal events.
- `FCCCodeDesktop.Application.Persistence.IExecutionJournalStore` is the application-owned persistence contract.
- `FCCCodeDesktop.Persistence.SqliteExecutionJournalStore` is the SQLite implementation.
- Later runtime, tool, queue, recovery, and UI layers consume the application contract rather than issuing SQL directly.

## Schema migration v3

Migration `create_tasks_execution_journal` adds:

- `Tasks`
  - stable task GUID,
  - owning session foreign key,
  - persisted state string and optional summary,
  - immutable creation timestamp and mutable update timestamp.
- `AgentRuns`
  - stable run GUID and owning task,
  - runtime-kind identity,
  - lifecycle state,
  - started/completed UTC timestamps.
- `ToolRuns`
  - stable run GUID and owning task,
  - optional correlated agent run,
  - tool kind and operation identity,
  - lifecycle state and timestamps.
- `ProcessRuns`
  - stable run GUID and owning task,
  - optional correlated agent/tool runs,
  - logical operation GUID,
  - executable, already-sanitized argument text, canonical working directory,
  - optional operating-system process ID,
  - lifecycle state, timestamps, and optional exit code.
- `TaskEvents`
  - stable event GUID,
  - owning task,
  - unique non-negative per-task sequence,
  - one of `TASK`, `AGENT`, `TOOL`, or `PROCESS`,
  - event type,
  - optional correlated agent/tool/process run IDs,
  - optional validated JSON metadata,
  - occurred UTC timestamp.

The schema remains explicit SQL under the ordered checksum-verified migration contract established by ADR-021.

## Correlation and integrity rules

Run tables expose task-scoped identity through unique `(Id, TaskId)` keys. Composite foreign keys require a correlated `AgentRun`, `ToolRun`, or `ProcessRun` to belong to the same task as the child record or event referencing it. A run from task A therefore cannot be attached accidentally to task B merely because its GUID exists.

`Tasks.SessionId` references the durable P03-002 session. Deleting a session cascades to its task execution state. Deleting a task cascades to its runs and events.

Optional correlation IDs are either null or non-empty GUIDs. Process IDs, when known, must be positive. Required state/type/identity strings cannot be blank.

## Write semantics

Task and run records are lifecycle snapshots keyed by stable identity. Their ownership and immutable identity fields are set by the first insert and are not replaced by later upserts. Subsequent writes update only lifecycle fields:

- task state, summary, and `UpdatedUtc`,
- agent/tool run state and `CompletedUtc`,
- process run OS process ID, state, `CompletedUtc`, and exit code.

Task events are append-only. Duplicate event IDs or duplicate `(TaskId, Sequence)` values are rejected rather than overwritten. Event insertion and monotonic advancement of the owning task's `UpdatedUtc` occur in one SQLite transaction, so a rejected event cannot partially advance task metadata.

Reads return events in deterministic sequence order. Persisted timestamps are normalized to UTC round-trip text. Repository connections enable foreign keys, use the configured busy timeout, and disable pooling so short-lived operations do not retain database handles after disposal.

## Event payload boundary

`DataJson` is optional structured metadata. When present it must contain syntactically valid JSON before persistence. It is metadata, not a giant raw-log storage surface; the architecture keeps large output file-backed with indexed metadata.

This store does not perform secret discovery or redaction. Producers must provide already-sanitized persisted content according to the later diagnostics/security boundary. In particular, `ArgumentsSanitized` is intentionally named as a contract: P03 stores the sanitized value supplied by the caller and does not claim that raw process arguments are safe to persist.

## Recovery foundation

P03-003 provides durable facts required by later recovery: task identity/state, run ownership, start/completion information, process identity, correlation IDs, and ordered events. It does not decide whether a persisted non-terminal task or run is alive, failed, retryable, recoverable, or terminal.

Startup reconciliation, crash/reboot fault injection, process reality checks, and duplicate-launch prevention remain owned by later recovery/runtime/process phases. `FCCD-P03-007` owns the phase-level migration/reopen/recovery closure suite for the complete P03 entity set.

## Verification boundaries

Integration coverage for this task verifies:

- task, agent-run, tool-run, process-run, and event state survives store recreation,
- Unicode/Arabic metadata and valid JSON persist,
- event ordering is deterministic,
- immutable run identity/start metadata survives lifecycle upserts,
- duplicate event sequence rolls back without partial task timestamp advancement,
- cross-task correlation is rejected by database foreign keys,
- malformed JSON and invalid process identity are rejected before persistence,
- the existing migration checksum/gap/rollback protections remain valid after baseline schema version 3.

## Explicit boundaries

This task does **not** implement:

- `FCCD-P03-004` queue persistence,
- `FCCD-P03-005` settings persistence,
- `FCCD-P03-006` integrity checks/backup rotation,
- `FCCD-P03-007` complete migration/recovery closure testing,
- P04 agent runtime execution or normalized runtime transport,
- P05 canonical task transition validation or conversation/task UX,
- P08 owned-process launching/cancellation/supervision,
- P09+ external-tool execution semantics,
- P14 global queue/cooldown coordination,
- P15 startup/crash/reboot recovery orchestration,
- P16 diagnostics sink redaction.

Later work may extend these entities only through new ordered migrations. Applied migration v3 SQL must never be rewritten after canonical integration.
