from pathlib import Path
import subprocess

BASE = "f86e8bc5e08b4def96fc50fc2cc3d3819621aa68"
BRANCH = "reconcile/fccd-p08-004-integrated"
WORKFLOW = ".github/workflows/temp-p08-004-reconcile.yml"
HELPER = ".github/temp/p08-004-reconcile.py"
EVIDENCE = "evidence/phases/P08/P08_004_INTEGRATED_RECONCILIATION_2026-09-07.md"


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one guarded match, found {count}: {old[:120]!r}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8")


def insert_once(path: str, marker: str, block: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(marker)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one insertion marker, found {count}: {marker!r}")
    if block.strip() in text:
        raise SystemExit(f"{path}: reconciliation block already exists")
    p.write_text(text.replace(marker, block + marker, 1), encoding="utf-8")


def run(*args: str, capture: bool = False) -> str:
    result = subprocess.run(args, check=True, text=True, capture_output=capture)
    return result.stdout if capture else ""


# Current phase inventory and stale post-P08-003 summary.
replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P08-004` — ConPTY terminal host — PENDING.",
    "- `FCCD-P08-004` — ConPTY terminal host — CLOSED.",
)
replace_once(
    "CURRENT_PHASE.md",
    "- Evidence is cloud/hosted-Windows bounded-process-output evidence only. P08 remains IN_PROGRESS; P08-004, P08-007, and P08-008 remain PENDING; P09/P14 and later phases remain prohibited. The owner queue is unchanged and `VERIFIED_FINAL_COMPLETE` remains false.\n",
    "- Evidence is cloud/hosted-Windows bounded-process-output evidence only. P08 remains IN_PROGRESS; P08-007 and P08-008 remain PENDING; P09/P14 and later phases remain prohibited. The owner queue is unchanged and `VERIFIED_FINAL_COMPLETE` remains false.\n",
)

current_provenance = """## P08-004 integration provenance

- Task: `FCCD-P08-004 — ConPTY terminal host` — CLOSED.
- Implementation PR: #196 (`worker/fccd-p08-004-conpty-terminal-host`).
- Exact implementation candidate: `218cc7d93fafb690a5e2cf7192970bf6be0caa1f`.
- Exact implementation-head P08-004 ConPTY Terminal Host: run `34119217478` / #29 — SUCCESS.
- Exact implementation-head Windows CI: run `34119217635` / #534 — SUCCESS.
- Exact implementation-head P06-007 Workspace Search: run `34119217493` / #263 — SUCCESS.
- Exact implementation-head P06-008 Large Workspace Safeguards: run `34119217492` / #247 — SUCCESS.
- Normal implementation merge: `da9caf9fb1542b7e080ddd768b77f73e653bb08b`.
- Exact merge-main P08-004 ConPTY Terminal Host: run `34119961238` / #30 — SUCCESS.
- The first merge-main Windows CI run `34119961204` / #535 was cancelled by the workflow's `cancel-in-progress` concurrency policy after a later main push; it is not counted as PASS evidence. Before cancellation its Windows Release baseline and P05-005/P05-006/P05-007 validators had passed.
- A mistaken reconciliation placeholder was immediately reverted; canonical main `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68` has repository tree `baded42ef7666d85016c1788d92a50b412744d83`, exactly the same tree as implementation merge `da9caf9fb1542b7e080ddd768b77f73e653bb08b`, so no product/task bytes changed across that net-zero sequence.
- Current canonical-main Windows CI: run `34120686533` / #537 — SUCCESS.
- Current canonical-main P06-007 Workspace Search: run `34120686443` / #266 — SUCCESS.
- Current canonical-main P06-008 Large Workspace Safeguards: run `34120686425` / #250 — SUCCESS.
- Integrated evidence: `evidence/phases/P08/P08_004_INTEGRATED_RECONCILIATION_2026-09-07.md`.
- Evidence is cloud/hosted-Windows ConPTY evidence only. No owner-only evidence is added. P08 remains IN_PROGRESS; P08-007 and P08-008 remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P09/P14 and later phases remain prohibited. The owner queue is unchanged and `VERIFIED_FINAL_COMPLETE` remains false.

"""
insert_once("CURRENT_PHASE.md", "## P08-005 integration provenance\n", current_provenance)

# Project-control current narrative: add P08-004 integrated closure without changing phase or owner-last state.
replace_once(
    "PROJECT_CONTROL.md",
    "`FCCD-P08-004`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING.",
    "`FCCD-P08-004 — ConPTY terminal host` is CLOSED after PR #196 exact candidate `218cc7d93fafb690a5e2cf7192970bf6be0caa1f` passed dedicated ConPTY run `34119217478`, Windows CI `34119217635`, Workspace Search `34119217493`, and Large Workspace Safeguards `34119217492`; PR #196 was normally merged as `da9caf9fb1542b7e080ddd768b77f73e653bb08b`, dedicated exact merge-main ConPTY run `34119961238` passed, and after a net-zero mistaken placeholder/revert advanced canonical history without changing repository tree `baded42ef7666d85016c1788d92a50b412744d83`, current canonical main `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68` passed Windows CI `34120686533`, Workspace Search `34120686443`, and Large Workspace Safeguards `34120686425`. `FCCD-P08-007` and `FCCD-P08-008` remain PENDING.",
)

# Ledger task row and current next-action reconciliation.
replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P08-004 | ConPTY terminal host | PENDING |",
    "| FCCD-P08-004 | ConPTY terminal host | CLOSED |",
)

ledger_evidence = """
`FCCD-P08-004` is CLOSED from the Windows ConPTY terminal-host implementation integrated by PR #196. Exact implementation candidate `218cc7d93fafb690a5e2cf7192970bf6be0caa1f` passed dedicated P08-004 ConPTY Terminal Host run `34119217478` / #29, Windows CI `34119217635` / #534, P06-007 Workspace Search `34119217493` / #263, and P06-008 Large Workspace Safeguards `34119217492` / #247. PR #196 was normally merged as `da9caf9fb1542b7e080ddd768b77f73e653bb08b`; dedicated exact merge-main ConPTY run `34119961238` / #30 completed SUCCESS. Windows CI #535 on that merge SHA was later cancelled only by the repository's `cancel-in-progress` policy when canonical main moved, so it is explicitly not used as PASS evidence. The intervening mistaken reconciliation placeholder was reverted, leaving canonical main `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68` with tree `baded42ef7666d85016c1788d92a50b412744d83`, identical to the implementation merge tree. That current canonical main passed Windows CI `34120686533` / #537, P06-007 Workspace Search `34120686443` / #266, and P06-008 Large Workspace Safeguards `34120686425` / #250. Coverage includes native-safe ConPTY dimensions, immutable fully-qualified launch contracts, atomic `PROC_THREAD_ATTRIBUTE_JOB_LIST` ownership with kill-on-close semantics, isolated inherited standard handles, no-argument CMD profile launch, negative/missing/cancel paths, actual terminal-input execution, independently proven output, live resize, clean exit/EOF, Unicode/space-containing working paths, owner-data preservation, async disposal, owned-descendant termination, and unrelated-process isolation. Task evidence: `evidence/phases/P08/P08_004_INTEGRATED_RECONCILIATION_2026-09-07.md`. No shell-profile discovery, optional-shell launch, terminal UI, P08 phase closure, P09/P14 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is claimed; P08 remains `IN_PROGRESS`, only P08-007 and P08-008 remain PENDING, and the two existing owner-last release blockers remain unchanged.

"""
insert_once("docs/TASK_LEDGER.md", "## P09 — External Tool Gateway\n", ledger_evidence)

old_next = """`CURRENT_PHASE = P08` is `IN_PROGRESS`. `FCCD-P08-001 — Process supervisor with owned process-tree tracking`, `FCCD-P08-002 — Graceful→forced cancellation escalation`, `FCCD-P08-003 — Bounded streaming log pipeline`, `FCCD-P08-005 — PowerShell/CMD profiles`, and `FCCD-P08-006 — Optional Git Bash/WSL detection` are CLOSED. P08-003 is accepted only after implementation PR #195 exact candidate `9ffabc1c85a70ebea9d6bbbc0c2478bdffae3136` passed Windows CI `34090112839`, Workspace Search `34090112868`, Large Workspace Safeguards `34090112831`, and dedicated P08-003 gate `34090112927`; PR #195 was normally merged as `eae2134666fe659fd26bf065b89d8fd661c9b0da`, whose exact-main Windows CI `34090854609`, Workspace Search `34090854605`, Large Workspace Safeguards `34090854583`, and dedicated P08-003 gate `34090854589` all completed SUCCESS. `FCCD-P08-004`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`. The next legal cloud action is to recover/integrate legitimate active P08 work first — currently PR #196 for P08-004 if it remains live — otherwise re-read claims and select the highest-value dependency-valid unclaimed P08 task. Do not steal active P08 work, do not skip to P09/P14 or any later phase, and do not fabricate owner/manual evidence.
"""
new_next = """`CURRENT_PHASE = P08` is `IN_PROGRESS`. `FCCD-P08-001` through `FCCD-P08-006` are now CLOSED. `FCCD-P08-004 — ConPTY terminal host` is accepted after exact implementation candidate `218cc7d93fafb690a5e2cf7192970bf6be0caa1f` passed dedicated ConPTY run `34119217478`, Windows CI `34119217635`, Workspace Search `34119217493`, and Large Workspace Safeguards `34119217492`; PR #196 was normally merged as `da9caf9fb1542b7e080ddd768b77f73e653bb08b`, dedicated exact merge-main ConPTY run `34119961238` passed, and current net-equivalent canonical main `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68` passed Windows CI `34120686533`, Workspace Search `34120686443`, and Large Workspace Safeguards `34120686425`. `FCCD-P08-007` and `FCCD-P08-008` remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`. The next legal cloud action is to recover/integrate legitimate active P08 work first; otherwise re-read live claims and select the highest-value dependency-valid unclaimed P08 task. Do not steal active P08 work, do not skip to P09/P14 or any later phase, and do not fabricate owner/manual evidence.
"""
replace_once("docs/TASK_LEDGER.md", old_next, new_next)
replace_once(
    "docs/TASK_LEDGER.md",
    "Do not steal active P08-003/P08-004/P08-005 work, do not skip to P09/P14 or any later phase, and do not fabricate owner/manual evidence.",
    "Do not steal active P08 work, do not skip to P09/P14 or any later phase, and do not fabricate owner/manual evidence.",
)

Path(EVIDENCE).parent.mkdir(parents=True, exist_ok=True)
Path(EVIDENCE).write_text("""# P08-004 — Integrated reconciliation

- **Date:** 2026-09-07
- **Task:** `FCCD-P08-004 — ConPTY terminal host`
- **Classification:** CLOUD / HOSTED-WINDOWS / INTEGRATED
- **Canonical result:** `CLOSED` after normal implementation merge, task-focused hosted-Windows validation, current-main non-regression validation, and durable reconciliation.

## Implementation and repaired acceptance

Implementation PR #196 (`worker/fccd-p08-004-conpty-terminal-host`) converged on exact candidate `218cc7d93fafb690a5e2cf7192970bf6be0caa1f`.

The final implementation provides the Application-owned ConPTY contracts and Windows host with native-safe dimensions, immutable fully-qualified launch requests, `CreatePseudoConsole` / `ResizePseudoConsole`, `STARTUPINFOEX`, atomic `PROC_THREAD_ATTRIBUTE_JOB_LIST` ownership, a private kill-on-close Job Object, isolated child standard handles through `STARTF_USESTDHANDLES` with null std handles and `bInheritHandles=false`, interactive byte streams, deterministic Windows quoting, root completion, live resize, and async disposal that owns only the launched process tree.

During convergence the permanent hosted-Windows gate exposed real issues rather than treating them as owner-only. The implementation was repaired so a plain no-argument CMD profile does not inherit redirected CI standard handles and exit prematurely. The validator itself was then hardened to cover missing executable/working-directory paths, pre-cancelled launch, actual filesystem-proven terminal input execution, independently proven child output, live resize, clean exit/EOF, Unicode/space-containing paths, owner-data preservation, disposal of root plus owned descendant, and preservation of an unrelated sentinel process. Reflection/PowerShell harness defects found by CI were repaired and rerun rather than waived.

## Exact implementation-head validation

Exact candidate `218cc7d93fafb690a5e2cf7192970bf6be0caa1f` passed:

- P08-004 ConPTY Terminal Host run `34119217478` / #29 — **SUCCESS**.
- Windows CI run `34119217635` / #534 — **SUCCESS**.
- P06-007 Workspace Search run `34119217493` / #263 — **SUCCESS**.
- P06-008 Large Workspace Safeguards run `34119217492` / #247 — **SUCCESS**.

The focused run included Release build with zero warnings/errors, 3/3 task contract tests, launch negative/cancellation paths, exact no-argument CMD launch/input/output/resize/clean-exit, owned-descendant cleanup, and unrelated-process isolation.

## Normal integration and canonical-main verification

PR #196 was normally merged as `da9caf9fb1542b7e080ddd768b77f73e653bb08b`.

On that exact merge SHA:

- P08-004 ConPTY Terminal Host run `34119961238` / #30 — **SUCCESS**.
- P06-007 Workspace Search run `34119961241` / #264 — **SUCCESS**.
- P06-008 Large Workspace Safeguards run `34119961210` / #248 — **SUCCESS**.
- Windows CI run `34119961204` / #535 was **CANCELLED**, not PASS, because a later main push activated the workflow's `cancel-in-progress` concurrency policy. Before cancellation the Windows Release baseline and P05-005/P05-006/P05-007 validators had completed successfully. This cancelled run is deliberately not counted as closure evidence.

A mistaken P08-004 reconciliation placeholder was immediately reverted. The resulting canonical main `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68` has repository tree `baded42ef7666d85016c1788d92a50b412744d83`, exactly the same tree as merge `da9caf9fb1542b7e080ddd768b77f73e653bb08b`; the net-zero placeholder/revert changed no product, test, validator, workflow, or documentation bytes relative to the accepted implementation merge.

Current canonical main `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68` then passed:

- Windows CI run `34120686533` / #537 — **SUCCESS**.
- P06-007 Workspace Search run `34120686443` / #266 — **SUCCESS**.
- P06-008 Large Workspace Safeguards run `34120686425` / #250 — **SUCCESS**.

The P08-004 focused workflow did not retrigger for the net-zero placeholder/revert because its path filter covers only task-owned implementation/test/validator/workflow/documentation paths. The exact repository tree remained identical to the merge SHA already proven by focused run #30.

## Reconciliation

This reconciliation changes only durable governance/evidence state for the already-integrated task:

- `CURRENT_PHASE.md`: P08-004 becomes `CLOSED` and its integration provenance is recorded.
- `PROJECT_CONTROL.md`: current P08 narrative records P08-004 integrated closure.
- `docs/TASK_LEDGER.md`: P08-004 becomes `CLOSED` and current legal work no longer points at merged PR #196.
- this evidence file records exact implementation, merge, current-main, and cancellation provenance.

P08 remains `IN_PROGRESS`; only `FCCD-P08-007` and `FCCD-P08-008` remain PENDING, and `PHASE_EXIT_GATE=NOT_RUN`. P09/P14 and later implementation remain prohibited until P08 is truthfully closed. The owner-last queue remains exactly the existing two release blockers (`OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET`), and `VERIFIED_FINAL_COMPLETE=false` remains unchanged.
""", encoding="utf-8")

# Fail closed if task/phase/release invariants were accidentally weakened.
current = Path("CURRENT_PHASE.md").read_text(encoding="utf-8")
ledger = Path("docs/TASK_LEDGER.md").read_text(encoding="utf-8")
if "CURRENT_PHASE: P08" not in current or "CURRENT_PHASE_STATE: IN_PROGRESS" not in current:
    raise SystemExit("P08 current-phase invariant changed")
if "PHASE_EXIT_GATE: NOT_RUN" not in current:
    raise SystemExit("P08 exit gate was changed")
if "KNOWN_RELEASE_BLOCKERS: 2" not in current:
    raise SystemExit("owner-last release blocker count changed")
if "VERIFIED_FINAL_COMPLETE: false" not in current:
    raise SystemExit("VERIFIED_FINAL_COMPLETE was changed")
if "| FCCD-P08-004 | ConPTY terminal host | CLOSED |" not in ledger:
    raise SystemExit("P08-004 ledger closure missing")
if "| FCCD-P08-007 | Interactive terminal UX | PENDING |" not in ledger or "| FCCD-P08-008 | Process/terminal safety tests | PENDING |" not in ledger:
    raise SystemExit("later P08 task state changed unexpectedly")

# Remove temporary orchestration before the durable reconciliation commit.
Path(HELPER).unlink()
Path(WORKFLOW).unlink()

status = run("git", "status", "--porcelain", capture=True)
changed = set()
for line in status.splitlines():
    if not line.strip():
        continue
    changed.add(line[3:])
expected = {
    "CURRENT_PHASE.md",
    "PROJECT_CONTROL.md",
    "docs/TASK_LEDGER.md",
    EVIDENCE,
    HELPER,
    WORKFLOW,
}
if changed != expected:
    raise SystemExit(f"unexpected reconciliation scope: {sorted(changed)}")

run("git", "config", "user.name", "github-actions[bot]")
run("git", "config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com")
run("git", "add", "-A")
run("git", "commit", "-m", "Reconcile integrated FCCD-P08-004 closure")
run("git", "push", "origin", f"HEAD:{BRANCH}")
