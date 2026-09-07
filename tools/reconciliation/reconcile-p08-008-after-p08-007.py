from pathlib import Path
import re


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    Path(path).write_text(text, encoding="utf-8", newline="\n")


def replace_once(path: str, old: str, new: str) -> None:
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one exact replacement target, found {count}: {old!r}")
    write(path, text.replace(old, new, 1))


replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P08-008` — Process/terminal safety tests — PENDING.",
    "- `FCCD-P08-008` — Process/terminal safety tests — CLOSED.",
)

current_phase = read("CURRENT_PHASE.md")
marker = "<!-- FCCD-P08-008-FINAL-CLOSURE-PROVENANCE -->"
if marker in current_phase:
    raise SystemExit("CURRENT_PHASE.md already contains P08-008 closure marker")
current_phase = current_phase.rstrip() + "\n\n" + r'''<!-- FCCD-P08-008-FINAL-CLOSURE-PROVENANCE -->
## FCCD-P08-008 final canonical closure provenance

- Task: `FCCD-P08-008 — Process/terminal safety tests` — `CLOSED` in this reconciliation candidate.
- Recovery/integration PR: #208 (`recovery/fccd-p08-008-main-integration`); exact candidate `8de5f4722b1eeba03966494738f9738bcc106adc`; normal merge `7aac8a426031358954ba754a3c98ed1b458f1279`.
- Exact implementation-head gates: Windows CI `34160742045` / #549, Workspace Search `34160742043` / #278, Large Workspace Safeguards `34160742035` / #262, and P08-008 Process Terminal Safety `34160742072` / #4 — all `SUCCESS`.
- Exact implementation-merge gates on `7aac8a426031358954ba754a3c98ed1b458f1279`: Windows CI `34161388230` / #553, Workspace Search `34161388136` / #282, Large Workspace Safeguards `34161388091` / #266, and P08-008 Process Terminal Safety `34161388100` / #5 — all `SUCCESS`.
- Pre-reconciliation canonical main `072b6470bef7bb7a540098e338050cb299d1ac2f` is a descendant of the accepted P08-008 merge. The intervening compare modifies P08-007 terminal UX/composition/governance only and no path selected by the P08-008 safety workflow.
- Exact pre-reconciliation canonical-main regression gates: Windows CI `34163272333` / #558, Workspace Search `34163272378` / #287, Large Workspace Safeguards `34163272414` / #271, and P08-007 Interactive Terminal UX `34163272377` / #6 — all `SUCCESS`.
- Integrated evidence: `evidence/phases/P08/P08_008_INTEGRATED_RECONCILIATION_2026-09-08.md`.
- No owner-only evidence is required or added. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; P05 remains `PASS_INTEGRATED`; `VERIFIED_FINAL_COMPLETE=false`.
- P08 remains `IN_PROGRESS` and `PHASE_EXIT_GATE=NOT_RUN`. With P08-001 through P08-008 CLOSED in this candidate, the only legal next action after normal integration and exact-main verification is P08 phase-exit convergence. P09 and later implementation remain prohibited until P08 itself is canonically CLOSED with gate PASS.
''' + "\n"
write("CURRENT_PHASE.md", current_phase)

replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P08-008 | Process/terminal safety tests | PENDING |",
    "| FCCD-P08-008 | Process/terminal safety tests | CLOSED |",
)

ledger = read("docs/TASK_LEDGER.md")
provenance_marker = "`FCCD-P08-008` is CLOSED from the permanent process/terminal safety suite integrated in PR #208."
if provenance_marker in ledger:
    raise SystemExit("TASK_LEDGER already contains P08-008 integrated provenance")
insert = r'''`FCCD-P08-008` is CLOSED from the permanent process/terminal safety suite integrated in PR #208. Exact recovered candidate `8de5f4722b1eeba03966494738f9738bcc106adc` passed Windows CI `34160742045` / #549, P06-007 Workspace Search `34160742043` / #278, P06-008 Large Workspace Safeguards `34160742035` / #262, and P08-008 Process Terminal Safety `34160742072` / #4. PR #208 was normally merged as `7aac8a426031358954ba754a3c98ed1b458f1279`; that exact main passed Windows CI `34161388230` / #553, Workspace Search `34161388136` / #282, Large Workspace Safeguards `34161388091` / #266, and P08-008 Process Terminal Safety `34161388100` / #5. Current pre-reconciliation main `072b6470bef7bb7a540098e338050cb299d1ac2f` descends from that merge; the intervening compare changes P08-007 UX/composition/governance only and no P08-008 safety-workflow-selected path. That exact current main passed Windows CI `34163272333` / #558, Workspace Search `34163272378` / #287, Large Workspace Safeguards `34163272414` / #271, and P08-007 Interactive Terminal UX `34163272377` / #6. Safety coverage includes owned process lifecycle, graceful-to-forced cancellation, bounded output, ConPTY argv round trips for hostile/special argument shapes, concurrent/idempotent disposal, owned-tree cleanup without killing unrelated processes, fail-closed closed-session input/resize, cancelled-resize state preservation, and completed-session resize rejection. Task evidence: `evidence/phases/P08/P08_008_INTEGRATED_RECONCILIATION_2026-09-08.md`. No owner-only evidence is required. P08 remains `IN_PROGRESS`; all P08 task rows are CLOSED in this reconciliation candidate; `PHASE_EXIT_GATE=NOT_RUN`; P09 and later implementation remain prohibited until separate P08 phase-exit closure.

'''
anchor = "## P09 — External Tool Gateway\n"
if ledger.count(anchor) != 1:
    raise SystemExit("TASK_LEDGER P09 anchor missing or ambiguous")
ledger = ledger.replace(anchor, insert + anchor, 1)

pattern = re.compile(r"## Current next action\n\n.*?(?=\n<!-- FCCD-P08-004-FINAL-CLOSURE-PROVENANCE -->)", re.S)
replacement = r'''## Current next action

`CURRENT_PHASE = P08` remains `IN_PROGRESS`. `FCCD-P08-001` through `FCCD-P08-008` are CLOSED in this reconciliation candidate after implementation, focused validation, normal integration, exact-main validation, and task evidence. `PHASE_EXIT_GATE=NOT_RUN`.

P04/P05 owner-last status remains governed exclusively by the canonical owner queue and current control files. P08-008 adds no owner-only obligation; `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item and P05 remains `PASS_INTEGRATED`.

After this reconciliation is normally merged and its exact resulting `main` remains green, the only legal P08 action is phase-exit convergence on an immutable exact candidate: verify all eight task rows CLOSED, run the P08 exit gate, record `evidence/phases/P08/CLOSURE.md`, and close P08 through a separate validated normal-merge closure change. P09 and later implementation remain prohibited until P08 is canonically CLOSED with `PHASE_EXIT_GATE=PASS` and exact post-closure main is green.
'''
ledger, count = pattern.subn(replacement.rstrip(), ledger, count=1)
if count != 1:
    raise SystemExit(f"TASK_LEDGER current-next-action replacement count={count}")
write("docs/TASK_LEDGER.md", ledger)

evidence = r'''# P08-008 — Integrated reconciliation

**Task:** `FCCD-P08-008 — Process/terminal safety tests`  
**Canonical status in this reconciliation candidate:** `CLOSED`  
**Current phase after reconciliation:** `P08 — Terminal/process supervision` / `IN_PROGRESS`  
**Phase exit gate:** `NOT_RUN`

## Recovery and implementation

PR #207 carried legitimate P08-008 work but was stacked on the then-active P08-007 branch. It was not merged. Recovery PR #208 (`recovery/fccd-p08-008-main-integration`) started from canonical main and transplanted only the P08-008 safety workflow/validator without taking P08-007 ownership.

The exact recovered candidate was `8de5f4722b1eeba03966494738f9738bcc106adc`. Coverage includes:

- `ProcessSupervisor`, `ProcessCancellationEscalator`, `BoundedProcessOutputPipeline`, and ConPTY contracts;
- argv round-trip safety for spaces, quotes, shell metacharacters, semicolons, empty arguments, and trailing backslashes;
- concurrent/idempotent `DisposeAsync` behavior;
- owned-process termination and unrelated-process isolation;
- fail-closed input/resize after disposal;
- cancelled-resize state preservation; and
- completed-session resize rejection.

Exact candidate gates completed `SUCCESS`:

- Windows CI run `34160742045` / #549;
- P06-007 Workspace Search run `34160742043` / #278;
- P06-008 Large Workspace Safeguards run `34160742035` / #262; and
- P08-008 Process Terminal Safety run `34160742072` / #4.

## Exact integration verification

PR #208 was normally merged as `7aac8a426031358954ba754a3c98ed1b458f1279`.

That exact resulting main completed the applicable gate set with `SUCCESS`:

- Windows CI run `34161388230` / #553;
- P06-007 Workspace Search run `34161388136` / #282;
- P06-008 Large Workspace Safeguards run `34161388091` / #266; and
- P08-008 Process Terminal Safety run `34161388100` / #5.

## Current-main applicability

Pre-reconciliation canonical main is `072b6470bef7bb7a540098e338050cb299d1ac2f`, a descendant of the accepted P08-008 merge. Compare review from `7aac8a426031358954ba754a3c98ed1b458f1279` to `072b6470bef7bb7a540098e338050cb299d1ac2f` changes P08-007 terminal UI/composition/validator/governance paths only. It changes no path selected by `.github/workflows/p08-008-process-terminal-safety.yml`, so the dedicated P08-008 exact-integration evidence remains directly applicable.

The exact descendant baseline also passed the complete current regression set triggered by the P08-007 reconciliation:

- Windows CI run `34163272333` / #558 — `SUCCESS`;
- P06-007 Workspace Search run `34163272378` / #287 — `SUCCESS`;
- P06-008 Large Workspace Safeguards run `34163272414` / #271 — `SUCCESS`; and
- P08-007 Interactive Terminal UX run `34163272377` / #6 — `SUCCESS`.

No P08-008 safety regression is known on this descendant baseline.

## Closure boundary

`FCCD-P08-008` is closed in this reconciliation candidate from normally integrated production safety validation plus exact-candidate, exact-integration-main, ancestry/applicability, and current-main regression evidence. Canonical closure still requires this reconciliation to pass exact-head CI, normal merge, and exact resulting-main verification.

This task closure does **not** close P08 or activate P09. P08 remains `IN_PROGRESS` and `PHASE_EXIT_GATE=NOT_RUN` until separate exact-candidate phase-exit validation and canonical closure evidence complete. P09 and later implementation remain prohibited.

No owner-only evidence is required or added for P08-008. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `OWNER-P05-EXIT-REAL-TARGET` remains `PASS_INTEGRATED`; `VERIFIED_FINAL_COMPLETE=false` remains unchanged.
'''
path = Path("evidence/phases/P08/P08_008_INTEGRATED_RECONCILIATION_2026-09-08.md")
if path.exists():
    raise SystemExit(f"Evidence already exists unexpectedly: {path}")
path.write_text(evidence, encoding="utf-8", newline="\n")
