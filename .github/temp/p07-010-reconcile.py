from __future__ import annotations

import pathlib
import subprocess
import sys

EXPECTED_MAIN = "161e725e3c72743ed31ddcbd277b8b0ee3354f66"
BRANCH = "reconcile/fccd-p07-010-integrated-closure"
SCRIPT = pathlib.Path(".github/temp/p07-010-reconcile.py")
WORKFLOW = pathlib.Path(".github/workflows/temp-p07-010-reconcile.yml")


def run(*args: str) -> str:
    return subprocess.check_output(args, text=True).strip()


def replace_once(path: str, old: str, new: str) -> None:
    target = pathlib.Path(path)
    data = target.read_bytes()
    old_bytes = old.encode("utf-8")
    count = data.count(old_bytes)
    if count != 1:
        raise RuntimeError(f"{path}: expected exactly one match, found {count}: {old!r}")
    target.write_bytes(data.replace(old_bytes, new.encode("utf-8"), 1))


def insert_once(path: str, anchor: str, block: str) -> None:
    replace_once(path, anchor, block + anchor)


subprocess.check_call(["git", "fetch", "origin", "main", "--no-tags"])
actual_main = run("git", "rev-parse", "origin/main")
if actual_main != EXPECTED_MAIN:
    raise RuntimeError(f"origin/main drifted: expected {EXPECTED_MAIN}, got {actual_main}")
subprocess.check_call(["git", "merge-base", "--is-ancestor", EXPECTED_MAIN, "HEAD"])

replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P07-010` — Destructive-operation safeguards — PENDING.\n",
    "- `FCCD-P07-010` — Destructive-operation safeguards — CLOSED.\n",
)

p07_010_provenance = """## P07-010 integration provenance

- Exact implementation candidate: `b2ebc3b811f1b0ac0320fa01212567a8256f29a6` from PR #183 (`worker-b/fccd-p07-010-destructive-operation-safeguards`).
- PR #183 exact-head Windows CI: run `34064091958` / run #424 — SUCCESS.
- PR #183 exact-head P06-007 Workspace Search: run `34064092009` / run #153 — SUCCESS.
- PR #183 exact-head P06-008 Large Workspace Safeguards: run `34064092001` / run #137 — SUCCESS.
- Normal merge commit: `161e725e3c72743ed31ddcbd277b8b0ee3354f66`.
- Exact post-merge canonical-main Windows CI: run `34064629191` / run #425 — SUCCESS.
- Exact post-merge P06-007 Workspace Search: run `34064629184` / run #154 — SUCCESS.
- Exact post-merge P06-008 Large Workspace Safeguards: run `34064629256` / run #138 — SUCCESS.
- Integrated evidence: `evidence/phases/P07/P07_010_INTEGRATED_RECONCILIATION_2026-09-07.md`.
- Evidence class remains cloud/self-test for fail-closed destructive Git command safeguards plus canonical integration provenance; no conflict-scenario closure, P07 phase closure, P08/P11/P12 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is implied.

"""
insert_once(
    "CURRENT_PHASE.md",
    "## P07 cloud activation provenance\n",
    p07_010_provenance,
)

replace_once(
    "PROJECT_CONTROL.md",
    "`FCCD-P07-008 — History`, and `FCCD-P07-009 — Dirty/pre-existing-change provenance` are CLOSED",
    "`FCCD-P07-008 — History`, `FCCD-P07-009 — Dirty/pre-existing-change provenance`, and `FCCD-P07-010 — Destructive-operation safeguards` are CLOSED",
)
replace_once(
    "PROJECT_CONTROL.md",
    "`FCCD-P07-010` and `FCCD-P07-011` remain PENDING",
    "`FCCD-P07-011` remains PENDING",
)
replace_once(
    "PROJECT_CONTROL.md",
    "`evidence/phases/P07/P07_008_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_009_INTEGRATED_RECONCILIATION_2026-09-07.md`. P08",
    "`evidence/phases/P07/P07_008_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_009_INTEGRATED_RECONCILIATION_2026-09-07.md`, and `evidence/phases/P07/P07_010_INTEGRATED_RECONCILIATION_2026-09-07.md`. P08",
)

replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P07-010 | Destructive-operation safeguards | PENDING |\n",
    "| FCCD-P07-010 | Destructive-operation safeguards | CLOSED |\n",
)

ledger_paragraph = """`FCCD-P07-010` is CLOSED from the production fail-closed destructive Git command safeguard integration in PR #183. Exact implementation candidate `b2ebc3b811f1b0ac0320fa01212567a8256f29a6` passed Windows CI `34064091958` / #424, P06-007 Workspace Search `34064092009` / #153, and P06-008 Large Workspace Safeguards `34064092001` / #137. PR #183 was normally merged as `161e725e3c72743ed31ddcbd277b8b0ee3354f66`; that exact canonical main passed Windows CI `34064629191` / #425, P06-007 Workspace Search `34064629184` / #154, and P06-008 Large Workspace Safeguards `34064629256` / #138. Coverage includes the fail-closed `GitCommandSafetyPolicy` at every existing Git mutation process-start boundary, allowlisting only the bounded command shapes already owned by P07-004 through P07-007, rejection of reset/clean/forced checkout/work-tree restore/broad staging/forced or deleting push/history rewrite/unknown mutation shapes before launch, preservation of the intentional unborn-repository cached-only forced index removal path while rejecting non-cached removal, rejection of unknown global `-c` configuration overrides, and diagnostics that do not echo blocked command arguments. Task evidence: `evidence/phases/P07/P07_010_INTEGRATED_RECONCILIATION_2026-09-07.md`. No conflict-scenario closure, P07 phase closure, P08/P11/P12 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE` is claimed; P07 remains `IN_PROGRESS`, only P07-011 remains PENDING, and the two existing owner-last release blockers remain unchanged.

"""
insert_once(
    "docs/TASK_LEDGER.md",
    "## P08 — Terminal/process supervision\n",
    ledger_paragraph,
)

evidence_path = pathlib.Path("evidence/phases/P07/P07_010_INTEGRATED_RECONCILIATION_2026-09-07.md")
if evidence_path.exists():
    raise RuntimeError(f"Evidence already exists unexpectedly: {evidence_path}")
evidence_path.parent.mkdir(parents=True, exist_ok=True)
evidence_path.write_text(
    """# FCCD-P07-010 — Integrated reconciliation evidence

Date: 2026-09-07  
Phase: P07 — Change review + Git  
Task: `FCCD-P07-010 — Destructive-operation safeguards`  
Evidence class: cloud/self-test + canonical integration provenance

## Result

`FCCD-P07-010` is CLOSED from the production fail-closed destructive Git command safeguards after exact implementation-head validation, normal merge integration, and exact post-merge canonical-main non-regression validation all completed SUCCESS.

## Implementation provenance

- Implementation PR: #183 — `P07-010: add fail-closed destructive Git safeguards`.
- Branch: `worker-b/fccd-p07-010-destructive-operation-safeguards`.
- Exact implementation candidate: `b2ebc3b811f1b0ac0320fa01212567a8256f29a6`.
- Normal merge commit: `161e725e3c72743ed31ddcbd277b8b0ee3354f66`.
- Merge parents preserve tested ancestry; no squash/rebase closure is claimed.

## Exact implementation-head gates

- Windows CI #424: `34064091958` — SUCCESS.
- P06-007 Workspace Search #153: `34064092009` — SUCCESS.
- P06-008 Large Workspace Safeguards #137: `34064092001` — SUCCESS.

## Exact post-merge canonical-main gates

- Windows CI #425: `34064629191` — SUCCESS.
- P06-007 Workspace Search #154: `34064629184` — SUCCESS.
- P06-008 Large Workspace Safeguards #138: `34064629256` — SUCCESS.

## Verified cloud boundary

The integrated implementation places a fail-closed `GitCommandSafetyPolicy` at the process-start boundary of all existing Git mutation adapters. It permits only the bounded command shapes already owned by P07-004 through P07-007 and rejects reset, clean, forced checkout, work-tree restore, broad staging, forced/deleting push, history rewrite, and unknown Git mutation shapes before process launch. It preserves the intentional unborn-repository `git rm --cached --force` index-only path while rejecting non-cached removal, rejects unknown global `-c` configuration overrides, and avoids echoing blocked command arguments into guard diagnostics. Dedicated positive/negative policy coverage and the existing disposable real-Git mutation suites verify safe-path non-regression without adding a new destructive operation.

## Governance boundary

- P07 remains `IN_PROGRESS` and its exit gate remains `NOT_RUN`.
- `FCCD-P07-011` remains PENDING and is the only remaining P07 task.
- No P08, P11, P12, or later-phase implementation is authorized by this evidence.
- `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain the unchanged release-blocking owner queue obligations.
- `KNOWN_RELEASE_BLOCKERS=2` and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.
- No owner/manual/target evidence is fabricated or implied.

Permanent reconciliation validation is required on this exact reconciliation candidate before normal merge, followed by exact-main permanent validation.
""",
    encoding="utf-8",
    newline="\n",
)

# The temporary orchestration must leave no durable tree footprint.
for temp in (SCRIPT, WORKFLOW):
    if not temp.exists():
        raise RuntimeError(f"Missing temporary file before self-deletion: {temp}")
    subprocess.check_call(["git", "rm", "--", str(temp)])

subprocess.check_call(["git", "add", "CURRENT_PHASE.md", "PROJECT_CONTROL.md", "docs/TASK_LEDGER.md", str(evidence_path)])
allowed = {
    "CURRENT_PHASE.md",
    "PROJECT_CONTROL.md",
    "docs/TASK_LEDGER.md",
    str(evidence_path).replace("\\", "/"),
}
changed = set(run("git", "diff", "--cached", "--name-only", "origin/main").splitlines())
if changed != allowed:
    raise RuntimeError(f"Unexpected durable reconciliation diff: {sorted(changed)}; expected {sorted(allowed)}")

# Fail closed if release/phase invariants were accidentally weakened.
current_phase = pathlib.Path("CURRENT_PHASE.md").read_text(encoding="utf-8")
for invariant in (
    "CURRENT_PHASE: P07",
    "CURRENT_PHASE_STATE: IN_PROGRESS",
    "PHASE_EXIT_GATE: NOT_RUN",
    "KNOWN_RELEASE_BLOCKERS: 2",
    "VERIFIED_FINAL_COMPLETE: false",
    "OWNER-P04-008-REAL-TARGET;OWNER-P05-EXIT-REAL-TARGET",
    "- `FCCD-P07-010` — Destructive-operation safeguards — CLOSED.",
    "- `FCCD-P07-011` — Git integration tests/conflict scenarios — PENDING.",
):
    if invariant not in current_phase:
        raise RuntimeError(f"Required invariant missing after reconciliation: {invariant}")

subprocess.check_call(["git", "config", "user.name", "walidatiyaai2025-gif"])
subprocess.check_call(["git", "config", "user.email", "walidatiyaai2025@gmail.com"])
subprocess.check_call([
    "git", "commit", "-m", "FCCD-P07-010: reconcile integrated destructive safeguards closure"
])
subprocess.check_call(["git", "push", "origin", f"HEAD:{BRANCH}"])
print("P07-010 guarded reconciliation committed and pushed successfully.")
