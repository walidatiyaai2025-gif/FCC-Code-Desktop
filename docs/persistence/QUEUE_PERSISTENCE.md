# Queue Persistence Contract

**Task:** `FCCD-P03-004 — Queue persistence`  
**Phase:** P03 — Persistence + canonical state model

## Purpose

Persist durable queue membership, ordering, and lifecycle state so queued task state survives application restart before the runtime coordinator is implemented.

This contract is deliberately persistence-only. It does **not** implement global dispatch, concurrency enforcement, the 15-second cooldown, rate-limit backoff, execution ownership, queue UI, or restart launch reconciliation. Those behaviors remain owned by their canonical later phases, especially P14.

## Durable model

`PersistedQueueItem` contains:

- `Id` — immutable queue-entry identity.
- `TaskId` — immutable owning durable task identity.
- `OrderKey` — non-negative persisted ordering key.
- `State` — non-empty durable queue lifecycle label.
- `EnqueuedUtc` — immutable original enqueue timestamp, normalized to UTC on disk.
- `UpdatedUtc` — mutable queue-state timestamp, normalized to UTC on disk and never earlier than `EnqueuedUtc`.

The persistence layer intentionally keeps lifecycle values as strings. P03 stores truthful state; later coordinator/state-machine phases own the closed set of queue execution transitions.

## SQLite schema

Migration v4 adds `QueueItems`:

```text
QueueItems
  Id           TEXT PRIMARY KEY
  TaskId       TEXT NOT NULL UNIQUE -> Tasks(Id) ON DELETE CASCADE
  OrderKey     INTEGER NOT NULL CHECK >= 0
  State        TEXT NOT NULL, non-empty
  EnqueuedUtc  TEXT NOT NULL
  UpdatedUtc   TEXT NOT NULL
```

The one-row-per-task uniqueness boundary prevents duplicate durable queue membership for the same task. Queue rows cannot outlive their owning task.

`IX_QueueItems_State_Order` indexes `(State, OrderKey, EnqueuedUtc, Id)` for deterministic ordered reads and later state-scoped coordinator access.

## Application contract

`IQueueStateStore` exposes:

- `UpsertQueueItemAsync`
- `GetQueueItemAsync`
- `GetQueueItemByTaskIdAsync`
- `ListQueueItemsAsync`

`SqliteQueueStateStore` uses short-lived, non-pooled SQLite connections, enables foreign keys on every connection, and applies the configured busy timeout consistently with the earlier P03 stores.

## Mutation invariants

An upsert may change only:

- `OrderKey`
- `State`
- `UpdatedUtc`

It may not change the queue entry's `TaskId` or original `EnqueuedUtc`. An attempted identity/timestamp rewrite fails instead of silently mutating history.

A second queue entry for the same durable task is rejected by the database uniqueness constraint. An entry referencing a missing task is rejected by the foreign key.

## Deterministic ordering

`ListQueueItemsAsync` orders by:

1. `OrderKey ASC`
2. `EnqueuedUtc ASC`
3. `Id ASC`

Duplicate order keys are permitted so later queue-reorder logic can persist intermediate or grouped order values without requiring P03 to own dispatch semantics. The timestamp and ID tie-breakers keep reads deterministic.

## Validation coverage

Integration tests verify:

- queue entries survive store recreation;
- deterministic order survives persistence, including duplicate order keys;
- lookup by queue-item ID and owning task ID;
- mutable order/state updates while identity/enqueue time remain immutable;
- attempted identity/enqueue-time rewrites are rejected without changing the stored row;
- duplicate queue membership for one task is rejected;
- orphan task references are rejected;
- negative order keys, blank state, and backwards timestamps are rejected before persistence;
- SQLite bootstrap advances to schema version 4 and migration rollback/checksum/gap protections continue from the new baseline.

## Phase boundary

P03-004 establishes only durable queue state. It does not claim:

- `GLOBAL_AGENT_CONCURRENCY = 1` runtime enforcement;
- inter-run cooldown enforcement;
- queue cancellation/reorder UX;
- provider rate-limit classification or retry/backoff;
- duplicate-launch prevention after restart;
- runtime/task execution orchestration.

Those remain mandatory downstream work and must not be inferred as complete from this persistence contract.
