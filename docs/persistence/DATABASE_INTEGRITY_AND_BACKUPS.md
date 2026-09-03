# Database integrity and backup rotation

`FCCD-P03-006` establishes the persistence-layer database maintenance contract used by FCC Code Desktop before later crash/reboot recovery orchestration is introduced.

## Scope

The application-owned `IDatabaseMaintenanceService` exposes:

- full SQLite integrity verification,
- creation of a verified consistent database backup,
- deterministic managed-backup inventory,
- bounded backup retention.

`SqliteDatabaseMaintenanceService` is the SQLite implementation.

## Integrity contract

Integrity verification executes SQLite `PRAGMA integrity_check` against a non-pooled read-only connection.

A database is healthy only when SQLite returns exactly one `ok` result. Missing databases, malformed/corrupt databases, or SQLite errors are reported as unhealthy rather than being silently treated as usable.

Backup creation refuses to proceed when the source database is unhealthy.

## Backup publication contract

Backup creation uses SQLite's online backup API instead of copying a potentially active database file byte-for-byte.

The publication sequence is:

1. verify source integrity,
2. create the backup into a unique temporary file,
3. run full integrity verification against that temporary backup,
4. publish the verified file with an atomic same-directory move,
5. rotate older managed backups only after verified publication succeeds.

An incomplete or unverified temporary file never participates in backup inventory or retention.

If source integrity fails, no new backup is published and existing verified backups are preserved.

## Rotation contract

`SqliteBackupOptions` defaults to five retained managed backups and permits an explicit range of 1 through 100.

Managed backup filenames include:

- the source database filename,
- a UTC timestamp with seven fractional-second digits,
- a unique GUID,
- the `.backup` suffix.

Inventory ordering is newest UTC timestamp first with path ordering as a deterministic tie-breaker. Rotation deletes only recognized managed backup filenames for the configured source database. Unrelated or malformed files in the backup directory are ignored.

The default backup directory is a `backups` directory beside the SQLite database. Callers may provide an explicit backup directory.

## Security and data-safety boundary

P03-006 does not persist credentials or secrets and does not place secret material into backup metadata. Backups contain the same local application database content as the source database and therefore inherit the product's local-data protection requirements.

No destructive source-database repair is attempted automatically. Integrity failure is surfaced truthfully and the last verified backup is preserved.

## Phase boundary

This task does **not** implement:

- P03-007 migration/recovery closure tests across the complete P03 entity set,
- P15 startup reconciliation or automatic crash/reboot restoration,
- P15 interrupted-operation recovery,
- release-time installer upgrade/rollback behavior,
- diagnostics UI or user-facing recovery UX.

Those later tasks consume this maintenance foundation rather than being implemented early in P03-006.
