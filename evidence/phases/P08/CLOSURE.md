# P08 Phase Closure — Terminal/process supervision

```text
PHASE: P08
PHASE_NAME: Terminal/process supervision
CANDIDATE_SHA: bb372da0a4506b3edc508156f06fc60ced8cc3d4
DATE: 2026-09-08
EXIT_GATE: PASS
KNOWN_BLOCKERS: 0
KNOWN_REGRESSIONS: 0
MANDATORY_TASKS: 8/8 CLOSED
EXACT_GATE_RUN: 34165022901
EXACT_GATE_JOB: 101874255929
PRE_CLOSURE_MAIN_WINDOWS_CI_RUN: 34164500457
PRE_CLOSURE_WORKSPACE_SEARCH_RUN: 34164500513
PRE_CLOSURE_LARGE_WORKSPACE_RUN: 34164500496
OWNER_PENDING_P08: NONE
GLOBAL_RELEASE_BLOCKERS: 1
VERIFIED_FINAL_COMPLETE: false
```

## 1. Mandatory task reconciliation

All eight mandatory P08 task rows were canonically `CLOSED` before this phase gate ran. Durable task evidence is retained under `evidence/phases/P08/`. The integrated phase surface covers owned process-tree supervision, graceful-to-forced cancellation escalation, bounded streaming output, native ConPTY hosting, PowerShell/CMD profiles, read-only optional Git Bash/WSL discovery, interactive terminal UX, and permanent process/terminal safety acceptance.

No new product implementation was added by this exit gate.

## 2. Exact-candidate automated verification

Validation-only branch `worker-b/p08-exit-gate` ran workflow `P08 Exit Gate Exact Validation`. The workflow definition is not product evidence by itself: its checkout explicitly replaced the branch worktree with immutable canonical candidate `bb372da0a4506b3edc508156f06fc60ced8cc3d4` before every acceptance command.

Authoritative gate:
- run `34165022901` / job `101874255929` — **SUCCESS**.
- GitHub-hosted Microsoft Windows Server 2025.
- .NET SDK exactly `10.0.400`.

The gate completed:

```text
pre-closure canonical-state guards
RESULT: PASS

.\tools\ci\run-windows-ci.ps1
RESULT: PASS

.\tools\terminal\validate-interactive-terminal-ux.ps1 -RunFixtures -RequireRuntime
RESULT: PASS

.\tools\terminal\validate-process-terminal-safety.ps1 -Configuration Release
RESULT: PASS

git diff --check
git diff --cached --check
final clean-worktree and exact-SHA assertions
RESULT: PASS
```

Before the dedicated gate, the same exact candidate was already green on canonical main:
- Windows CI `34164500457` / #560 — SUCCESS.
- P06-007 Workspace Search `34164500513` / #289 — SUCCESS.
- P06-008 Large Workspace Safeguards `34164500496` / #273 — SUCCESS.

## 3. Exit-criterion acceptance

The canonical P08 exit criterion is that interactive and non-interactive process scenarios execute, stream, resize, cancel, and clean up without orphaned owned processes in the defined test set.

The exact candidate proves that criterion through the permanent terminal/process contracts and the dedicated P08-007/P08-008 acceptance validators: owned process trees are tracked and disposed, cancellation escalates within bounded policy, output remains bounded, ConPTY input/output and resize operate on Windows, hostile/special argv shapes round-trip without shell reinterpretation, terminal UI shutdown awaits async disposal, and unrelated processes are not killed by owned cleanup.

## 4. Safety and integrity conclusion

No P08 phase-local defect or regression is known on the exact candidate. The gate finished on the unchanged candidate SHA with a clean worktree. No safety assertion, analyzer, cleanup rule, process-ownership boundary, or CI guard was weakened to obtain PASS.

## 5. Owner-last classification

No P08 phase-exit requirement is genuinely owner-only. Hosted Windows plus real process/ConPTY fixtures exercise the P08 acceptance boundary directly. No P08 owner queue item is created.

`OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item. `OWNER-P05-EXIT-REAL-TARGET` remains `PASS_INTEGRATED`. `P04=NOT_RUN`, `KNOWN_RELEASE_BLOCKERS=1`, and `VERIFIED_FINAL_COMPLETE=false` remain truthful.

## 6. Known defects and regressions

```text
KNOWN_P08_PHASE_LOCAL_DEFECTS: NONE
KNOWN_P08_REGRESSIONS: NONE
```

## 7. Exit decision

```text
ALL_P08_MANDATORY_TASKS_CLOSED: true
P08_EXACT_HEAD_GATE_PASS: true
P08_CANDIDATE_MAIN_GREEN: true
P08_KNOWN_PHASE_BLOCKERS: 0
P08_KNOWN_REGRESSIONS: 0
P08_OWNER_EVIDENCE_QUEUED: 0
EXIT_GATE: PASS
P08_PHASE_STATE: CLOSED
AUTHORIZED_NEXT_PHASE: P09
P09_IMPLEMENTATION_IN_THIS_CLOSURE: NONE
VERIFIED_FINAL_COMPLETE: false
```

P08 is therefore truthfully closed on exact candidate `bb372da0a4506b3edc508156f06fc60ced8cc3d4`. This closure record **does not activate P09**. After this closure/control-state change is normally merged and the resulting exact canonical `main` remains green, a separate governance transition may activate P09 as the next sequential cloud implementation phase while preserving the unresolved P04 owner-last release blocker.
