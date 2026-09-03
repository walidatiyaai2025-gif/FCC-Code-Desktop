# Migration and recovery test matrix

`FCCD-P03-007` closes the P03 persistence phase's cross-cutting migration/recovery verification boundary. It does not add a new production persistence subsystem; it verifies that the P03-001 through P03-006 contracts compose safely across reopen, migration failure, schema history, corruption detection, and verified backups.

## Required P03 recovery behavior

The P03 exit contract requires persisted state to remain truthful and usable across:

1. create and persist,
2. disposal/close of the original store objects,
3. reopen through newly-created store instances,
4. migration from an older supported schema ledger,
5. failed migration rollback and corrected retry,
6. migration-ledger corruption/newer-version rejection,
7. primary-database corruption detection,
8. verified backup preservation and independent readability.

The test suite covers the complete P03 entity set:

- projects,
- sessions,
- messages,
- tasks,
- agent runs,
- tool runs,
- process runs,
- task events,
- queue items,
- global settings,
- project/workspace settings,
- migration ledger,
- database integrity reports,
- verified backup artifacts.

## Cross-cutting scenarios

`SqliteMigrationRecoveryTests` provides the phase-level composition tests.

### Complete state reopen plus backup recovery boundary

A database is seeded through every P03 store, then all state is read through newly-created store instances. A verified online backup is created and the same complete state is read from the backup through fresh store instances. The primary file is then deliberately corrupted in the disposable fixture; integrity checking must report it unhealthy while the previously verified backup remains healthy and readable.

This proves the P03 backup is a coherent recovery input. It does **not** implement automatic replacement/restoration of the primary database; startup/crash/reboot restoration orchestration remains P15.

### Historical schema upgrade

A current disposable fixture is reduced to the canonical version-2 schema state by removing only later P03 tables and migration-ledger rows while preserving version-2 project/session/message data and the original migration checksums. Reinitialization must apply versions 3, 4, and 5 in order without losing the historical data. Newly introduced journal, queue, settings, and integrity capabilities must then be usable.

### Failed migration rollback and retry

A synthetic version-6 migration deliberately fails after attempting DDL. The transaction must roll back: no partial table and no migration-ledger row may remain, and the complete pre-existing P03 state must still be readable. Replacing the failed migration with a corrected migration of the same version/name must then apply successfully while preserving all earlier state.

### Ledger corruption / unsupported future schema

The initializer must refuse a migration ledger with a missing applied version before the highest applied version. It must also refuse a database whose ledger reports a schema version newer than the application supports. In both cases the refusal must not destroy existing domain state.

## Existing task-local tests retained

P03-007 composes rather than replaces the earlier task suites. The permanent Windows CI baseline continues to run:

- `SqliteDatabaseInitializerTests` for bootstrap, idempotency, checksum drift, rollback/retry, and migration-plan gaps;
- `SqliteConversationStateStoreTests`;
- `SqliteExecutionJournalStoreTests`;
- `SqliteQueueStateStoreTests`;
- `SqliteSettingsStoreTests`;
- `SqliteDatabaseMaintenanceServiceTests` for integrity, online backup verification, retention, and corrupt-source refusal.

## Phase boundary

P03-007 does not implement:

- P15 startup reconciliation,
- automatic selection/restoration of a backup after application crash or reboot,
- interrupted file/Git/runtime/Unity/Blender operation recovery,
- installer upgrade/rollback behavior,
- user-facing recovery UX.

Those later phases consume the persistence guarantees verified here.
