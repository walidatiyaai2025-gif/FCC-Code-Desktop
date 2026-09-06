from pathlib import Path

BASE_SHA = "f22eb711bef214e222fc22cc670e08b90fd58a1b"
IMPLEMENTATION_SHA = "e7e6365ae0f2113a23f7b48327a537ab7af6298d"
MERGE_SHA = "f22eb711bef214e222fc22cc670e08b90fd58a1b"
EVIDENCE_PATH = Path("evidence/phases/P07/P07_007_INTEGRATED_RECONCILIATION_2026-09-06.md")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


def write(path: str, text: str) -> None:
    Path(path).write_text(text, encoding="utf-8", newline="\n")

current_path = Path("CURRENT_PHASE.md")
current = current_path.read_text(encoding="utf-8")
current = replace_once(
    current,
    "- `FCCD-P07-007` — Commit/push — PENDING.",
    "- `FCCD-P07-007` — Commit/push — CLOSED.",
    "CURRENT_PHASE inventory",
)
if "## P07-007 integration provenance" in current:
    raise SystemExit("CURRENT_PHASE already contains P07-007 provenance")
current_marker = "## P07 cloud activation provenance"
if current.count(current_marker) != 1:
    raise SystemExit("CURRENT_PHASE provenance marker mismatch")
current_provenance = f"""## P07-007 integration provenance

- Exact implementation candidate: `{IMPLEMENTATION_SHA}` from PR #177 (`worker-b/fccd-p07-007-commit-push`).
- PR #177 exact-head Windows CI: run `34055661399` / run #411 — SUCCESS.
- PR #177 exact-head P06-007 Workspace Search: run `34055661425` / run #140 — SUCCESS.
- PR #177 exact-head P06-008 Large Workspace Safeguards: run `34055661393` / run #124 — SUCCESS.
- Normal merge commit: `{MERGE_SHA}`.
- Exact post-merge canonical-main Windows CI: run `34056109391` / run #412 — SUCCESS.
- Exact post-merge P06-007 Workspace Search: run `34056109410` / run #141 — SUCCESS.
- Exact post-merge P06-008 Large Workspace Safeguards: run `34056109409` / run #125 — SUCCESS.
- Integrated evidence: `evidence/phases/P07/P07_007_INTEGRATED_RECONCILIATION_2026-09-06.md`.
- Evidence class remains cloud/self-test for bounded staged-index commit and non-force current-branch push plus canonical integration provenance; no history, dirty-provenance, destructive-operation safeguard closure, P07 phase closure, P08/P11 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is implied.

"""
current = current.replace(current_marker, current_provenance + current_marker, 1)
write("CURRENT_PHASE.md", current)

project_path = Path("PROJECT_CONTROL.md")
project = project_path.read_text(encoding="utf-8")
project_old = "P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection`, `FCCD-P07-002 — Status/changed-files surface`, `FCCD-P07-003 — Diff viewer`, `FCCD-P07-004 — Stage/unstage`, `FCCD-P07-005 — Branch create/checkout`, and `FCCD-P07-006 — Fetch/pull` are CLOSED after exact PR-head validation, normal merge integration, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-007` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_003_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_005_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_006_INTEGRATED_RECONCILIATION_2026-09-06.md`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes."
project_new = "P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection`, `FCCD-P07-002 — Status/changed-files surface`, `FCCD-P07-003 — Diff viewer`, `FCCD-P07-004 — Stage/unstage`, `FCCD-P07-005 — Branch create/checkout`, `FCCD-P07-006 — Fetch/pull`, and `FCCD-P07-007 — Commit/push` are CLOSED after exact PR-head validation, normal merge integration, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-008` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_003_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_005_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_006_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_007_INTEGRATED_RECONCILIATION_2026-09-06.md`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes."
project = replace_once(project, project_old, project_new, "PROJECT_CONTROL P07 status")
write("PROJECT_CONTROL.md", project)

ledger_path = Path("docs/TASK_LEDGER.md")
ledger = ledger_path.read_text(encoding="utf-8")
ledger = replace_once(
    ledger,
    "| FCCD-P07-007 | Commit/push | PENDING |",
    "| FCCD-P07-007 | Commit/push | CLOSED |",
    "TASK_LEDGER row",
)
if "P07_007_INTEGRATED_RECONCILIATION_2026-09-06.md" in ledger:
    raise SystemExit("TASK_LEDGER already contains P07-007 reconciliation evidence")
ledger_marker = "\n## P08 — Terminal/process supervision"
if ledger.count(ledger_marker) != 1:
    raise SystemExit("TASK_LEDGER P08 marker mismatch")
ledger_paragraph = f"""

`FCCD-P07-007` is CLOSED from the bounded staged-index commit and non-force current-branch push implementation integrated in PR #177. Exact implementation candidate `{IMPLEMENTATION_SHA}` passed Windows CI `34055661399` / #411, P06-007 Workspace Search `34055661425` / #140, and P06-008 Large Workspace Safeguards `34055661393` / #124. PR #177 was normally merged as `{MERGE_SHA}`; that exact canonical main passed Windows CI `34056109391` / #412, P06-007 Workspace Search `34056109410` / #141, and P06-008 Large Workspace Safeguards `34056109409` / #125. Coverage includes a dedicated Application-owned `IGitCommitPushService`, staged-index-only commit semantics that preserve unstaged owner work, typed empty/invalid/no-staged-change outcomes, bounded non-interactive commit execution with editor/signing/repository hooks disabled, verification that commit advances HEAD, current-attached-branch push through an explicit same-branch refspec, no force/delete/rewrite options, typed non-fast-forward and other push rejection, local bare-remote real-Git fixtures, timeout/cancellation, and owned-process-tree cleanup. Task evidence: `evidence/phases/P07/P07_007_INTEGRATED_RECONCILIATION_2026-09-06.md`. No history, dirty/pre-existing-change provenance, destructive-operation safeguard closure, P07 phase closure, P08/P11 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE` is claimed; P07 remains `IN_PROGRESS`, P07-008 through P07-011 remain PENDING, and the two existing owner-last release blockers remain unchanged.
"""
ledger = ledger.replace(ledger_marker, ledger_paragraph + ledger_marker, 1)
ledger = replace_once(
    ledger,
    "`CURRENT_PHASE = P07` and P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`. `FCCD-P07-001` through `FCCD-P07-006` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable reconciliation evidence. `FCCD-P07-007` through `FCCD-P07-011` remain PENDING.",
    "`CURRENT_PHASE = P07` and P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`. `FCCD-P07-001` through `FCCD-P07-007` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable reconciliation evidence. `FCCD-P07-008` through `FCCD-P07-011` remain PENDING.",
    "TASK_LEDGER current action summary",
)
ledger = replace_once(
    ledger,
    "After this P07-006 reconciliation is integrated and exact resulting `main` remains green, re-run the Worker Protocol claim map. Recover/integrate any newly surfaced higher-priority legitimate defect first. Otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-007 — Commit/push` if still unclaimed and dependency-valid. Do not advance to P08 until every mandatory P07 task is CLOSED and the P07 phase exit gate is truthfully resolved under canonical governance. Only a genuinely owner-environment-bound residual may be queued under owner-last; do not fabricate target/manual evidence.",
    "After this P07-007 reconciliation is integrated and exact resulting `main` remains green, re-run the Worker Protocol claim map. Recover/integrate any newly surfaced higher-priority legitimate defect first. Otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-008 — History` if still unclaimed and dependency-valid. Do not advance to P08 until every mandatory P07 task is CLOSED and the P07 phase exit gate is truthfully resolved under canonical governance. Only a genuinely owner-environment-bound residual may be queued under owner-last; do not fabricate target/manual evidence.",
    "TASK_LEDGER next action",
)
write("docs/TASK_LEDGER.md", ledger)

if EVIDENCE_PATH.exists():
    raise SystemExit("P07-007 evidence already exists")
EVIDENCE_PATH.parent.mkdir(parents=True, exist_ok=True)
EVIDENCE_PATH.write_text(f"""# FCCD-P07-007 — Integrated reconciliation evidence

Date: 2026-09-06  
Phase: P07 — Change review + Git  
Task: `FCCD-P07-007 — Commit/push`  
Evidence class: cloud/self-test + canonical integration provenance

## Result

`FCCD-P07-007` is CLOSED from the production bounded staged-index commit and non-force current-branch push implementation after exact candidate validation, normal merge integration, and exact post-merge canonical-main non-regression validation all completed SUCCESS.

## Implementation provenance

- Implementation PR: #177 — `P07-007: add bounded commit and push service`.
- Branch: `worker-b/fccd-p07-007-commit-push`.
- Exact implementation candidate: `{IMPLEMENTATION_SHA}`.
- Normal merge commit: `{MERGE_SHA}`.
- Merge parents preserve tested ancestry; no squash/rebase closure is claimed.

## Exact implementation-head gates

- Windows CI #411: `34055661399` — SUCCESS.
- P06-007 Workspace Search #140: `34055661425` — SUCCESS.
- P06-008 Large Workspace Safeguards #124: `34055661393` — SUCCESS.

## Exact post-merge canonical-main gates

- Windows CI #412: `34056109391` — SUCCESS.
- P06-007 Workspace Search #141: `34056109410` — SUCCESS.
- P06-008 Large Workspace Safeguards #125: `34056109409` — SUCCESS.

## Verified cloud boundary

The integrated implementation provides a dedicated Application-owned `IGitCommitPushService`; commit consumes only the staged index and preserves unstaged owner work; invalid/empty messages and no-staged-change states are typed; commit is bounded/non-interactive and disables editor/signing/repository hooks; successful commit verifies a new HEAD SHA; push publishes only the current attached branch to the same branch name through an explicit refspec; force/delete/rewrite options are absent; push hooks are disabled; non-fast-forward and other Git refusals return typed `PushRejected` without destructive retry; local bare-remote fixtures verify real push behavior without external networking; cancellation, timeout, repository/remote failures, and owned-process cleanup are covered.

## Governance boundary

- P07 remains `IN_PROGRESS` and `PHASE_EXIT_GATE=NOT_RUN`.
- P07-008 through P07-011 remain PENDING.
- P08 and later implementation, including P11 Blender work, remain prohibited until P07 closes sequentially.
- No new owner-only evidence is required for P07-007.
- Existing owner queue items `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain unchanged and release-blocking.
- `VERIFIED_FINAL_COMPLETE=false` remains mandatory.
""", encoding="utf-8", newline="\n")

# Ensure temporary orchestration never survives the durable reconciliation commit.
for temp in [Path(".github/temp/p07-007-reconcile.py"), Path(".github/workflows/temp-p07-007-reconcile.yml")]:
    if temp.exists():
        temp.unlink()

allowed = {
    "CURRENT_PHASE.md",
    "PROJECT_CONTROL.md",
    "docs/TASK_LEDGER.md",
    "evidence/phases/P07/P07_007_INTEGRATED_RECONCILIATION_2026-09-06.md",
}
import subprocess
changed = subprocess.check_output(["git", "diff", "--name-only", BASE_SHA], text=True).splitlines()
unexpected = sorted(set(changed) - allowed)
if unexpected:
    raise SystemExit(f"unexpected durable reconciliation paths: {unexpected}")
missing = sorted(allowed - set(changed))
if missing:
    raise SystemExit(f"missing expected durable reconciliation paths: {missing}")

print("P07-007 guarded reconciliation prepared successfully")
