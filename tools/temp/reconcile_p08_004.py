from pathlib import Path
import re
import subprocess

PRODUCT_MERGE = "da9caf9fb1542b7e080ddd768b77f73e653bb08b"
CURRENT_MAIN = "f86e8bc5e08b4def96fc50fc2cc3d3819621aa68"
PRODUCT_TREE = "baded42ef7666d85016c1788d92a50b412744d83"
EVIDENCE = Path("evidence/phases/P08/P08_004_INTEGRATED_RECONCILIATION_2026-09-07.md")


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one match, found {count}: {old[:180]!r}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8")


def regex_once(path: str, pattern: str, replacement: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    updated, count = re.subn(pattern, replacement, text, count=1, flags=re.S)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one regex match, found {count}: {pattern!r}")
    p.write_text(updated, encoding="utf-8")


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], text=True).strip()


# Provenance guard: the current canonical no-content repair head resolves to the
# exact same repository tree as the normally merged P08-004 product candidate.
if git("rev-parse", f"{PRODUCT_MERGE}^{{tree}}") != PRODUCT_TREE:
    raise SystemExit("P08-004 product merge tree no longer matches recorded provenance")
if git("rev-parse", f"{CURRENT_MAIN}^{{tree}}") != PRODUCT_TREE:
    raise SystemExit("Current accepted main tree does not match the validated P08-004 product tree")

# CURRENT_PHASE: close P08-004 only and append exact provenance.
replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P08-004` — ConPTY terminal host — PENDING.",
    "- `FCCD-P08-004` — ConPTY terminal host — CLOSED.",
)

provenance = """## P08-004 integration provenance

- Task: `FCCD-P08-004 — ConPTY terminal host` — CLOSED.
- Implementation PR: #196 (`worker/fccd-p08-004-conpty-terminal-host`).
- Exact implementation candidate: `218cc7d93fafb690a5e2cf7192970bf6be0caa1f`.
- Exact implementation-head Windows CI: run `34119217635` / #534 — SUCCESS.
- Exact implementation-head P06-007 Workspace Search: run `34119217493` / #263 — SUCCESS.
- Exact implementation-head P06-008 Large Workspace Safeguards: run `34119217492` / #247 — SUCCESS.
- Exact implementation-head P08-004 ConPTY Terminal Host: run `34119217478` / #29 — SUCCESS.
- Normal implementation merge: `da9caf9fb1542b7e080ddd768b77f73e653bb08b`.
- Exact implementation-merge P08-004 ConPTY Terminal Host: run `34119961238` / #30 — SUCCESS.
- Exact implementation-merge P06-007 Workspace Search: run `34119961241` / #264 — SUCCESS.
- Exact implementation-merge P06-008 Large Workspace Safeguards: run `34119961210` / #248 — SUCCESS.
- The implementation-merge Windows CI run `34119961204` / #535 was cancelled by a later main push and is not counted as PASS evidence.
- Current accepted main `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68` is two commits ahead of the implementation merge with zero changed files and the identical Git tree `baded42ef7666d85016c1788d92a50b412744d83`.
- Exact current-main Windows CI: run `34120686533` / #537 — SUCCESS.
- Exact current-main P06-007 Workspace Search: run `34120686443` / #266 — SUCCESS.
- Exact current-main P06-008 Large Workspace Safeguards: run `34120686425` / #250 — SUCCESS.
- Integrated evidence: `evidence/phases/P08/P08_004_INTEGRATED_RECONCILIATION_2026-09-07.md`.
- Evidence is cloud/hosted-Windows ConPTY evidence only. No owner-only evidence is added; P08 remains `IN_PROGRESS`, P08-007 and P08-008 remain PENDING, P09/P15 and later phases remain prohibited, and `VERIFIED_FINAL_COMPLETE` remains false.

"""
replace_once("CURRENT_PHASE.md", "## P08-005 integration provenance\n", provenance + "## P08-005 integration provenance\n")

# PROJECT_CONTROL: change only the P08-004 pending clause, preserving any owner
# reconciliation state and all unrelated phase text.
project_old = "`FCCD-P08-004`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING."
project_new = (
    "`FCCD-P08-004 — ConPTY terminal host` is CLOSED after implementation PR #196 exact candidate "
    "`218cc7d93fafb690a5e2cf7192970bf6be0caa1f` passed Windows CI `34119217635`, Workspace Search "
    "`34119217493`, Large Workspace Safeguards `34119217492`, and dedicated ConPTY run `34119217478`; "
    "PR #196 was normally merged as `da9caf9fb1542b7e080ddd768b77f73e653bb08b`, whose dedicated ConPTY run "
    "`34119961238`, Workspace Search `34119961241`, and Large Workspace Safeguards `34119961210` passed. "
    "After the merge-head Windows CI was cancelled by a newer no-content main push, current main "
    "`f86e8bc5e08b4def96fc50fc2cc3d3819621aa68` retained the identical product tree "
    "`baded42ef7666d85016c1788d92a50b412744d83` and passed Windows CI `34120686533`, Workspace Search "
    "`34120686443`, and Large Workspace Safeguards `34120686425`. `FCCD-P08-007` and `FCCD-P08-008` remain PENDING."
)
replace_once("PROJECT_CONTROL.md", project_old, project_new)

# TASK_LEDGER: close only the task row and add durable evidence.
replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P08-004 | ConPTY terminal host | PENDING |",
    "| FCCD-P08-004 | ConPTY terminal host | CLOSED |",
)

ledger_note = """`FCCD-P08-004` is CLOSED from the production Windows ConPTY terminal host integrated in PR #196. Exact implementation candidate `218cc7d93fafb690a5e2cf7192970bf6be0caa1f` passed Windows CI `34119217635` / #534, P06-007 Workspace Search `34119217493` / #263, P06-008 Large Workspace Safeguards `34119217492` / #247, and dedicated P08-004 ConPTY Terminal Host `34119217478` / #29. PR #196 was normally merged as `da9caf9fb1542b7e080ddd768b77f73e653bb08b`; exact implementation-merge ConPTY `34119961238` / #30, Workspace Search `34119961241` / #264, and Large Workspace Safeguards `34119961210` / #248 all passed. The merge-head Windows CI `34119961204` / #535 was cancelled by a later main push and is explicitly not counted as PASS. Current main `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68` is two commits ahead with zero changed files and the same Git tree `baded42ef7666d85016c1788d92a50b412744d83`; that exact current main passed Windows CI `34120686533` / #537, Workspace Search `34120686443` / #266, and Large Workspace Safeguards `34120686425` / #250. Coverage includes Application-owned terminal contracts, native ConPTY creation and resize, atomic child ownership through a private kill-on-close Job Object, isolated standard handles, real CMD-profile input/output/resize/clean-exit validation, negative/cancellation paths, deterministic async disposal, owned-descendant cleanup, and unrelated-process isolation. Task evidence: `evidence/phases/P08/P08_004_INTEGRATED_RECONCILIATION_2026-09-07.md`. No owner-only evidence is required or added. P08 remains `IN_PROGRESS`; P08-007 and P08-008 remain PENDING; P09/P15 and later phases remain prohibited; `VERIFIED_FINAL_COMPLETE=false`.

"""
replace_once("docs/TASK_LEDGER.md", "## P09 — External Tool Gateway\n", ledger_note + "## P09 — External Tool Gateway\n")

next_action = """## Current next action

`CURRENT_PHASE = P08` is `IN_PROGRESS`. `FCCD-P08-001 — Process supervisor with owned process-tree tracking`, `FCCD-P08-002 — Graceful→forced cancellation escalation`, `FCCD-P08-003 — Bounded streaming log pipeline`, `FCCD-P08-004 — ConPTY terminal host`, `FCCD-P08-005 — PowerShell/CMD profiles`, and `FCCD-P08-006 — Optional Git Bash/WSL detection` are CLOSED. P08-004 is accepted after implementation PR #196 exact candidate `218cc7d93fafb690a5e2cf7192970bf6be0caa1f` passed Windows CI `34119217635`, Workspace Search `34119217493`, Large Workspace Safeguards `34119217492`, and dedicated ConPTY `34119217478`; normal merge `da9caf9fb1542b7e080ddd768b77f73e653bb08b` passed exact-merge dedicated ConPTY `34119961238`, Workspace Search `34119961241`, and Large Workspace Safeguards `34119961210`. Its merge-head Windows CI `34119961204` was cancelled by a later no-content push and is not counted as PASS; current main `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68` has the identical product tree and passed Windows CI `34120686533`, Workspace Search `34120686443`, and Large Workspace Safeguards `34120686425`. `FCCD-P08-007` and `FCCD-P08-008` remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`.

P04/P05 owner-last status remains governed exclusively by the canonical owner queue and current control files; this P08-004 reconciliation does not add, remove, pass, or waive any owner obligation.

The next legal cloud action is to re-read live P08 claims and recover any legitimate integration-pending current-phase work first; otherwise select the highest-value dependency-valid unclaimed P08 task. If no P08 claim exists, `FCCD-P08-007 — Interactive terminal UX` is the next implementation task before final P08 safety convergence. Do not skip to P09/P15 or any later phase, and do not fabricate owner/manual evidence.
"""
regex_once("docs/TASK_LEDGER.md", r"## Current next action\n\n.*\Z", next_action)

# Durable evidence.
EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
EVIDENCE.write_text("""# P08-004 — Integrated reconciliation

**Task:** `FCCD-P08-004 — ConPTY terminal host`  
**Canonical status:** `CLOSED`  
**Current phase after reconciliation:** `P08 — Terminal/process supervision` / `IN_PROGRESS`  
**Phase exit gate:** `NOT_RUN`

## Implementation

PR #196 implemented the Application-owned ConPTY contracts and production Windows host. The exact implementation candidate was `218cc7d93fafb690a5e2cf7192970bf6be0caa1f`.

The implementation uses native ConPTY creation/resize, `STARTUPINFOEX`, atomic `PROC_THREAD_ATTRIBUTE_JOB_LIST` assignment to a private `KILL_ON_JOB_CLOSE` Job Object, isolated standard handles, managed interactive input/output streams, deterministic completion/disposal, and owned process-tree cleanup without targeting unrelated processes. Shell selection remains outside P08-004; the hosted acceptance uses the exact resolved CMD profile contract.

Exact implementation-head gates completed SUCCESS:

- Windows CI #534 / run `34119217635`
- P06-007 Workspace Search #263 / run `34119217493`
- P06-008 Large Workspace Safeguards #247 / run `34119217492`
- P08-004 ConPTY Terminal Host #29 / run `34119217478`

The dedicated hosted-Windows ConPTY fixture proves negative/cancellation handling, a real CMD-profile launch, actual injected-input execution, independently proven output, live resize, clean exit/output EOF, owner-data preservation, dispose-time owned-descendant termination, and unrelated-process isolation.

PR #196 was normally merged as `da9caf9fb1542b7e080ddd768b77f73e653bb08b`.

## Canonical-main verification

On exact implementation-merge main `da9caf9fb1542b7e080ddd768b77f73e653bb08b`:

- P08-004 ConPTY Terminal Host #30 / run `34119961238` — SUCCESS
- P06-007 Workspace Search #264 / run `34119961241` — SUCCESS
- P06-008 Large Workspace Safeguards #248 / run `34119961210` — SUCCESS
- Windows CI #535 / run `34119961204` — CANCELLED after a later main push; it is not counted as PASS evidence.

A temporary no-content repository-history correction then left canonical main at `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68`. GitHub compare reports that head as two commits ahead of `da9caf9f...` with zero changed files, and both commits resolve to the exact same Git tree `baded42ef7666d85016c1788d92a50b412744d83`. The current exact main then completed the full applicable non-regression set:

- Windows CI #537 / run `34120686533` — SUCCESS
- P06-007 Workspace Search #266 / run `34120686443` — SUCCESS
- P06-008 Large Workspace Safeguards #250 / run `34120686425` — SUCCESS

The content accepted by the successful dedicated ConPTY run and by the successful current-main non-regression runs is therefore the same product tree. No cancelled run is represented as successful evidence.

## Closure boundary

P08-004 is therefore CLOSED. P08 remains `IN_PROGRESS`; P08-007 and P08-008 remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`. P09, P15, and later phases remain prohibited until P08 closes through normal governance.

P08-004 requires no owner-only/manual evidence: its Windows-specific acceptance is covered by the real GitHub-hosted Windows ConPTY fixture. This reconciliation does not modify `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md`, does not waive any existing owner obligation, does not claim release eligibility, and does not set `VERIFIED_FINAL_COMPLETE=true`.
""", encoding="utf-8")

# Fail closed on phase/task/safety invariants.
current = Path("CURRENT_PHASE.md").read_text(encoding="utf-8")
project = Path("PROJECT_CONTROL.md").read_text(encoding="utf-8")
ledger = Path("docs/TASK_LEDGER.md").read_text(encoding="utf-8")
queue = Path("docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md").read_text(encoding="utf-8")

for text, name in ((current, "CURRENT_PHASE.md"), (project, "PROJECT_CONTROL.md")):
    for required in (
        "CURRENT_PHASE: P08",
        "CURRENT_PHASE_STATE: IN_PROGRESS",
        "PHASE_EXIT_GATE: NOT_RUN",
        "VERIFIED_FINAL_COMPLETE: false",
    ):
        if required not in text:
            raise SystemExit(f"{name}: missing invariant {required!r}")

if "| FCCD-P08-004 | ConPTY terminal host | CLOSED |" not in ledger:
    raise SystemExit("TASK_LEDGER.md: P08-004 was not closed")
for task, title in (("007", "Interactive terminal UX"), ("008", "Process/terminal safety tests")):
    row = f"| FCCD-P08-{task} | {title} | PENDING |"
    if row not in ledger:
        raise SystemExit(f"TASK_LEDGER.md: {row} must remain PENDING")
if "| FCCD-P15-003 | Interrupted agent-run recovery | PENDING |" not in ledger:
    raise SystemExit("TASK_LEDGER.md: future P15-003 must remain PENDING")
if "OWNER-P04-008-REAL-TARGET" not in queue or "OWNER-P05-EXIT-REAL-TARGET" not in queue:
    raise SystemExit("Owner queue provenance changed unexpectedly during P08-004 reconciliation")
if "P08-004 requires no owner-only/manual evidence" not in EVIDENCE.read_text(encoding="utf-8"):
    raise SystemExit("P08-004 evidence boundary missing")

print(f"P08-004 guarded reconciliation prepared from current main {CURRENT_MAIN}")
