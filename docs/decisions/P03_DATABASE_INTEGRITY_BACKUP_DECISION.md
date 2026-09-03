# P03 database integrity and backup decision

**Status:** Accepted  
**Date:** 2026-09-04  
**Task:** `FCCD-P03-006`

## Decision

FCC Code Desktop uses SQLite's online backup API for database snapshots rather than raw file copying. Every backup must pass full `PRAGMA integrity_check` before publication. The source database must also pass integrity verification before backup creation begins.

Backups are first written to an unrecognized temporary file, verified, then atomically published within the configured backup directory. Retention runs only after verified publication. Existing verified backups are therefore not rotated away when the source is corrupt or a new backup cannot be verified.

Managed backup retention defaults to five files and is configurable from 1 through 100. Rotation recognizes only the product's timestamped/GUID backup filename contract for the configured source database; unrelated files are never deleted by this policy.

## Rationale

Raw copying of an active SQLite file can produce an inconsistent snapshot and would make backup success depend on timing. SQLite's backup API provides a database-aware consistent copy. Integrity-before-publish prevents corrupt snapshots from being promoted to trusted recovery material, and rotate-after-publish preserves a known-good recovery point when new backup creation fails.

## Boundary

P03-006 provides maintenance primitives only. P03-007 owns complete migration/recovery test closure for P03, while P15 owns startup/crash/reboot recovery orchestration and automatic restoration behavior.

See `docs/persistence/DATABASE_INTEGRITY_AND_BACKUPS.md`.
