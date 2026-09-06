from pathlib import Path

implementation_head = "f45018c57fb5474730f4007a55bd9999429eaa4e"
implementation_initial = "897fbe79f3844d452ac2a0c1f93a29c3dc575bf7"
implementation_merge = "238bc26e7e6aa96b4cd504fca17ba882d42db35f"
evidence_path = "evidence/phases/P07/P07_005_INTEGRATED_RECONCILIATION_2026-09-06.md"

current = Path("CURRENT_PHASE.md")
text = current.read_text(encoding="utf-8")
old = "- `FCCD-P07-005` — Branch create/checkout — PENDING."
new = "- `FCCD-P07-005` — Branch create/checkout — CLOSED."
if text.count(old) != 1:
    raise SystemExit(f"CURRENT_PHASE expected exactly one P07-005 PENDING row, found {text.count(old)}")
text = text.replace(old, new, 1)
marker = "## P07 cloud activation provenance"
if text.count(marker) != 1:
    raise SystemExit("CURRENT_PHASE P07 activation marker mismatch")
provenance = f"""## P07-005 integration provenance

- Exact accepted implementation candidate: `{implementation_head}` from PR #173 (`worker-b/fccd-p07-005-branch-create-checkout`).
- Initial implementation commit `{implementation_initial}` exposed a Windows fixture-only Unicode console-decoding defect; the production adapter had explicit UTF-8 streams and was unchanged by the repair. Final candidate `{implementation_head}` made the disposable-Git Unicode branch assertion encoding-stable by reading `.git/HEAD` as UTF-8.
- PR #173 exact-head Windows CI: run `34050245282` / run #402 — SUCCESS.
- PR #173 exact-head P06-007 Workspace Search: run `34050245383` / run #131 — SUCCESS.
- PR #173 exact-head P06-008 Large Workspace Safeguards: run `34050245390` / run #115 — SUCCESS.
- Normal merge commit: `{implementation_merge}`.
- Exact post-merge canonical-main Windows CI: run `34050681680` / run #403 — SUCCESS.
- Exact post-merge P06-007 Workspace Search: run `34050681720` / run #132 — SUCCESS.
- Exact post-merge P06-008 Large Workspace Safeguards: run `34050681691` / run #116 — SUCCESS.
- Integrated evidence: `{evidence_path}`.
- Evidence class remains cloud/self-test for bounded safe local Git branch create/checkout plus canonical integration provenance; no fetch/pull, commit/push, history, destructive Git operation, P07 phase closure, P08/P11 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE` is implied.

"""
text = text.replace(marker, provenance + marker, 1)
current.write_text(text, encoding="utf-8", newline="\n")

ledger = Path("docs/TASK_LEDGER.md")
text = ledger.read_text(encoding="utf-8")
old = "| FCCD-P07-005 | Branch create/checkout | PENDING |"
new = "| FCCD-P07-005 | Branch create/checkout | CLOSED |"
if text.count(old) != 1:
    raise SystemExit(f"TASK_LEDGER expected exactly one P07-005 PENDING row, found {text.count(old)}")
text = text.replace(old, new, 1)
p08 = "## P08 — Terminal/process supervision"
if text.count(p08) != 1:
    raise SystemExit("TASK_LEDGER P08 marker mismatch")
task_note = f"""`FCCD-P07-005` is CLOSED from the production bounded local branch create/checkout implementation integrated in PR #173. Final exact candidate `{implementation_head}` passed Windows CI `34050245282` / #402, P06-007 Workspace Search `34050245383` / #131, and P06-008 Large Workspace Safeguards `34050245390` / #115. PR #173 was normally merged as `{implementation_merge}`; that exact canonical main passed Windows CI `34050681680` / #403, P06-007 Workspace Search `34050681720` / #132, and P06-008 Large Workspace Safeguards `34050681691` / #116. Coverage includes Application-owned `IGitBranchService`, bounded local `git switch --create` / `git switch`, `git check-ref-format --branch`, typed invalid/missing/existing/blocked/repository/unavailable outcomes, non-interactive UTF-8 process execution, timeout/cancellation with owned-process cleanup, Unicode/Arabic branch names, safe dirty-tree carryover, and conflicting dirty-tree refusal preserving the current branch and owner bytes. CI exposed a fixture-only Windows console-decoding defect on the initial `{implementation_initial}` candidate; it was repaired in `{implementation_head}` without changing production semantics. Task evidence: `{evidence_path}`. No fetch/pull, commit/push, history, later-phase work, new owner-only obligation, P07 phase closure, release eligibility, or `VERIFIED_FINAL_COMPLETE` is claimed; P07 remains `IN_PROGRESS`, P07-006 through P07-011 remain PENDING, and P08+ remain prohibited.

"""
text = text.replace(p08, task_note + p08, 1)
current_action = "## Current next action"
idx = text.find(current_action)
if idx < 0:
    raise SystemExit("TASK_LEDGER current action marker missing")
replacement = f"""## Current next action

`CURRENT_PHASE = P07` and P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`. `FCCD-P07-001` through `FCCD-P07-005` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable reconciliation evidence. `FCCD-P07-006` through `FCCD-P07-011` remain PENDING.

P04 remains acceptance-unresolved through `FCCD-P04-008` and its one-to-one queued `OWNER-P04-008-REAL-TARGET` obligation. P05 cloud implementation remains integrated, but its standalone exit observation remains queued as `OWNER-P05-EXIT-REAL-TARGET`. Their phase gates remain `P04=NOT_RUN;P05=NOT_RUN`; owner-last scheduling permits P07 cloud implementation but does not close either deferred acceptance requirement or permit release.

After this P07-005 reconciliation is integrated and exact resulting `main` remains green, re-run the Worker Protocol claim map. Recover/integrate any newly surfaced higher-priority legitimate defect first. Otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-006 — Fetch/pull` if still unclaimed and dependency-valid. Do not advance to P08 until every mandatory P07 task is CLOSED and the P07 phase exit gate is truthfully resolved under canonical governance. Only a genuinely owner-environment-bound residual may be queued under owner-last; do not fabricate target/manual evidence.

P06 is canonically CLOSED with `PHASE_EXIT_GATE=PASS`; closure evidence remains `evidence/phases/P06/CLOSURE.md`.
"""
text = text[:idx] + replacement
ledger.write_text(text, encoding="utf-8", newline="\n")

control = Path("PROJECT_CONTROL.md")
text = control.read_text(encoding="utf-8")
old = "P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection`, `FCCD-P07-002 — Status/changed-files surface`, `FCCD-P07-003 — Diff viewer`, and `FCCD-P07-004 — Stage/unstage` are CLOSED after exact PR-head validation, normal merge integration, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-005` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_003_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes."
new = f"P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection`, `FCCD-P07-002 — Status/changed-files surface`, `FCCD-P07-003 — Diff viewer`, `FCCD-P07-004 — Stage/unstage`, and `FCCD-P07-005 — Branch create/checkout` are CLOSED after exact PR-head validation, normal merge integration, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-006` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_003_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `{evidence_path}`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes."
if text.count(old) != 1:
    raise SystemExit(f"PROJECT_CONTROL expected canonical P07 summary paragraph once, found {text.count(old)}")
control.write_text(text.replace(old, new, 1), encoding="utf-8", newline="\n")

evidence = Path(evidence_path)
evidence.parent.mkdir(parents=True, exist_ok=True)
evidence.write_text(f"""# FCCD-P07-005 Integrated Reconciliation — 2026-09-06

## Decision

`FCCD-P07-005 — Branch create/checkout` is **CLOSED** as a cloud-actionable task. Its production implementation is normally integrated and exact-main verified. P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`; this task closure does not advance P08 or authorize P11 work.

## Production integration

The accepted implementation candidate is `{implementation_head}` from PR #173 (`worker-b/fccd-p07-005-branch-create-checkout`). The task recovered the pre-existing stale zero-delta P07-005 branch rather than creating a duplicate implementation claim, then added an Application-owned `IGitBranchService` mutation boundary so branch writes do not weaken the read-only `IGitService` contract.

The implementation validates bounded branch names with `git check-ref-format --branch`, creates/switches local branches only through `git switch --create` and `git switch`, and never supplies force/discard/reset/clean semantics or any remote/network command. Typed results distinguish success, non-repository, bare repository, Git unavailable, invalid name, missing branch, existing branch, checkout blocked, and query failure. Git execution uses `ProcessStartInfo.ArgumentList`, explicit UTF-8 output decoding, non-interactive environment settings, bounded timeout/cancellation, and cleanup of only the owned process tree.

Exact PR-head gates on `{implementation_head}` all completed SUCCESS:

- Windows CI run `34050245282` / run #402 — SUCCESS.
- P06-007 Workspace Search run `34050245383` / run #131 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34050245390` / run #115 — SUCCESS.

PR #173 was normally merged without squash/rebase as `{implementation_merge}`, preserving tested implementation ancestry and canonical main ancestry.

Exact post-merge canonical-main gates on `{implementation_merge}` all completed SUCCESS:

- Windows CI run `34050681680` / run #403 — SUCCESS.
- P06-007 Workspace Search run `34050681720` / run #132 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34050681691` / run #116 — SUCCESS.

No task-local product defect or exact-main regression remained after integration.

## Cloud repair and validation evidence

Real disposable-Git tests cover Unicode/Arabic hierarchical branch creation, safe dirty-tree carryover, conflicting dirty-tree checkout refusal with current branch and owner bytes preserved, invalid/already-existing/missing branch outcomes, non-repository/bare/unavailable states, caller cancellation, and timeout bounds.

The initial implementation candidate `{implementation_initial}` built with 0 warnings/0 errors but Windows CI #401 failed one Unicode branch assertion because the generic test-process helper decoded Git console output with the runner code page, producing mojibake. The production branch adapter already configured `StandardOutputEncoding`/`StandardErrorEncoding` as UTF-8. The cloud-repairable fixture defect was fixed in `{implementation_head}` by reading `.git/HEAD` as UTF-8 for the independent branch assertion; production semantics were not weakened. The final exact-head and exact-main gates above prove the repair.

## Cloud evidence boundary

This evidence proves bounded safe **local** branch create/checkout and canonical integration provenance. It does not claim fetch/pull, commit/push, history, dirty/pre-existing-change provenance, destructive-operation safeguards, P07 phase closure, P08/P11 functionality, or release readiness.

## Owner-last classification

P07-005 introduces no new owner-only acceptance obligation. The canonical owner queue remains exactly:

- `OWNER-P04-008-REAL-TARGET` — QUEUED / release blocking.
- `OWNER-P05-EXIT-REAL-TARGET` — QUEUED / release blocking.

Their source task/gate states remain unresolved as already recorded; `P04=NOT_RUN`, `P05=NOT_RUN`, `KNOWN_RELEASE_BLOCKERS=2`, and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.

## Remaining phase state

- `CURRENT_PHASE=P07`.
- `CURRENT_PHASE_STATE=IN_PROGRESS`.
- `PHASE_EXIT_GATE=NOT_RUN`.
- `FCCD-P07-001` through `FCCD-P07-005` are CLOSED.
- `FCCD-P07-006` through `FCCD-P07-011` remain PENDING.
- P08 and later implementation remain prohibited until P07 is truthfully closed under canonical governance.

## Next legal cloud action

After this reconciliation is normally integrated and the resulting exact canonical `main` remains green, rebuild the live P07 claim map. Recover any legitimate earlier regression/integration-pending work first; otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-006 — Fetch/pull` if still unclaimed. P08 and later phases remain prohibited until P07 is truthfully closed under canonical governance.
""", encoding="utf-8", newline="\n")
