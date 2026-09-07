from pathlib import Path
import re

BASE = "e43ef53eb8b7826e8541875576a5fa097c5c81a4"
EVIDENCE = Path("evidence/phases/P08/P08_006_INTEGRATED_RECONCILIATION_2026-09-07.md")


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one match, found {count}: {old[:160]!r}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8")


def regex_once(path: str, pattern: str, replacement: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    updated, count = re.subn(pattern, replacement, text, count=1, flags=re.S)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one regex match, found {count}: {pattern!r}")
    p.write_text(updated, encoding="utf-8")


# CURRENT_PHASE: close only P08-006 and append exact implementation/integration provenance.
replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P08-006` — Optional Git Bash/WSL detection — PENDING.",
    "- `FCCD-P08-006` — Optional Git Bash/WSL detection — CLOSED.",
)

provenance = """## P08-006 integration provenance\n\n- Task: `FCCD-P08-006 — Optional Git Bash/WSL detection` — CLOSED.\n- Implementation PR: #197 (`worker/fccd-p08-006-optional-shell-detection`).\n- Exact implementation candidate: `72f313ab27a54a0188ee31faea69f00aaeb0f0c9`.\n- Exact implementation-head Windows CI: run `34083435776` / #474 — SUCCESS.\n- Exact implementation-head P06-007 Workspace Search: run `34083435742` / #203 — SUCCESS.\n- Exact implementation-head P06-008 Large Workspace Safeguards: run `34083435765` / #187 — SUCCESS.\n- Exact implementation-head P08-006 Optional Shell Detection: run `34083435787` / #6 — SUCCESS.\n- Normal implementation merge / accepted main: `e43ef53eb8b7826e8541875576a5fa097c5c81a4`.\n- Exact accepted-main Windows CI: run `34084399929` / #476 — SUCCESS.\n- Exact accepted-main P06-007 Workspace Search: run `34084399920` / #205 — SUCCESS.\n- Exact accepted-main P06-008 Large Workspace Safeguards: run `34084399959` / #189 — SUCCESS.\n- Exact accepted-main P08-006 Optional Shell Detection: run `34084399934` / #7 — SUCCESS.\n- Integrated evidence: `evidence/phases/P08/P08_006_INTEGRATED_RECONCILIATION_2026-09-07.md`.\n- Evidence is cloud/hosted-Windows read-only shell-discovery evidence only. No process launch, WSL distribution enumeration/startup, environment/registry/filesystem mutation, owner-only evidence, P08 phase closure, P09/P14 authorization, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is claimed.\n\n"""
replace_once("CURRENT_PHASE.md", "## P07 cloud task inventory\n", provenance + "## P07 cloud task inventory\n")

# PROJECT_CONTROL: reconcile only P08-006 without advancing phase or changing owner-last state.
project_new = """P08 — Terminal/process supervision — is now the single active cloud implementation/convergence phase. `FCCD-P08-001 — Process supervisor with owned process-tree tracking` is CLOSED after implementation PR #189, post-merge regression repair PR #190, and exact accepted-main Windows CI `34074668199`, Workspace Search `34074668196`, and Large Workspace Safeguards `34074668191` all completed SUCCESS on `ac54e739019e7264db5de3f9b26b700735924bc1`. `FCCD-P08-002 — Graceful→forced cancellation escalation` is CLOSED after implementation PR #192, post-merge regression recovery PR #193, and exact accepted-main Windows CI `34079056645`, Workspace Search `34079056639`, and Large Workspace Safeguards `34079056670` all completed SUCCESS on `4f80433830684966405c7d76aea50583ae4df75b`; the repair was limited to the bounded P05-005 hosted-Windows settlement fixture and did not weaken P08 production semantics. `FCCD-P08-006 — Optional Git Bash/WSL detection` is CLOSED after implementation PR #197 and exact accepted-main Windows CI `34084399929`, Workspace Search `34084399920`, Large Workspace Safeguards `34084399959`, and dedicated Optional Shell Detection run `34084399934` all completed SUCCESS on `e43ef53eb8b7826e8541875576a5fa097c5c81a4`. `FCCD-P08-003`, `FCCD-P08-004`, `FCCD-P08-005`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING. Workers must select dependency-valid unclaimed P08 work while preserving owned-process boundaries, bounded output, cancellation escalation, interactive terminal safety, read-only optional-shell discovery, and owner work. P09 and later implementation remain prohibited until P08 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes."""
regex_once(
    "PROJECT_CONTROL.md",
    r"P08 — Terminal/process supervision — is now the single active cloud implementation/convergence phase\..*?every normal mandatory release gate passes\.",
    project_new,
)

# TASK_LEDGER: close only the P08-006 row, add durable task evidence, and refresh next action.
replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P08-006 | Optional Git Bash/WSL detection | PENDING |",
    "| FCCD-P08-006 | Optional Git Bash/WSL detection | CLOSED |",
)

ledger_note = """`FCCD-P08-006` is CLOSED from the read-only optional Git Bash/WSL detection implementation integrated in PR #197. Exact implementation candidate `72f313ab27a54a0188ee31faea69f00aaeb0f0c9` passed Windows CI `34083435776` / #474, P06-007 Workspace Search `34083435742` / #203, P06-008 Large Workspace Safeguards `34083435765` / #187, and the dedicated P08-006 Optional Shell Detection gate `34083435787` / #6. PR #197 was normally merged as `e43ef53eb8b7826e8541875576a5fa097c5c81a4`; that exact canonical main passed Windows CI `34084399929` / #476, P06-007 Workspace Search `34084399920` / #205, P06-008 Large Workspace Safeguards `34084399959` / #189, and P08-006 Optional Shell Detection `34084399934` / #7. Coverage includes an Application-owned typed optional-shell detection contract, read-only Git-for-Windows/Git Bash discovery from standard install roots and conservative PATH-derived Git roots, read-only `%SystemRoot%/System32/wsl.exe` detection, deterministic immutable results, case-insensitive de-duplication, malformed PATH tolerance, cancellation, hosted-Windows positive/portable/negative fixtures, and owner-byte preservation. No shell/WSL process launch, WSL distribution enumeration/startup, environment/registry/filesystem mutation, P08 phase closure, P09/P14 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is claimed. Task evidence: `evidence/phases/P08/P08_006_INTEGRATED_RECONCILIATION_2026-09-07.md`. P08 remains `IN_PROGRESS`; P08-003, P08-004, P08-005, P08-007, and P08-008 remain PENDING, and the two existing owner-last release blockers remain unchanged.\n\n"""
replace_once("docs/TASK_LEDGER.md", "## P09 — External Tool Gateway\n", ledger_note + "## P09 — External Tool Gateway\n")

next_action = """## Current next action\n\n`CURRENT_PHASE = P08` is `IN_PROGRESS`. `FCCD-P08-001 — Process supervisor with owned process-tree tracking`, `FCCD-P08-002 — Graceful→forced cancellation escalation`, and `FCCD-P08-006 — Optional Git Bash/WSL detection` are CLOSED. P08-006 is accepted only after implementation PR #197 exact-head Windows CI `34083435776`, Workspace Search `34083435742`, Large Workspace Safeguards `34083435765`, and dedicated P08-006 gate `34083435787` all completed SUCCESS; PR #197 was normally merged as `e43ef53eb8b7826e8541875576a5fa097c5c81a4`, whose exact-main Windows CI `34084399929`, Workspace Search `34084399920`, Large Workspace Safeguards `34084399959`, and dedicated P08-006 gate `34084399934` all completed SUCCESS. `FCCD-P08-003`, `FCCD-P08-004`, `FCCD-P08-005`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`.\n\nP04 remains acceptance-unresolved through `FCCD-P04-008` and its one-to-one queued `OWNER-P04-008-REAL-TARGET` obligation. P05 cloud implementation remains integrated, but its standalone exit observation remains queued as `OWNER-P05-EXIT-REAL-TARGET`. Their gates remain `P04=NOT_RUN;P05=NOT_RUN`; owner-last scheduling does not waive either obligation or permit release.\n\nThe next legal cloud action is to re-read live P08 claims and recover any legitimate integration-pending current-phase work first; otherwise select the highest-value dependency-valid unclaimed P08 task. Do not steal active P08-003/P08-004/P08-005 work, do not skip to P09/P14 or any later phase, and do not fabricate owner/manual evidence.\n"""
regex_once("docs/TASK_LEDGER.md", r"## Current next action\n\n.*\Z", next_action)

# Durable evidence.
EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
EVIDENCE.write_text("""# P08-006 — Integrated reconciliation\n\n**Task:** `FCCD-P08-006 — Optional Git Bash/WSL detection`  \n**Canonical status:** `CLOSED`  \n**Current phase after reconciliation:** `P08 — Terminal/process supervision` / `IN_PROGRESS`  \n**Phase exit gate:** `NOT_RUN`\n\n## Implementation\n\nPR #197 implemented an Application-owned typed optional-shell detection contract and a Windows read-only detector. The exact implementation candidate was `72f313ab27a54a0188ee31faea69f00aaeb0f0c9`. Detection is intentionally conservative: Git Bash is discovered only through standard Git-for-Windows install roots plus PATH entries that can be related back to a Git root, and WSL availability is represented only by the presence of `%SystemRoot%/System32/wsl.exe`. No detector path launches a shell, starts/enumerates WSL distributions, writes files, mutates environment variables, writes the registry, or uses the network.\n\nExact implementation-head gates completed SUCCESS:\n\n- Windows CI #474 / run `34083435776`\n- P06-007 Workspace Search #203 / run `34083435742`\n- P06-008 Large Workspace Safeguards #187 / run `34083435765`\n- P08-006 Optional Shell Detection #6 / run `34083435787`\n\nPR #197 was normally merged as `e43ef53eb8b7826e8541875576a5fa097c5c81a4`, preserving exact candidate ancestry.\n\n## Exact accepted-main verification\n\nThe exact resulting main `e43ef53eb8b7826e8541875576a5fa097c5c81a4` passed the complete applicable gate set:\n\n- Windows CI #476 / run `34084399929` — SUCCESS\n- P06-007 Workspace Search #205 / run `34084399920` — SUCCESS\n- P06-008 Large Workspace Safeguards #189 / run `34084399959` — SUCCESS\n- P08-006 Optional Shell Detection #7 / run `34084399934` — SUCCESS\n\nThis is the accepted canonical integration baseline for P08-006.\n\n## Closure boundary\n\nP08-006 is therefore CLOSED. P08 remains `IN_PROGRESS`; P08-003, P08-004, P08-005, P08-007, and P08-008 remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`. P09, P14, and later phases remain prohibited.\n\nNo new owner-only evidence is required by this task. The existing release-blocking owner queue remains unchanged:\n\n- `OWNER-P04-008-REAL-TARGET`\n- `OWNER-P05-EXIT-REAL-TARGET`\n\n`KNOWN_RELEASE_BLOCKERS=2`, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.\n""", encoding="utf-8")

# Fail closed on governance/safety invariants.
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
if "| FCCD-P08-006 | Optional Git Bash/WSL detection | CLOSED |" not in ledger:
    raise SystemExit("TASK_LEDGER.md: P08-006 was not closed")
for task in (3, 4, 5, 7, 8):
    row = f"| FCCD-P08-00{task} |"
    if row not in ledger:
        raise SystemExit(f"TASK_LEDGER.md: missing P08-00{task} row")
    current = ledger.split(row, 1)[1].splitlines()[0]
    if "PENDING" not in current:
        raise SystemExit(f"TASK_LEDGER.md: P08-00{task} must remain PENDING")
if "| FCCD-P14-005 | Rate-limit detection/classification | PENDING |" not in ledger:
    raise SystemExit("TASK_LEDGER.md: P14-005 must remain PENDING")

print(f"P08-006 guarded reconciliation prepared from accepted main {BASE}")
