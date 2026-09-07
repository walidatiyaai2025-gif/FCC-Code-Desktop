# P08-008 — Integrated reconciliation

**Task:** `FCCD-P08-008 — Process/terminal safety tests`  
**Canonical status:** `CLOSED`  
**Current phase after reconciliation:** `P08 — Terminal/process supervision` / `IN_PROGRESS`  
**Phase exit gate:** `NOT_RUN`

## Recovery and implementation

PR #207 carried legitimate P08-008 work but was stacked on the then-active P08-007 branch. It was not merged. Recovery PR #208 (`recovery/fccd-p08-008-main-integration`) started from canonical main `596c43641b8f0706f27b96bfcb92cabb5859df62` and transplanted only the two P08-008 files, avoiding ownership or integration of P08-007.

The exact recovered candidate was `8de5f4722b1eeba03966494738f9738bcc106adc`. The task adds a permanent Windows-2025 process/terminal safety gate and hosted acceptance coverage for the existing P08 process/ConPTY boundaries, including:

- `ProcessSupervisor`, `ProcessCancellationEscalator`, `BoundedProcessOutputPipeline`, and ConPTY contract selection;
- argv round-trip safety for spaces, quotes, shell metacharacters, semicolons, empty arguments, and trailing backslashes;
- concurrent/idempotent `DisposeAsync` behavior;
- owned-process termination and unrelated-process isolation inherited from the P08-004 ConPTY baseline;
- fail-closed input/resize after disposal;
- cancelled-resize state preservation; and
- completed-session resize rejection.

Exact candidate gates completed `SUCCESS`:

- Windows CI run `34160742045`;
- P06-007 Workspace Search run `34160742043`;
- P06-008 Large Workspace Safeguards run `34160742035`; and
- P08-008 Process Terminal Safety run `34160742072`.

## Exact integration verification

PR #208 was normally merged as `7aac8a426031358954ba754a3c98ed1b458f1279` without force-push, squash substitution, or fabricated evidence.

The exact resulting main completed the applicable gate set with `SUCCESS`:

- Windows CI run `34161388230` / #553;
- P06-007 Workspace Search run `34161388136` / #282;
- P06-008 Large Workspace Safeguards run `34161388091` / #266; and
- P08-008 Process Terminal Safety run `34161388100` / #5.

## Current-main applicability after P08-007 closure

Before this reconciliation, canonical main was `072b6470bef7bb7a540098e338050cb299d1ac2f`, a descendant of the accepted P08-008 integration merge `7aac8a426031358954ba754a3c98ed1b458f1279` and the normal P08-007 reconciliation merge from PR #209.

The compare from `7aac8a426031358954ba754a3c98ed1b458f1279` to `072b6470bef7bb7a540098e338050cb299d1ac2f` contains P08-007 application/UX/validator files and canonical P08-007 reconciliation documentation only. It does not modify any path selected by `.github/workflows/p08-008-process-terminal-safety.yml`, so the dedicated P08-008 exact-integration evidence remains byte-applicable.

The exact current main completed the applicable regression gates with `SUCCESS`:

- Windows CI run `34163272333`;
- P06-007 Workspace Search run `34163272378`;
- P06-008 Large Workspace Safeguards run `34163272414`; and
- P08-007 Interactive Terminal UX run `34163272377`.

No task-local regression is known on the current descendant baseline.

## Closure boundary

`FCCD-P08-008` is therefore `CLOSED` from normally integrated production safety validation plus exact-candidate, exact-integration-main, ancestry/applicability, and exact-current-main regression evidence.

With P08-007 already canonically CLOSED by PR #209, all eight P08 task rows are now CLOSED after this reconciliation. This task does **not** itself close P08: `CURRENT_PHASE` remains P08 / `IN_PROGRESS`, and `PHASE_EXIT_GATE=NOT_RUN`. The next legal unit is separate P08 phase-exit convergence/closure; P09 and P15 implementation remain prohibited until that gate and closure are formally integrated and exact-main verified.

No owner-only evidence is required or added for P08-008. The canonical owner-last queue is unchanged: `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item, while `OWNER-P05-EXIT-REAL-TARGET` remains `PASS_INTEGRATED`. `VERIFIED_FINAL_COMPLETE=false` remains unchanged.
