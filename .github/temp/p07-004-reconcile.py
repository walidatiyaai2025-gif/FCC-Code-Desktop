from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one guarded match, found {count}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8")


replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P07-004` — Stage/unstage — PENDING.",
    "- `FCCD-P07-004` — Stage/unstage — CLOSED.",
)

current_anchor = """- Evidence class remains cloud/self-test for bounded read-only Git diff review plus canonical integration provenance; no stage/unstage, branch/fetch/pull, commit/push, P07 phase closure, P08 authorization, owner-only evidence, release eligibility, P11 implementation, or `VERIFIED_FINAL_COMPLETE` is implied.
"""
current_insert = current_anchor + """
## P07-004 integration provenance

- Exact implementation candidate: `5ea39d620def36a0855bf88fab67860ea9899c06` from PR #171 (`worker-b/fccd-p07-004-stage-unstage`).
- PR #171 exact-head Windows CI: run `34046933272` / run #397 — SUCCESS.
- PR #171 exact-head P06-007 Workspace Search: run `34046933243` / run #126 — SUCCESS.
- PR #171 exact-head P06-008 Large Workspace Safeguards: run `34046933327` / run #110 — SUCCESS.
- Normal merge commit: `106ca224d01b2398c5a3e799a1943213df57b667`.
- Exact post-merge canonical-main Windows CI: run `34047377699` / run #398 — SUCCESS.
- Exact post-merge P06-007 Workspace Search: run `34047377677` / run #127 — SUCCESS.
- Exact post-merge P06-008 Large Workspace Safeguards: run `34047377708` / run #111 — SUCCESS.
- Integrated evidence: `evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`.
- Evidence class remains cloud/self-test for bounded explicit Git index stage/unstage plus canonical integration provenance; no branch create/checkout, fetch/pull, commit/push, history, dirty provenance, destructive-operation safeguards, P07 phase closure, P08/P11 authorization, owner-only evidence, release eligibility, or `VERIFIED_FINAL_COMPLETE` is implied.
"""
replace_once("CURRENT_PHASE.md", current_anchor, current_insert)

project_old = """P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection`, `FCCD-P07-002 — Status/changed-files surface`, and `FCCD-P07-003 — Diff viewer` are CLOSED after exact PR-head validation, normal merge integration, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-004` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_003_INTEGRATED_RECONCILIATION_2026-09-06.md`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes.
"""
project_new = """P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection`, `FCCD-P07-002 — Status/changed-files surface`, `FCCD-P07-003 — Diff viewer`, and `FCCD-P07-004 — Stage/unstage` are CLOSED after exact PR-head validation, normal merge integration, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-005` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_003_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes.
"""
replace_once("PROJECT_CONTROL.md", project_old, project_new)

replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P07-004 | Stage/unstage | PENDING |",
    "| FCCD-P07-004 | Stage/unstage | CLOSED |",
)

ledger_anchor = """`FCCD-P07-003` is CLOSED from the bounded read-only Git diff viewer integrated in PR #169. Exact implementation candidate `4f046aa1f39a3107d9e74ff1d889d66b0f881e42` passed Windows CI `34042982547` / #384, P06-007 Workspace Search `34042982551` / #113, and P06-008 Large Workspace Safeguards `34042982600` / #97. PR #169 was normally merged as `c4a743352d0858fce7ecaafbb8bcf2ffe4756d9b`; that exact canonical main passed Windows CI `34043423766` / #385, P06-007 Workspace Search `34043423776` / #114, and P06-008 Large Workspace Safeguards `34043423769` / #98. Coverage includes staged/index versus work-tree separation, literal repository-relative pathspecs, explicit UTF-8 handling for Arabic/Unicode/space-containing paths, read-only untracked additions including empty files, binary classification, bounded `TooLarge` handling, unsafe-path rejection, cancellation/owned-process cleanup, and index non-mutation. Task evidence: `evidence/phases/P07/P07_003_INTEGRATED_RECONCILIATION_2026-09-06.md`. No stage/unstage or later-P07 mutation, P07 phase closure, P08/P11 authorization, owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE` is claimed; P07 remains `IN_PROGRESS`, P07-004 through P07-011 remain PENDING, P08+ remain prohibited, and the two existing owner-last queue blockers remain unchanged.
"""
ledger_insert = ledger_anchor + """
`FCCD-P07-004` is CLOSED from the bounded explicit Git index stage/unstage implementation integrated in PR #171. Exact implementation candidate `5ea39d620def36a0855bf88fab67860ea9899c06` passed Windows CI `34046933272` / #397, P06-007 Workspace Search `34046933243` / #126, and P06-008 Large Workspace Safeguards `34046933327` / #110. PR #171 was normally merged as `106ca224d01b2398c5a3e799a1943213df57b667`; that exact canonical main passed Windows CI `34047377699` / #398, P06-007 Workspace Search `34047377677` / #127, and P06-008 Large Workspace Safeguards `34047377708` / #111. Coverage includes selective literal-path staging, index-only unstage with existing HEAD, unborn-repository cached-only unstage, rename effective-path provenance, deletion handling without work-tree recreation, preservation of unrelated owner changes and work-tree bytes, repository-relative/path-metadata safety bounds, Unicode/Arabic/space-containing paths, typed repository failures, non-interactive execution, timeout/cancellation, and owned-process-tree cleanup. Analyzer `CA1859` findings and a rename lifecycle fixture defect were repaired rather than deferred. Task evidence: `evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`. No branch create/checkout, fetch/pull, commit/push, history, dirty provenance, destructive-operation safeguards, P07 phase closure, P08/P11 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE` is claimed; P07 remains `IN_PROGRESS`, P07-005 through P07-011 remain PENDING, P08+ remain prohibited, and the two existing owner-last queue blockers remain unchanged.
"""
replace_once("docs/TASK_LEDGER.md", ledger_anchor, ledger_insert)

evidence = Path("evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md")
if evidence.exists():
    raise SystemExit(f"{evidence}: already exists")
evidence.write_text("""# FCCD-P07-004 Integrated Reconciliation — 2026-09-06

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
""", encoding="utf-8")

Path(".github/workflows/temp-p07-004-reconciliation.yml").unlink()
Path(".github/temp/p07-004-reconcile.py").unlink()
