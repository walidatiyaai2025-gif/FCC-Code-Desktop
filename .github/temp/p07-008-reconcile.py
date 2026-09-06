from pathlib import Path
import subprocess

BASE = "37bcd9ea636d278e852962a0fe05f112bc6adc6a"
ALLOWED = {
    "CURRENT_PHASE.md",
    "PROJECT_CONTROL.md",
    "docs/TASK_LEDGER.md",
    "evidence/phases/P07/P07_008_INTEGRATED_RECONCILIATION_2026-09-06.md",
}
SELF = Path(".github/temp/p07-008-reconcile.py")
WORKFLOW = Path(".github/workflows/temp-p07-008-reconcile.yml")
TEMP = {str(SELF), str(WORKFLOW)}

def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"guard failed for {path}: expected 1 occurrence, found {count}: {old[:120]!r}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8")

# Branch ancestry must still contain the exact verified implementation merge.
subprocess.run(["git", "merge-base", "--is-ancestor", BASE, "HEAD"], check=True)

replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P07-008` — History — PENDING.\n- `FCCD-P07-009` — Dirty/pre-existing-change provenance — PENDING.",
    "- `FCCD-P07-008` — History — CLOSED.\n- `FCCD-P07-009` — Dirty/pre-existing-change provenance — PENDING.",
)

current_phase_provenance = """## P07-008 integration provenance

- Exact implementation candidate: `78a3e789b89b6fe07b0d6ba92194a5cb9a5edec8` from PR #179 (`worker-b/fccd-p07-008-history`).
- PR #179 exact-head Windows CI: run `34058492299` / run #415 — SUCCESS.
- PR #179 exact-head P06-007 Workspace Search: run `34058492308` / run #144 — SUCCESS.
- PR #179 exact-head P06-008 Large Workspace Safeguards: run `34058492360` / run #128 — SUCCESS.
- Normal merge commit: `37bcd9ea636d278e852962a0fe05f112bc6adc6a`.
- Exact post-merge canonical-main Windows CI: run `34058964029` / run #416 — SUCCESS.
- Exact post-merge P06-007 Workspace Search: run `34058964036` / run #145 — SUCCESS.
- Exact post-merge P06-008 Large Workspace Safeguards: run `34058963979` / run #129 — SUCCESS.
- Integrated evidence: `evidence/phases/P07/P07_008_INTEGRATED_RECONCILIATION_2026-09-06.md`.
- Evidence class remains cloud/self-test for bounded read-only Git history plus canonical integration provenance; no dirty/pre-existing-change provenance, destructive-operation safeguard closure, conflict-scenario closure, P07 phase closure, P08/P11 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is implied.

"""
replace_once(
    "CURRENT_PHASE.md",
    "## P07 cloud activation provenance",
    current_phase_provenance + "## P07 cloud activation provenance",
)

old_project = """P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection`, `FCCD-P07-002 — Status/changed-files surface`, `FCCD-P07-003 — Diff viewer`, `FCCD-P07-004 — Stage/unstage`, `FCCD-P07-005 — Branch create/checkout`, `FCCD-P07-006 — Fetch/pull`, and `FCCD-P07-007 — Commit/push` are CLOSED after exact PR-head validation, normal merge integration, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-008` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_003_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_005_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_006_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_007_INTEGRATED_RECONCILIATION_2026-09-06.md`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes."""
new_project = """P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection`, `FCCD-P07-002 — Status/changed-files surface`, `FCCD-P07-003 — Diff viewer`, `FCCD-P07-004 — Stage/unstage`, `FCCD-P07-005 — Branch create/checkout`, `FCCD-P07-006 — Fetch/pull`, `FCCD-P07-007 — Commit/push`, and `FCCD-P07-008 — History` are CLOSED after exact PR-head validation, normal merge integration, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-009` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_003_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_005_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_006_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_007_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_008_INTEGRATED_RECONCILIATION_2026-09-06.md`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes."""
replace_once("PROJECT_CONTROL.md", old_project, new_project)

replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P07-008 | History | PENDING |",
    "| FCCD-P07-008 | History | CLOSED |",
)
ledger_paragraph = """
`FCCD-P07-008` is CLOSED from the bounded read-only Git history implementation integrated in PR #179. Exact implementation candidate `78a3e789b89b6fe07b0d6ba92194a5cb9a5edec8` passed Windows CI `34058492299` / #415, P06-007 Workspace Search `34058492308` / #144, and P06-008 Large Workspace Safeguards `34058492360` / #128. PR #179 was normally merged as `37bcd9ea636d278e852962a0fe05f112bc6adc6a`; that exact canonical main passed Windows CI `34058964029` / #416, P06-007 Workspace Search `34058964036` / #145, and P06-008 Large Workspace Safeguards `34058963979` / #129. Coverage includes Application-owned read-only `IGitHistoryService`, structured bounded commit metadata and parent linkage, newest-first pagination with an exclusive continuation cursor, literal repository-relative path filtering, bare and empty repositories, explicit UTF-8 handling, bounded output/count/timeout/cancellation, unsafe-path and cursor validation, owned-process cleanup, and preservation of dirty work-tree/index bytes. Task evidence: `evidence/phases/P07/P07_008_INTEGRATED_RECONCILIATION_2026-09-06.md`. No dirty/pre-existing-change provenance, destructive-operation safeguards, conflict integration closure, P07 phase closure, P08/P11 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE` is claimed; P07 remains `IN_PROGRESS`, P07-009 through P07-011 remain PENDING, and the two existing owner-last release blockers remain unchanged.

"""
replace_once("docs/TASK_LEDGER.md", "\n## P08 — Terminal/process supervision", "\n" + ledger_paragraph + "## P08 — Terminal/process supervision")

evidence = """# FCCD-P07-008 — Integrated reconciliation evidence

Date: 2026-09-06  
Phase: P07 — Change review + Git  
Task: `FCCD-P07-008 — History`  
Evidence class: cloud/self-test + canonical integration provenance

## Result

`FCCD-P07-008` is CLOSED from the production bounded read-only Git history implementation after exact candidate validation, normal merge integration, and exact post-merge canonical-main non-regression validation all completed SUCCESS.

## Implementation provenance

- Implementation PR: #179 — `P07-008: add bounded read-only Git history`.
- Branch: `worker-b/fccd-p07-008-history`.
- Exact implementation candidate: `78a3e789b89b6fe07b0d6ba92194a5cb9a5edec8`.
- Normal merge commit: `37bcd9ea636d278e852962a0fe05f112bc6adc6a`.
- Merge parents preserve tested ancestry; no squash/rebase closure is claimed.

## Exact implementation-head gates

- Windows CI #415: `34058492299` — SUCCESS.
- P06-007 Workspace Search #144: `34058492308` — SUCCESS.
- P06-008 Large Workspace Safeguards #128: `34058492360` — SUCCESS.

## Exact post-merge canonical-main gates

- Windows CI #416: `34058964029` — SUCCESS.
- P06-007 Workspace Search #145: `34058964036` — SUCCESS.
- P06-008 Large Workspace Safeguards #129: `34058963979` — SUCCESS.

## Verified cloud boundary

The integrated implementation provides an Application-owned read-only `IGitHistoryService`; structured commit IDs, parent IDs, author metadata, dates and subjects; bounded newest-first pagination using an exclusive continuation cursor; literal repository-relative path filtering; valid bare-repository history and typed empty-repository behavior; explicit UTF-8 process streams; non-interactive local Git-only execution; bounded commit count/output/timeout/cancellation; path and cursor validation; owned-process cleanup; and real disposable-Git verification that dirty work-tree bytes and index bytes remain unchanged.

## Governance boundary

- P07 remains `IN_PROGRESS` and `PHASE_EXIT_GATE=NOT_RUN`.
- P07-009 through P07-011 remain PENDING.
- P08 and later implementation, including P11 Blender work, remain prohibited until P07 closes sequentially.
- No new owner-only evidence is required for P07-008.
- Existing owner queue items `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain unchanged and release-blocking.
- `VERIFIED_FINAL_COMPLETE=false` remains mandatory.
"""
evidence_path = Path("evidence/phases/P07/P07_008_INTEGRATED_RECONCILIATION_2026-09-06.md")
if evidence_path.exists():
    raise SystemExit("evidence file already exists unexpectedly")
evidence_path.write_text(evidence, encoding="utf-8")

# Temporary orchestration must not survive the durable reconciliation commit.
SELF.unlink(missing_ok=True)
WORKFLOW.unlink(missing_ok=True)

subprocess.run(["git", "add", "-A"], check=True)
changed = subprocess.check_output(["git", "diff", "--cached", "--name-only"], text=True).splitlines()
expected_staged = ALLOWED | TEMP
if set(changed) != expected_staged or len(changed) != len(expected_staged):
    raise SystemExit(f"staged scope guard failed: {changed!r}")
deleted = subprocess.check_output(["git", "diff", "--cached", "--diff-filter=D", "--name-only"], text=True).splitlines()
if set(deleted) != TEMP or len(deleted) != len(TEMP):
    raise SystemExit(f"temporary deletion guard failed: {deleted!r}")

subprocess.run(["git", "config", "user.name", "github-actions[bot]"], check=True)
subprocess.run(["git", "config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com"], check=True)
subprocess.run(["git", "commit", "-m", "FCCD-P07-008: reconcile integrated Git history closure"], check=True)
subprocess.run(["git", "push", "origin", "HEAD:reconcile/fccd-p07-008-integrated-closure"], check=True)
