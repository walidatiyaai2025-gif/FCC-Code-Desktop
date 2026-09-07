from pathlib import Path
import re

CANDIDATE = "bb372da0a4506b3edc508156f06fc60ced8cc3d4"
WINDOWS_RUN = "34164500457"
WORKSPACE_RUN = "34164500513"
LARGE_RUN = "34164500496"
EXIT_RUN = "34165022901"
EXIT_JOB = "101874255929"


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    Path(path).write_text(text.rstrip() + "\n", encoding="utf-8", newline="\n")


def replace_first(path: str, old: str, new: str) -> None:
    text = read(path)
    if old not in text:
        raise SystemExit(f"{path}: required replacement target missing: {old!r}")
    write(path, text.replace(old, new, 1))


# CURRENT_PHASE.md: close P08 but deliberately keep CURRENT_PHASE=P08 until a later transition.
replace_first("CURRENT_PHASE.md", "CURRENT_PHASE_STATE: IN_PROGRESS", "CURRENT_PHASE_STATE: CLOSED")
replace_first("CURRENT_PHASE.md", "PHASE_EXIT_GATE: NOT_RUN", "PHASE_EXIT_GATE: PASS")
replace_first(
    "CURRENT_PHASE.md",
    "P08 — Terminal/process supervision — is now the sole legal cloud implementation/convergence phase. Only dependency-valid, unclaimed P08 work may begin. P09 and later implementation remain prohibited until P08 is truthfully closed with its exit gate resolved under canonical governance.",
    f"P08 — Terminal/process supervision — is canonically CLOSED in this closure state. `FCCD-P08-001` through `FCCD-P08-008` are normally integrated and exact-main verified. Immutable phase candidate `{CANDIDATE}` passed pre-closure Windows CI `{WINDOWS_RUN}` / #560, Workspace Search `{WORKSPACE_RUN}` / #289, and Large Workspace Safeguards `{LARGE_RUN}` / #273; dedicated exact-candidate P08 phase-exit run `{EXIT_RUN}` / job `{EXIT_JOB}` completed SUCCESS with the full Windows baseline, interactive terminal UX runtime acceptance, process/terminal safety acceptance, exact-SHA guards, and a clean worktree. Canonical closure evidence is `evidence/phases/P08/CLOSURE.md`.\n\n`CURRENT_PHASE` deliberately remains `P08` after closure. P09 is not active yet. A separate governance transition may activate `CURRENT_PHASE=P09` only after this closure state is normally merged and the resulting exact canonical `main` remains green. No P09 or later implementation is authorized inside this closure state."
)
replace_first(
    "CURRENT_PHASE.md",
    "- Exactly one cloud implementation/convergence phase is active: P08.",
    "- P08 is CLOSED and retained as the current closure checkpoint until a separate, validated transition activates P09; no later-phase implementation is authorized yet."
)

current = read("CURRENT_PHASE.md")
marker = "<!-- P08-PHASE-EXIT-CLOSURE -->"
if marker in current:
    raise SystemExit("CURRENT_PHASE.md already contains P08 phase-exit closure marker")
current += f'''\n{marker}\n## P08 phase-exit provenance\n\n- Exact immutable product candidate: `{CANDIDATE}`.\n- Exact candidate pre-closure Windows CI: run `{WINDOWS_RUN}` / #560 — SUCCESS.\n- Exact candidate pre-closure P06-007 Workspace Search: run `{WORKSPACE_RUN}` / #289 — SUCCESS.\n- Exact candidate pre-closure P06-008 Large Workspace Safeguards: run `{LARGE_RUN}` / #273 — SUCCESS.\n- Dedicated exact-candidate P08 phase-exit gate: run `{EXIT_RUN}` / job `{EXIT_JOB}` — SUCCESS.\n- Gate coverage: complete Windows baseline plus P08-007 interactive terminal UX runtime fixtures and P08-008 process/terminal safety acceptance, pre-closure state guards, exact-SHA/diff-hygiene guards, and final clean-worktree assertion.\n- Canonical closure evidence: `evidence/phases/P08/CLOSURE.md`.\n- P08 phase state: `CLOSED`; `PHASE_EXIT_GATE=PASS`; phase-local blockers/regressions: none.\n- No P08 owner-only acceptance item was created. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; P05 remains `PASS_INTEGRATED`; `VERIFIED_FINAL_COMPLETE=false`.\n- P09 is only the authorized next phase and is not active until a separate governance transition is normally integrated and exact-main verified.\n'''
write("CURRENT_PHASE.md", current)

# PROJECT_CONTROL.md: reconcile the phase checkpoint and replace the stale active-P08 paragraph.
replace_first("PROJECT_CONTROL.md", "CURRENT_PHASE_STATE: IN_PROGRESS", "CURRENT_PHASE_STATE: CLOSED")
replace_first("PROJECT_CONTROL.md", "PHASE_EXIT_GATE: NOT_RUN", "PHASE_EXIT_GATE: PASS")
project = read("PROJECT_CONTROL.md")
pattern = re.compile(
    r"P08 — Terminal/process supervision — is now the single active cloud implementation/convergence phase\..*?(?=\n\n---)",
    re.S,
)
replacement = (
    f"P08 — Terminal/process supervision — is canonically CLOSED in this closure state. "
    f"`FCCD-P08-001` through `FCCD-P08-008` are CLOSED after implementation, focused validation, normal integration, exact post-merge verification, and durable task reconciliation. "
    f"Exact immutable phase candidate `{CANDIDATE}` passed pre-closure Windows CI `{WINDOWS_RUN}` / #560, Workspace Search `{WORKSPACE_RUN}` / #289, and Large Workspace Safeguards `{LARGE_RUN}` / #273; dedicated P08 phase-exit run `{EXIT_RUN}` / job `{EXIT_JOB}` completed SUCCESS with the full Windows baseline, interactive terminal UX runtime validation, process/terminal safety validation, and exact-SHA/clean-worktree guards. "
    "Closure evidence is `evidence/phases/P08/CLOSURE.md`. `CURRENT_PHASE` deliberately remains P08 until this closure change is normally integrated and the resulting exact canonical `main` remains green; only then may a separate governance transition activate P09. "
    "`OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item, P05 remains `PASS_INTEGRATED`, `P04=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all mandatory owner and release acceptance requirements pass."
)
project, count = pattern.subn(replacement, project, count=1)
if count != 1:
    raise SystemExit(f"PROJECT_CONTROL.md P08 paragraph replacement count={count}")
write("PROJECT_CONTROL.md", project)

# TASK_LEDGER.md: record phase closure without activating P09.
ledger = read("docs/TASK_LEDGER.md")
closure_sentence = "P08 is canonically CLOSED at the phase level on immutable candidate"
if closure_sentence in ledger:
    raise SystemExit("TASK_LEDGER already contains P08 phase closure")
anchor = "## P09 — External Tool Gateway\n"
if ledger.count(anchor) != 1:
    raise SystemExit("TASK_LEDGER P09 anchor missing or ambiguous")
phase_note = f'''P08 is canonically CLOSED at the phase level on immutable candidate `{CANDIDATE}`. Dedicated exact-candidate exit-gate run `{EXIT_RUN}` / job `{EXIT_JOB}` completed SUCCESS after pre-closure guards, the complete Windows baseline, P08-007 interactive-terminal runtime acceptance, P08-008 process/terminal safety acceptance, and exact-SHA/clean-worktree verification. Pre-closure exact-main Windows CI `{WINDOWS_RUN}` / #560, Workspace Search `{WORKSPACE_RUN}` / #289, and Large Workspace Safeguards `{LARGE_RUN}` / #273 were SUCCESS. Canonical evidence is `evidence/phases/P08/CLOSURE.md`. P09 is only the authorized next phase and is not active until a separate governance transition is normally integrated and exact-main verified.\n\n'''
ledger = ledger.replace(anchor, phase_note + anchor, 1)

next_pattern = re.compile(r"## Current next action\n\n.*?(?=\n<!-- FCCD-P08-004-FINAL-CLOSURE-PROVENANCE -->)", re.S)
next_replacement = f'''## Current next action\n\n`CURRENT_PHASE = P08` is canonically `CLOSED` in this closure state. `FCCD-P08-001` through `FCCD-P08-008` are CLOSED, dedicated exact-candidate phase-exit run `{EXIT_RUN}` / job `{EXIT_JOB}` is `SUCCESS`, and `PHASE_EXIT_GATE=PASS`. Canonical phase evidence is `evidence/phases/P08/CLOSURE.md`.\n\nNormally merge this P08 closure state/evidence and require the resulting exact canonical `main` to remain green. Only then may a separate governance transition activate `CURRENT_PHASE=P09`. Do not implement P09 or any later phase inside this closure change. Preserve `OWNER-P04-008-REAL-TARGET` as the sole unresolved release-blocking owner item, keep P05 `PASS_INTEGRATED`, and keep `VERIFIED_FINAL_COMPLETE=false`.\n'''
ledger, count = next_pattern.subn(next_replacement.rstrip(), ledger, count=1)
if count != 1:
    raise SystemExit(f"TASK_LEDGER current-next-action replacement count={count}")
write("docs/TASK_LEDGER.md", ledger)

# Canonical phase closure evidence.
evidence_path = Path("evidence/phases/P08/CLOSURE.md")
if evidence_path.exists():
    raise SystemExit("P08 CLOSURE.md already exists unexpectedly")
evidence = f'''# P08 Phase Closure — Terminal/process supervision\n\n```text\nPHASE: P08\nPHASE_NAME: Terminal/process supervision\nCANDIDATE_SHA: {CANDIDATE}\nDATE: 2026-09-08\nEXIT_GATE: PASS\nKNOWN_BLOCKERS: 0\nKNOWN_REGRESSIONS: 0\nMANDATORY_TASKS: 8/8 CLOSED\nEXACT_GATE_RUN: {EXIT_RUN}\nEXACT_GATE_JOB: {EXIT_JOB}\nPRE_CLOSURE_MAIN_WINDOWS_CI_RUN: {WINDOWS_RUN}\nPRE_CLOSURE_WORKSPACE_SEARCH_RUN: {WORKSPACE_RUN}\nPRE_CLOSURE_LARGE_WORKSPACE_RUN: {LARGE_RUN}\nOWNER_PENDING_P08: NONE\nGLOBAL_RELEASE_BLOCKERS: 1\nVERIFIED_FINAL_COMPLETE: false\n```\n\n## 1. Mandatory task reconciliation\n\nAll eight mandatory P08 task rows were canonically `CLOSED` before this phase gate ran. Durable task evidence is retained under `evidence/phases/P08/`. The integrated phase surface covers owned process-tree supervision, graceful-to-forced cancellation escalation, bounded streaming output, native ConPTY hosting, PowerShell/CMD profiles, read-only optional Git Bash/WSL discovery, interactive terminal UX, and permanent process/terminal safety acceptance.\n\nNo new product implementation was added by this exit gate.\n\n## 2. Exact-candidate automated verification\n\nValidation-only branch `worker-b/p08-exit-gate` ran workflow `P08 Exit Gate Exact Validation`. The workflow definition is not product evidence by itself: its checkout explicitly replaced the branch worktree with immutable canonical candidate `{CANDIDATE}` before every acceptance command.\n\nAuthoritative gate:\n- run `{EXIT_RUN}` / job `{EXIT_JOB}` — **SUCCESS**.\n- GitHub-hosted Microsoft Windows Server 2025.\n- .NET SDK exactly `10.0.400`.\n\nThe gate completed:\n\n```text\npre-closure canonical-state guards\nRESULT: PASS\n\n.\\tools\\ci\\run-windows-ci.ps1\nRESULT: PASS\n\n.\\tools\\terminal\\validate-interactive-terminal-ux.ps1 -RunFixtures -RequireRuntime\nRESULT: PASS\n\n.\\tools\\terminal\\validate-process-terminal-safety.ps1 -Configuration Release\nRESULT: PASS\n\ngit diff --check\ngit diff --cached --check\nfinal clean-worktree and exact-SHA assertions\nRESULT: PASS\n```\n\nBefore the dedicated gate, the same exact candidate was already green on canonical main:\n- Windows CI `{WINDOWS_RUN}` / #560 — SUCCESS.\n- P06-007 Workspace Search `{WORKSPACE_RUN}` / #289 — SUCCESS.\n- P06-008 Large Workspace Safeguards `{LARGE_RUN}` / #273 — SUCCESS.\n\n## 3. Exit-criterion acceptance\n\nThe canonical P08 exit criterion is that interactive and non-interactive process scenarios execute, stream, resize, cancel, and clean up without orphaned owned processes in the defined test set.\n\nThe exact candidate proves that criterion through the permanent terminal/process contracts and the dedicated P08-007/P08-008 acceptance validators: owned process trees are tracked and disposed, cancellation escalates within bounded policy, output remains bounded, ConPTY input/output and resize operate on Windows, hostile/special argv shapes round-trip without shell reinterpretation, terminal UI shutdown awaits async disposal, and unrelated processes are not killed by owned cleanup.\n\n## 4. Safety and integrity conclusion\n\nNo P08 phase-local defect or regression is known on the exact candidate. The gate finished on the unchanged candidate SHA with a clean worktree. No safety assertion, analyzer, cleanup rule, process-ownership boundary, or CI guard was weakened to obtain PASS.\n\n## 5. Owner-last classification\n\nNo P08 phase-exit requirement is genuinely owner-only. Hosted Windows plus real process/ConPTY fixtures exercise the P08 acceptance boundary directly. No P08 owner queue item is created.\n\n`OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item. `OWNER-P05-EXIT-REAL-TARGET` remains `PASS_INTEGRATED`. `P04=NOT_RUN`, `KNOWN_RELEASE_BLOCKERS=1`, and `VERIFIED_FINAL_COMPLETE=false` remain truthful.\n\n## 6. Known defects and regressions\n\n```text\nKNOWN_P08_PHASE_LOCAL_DEFECTS: NONE\nKNOWN_P08_REGRESSIONS: NONE\n```\n\n## 7. Exit decision\n\n```text\nALL_P08_MANDATORY_TASKS_CLOSED: true\nP08_EXACT_HEAD_GATE_PASS: true\nP08_CANDIDATE_MAIN_GREEN: true\nP08_KNOWN_PHASE_BLOCKERS: 0\nP08_KNOWN_REGRESSIONS: 0\nP08_OWNER_EVIDENCE_QUEUED: 0\nEXIT_GATE: PASS\nP08_PHASE_STATE: CLOSED\nAUTHORIZED_NEXT_PHASE: P09\nP09_IMPLEMENTATION_IN_THIS_CLOSURE: NONE\nVERIFIED_FINAL_COMPLETE: false\n```\n\nP08 is therefore truthfully closed on exact candidate `{CANDIDATE}`. This closure record **does not activate P09**. After this closure/control-state change is normally merged and the resulting exact canonical `main` remains green, a separate governance transition may activate P09 as the next sequential cloud implementation phase while preserving the unresolved P04 owner-last release blocker.\n'''
evidence_path.parent.mkdir(parents=True, exist_ok=True)
evidence_path.write_text(evidence, encoding="utf-8", newline="\n")
