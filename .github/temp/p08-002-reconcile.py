from pathlib import Path
import re

BASE = "4f80433830684966405c7d76aea50583ae4df75b"
EVIDENCE = Path("evidence/phases/P08/P08_002_INTEGRATED_RECONCILIATION_2026-09-07.md")


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one match, found {count}: {old[:120]!r}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8")


def regex_once(path: str, pattern: str, replacement: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    updated, count = re.subn(pattern, replacement, text, count=1, flags=re.S)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one regex match, found {count}: {pattern!r}")
    p.write_text(updated, encoding="utf-8")


# CURRENT_PHASE: close only P08-002 and add exact integration/repair provenance.
replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P08-002` — Graceful→forced cancellation escalation — PENDING.",
    "- `FCCD-P08-002` — Graceful→forced cancellation escalation — CLOSED.",
)

provenance = """## P08-002 integration provenance\n\n- Task: `FCCD-P08-002 — Graceful→forced cancellation escalation` — CLOSED.\n- Implementation PR: #192 (`worker/fccd-p08-002-cancellation-escalation`).\n- Exact implementation candidate: `978d71baa75a21cb55a8c2ef4db546097e44b6c4`.\n- Exact implementation-head Windows CI: run `34077180195` / #450 — SUCCESS.\n- Exact implementation-head P06-007 Workspace Search: run `34077180198` / #179 — SUCCESS.\n- Exact implementation-head P06-008 Large Workspace Safeguards: run `34077180215` / #163 — SUCCESS.\n- Initial normal implementation merge: `3055b9f27baa047b3217b3256f2b229f78e53981`.\n- Post-merge convergence correctly remained open because Windows CI #451 exposed a real P05-005 hosted-Windows settlement-deadline regression after the full Release build/tests had passed; no task closure was claimed on that regressed main.\n- Recovery PR: #193 (`repair/p08-002-p05-005-settlement-fixture`) fixed only the bounded P05-005 settlement fixture/tolerance and diagnostics; it did not change P08 production cancellation semantics or weaken the settlement assertion.\n- Exact recovery candidate: `636b4df95d4fdd74fb8fb0cb6f9e1dd84f5940ce`.\n- Exact recovery-head Windows CI: run `34078491329` / #452 — SUCCESS.\n- Exact recovery-head P06-007 Workspace Search: run `34078491330` / #181 — SUCCESS.\n- Exact recovery-head P06-008 Large Workspace Safeguards: run `34078491338` / #165 — SUCCESS.\n- Recovery normal merge / accepted main: `4f80433830684966405c7d76aea50583ae4df75b`.\n- Exact accepted-main Windows CI: run `34079056645` / #453 — SUCCESS, including the previously failing P05-005 executable settlement validator.\n- Exact accepted-main P06-007 Workspace Search: run `34079056639` / #182 — SUCCESS.\n- Exact accepted-main P06-008 Large Workspace Safeguards: run `34079056670` / #166 — SUCCESS.\n- Integrated evidence: `evidence/phases/P08/P08_002_INTEGRATED_RECONCILIATION_2026-09-07.md`.\n- Evidence is cloud/Windows-CI process-supervision evidence only. No owner-only evidence is added; P08 remains IN_PROGRESS, P08-003..008 remain PENDING, P09+ remain prohibited, and `VERIFIED_FINAL_COMPLETE` remains false.\n\n"""
replace_once("CURRENT_PHASE.md", "## P07 cloud task inventory\n", provenance + "## P07 cloud task inventory\n")

# PROJECT_CONTROL: reconcile current P08 paragraph without advancing the phase or changing owner-last state.
project_new = """P08 — Terminal/process supervision — is now the single active cloud implementation/convergence phase. `FCCD-P08-001 — Process supervisor with owned process-tree tracking` is CLOSED after implementation PR #189, post-merge regression repair PR #190, and exact accepted-main Windows CI `34074668199`, Workspace Search `34074668196`, and Large Workspace Safeguards `34074668191` all completed SUCCESS on `ac54e739019e7264db5de3f9b26b700735924bc1`. `FCCD-P08-002 — Graceful→forced cancellation escalation` is CLOSED after implementation PR #192, post-merge regression recovery PR #193, and exact accepted-main Windows CI `34079056645`, Workspace Search `34079056639`, and Large Workspace Safeguards `34079056670` all completed SUCCESS on `4f80433830684966405c7d76aea50583ae4df75b`; the repair was limited to the bounded P05-005 hosted-Windows settlement fixture and did not weaken P08 production semantics. `FCCD-P08-003` through `FCCD-P08-008` remain PENDING. Workers must select dependency-valid unclaimed P08 work while preserving owned-process boundaries, bounded output, cancellation escalation, interactive terminal safety, and owner work. P09 and later implementation remain prohibited until P08 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes."""
regex_once(
    "PROJECT_CONTROL.md",
    r"P08 — Terminal/process supervision — is now the single active cloud implementation/convergence phase\..*?every normal mandatory release gate passes\.",
    project_new,
)

# TASK_LEDGER: close only the P08-002 row and refresh the live next-action block.
replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P08-002 | Graceful→forced cancellation escalation | PENDING |",
    "| FCCD-P08-002 | Graceful→forced cancellation escalation | CLOSED |",
)
next_action = """## Current next action\n\n`CURRENT_PHASE = P08` is `IN_PROGRESS`. `FCCD-P08-001 — Process supervisor with owned process-tree tracking` and `FCCD-P08-002 — Graceful→forced cancellation escalation` are CLOSED. P08-002 is accepted only after implementation PR #192, discovery of the post-merge P05-005 settlement regression, recovery PR #193, and exact accepted-main Windows CI `34079056645`, Workspace Search `34079056639`, and Large Workspace Safeguards `34079056670` all completed SUCCESS on `4f80433830684966405c7d76aea50583ae4df75b`. `FCCD-P08-003` through `FCCD-P08-008` remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`.\n\nP04 remains acceptance-unresolved through `FCCD-P04-008` and its one-to-one queued `OWNER-P04-008-REAL-TARGET` obligation. P05 cloud implementation remains integrated, but its standalone exit observation remains queued as `OWNER-P05-EXIT-REAL-TARGET`. Their gates remain `P04=NOT_RUN;P05=NOT_RUN`; owner-last scheduling does not waive either obligation or permit release.\n\nThe next legal cloud action is to re-read live claims and recover any legitimate integration-pending P08 work first; otherwise select the highest-value dependency-valid unclaimed P08 task, nominally `FCCD-P08-003 — Bounded streaming log pipeline` if it remains unclaimed. Do not skip to P09, P14, or any later phase and do not fabricate owner/manual evidence.\n"""
regex_once("docs/TASK_LEDGER.md", r"## Current next action\n\n.*\Z", next_action)

# Durable evidence records the full implementation -> regression -> repair -> accepted-main chain.
EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
EVIDENCE.write_text("""# P08-002 — Integrated reconciliation\n\n**Task:** `FCCD-P08-002 — Graceful→forced cancellation escalation`  \n**Canonical status:** `CLOSED`  \n**Current phase after reconciliation:** `P08 — Terminal/process supervision` / `IN_PROGRESS`  \n**Phase exit gate:** `NOT_RUN`\n\n## Implementation\n\nPR #192 implemented the bounded graceful-to-forced cancellation layer behind `IProcessCancellationEscalator`. The exact implementation candidate was `978d71baa75a21cb55a8c2ef4db546097e44b6c4`. It preserves the P08-001 owned-process boundary: graceful stop is caller-specific and bounded, forced cleanup is restricted to the owned-tree termination primitive, pre-cancelled calls fail before mutation, and once cancellation begins cleanup is non-abandonable so owned descendants are not orphaned.\n\nExact implementation-head gates all completed SUCCESS:\n\n- Windows CI #450 / run `34077180195`\n- P06-007 Workspace Search #179 / run `34077180198`\n- P06-008 Large Workspace Safeguards #163 / run `34077180215`\n\nPR #192 was normally merged as `3055b9f27baa047b3217b3256f2b229f78e53981`.\n\n## Post-merge regression and recovery\n\nThe first implementation merge was not reconciled closed. Exact-main Windows CI #451 exposed a real cloud-repairable regression in the permanent P05-005 executable validator: the full Release build/tests passed, but the hosted-Windows fixed 10-second settlement deadline could expire before complete lifecycle settlement. Forward convergence correctly stopped.\n\nPR #193 (`repair/p08-002-p05-005-settlement-fixture`) repaired that regression by keeping the same full-settlement assertion while using a bounded 30-second hosted-Windows tolerance and adding lifecycle/control diagnostics on timeout. The repair did **not** change `TaskExecutionState`, the P08-002 production cancellation contract, owned-tree safety, or owner-last governance.\n\nExact recovery candidate `636b4df95d4fdd74fb8fb0cb6f9e1dd84f5940ce` passed:\n\n- Windows CI #452 / run `34078491329`\n- P06-007 Workspace Search #181 / run `34078491330`\n- P06-008 Large Workspace Safeguards #165 / run `34078491338`\n\nPR #193 was normally merged as `4f80433830684966405c7d76aea50583ae4df75b`.\n\n## Exact accepted-main verification\n\nThe exact resulting main `4f80433830684966405c7d76aea50583ae4df75b` passed the complete permanent gate set:\n\n- Windows CI #453 / run `34079056645` — SUCCESS, including the formerly failing P05-005 executable settlement validator\n- P06-007 Workspace Search #182 / run `34079056639` — SUCCESS\n- P06-008 Large Workspace Safeguards #166 / run `34079056670` — SUCCESS\n\nThis is the accepted canonical integration baseline for P08-002.\n\n## Closure boundary\n\nP08-002 is therefore CLOSED. P08 remains `IN_PROGRESS`; P08-003 through P08-008 remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`. P09 and later phases remain prohibited.\n\nNo new owner-only evidence is required by this task. The existing release-blocking owner queue remains unchanged:\n\n- `OWNER-P04-008-REAL-TARGET`\n- `OWNER-P05-EXIT-REAL-TARGET`\n\n`KNOWN_RELEASE_BLOCKERS=2`, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.\n""", encoding="utf-8")

# Governance/safety invariants: fail rather than silently weaken or advance state.
for path in ("CURRENT_PHASE.md", "PROJECT_CONTROL.md"):
    text = Path(path).read_text(encoding="utf-8")
    required = [
        "CURRENT_PHASE: P08",
        "CURRENT_PHASE_STATE: IN_PROGRESS",
        "PHASE_EXIT_GATE: NOT_RUN",
        "OWNER-P04-008-REAL-TARGET",
        "OWNER-P05-EXIT-REAL-TARGET",
        "VERIFIED_FINAL_COMPLETE: false",
    ]
    missing = [item for item in required if item not in text]
    if missing:
        raise SystemExit(f"{path}: reconciliation invariant missing: {missing}")

ledger = Path("docs/TASK_LEDGER.md").read_text(encoding="utf-8")
if "| FCCD-P08-002 | Graceful→forced cancellation escalation | CLOSED |" not in ledger:
    raise SystemExit("TASK_LEDGER.md: P08-002 was not closed")
for task in range(3, 9):
    if f"| FCCD-P08-00{task} |" in ledger and "PENDING" not in ledger.split(f"| FCCD-P08-00{task} |", 1)[1].splitlines()[0]:
        raise SystemExit(f"TASK_LEDGER.md: P08-00{task} must remain PENDING")

print(f"P08-002 guarded reconciliation prepared from accepted main {BASE}")
