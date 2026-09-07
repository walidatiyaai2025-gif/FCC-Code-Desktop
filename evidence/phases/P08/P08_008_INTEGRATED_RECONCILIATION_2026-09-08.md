# P08-008 — Integrated reconciliation

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

## Reconciliation validation boundary

This documentation/evidence-only reconciliation must itself pass the permanent exact-head Windows CI, Workspace Search, and Large Workspace Safeguards gates before normal merge. After normal merge, the exact resulting canonical `main` must pass the same applicable shared gates before `FCCD-P08-008` is treated as canonical CLOSED. No task-local production byte is changed by this reconciliation.

## Closure boundary

`FCCD-P08-008` is closed in this reconciliation candidate from normally integrated production safety validation plus exact-candidate, exact-integration-main, ancestry/applicability, and current-main regression evidence. Canonical closure still requires this reconciliation to pass exact-head CI, normal merge, and exact resulting-main verification.

This task closure does **not** close P08 or activate P09. P08 remains `IN_PROGRESS` and `PHASE_EXIT_GATE=NOT_RUN` until separate exact-candidate phase-exit validation and canonical closure evidence complete. P09 and later implementation remain prohibited.

No owner-only evidence is required or added for P08-008. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `OWNER-P05-EXIT-REAL-TARGET` remains `PASS_INTEGRATED`; `VERIFIED_FINAL_COMPLETE=false` remains unchanged.
