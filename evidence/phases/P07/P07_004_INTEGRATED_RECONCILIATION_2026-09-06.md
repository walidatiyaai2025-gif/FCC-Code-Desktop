# FCCD-P07-004 Integrated Reconciliation — 2026-09-06

## Decision

`FCCD-P07-004 — Stage/unstage` is **CLOSED** as a cloud-actionable task. Its production implementation is normally integrated and exact-main verified. P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`; this task closure does not advance P08 or authorize P11 work.

## Production integration

The accepted implementation candidate is `5ea39d620def36a0855bf88fab67860ea9899c06` from PR #171 (`worker-b/fccd-p07-004-stage-unstage`). The task recovered the pre-existing stale zero-delta P07-004 branch instead of creating a duplicate claim, then added a dedicated Application-owned `IGitIndexService` boundary so write operations do not weaken the read-only `IGitService` contract.

The implementation performs only explicit Git index mutation over normalized literal repository-relative paths. Stage uses explicit `git add -- :(literal)<path>` pathspecs and exposes no add-all or wildcard operation. Unstage uses index-only `git restore --staged` when HEAD exists and cached-only `git rm --cached --force --ignore-unmatch` for unborn repositories, preserving work-tree files. Rename status selections expand to both current and original paths and return requested/effective path provenance so callers can preserve correlation across the unstage lifecycle. Requests are count/text bounded, traversal/rooted/`.git` metadata targeting is rejected, Git is non-interactive with UTF-8 streams, and timeout/cancellation cleans up only the owned process tree.

Exact PR-head gates on `5ea39d620def36a0855bf88fab67860ea9899c06` all completed SUCCESS:

- Windows CI run `34046933272` / run #397 — SUCCESS.
- P06-007 Workspace Search run `34046933243` / run #126 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34046933327` / run #110 — SUCCESS.

PR #171 was normally merged without squash/rebase as `106ca224d01b2398c5a3e799a1943213df57b667`, preserving tested implementation ancestry and canonical main ancestry.

Exact post-merge canonical-main gates on `106ca224d01b2398c5a3e799a1943213df57b667` all completed SUCCESS:

- Windows CI run `34047377699` / run #398 — SUCCESS.
- P06-007 Workspace Search run `34047377677` / run #127 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34047377708` / run #111 — SUCCESS.

No task-local product defect or exact-main regression remained after integration.

## Cloud repair and validation evidence

The real disposable-Git suite covers selective staging, preservation of unrelated owner changes, modified-file unstage, deletion stage/unstage without recreating the deleted work-tree file, rename-pair handling, unborn-repository unstage without deleting the work-tree file, Arabic/Unicode and space-containing paths, typed non-repository/bare/unavailable states, pathset safety limits, cancellation, and constructor timeout bounds.

CI exposed cloud-repairable defects and each was repaired rather than deferred. Analyzer `CA1859` first identified private helper return types and then private parameters backed exclusively by `List<string>`; the implementation narrowed those private-only signatures without changing the public contract or mutation semantics. The next real-Git run exposed a rename lifecycle fixture assumption: after index-only unstage, Git correctly represents the former rename as a deleted source plus untracked destination and no longer carries the rename correlation. The production result already preserves the expanded pair in `EffectivePaths`; the fixture was corrected to reuse that stable provenance when restaging rather than weakening rename atomicity. The final exact-head and exact-main gates above prove the repaired code and tests.

## Cloud evidence boundary

This evidence proves bounded explicit Git index stage/unstage and canonical integration provenance. It does not claim branch create/checkout, fetch/pull, commit/push, history, dirty/pre-existing-change provenance, destructive-operation safeguards, P07 phase closure, P08 authorization, Blender/P11 functionality, or release readiness.

## Owner-last classification

P07-004 introduces no new owner-only acceptance obligation. The canonical owner queue remains exactly:

- `OWNER-P04-008-REAL-TARGET` — QUEUED / release blocking.
- `OWNER-P05-EXIT-REAL-TARGET` — QUEUED / release blocking.

Their source task/gate states remain unresolved as already recorded; `P04=NOT_RUN`, `P05=NOT_RUN`, `KNOWN_RELEASE_BLOCKERS=2`, and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.

## Next legal action

After this reconciliation is normally integrated and the resulting exact canonical `main` remains green, rebuild the live P07 claim map. Recover any legitimate earlier regression/integration-pending work first; otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-005 — Branch create/checkout` if still unclaimed. P08 and later phases remain prohibited until P07 is truthfully closed under canonical governance.
