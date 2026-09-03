# P03-002 — Project / Session / Message Persistence

## Scope

`FCCD-P03-002` establishes the durable project/session/message state owned by P03. It extends the P03-001 SQLite migration baseline without implementing task/event journaling, queue state, settings, backup rotation, or later runtime behavior.

## Ownership

- `FCCCodeDesktop.Core.State` owns persistence-neutral records for projects, sessions, and messages.
- `FCCCodeDesktop.Application.Persistence.IConversationStateStore` is the application-owned contract.
- `FCCCodeDesktop.Persistence.SqliteConversationStateStore` is the SQLite implementation.
- UI and later runtime adapters must consume the application contract rather than issuing SQL directly.

## Schema migration v2

Migration `create_projects_sessions_messages` adds:

- `Projects`
  - stable GUID identity stored as canonical text,
  - canonical full root path,
  - display name,
  - created/updated UTC timestamps,
  - case-insensitive unique root-path index for Windows path identity.
- `Sessions`
  - stable GUID identity,
  - owning project foreign key with cascade delete,
  - optional externally observed/runtime session ID,
  - title,
  - created/updated UTC timestamps,
  - project/update index for recent-session queries.
- `Messages`
  - stable GUID identity,
  - owning session foreign key with cascade delete,
  - non-negative per-session sequence,
  - role/content,
  - created UTC timestamp,
  - unique `(SessionId, Sequence)` ordering constraint.

The schema remains explicit SQL under the ordered checksum-verified migration contract established by ADR-021.

## Write semantics

Project and session writes are idempotent upserts by stable identity. Their original `CreatedUtc` value is immutable after first insert; mutable fields and `UpdatedUtc` may advance.

Messages are append-only. A duplicate message ID or duplicate sequence in one session is rejected rather than overwritten. Message insertion and the owning session's monotonic `UpdatedUtc` advance occur in one SQLite transaction.

All persisted timestamps are normalized to UTC round-trip text. Repository connections enable SQLite foreign keys and use the configured busy timeout. Pooling is disabled so short-lived repository operations do not retain database file handles after disposal.

## Recovery and data-integrity behavior

- An orphan session is rejected by the project foreign key.
- An orphan message is rejected by the session foreign key.
- Duplicate per-session message sequence is rejected without partially advancing session metadata.
- A second project cannot claim the same Windows root path under case-insensitive path identity.
- Reads after constructing a new store instance prove state is durable and not in-memory state.

## Explicit boundaries

This task does **not** implement:

- `FCCD-P03-003` task/agent/tool/process event journal,
- `FCCD-P03-004` queue persistence,
- `FCCD-P03-005` settings persistence,
- `FCCD-P03-006` integrity checks/backup rotation,
- `FCCD-P03-007` migration/recovery closure suite,
- P04 FCC runtime/session orchestration,
- P05 conversation UI behavior.

Later phases may add fields only through new ordered migrations. Applied migration SQL must never be rewritten after canonical integration.
