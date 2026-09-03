# P03-005 — Settings Persistence Contract

## Scope

`FCCD-P03-005` establishes durable, non-secret application and project/workspace settings on the existing P03 SQLite persistence boundary.

This task owns storage mechanics only. It does not implement later permission-policy semantics, FCC/runtime configuration behavior, queue coordination, diagnostics metadata, or UI settings screens.

## Persistence model

Schema migration v5 adds two explicit scopes:

- `GlobalSettings` — application-wide preferences.
- `ProjectSettings` — preferences owned by one durable `Projects` row.

Both scopes store:

- a non-empty setting key,
- a syntactically valid JSON value,
- the UTC timestamp of the latest write.

Setting keys use SQLite `NOCASE` identity. A consumer may therefore read or update `Appearance.Theme` using a differently cased key without creating duplicate rows. Listing is deterministic by case-insensitive key ordering.

Project settings use `(ProjectId, Key)` as their durable identity. The foreign key points to `Projects(Id)` with `ON DELETE CASCADE`, so deleting a project removes its workspace settings without affecting global settings or other projects.

## Application contract

`ISettingsStore` is owned by the Application layer and exposes separate operations for global and project settings:

- upsert,
- get by key,
- deterministic list,
- delete.

`SqliteSettingsStore` implements the contract using non-pooled short-lived `Microsoft.Data.Sqlite` connections, foreign-key enforcement, and the same bounded busy timeout used by the earlier P03 stores.

The persisted record is `PersistedSetting(Key, ValueJson, UpdatedUtc)`. JSON is intentionally schema-neutral at this persistence boundary so later typed feature settings can evolve without coupling Core/Application contracts to SQLite columns. Typed consumers remain responsible for their own semantic validation and defaults.

## Non-secret boundary

These tables are **not credential storage**.

The product specification requires product-owned secrets, if ever needed, to use Windows-protected storage rather than plaintext SQLite. API keys, bearer tokens, provider credentials, authorization headers, passwords, FCC secrets, or equivalent protected values must not be written through `ISettingsStore` merely because JSON can represent them.

P03-005 does not introduce a secret store and does not take ownership of FCC/provider configuration.

## Intended examples

Appropriate settings include non-secret preferences such as:

- appearance/theme identity,
- persisted panel/layout sizes,
- editor presentation preferences,
- workspace-specific non-secret behavior preferences,
- future typed settings whose canonical owning phase has been reached.

The presence of a generic storage seam is not authorization to implement later-phase product semantics early.

## Validation and integrity

The persistence layer rejects:

- blank setting keys,
- malformed JSON values,
- empty project identifiers,
- project settings referencing a project that does not exist.

SQLite also enforces non-empty keys, JSON validity, case-insensitive key uniqueness, project ownership, and cascade cleanup.

Integration coverage verifies:

- settings survive store recreation,
- Unicode/Arabic JSON survives persistence,
- case-insensitive lookup and upsert behavior,
- deterministic listing,
- isolation between global and per-project scopes,
- isolation between different projects,
- delete behavior,
- project-delete cascade behavior,
- rejection of malformed values and orphan project settings,
- migration baseline advancement from schema v4 to v5 without weakening checksum/gap/rollback protections.

## Explicitly deferred

P03-005 does not implement:

- permissions/profile semantics (P13),
- global queue concurrency/cooldown/rate-limit behavior (P14),
- diagnostics metadata persistence/diagnostics center (P16),
- settings UI or feature-specific UI workflows,
- protected secret storage,
- P03 database backup/corruption recovery (P03-006/P03-007).
