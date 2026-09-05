# P06-001 Integrated Reconciliation — 2026-09-05

## Task

- Task: `FCCD-P06-001 — Add/open/recent project workflows`.
- Reconciliation classification: cloud integration evidence only.
- Canonical task closure is justified by production integration plus exact-head and exact-post-merge Windows CI; no provider-backed, manual, target-machine, or owner-only evidence is claimed here.

## Live-state recovery

The scheduling hint for this worker was `FCCD-P07-007 — Commit/push`, but live canonical state remained `CURRENT_PHASE=P06`. Existing PR #142 contained legitimate integration-pending P06-001 work and was the only open PR, so recovery correctly continued that unit instead of starting future P07 work or duplicating a P06 task.

- Canonical main when the recovered P06-001 implementation unit began: `b2909ed049e0d2bdd9069b24550c012ed26da53f`.
- Recovered branch: `worker/p06-001-project-workflows`.
- Final exact implementation candidate: `2b51797dcf2b6ac674c116bfeb1cc33497f8b878`.
- Implementation PR: `#142 — P06-001: add persistent project open/recent workflows`.

## Production implementation

P06-001 integrates the first production project-workspace workflow over the existing durable project schema:

- `ProjectCatalogService` owns add/open/recent-project orchestration in the Application layer;
- `IProjectCatalogStore` and `IProjectDirectoryProbe` isolate persistence and operating-system path behavior;
- `SqliteProjectCatalogStore` reuses the existing `Projects` table and case-insensitive root-path identity without introducing a schema migration;
- opening a new root persists project metadata, while reopening the same root preserves project identity and creation time and refreshes recency;
- recent projects are returned deterministically by most-recent update time with a bounded query;
- `SystemProjectDirectoryProbe` validates and normalizes existing directories without enumerating or mutating source content;
- Git and non-Git folders are both valid project roots;
- `ProjectWorkspaceState` owns presentation state, recent-project refresh, busy/error state, and activation of the existing durable session workspace;
- `ProjectWorkspaceSurface` provides the Windows folder picker, recent-project list, open/refresh interactions, loading/disabled state, and inline failure presentation;
- the production Projects navigation surface is mounted only after durable workspace initialization succeeds, preserving the established startup empty-state contract.

This task does not implement technology detection, the lazy file explorer, safe file mutation, the bundled editor, editor tabs/save/reload/dirty state, workspace search, large-tree safeguards, or any P07 Git operation.

## Permanent validation

P06-001 adds `tools/projects/validate-project-workflows.ps1` and a dedicated `Validate P06-001 project workflows` Windows CI step. Deterministic coverage includes:

- persistence and store recreation;
- reopen identity and `CreatedUtc` preservation;
- deterministic recent-project ordering and limits;
- paths containing spaces and non-ASCII/Arabic text;
- Git and non-Git roots;
- source sentinel non-mutation;
- missing-directory rejection before persistence;
- production project-workspace wiring and negative/static fixtures.

## Cloud defects found and repaired

All cloud-actionable defects exposed during integration were repaired before merge; none was deferred to the owner.

1. An unnecessary new integration-test project reference made the committed dependency lock inconsistent and caused locked restore `NU1004`. The test design was changed to use a test-local directory probe so the existing locked project graph remained intact; locked restore was not bypassed or weakened.
2. xUnit analyzer `xUnit2014` rejected synchronous exception assertions around Task-returning APIs. The tests were corrected to use asynchronous exception assertions.
3. A P05 session-workspace validator required the established direct `SessionWorkspaceState(new SqliteConversationStateStore(options))` composition. P06 had introduced only an unnecessary local variable, so production composition was restored to the established form instead of weakening the earlier validator.
4. Exact-head CI then exposed a common-state runtime regression: mounting the Projects surface during constructor-time shell composition removed the established startup project empty-state before persistence initialization. Production wiring was corrected so the real Projects surface is mounted only after durable initialization succeeds. The P02 common-state contract remained unchanged.

## Verified integration provenance

- Final exact PR head: `2b51797dcf2b6ac674c116bfeb1cc33497f8b878`.
- Exact-head Windows CI: run `33991050636` / run #250 — **SUCCESS**.
- Run #250 passed the CI contract, the complete Windows Release baseline, all inherited dedicated P05 regression gates, and the dedicated P06-001 project-workflow gate.
- The Release baseline included locked restore, format verification, Release build, complete unit/integration tests, build/dependency/quality/test-infrastructure policy, owner-last governance, FCC runtime contract validation, and all permanent UI/runtime gates.
- Merge method: normal merge; tested ancestry preserved; no squash/rebase/force-push.
- Normal implementation merge commit: `dc08e0cb9eb98bd4eb8d8290b1d69fef1402697a`.
- Exact post-merge canonical-main Windows CI: run `33991429277` / run #251 — **SUCCESS** on exact merge SHA `dc08e0cb9eb98bd4eb8d8290b1d69fef1402697a`.
- Run #251 again passed the full Windows Release baseline, P05-005/P05-006/P05-007/P05-008 regression gates, and the dedicated P06-001 project-workflow gate.

## Owner-last boundary

P06-001 requires no new owner/manual/REAL_TARGET evidence. The canonical final-owner queue remains unchanged with exactly the existing release-blocking obligations:

- `OWNER-P04-008-REAL-TARGET` — `QUEUED`;
- `OWNER-P05-EXIT-REAL-TARGET` — `QUEUED`.

This reconciliation does **not** claim P04 closure, P05 exit-gate PASS, P06 phase closure, release eligibility, or `VERIFIED_FINAL_COMPLETE`. `P04=NOT_RUN` and `P05=NOT_RUN` remain deferred exactly as recorded by the owner-last governance.

## Reconciliation result

`FCCD-P06-001` is eligible for canonical `CLOSED`: its production implementation is normally merged, its final candidate passed exact-head Windows CI, the exact implementation merge SHA passed exact-main Windows CI, and no unresolved P06-001-local cloud defect or owner-only requirement remains.

After this reconciliation is integrated and exact resulting `main` remains green, the next legal cloud action is to re-run the live claim map and, if no higher-priority recovery exists, select `FCCD-P06-002 — Project technology/tool detection framework`. P07 remains future work until P06 cloud implementation is complete and governance truthfully advances the current cloud phase.