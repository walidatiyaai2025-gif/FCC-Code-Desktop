from pathlib import Path

implementation_head = "64324363aed3936e8e882096f65a8449c3eb8bc2"
implementation_merge = "9c3b0437f92a547453e8fdcdce22ab96d0084ade"
evidence_path = "evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md"

current = Path("CURRENT_PHASE.md")
text = current.read_text(encoding="utf-8")
old = "- `FCCD-P07-001` — `IGitService` and repository detection — PENDING."
new = "- `FCCD-P07-001` — `IGitService` and repository detection — CLOSED."
if text.count(old) != 1:
    raise SystemExit(f"CURRENT_PHASE expected exactly one P07-001 PENDING row, found {text.count(old)}")
text = text.replace(old, new, 1)
marker = "## P07 cloud activation provenance"
if text.count(marker) != 1:
    raise SystemExit("CURRENT_PHASE P07 activation marker mismatch")
provenance = f"""## P07-001 integration provenance

- Exact implementation candidate: `{implementation_head}` from PR #163 (`worker-b/fccd-p07-001-git-repository-detection`).
- PR #163 exact-head Windows CI: run `34036133218` / run #369 — SUCCESS.
- PR #163 exact-head P06-007 Workspace Search: run `34036133192` / run #98 — SUCCESS.
- PR #163 exact-head P06-008 Large Workspace Safeguards: run `34036133226` / run #82 — SUCCESS.
- Normal merge commit: `{implementation_merge}`.
- Exact post-merge canonical-main Windows CI: run `34036509721` / run #370 — SUCCESS.
- Exact post-merge P06-007 Workspace Search: run `34036509713` / run #99 — SUCCESS.
- Exact post-merge P06-008 Large Workspace Safeguards: run `34036509714` / run #83 — SUCCESS.
- Integrated evidence: `{evidence_path}`.
- Evidence class remains cloud/self-test for bounded read-only Git repository detection plus canonical integration provenance; no new owner-only evidence, P07 phase closure, P08 authorization, release eligibility, or `VERIFIED_FINAL_COMPLETE` is implied.

"""
text = text.replace(marker, provenance + marker, 1)
current.write_text(text, encoding="utf-8", newline="\n")

ledger = Path("docs/TASK_LEDGER.md")
text = ledger.read_text(encoding="utf-8")
old = "| FCCD-P07-001 | `IGitService` and repository detection | PENDING |"
new = "| FCCD-P07-001 | `IGitService` and repository detection | CLOSED |"
if text.count(old) != 1:
    raise SystemExit(f"TASK_LEDGER expected exactly one P07-001 PENDING row, found {text.count(old)}")
text = text.replace(old, new, 1)
p08 = "## P08 — Terminal/process supervision"
if text.count(p08) != 1:
    raise SystemExit("TASK_LEDGER P08 marker mismatch")
task_note = f"""`FCCD-P07-001` is CLOSED from the production Application-owned `IGitService` repository-detection contract and bounded read-only Git CLI adapter. Exact implementation candidate `{implementation_head}` passed Windows CI `34036133218` / #369, P06-007 Workspace Search `34036133192` / #98, and P06-008 Large Workspace Safeguards `34036133226` / #82. PR #163 was normally merged as `{implementation_merge}`; that exact canonical main passed Windows CI `34036509721` / #370, P06-007 Workspace Search `34036509713` / #99, and P06-008 Large Workspace Safeguards `34036509714` / #83. Coverage includes nested worktrees, bare repositories, ordinary non-repositories, Git-unavailable/probe-failure classification, bounded timeout/cancellation with owned-process cleanup, Unicode/Arabic/space-containing paths, and no-mutation verification. Task evidence: `{evidence_path}`. No new owner-only obligation is introduced; P07 remains `IN_PROGRESS`, P07-002 through P07-011 remain PENDING, P08+ remain prohibited, and `VERIFIED_FINAL_COMPLETE=false`.

"""
text = text.replace(p08, task_note + p08, 1)
current_action = "## Current next action"
idx = text.find(current_action)
if idx < 0:
    raise SystemExit("TASK_LEDGER current action marker missing")
replacement = f"""## Current next action

`CURRENT_PHASE = P07` and P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`. `FCCD-P07-001` is CLOSED after exact PR-head validation, normal merge integration as `{implementation_merge}`, exact post-merge canonical-main validation, and durable reconciliation evidence. `FCCD-P07-002` through `FCCD-P07-011` remain PENDING.

P04 remains acceptance-unresolved through `FCCD-P04-008` and its one-to-one queued `OWNER-P04-008-REAL-TARGET` obligation. P05 cloud implementation remains integrated, but its standalone exit observation remains queued as `OWNER-P05-EXIT-REAL-TARGET`. Their phase gates remain `P04=NOT_RUN;P05=NOT_RUN`; owner-last scheduling permits P07 cloud implementation but does not close either deferred acceptance requirement or permit release.

After this P07-001 reconciliation is integrated and exact resulting `main` remains green, re-run the Worker Protocol claim map. Recover/integrate any newly surfaced higher-priority legitimate defect first. Otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-002 — Status/changed-files surface`. Do not advance to P08 until every mandatory P07 task is CLOSED and the P07 phase exit gate is truthfully resolved under canonical governance. Only a genuinely owner-environment-bound residual may be queued under owner-last; do not fabricate target/manual evidence.

P06 is canonically CLOSED with `PHASE_EXIT_GATE=PASS`; closure evidence remains `evidence/phases/P06/CLOSURE.md`.
"""
text = text[:idx] + replacement
ledger.write_text(text, encoding="utf-8", newline="\n")

control = Path("PROJECT_CONTROL.md")
text = control.read_text(encoding="utf-8")
old = "P07 — Change review + Git — is now the single active cloud implementation/convergence phase. Its eleven mandatory ledger tasks remain PENDING at activation; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes."
new = f"P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection` is CLOSED after PR #163 exact-head validation, normal merge `{implementation_merge}`, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-002` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `{evidence_path}`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes."
if text.count(old) != 1:
    raise SystemExit(f"PROJECT_CONTROL expected canonical P07 activation paragraph once, found {text.count(old)}")
control.write_text(text.replace(old, new, 1), encoding="utf-8", newline="\n")

evidence = Path(evidence_path)
evidence.parent.mkdir(parents=True, exist_ok=True)
evidence.write_text(f"""# FCCD-P07-001 Integrated Reconciliation — 2026-09-06

## Decision

`FCCD-P07-001 — IGitService and repository detection` is **CLOSED** as a cloud-actionable task. Its production implementation is normally integrated and exact-main verified. P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`; this task closure does not advance P08 or authorize any P10 work.

## Production integration

The accepted implementation candidate is `{implementation_head}` from PR #163 (`worker-b/fccd-p07-001-git-repository-detection`). It provides the Application-owned typed `IGitService` repository-detection contract and a read-only Git CLI adapter using fixed `git rev-parse` operations. The implementation classifies nested worktrees, bare repositories, ordinary non-repositories, Git unavailability, and probe failures; it uses bounded timeout/cancellation and owned-process cleanup; it does not add `safe.directory` overrides, interactive prompts, optional-lock overrides, shell command strings, or any P07-002+ mutation/status/diff surface ownership.

Exact PR-head gates on `{implementation_head}` all completed SUCCESS:

- Windows CI run `34036133218` / run #369 — SUCCESS.
- P06-007 Workspace Search run `34036133192` / run #98 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34036133226` / run #82 — SUCCESS.

PR #163 was normally merged without squash/rebase as `{implementation_merge}`, preserving both tested implementation ancestry and canonical main ancestry.

Exact post-merge canonical-main gates on `{implementation_merge}` all completed SUCCESS:

- Windows CI run `34036509721` / run #370 — SUCCESS.
- P06-007 Workspace Search run `34036509713` / run #99 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34036509714` / run #83 — SUCCESS.

No task-local product defect or exact-main regression remained after integration.

## Cloud evidence boundary

Cloud/self-test evidence proves the bounded read-only Git repository-detection contract, real disposable Git repository behavior in unit tests, Unicode/Arabic/space-containing paths, bare/non-repository cases, Git-unavailable/probe-failure classification, cancellation/timeout cleanup, and source non-mutation. The implementation documentation remains `docs/git/GIT_REPOSITORY_DETECTION.md`; this document adds canonical integration and exact-main provenance.

This task does not claim status/changed-files, diff, stage/unstage, branch mutation, fetch/pull, commit/push, history, dirty provenance, destructive Git operations, P07 phase closure, or later-phase functionality.

## Owner-last classification

P07-001 introduces no genuinely owner-only acceptance requirement. No manual/target evidence was fabricated or newly queued. `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` remains unchanged with exactly the two pre-existing release-blocking obligations:

- `OWNER-P04-008-REAL-TARGET`.
- `OWNER-P05-EXIT-REAL-TARGET`.

`KNOWN_RELEASE_BLOCKERS=2` and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.

## Remaining phase state

- `CURRENT_PHASE=P07`.
- `CURRENT_PHASE_STATE=IN_PROGRESS`.
- `PHASE_EXIT_GATE=NOT_RUN`.
- `FCCD-P07-001` is CLOSED.
- `FCCD-P07-002` through `FCCD-P07-011` remain PENDING.
- P08 and later implementation remain prohibited until P07 is truthfully closed under canonical governance.

## Next legal cloud action

After this reconciliation is normally integrated and its exact merge SHA remains green, re-fetch live claims. Recover any newly surfaced higher-priority regression/integration work first; otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-002 — Status/changed-files surface` if still unclaimed. Do not start P10 while P07/P08/P09 remain incomplete.
""", encoding="utf-8", newline="\n")
