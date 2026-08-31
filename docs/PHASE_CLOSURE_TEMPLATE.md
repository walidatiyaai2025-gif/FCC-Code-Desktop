# Phase Closure Evidence — PXX

> Copy this file to `evidence/phases/PXX/CLOSURE.md` when closing a phase. Do not mark a phase closed without completing every required field.

```text
PHASE: PXX
PHASE_NAME: <name>
CANDIDATE_SHA: <exact commit>
DATE: <YYYY-MM-DD>
EXIT_GATE: PASS | FAIL
KNOWN_BLOCKERS: <count>
KNOWN_REGRESSIONS: <count>
```

## 1. Mandatory task reconciliation

| Task ID | Final state | Evidence |
|---|---|---|
| FCCD-PXX-001 | CLOSED | <test/commit/artifact> |

All mandatory current-phase tasks must be `CLOSED`.

## 2. Commands / automated verification

Record exact commands and results.

```text
<command>
RESULT: PASS
```

## 3. Runtime/environment verification

Record environment-specific checks required by this phase, including relevant tool/application versions.

## 4. Negative/error-path verification

List defined failure cases and their results. Happy-path-only evidence is insufficient.

## 5. Cancellation/recovery verification

Record cancellation, interruption and recovery evidence where applicable.

## 6. UI/UX verification

Record required visual, DPI, keyboard, accessibility and state checks where applicable.

## 7. Data/safety verification

Confirm no unintended data loss, destructive Git behavior, secret leakage or unsafe side effects were observed in required scenarios.

## 8. Known defects

```text
KNOWN_PHASE_LOCAL_DEFECTS: NONE
```

If this is not `NONE`, the phase cannot close unless the defect is explicitly proven outside mandatory scope by canonical specification.

## 9. Regression status

```text
EARLIER_PHASE_REGRESSIONS: NONE
```

Any earlier-phase regression must be fixed and reverified before closing the current phase.

## 10. Exit decision

A `PASS` decision certifies all of the following:

- every mandatory phase task is `CLOSED`,
- all phase tests pass on `CANDIDATE_SHA`,
- no known phase-local release blocker remains,
- no earlier closed guarantee is knowingly broken,
- evidence is sufficient to reproduce the decision,
- the canonical baseline is safe to advance to the next phase.

```text
EXIT_GATE: PASS
AUTHORIZED_NEXT_PHASE: PYY
```
