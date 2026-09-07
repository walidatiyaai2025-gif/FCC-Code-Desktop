from pathlib import Path
import re
import subprocess

BRANCH = "reconcile/fccd-p08-007-integrated-closure"


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8", newline="\n")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one literal replacement target, found {count}")
    return text.replace(old, new, 1)


def regex_once(text: str, pattern: str, replacement: str, label: str) -> str:
    updated, count = re.subn(pattern, replacement, text, count=1, flags=re.MULTILINE | re.DOTALL)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one regex replacement target, found {count}")
    return updated


# CURRENT_PHASE.md
current_path = "CURRENT_PHASE.md"
current = read(current_path)
current = replace_once(
    current,
    "LAST_RECONCILED: 2026-09-07",
    "LAST_RECONCILED: 2026-09-08",
    "current phase reconciliation date",
)
current = replace_once(
    current,
    "- `FCCD-P08-007` — Interactive terminal UX — PENDING.",
    "- `FCCD-P08-007` — Interactive terminal UX — CLOSED.",
    "current phase P08-007 row",
)
p08_007_provenance = """## P08-007 integration provenance

- Task: `FCCD-P08-007 — Interactive terminal UX` — CLOSED.
- Implementation PR: #206 (`worker/fccd-p08-007-interactive-terminal-ux`).
- Exact implementation candidate: `bebe178af5f36f97427d101c5b2e3ab2cd0ff075`.
- Exact implementation-head Windows CI: run `34161412445` / #554 — SUCCESS.
- Exact implementation-head P06-007 Workspace Search: run `34161412447` / #283 — SUCCESS.
- Exact implementation-head P06-008 Large Workspace Safeguards: run `34161412450` / #267 — SUCCESS.
- Exact implementation-head P08-007 Interactive Terminal UX: run `34161412438` / #3 — SUCCESS.
- Normal implementation merge / accepted main: `53df4d66026b0e793d7c7797a016ed654d8c3663`.
- Exact accepted-main Windows CI: run `34161963855` / #555 — SUCCESS.
- Exact accepted-main P06-007 Workspace Search: run `34161963822` / #284 — SUCCESS.
- Exact accepted-main P06-008 Large Workspace Safeguards: run `34161963867` / #268 — SUCCESS.
- Exact accepted-main P08-007 Interactive Terminal UX: run `34161963864` / #4 — SUCCESS.
- Integrated evidence: `evidence/phases/P08/P08_007_INTEGRATED_RECONCILIATION_2026-09-08.md`.
- Coverage includes Bottom Tool Panel composition without weakening the P02 shell contract, typed ConPTY shell launch, UTF-8 input/output, Ctrl+C copy-versus-interrupt semantics, Ctrl+V paste and navigation sequences, debounced resize, bounded/coalesced high-output presentation, ANSI SGR color rendering, and safe async window-close disposal. No owner-only evidence is required or added.
- P08 remains `IN_PROGRESS`; only `FCCD-P08-008` remains PENDING. `PHASE_EXIT_GATE=NOT_RUN`, P09 and later phases remain prohibited, and `VERIFIED_FINAL_COMPLETE=false`.

"""
current = replace_once(
    current,
    "## P07 cloud task inventory\n",
    p08_007_provenance + "## P07 cloud task inventory\n",
    "current phase P08-007 provenance insertion",
)
write(current_path, current)

# docs/TASK_LEDGER.md
ledger_path = "docs/TASK_LEDGER.md"
ledger = read(ledger_path)
ledger = replace_once(
    ledger,
    "| FCCD-P08-007 | Interactive terminal UX | PENDING |",
    "| FCCD-P08-007 | Interactive terminal UX | CLOSED |",
    "task ledger P08-007 row",
)
ledger_evidence = """`FCCD-P08-007` is CLOSED from the production interactive terminal UX integrated in PR #206. Exact implementation candidate `bebe178af5f36f97427d101c5b2e3ab2cd0ff075` passed Windows CI `34161412445` / #554, P06-007 Workspace Search `34161412447` / #283, P06-008 Large Workspace Safeguards `34161412450` / #267, and dedicated P08-007 Interactive Terminal UX `34161412438` / #3. PR #206 was normally merged as `53df4d66026b0e793d7c7797a016ed654d8c3663`; that exact canonical main passed Windows CI `34161963855` / #555, Workspace Search `34161963822` / #284, Large Workspace Safeguards `34161963867` / #268, and dedicated P08-007 `34161963864` / #4. Coverage includes the existing typed ConPTY host composed into the Bottom Tool Panel terminal seam without weakening the P02 contract; PowerShell/CMD plus read-only detected Git Bash/WSL selection; UTF-8 input/output; Ctrl+C selected-text copy versus ETX interrupt; clipboard paste and terminal navigation sequences; debounced resize; bounded/coalesced high-output presentation; ANSI SGR color rendering; deterministic session close; and window shutdown held until async terminal disposal completes. The task recovered its initial bottom-panel contract failure, a CA1859 analyzer failure, and an STA fixture defect without suppressing analyzers or weakening gates. Task evidence: `evidence/phases/P08/P08_007_INTEGRATED_RECONCILIATION_2026-09-08.md`. No owner-only evidence is required or added. P08 remains `IN_PROGRESS`; only P08-008 remains PENDING and `PHASE_EXIT_GATE=NOT_RUN`; P09 and later implementation remain prohibited; `VERIFIED_FINAL_COMPLETE=false`.

"""
ledger = replace_once(
    ledger,
    "## P09 — External Tool Gateway\n",
    ledger_evidence + "## P09 — External Tool Gateway\n",
    "task ledger P08-007 evidence insertion",
)
ledger = regex_once(
    ledger,
    r"## Current next action\n\n.*?(?=\n<!-- FCCD-P08-004-FINAL-CLOSURE-PROVENANCE -->)",
    """## Current next action

`CURRENT_PHASE = P08` remains `IN_PROGRESS`. `FCCD-P08-001` through `FCCD-P08-007` are now canonically CLOSED after implementation, exact-head validation, normal integration, exact-main validation, and durable reconciliation. `FCCD-P08-008 — Process/terminal safety tests` remains PENDING and `PHASE_EXIT_GATE=NOT_RUN`.

P04/P05 owner-last status remains governed exclusively by the canonical owner queue and current control files; this P08-007 reconciliation does not add, remove, pass, or waive any owner obligation.

The next legal action is a fresh live-state scan for current-phase claims, stale/integration-pending P08 work, open PRs, and exact-current-main CI. If no higher-priority P08 recovery exists, `FCCD-P08-008 — Process/terminal safety tests` is the final dependency-valid P08 task. P09 and later implementation remain prohibited until P08-008 is CLOSED and the P08 phase exit gate and closure evidence are formally integrated and exact-main verified.
""",
    "task ledger current next action",
)
write(ledger_path, ledger)

# PROJECT_CONTROL.md
control_path = "PROJECT_CONTROL.md"
control = read(control_path)
control = replace_once(
    control,
    "`FCCD-P08-007` and `FCCD-P08-008` remain PENDING.",
    "`FCCD-P08-007 — Interactive terminal UX` is CLOSED after PR #206 exact candidate `bebe178af5f36f97427d101c5b2e3ab2cd0ff075` passed Windows CI `34161412445`, Workspace Search `34161412447`, Large Workspace Safeguards `34161412450`, and dedicated P08-007 `34161412438`; PR #206 was normally merged as `53df4d66026b0e793d7c7797a016ed654d8c3663`, whose exact accepted-main Windows CI `34161963855`, Workspace Search `34161963822`, Large Workspace Safeguards `34161963867`, and dedicated P08-007 `34161963864` all completed SUCCESS. `FCCD-P08-008` remains PENDING.",
    "project control P08-007 status",
)
write(control_path, control)

# Final task contract/documentation.
write(
    "docs/terminal/INTERACTIVE_TERMINAL_UX.md",
    """# Interactive terminal UX — FCCD-P08-007

## Canonical scope

`FCCD-P08-007` composes the existing typed P08 ConPTY host into the Bottom Tool Panel terminal seam. It does not replace P08-004 process ownership and does not claim P08-008 final safety convergence.

## User interaction contract

- PowerShell and Command Prompt are available from validated Windows executable paths.
- Git Bash and WSL appear only when the read-only optional-shell detector reports them.
- Start and Close expose explicit terminal lifecycle ownership.
- UTF-8 input and output flow only through `IConPtyTerminalSession`; the UI does not bypass that boundary with ad-hoc process launch.
- Ctrl+C copies selected terminal text; without a selection it sends ETX to the active session.
- Ctrl+V sends clipboard text; terminal navigation keys map to terminal escape sequences.
- Terminal resize is debounced and forwarded to ConPTY.

## Presentation and responsiveness

The terminal uses a read-only `RichTextBox` document surface with bounded rendered scrollback and bounded pending output. Output arriving faster than the UI can paint is coalesced through a single scheduled dispatcher flush rather than rebuilding the whole transcript for every chunk. ANSI SGR foreground colors are rendered, including standard, bright, indexed, and true-color sequences; unknown control sequences are consumed conservatively rather than displayed as raw escape noise.

## Shutdown safety

Window close is cancelled until `InteractiveTerminalSurface.DisposeAsync()` completes. Session close cancels queued resize work, clears pending presentation work, cancels the output pump, disposes the ConPTY session, and awaits the pump before lifecycle completion. The P08-004 kill-on-close Job Object remains the authoritative owned-process cleanup boundary.

## Validation

`tools/terminal/validate-interactive-terminal-ux.ps1` provides static, negative/recovery, hosted-Windows/WPF runtime, ANSI-rendering, high-output bounding, input, resize, composition, and async-disposal coverage. `.github/workflows/p08-007-interactive-terminal-ux.yml` is the dedicated exact-head/exact-main gate.

Canonical integration evidence is `evidence/phases/P08/P08_007_INTEGRATED_RECONCILIATION_2026-09-08.md`. No owner-only evidence is required for P08-007. P08-008 remains separate and P09 remains prohibited until P08 is formally closed.
""",
)

# Durable integrated evidence.
write(
    "evidence/phases/P08/P08_007_INTEGRATED_RECONCILIATION_2026-09-08.md",
    """# P08-007 — Integrated reconciliation

**Task:** `FCCD-P08-007 — Interactive terminal UX`  
**Canonical status:** `CLOSED`  
**Current phase after reconciliation:** `P08 — Terminal/process supervision` / `IN_PROGRESS`  
**Phase exit gate:** `NOT_RUN`

## Implementation

PR #206 implemented the production interactive terminal surface by composing the existing typed ConPTY host into the existing Bottom Tool Panel terminal seam while preserving the P02 shell contract. The exact implementation candidate was `bebe178af5f36f97427d101c5b2e3ab2cd0ff075`.

Implemented behavior includes validated PowerShell/CMD and read-only detected Git Bash/WSL choices; UTF-8 input/output; Ctrl+C selected-text copy versus ETX interrupt; Ctrl+V paste; navigation escape sequences; debounced ConPTY resize; explicit Start/Close lifecycle; bounded/coalesced high-output rendering; ANSI SGR color presentation; and window-close cancellation until async terminal disposal completes.

Exact implementation-head gates completed SUCCESS:

- Windows CI #554 / run `34161412445`
- P06-007 Workspace Search #283 / run `34161412447`
- P06-008 Large Workspace Safeguards #267 / run `34161412450`
- P08-007 Interactive Terminal UX #3 / run `34161412438`

PR #206 was normally merged as `53df4d66026b0e793d7c7797a016ed654d8c3663`.

## Exact accepted-main verification

The exact resulting main `53df4d66026b0e793d7c7797a016ed654d8c3663` passed the complete applicable gate set:

- Windows CI #555 / run `34161963855` — SUCCESS
- P06-007 Workspace Search #284 / run `34161963822` — SUCCESS
- P06-008 Large Workspace Safeguards #268 / run `34161963867` — SUCCESS
- P08-007 Interactive Terminal UX #4 / run `34161963864` — SUCCESS

This is the accepted implementation integration baseline for P08-007.

## Recovered defects

The task remained open while repairable defects existed. The branch recovered:

- the inherited P02 bottom-tool-panel validator failure without weakening the canonical P02 composition token;
- missing deterministic async terminal disposal from the application window-close path;
- ANSI stripping, replacing it with bounded SGR color rendering;
- whole-transcript-per-chunk UI rebuilding, replacing it with bounded/coalesced dispatcher presentation;
- analyzer `CA1859` by correcting the private helper type rather than suppressing the analyzer;
- the WPF runtime fixture STA-entrypoint defect.

The one-off staging helper `tools/reconciliation/repair-p08-007-candidate.py` is removed during final reconciliation because it encoded superseded pre-repair assumptions and is not a production or permanent validation tool.

## Closure boundary

P08-007 is CLOSED only after this reconciliation is normally integrated and its reconciliation head/resulting main remain green. No owner-only evidence is required or added. P08 remains `IN_PROGRESS`; only `FCCD-P08-008 — Process/terminal safety tests` remains PENDING and `PHASE_EXIT_GATE=NOT_RUN`. P09 and later phases remain prohibited until P08-008 and the formal P08 phase exit/closure process are complete. `VERIFIED_FINAL_COMPLETE=false`.
""",
)

# Remove obsolete one-off staging helper and the temporary reconciliation executor/workflow.
Path("tools/reconciliation/repair-p08-007-candidate.py").unlink()
Path("tools/reconciliation/finalize-p08-007.py").unlink()
Path(".github/workflows/p08-007-reconciliation-bootstrap.yml").unlink()

subprocess.run(["git", "config", "user.name", "github-actions[bot]"], check=True)
subprocess.run(["git", "config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com"], check=True)
subprocess.run(["git", "add", "-A"], check=True)
status = subprocess.run(["git", "status", "--porcelain"], check=True, capture_output=True, text=True).stdout
if not status.strip():
    raise SystemExit("P08-007 reconciliation produced no changes")
subprocess.run(["git", "commit", "-m", "docs: reconcile P08-007 canonical closure"], check=True)
subprocess.run(["git", "push", "origin", f"HEAD:{BRANCH}"], check=True)
