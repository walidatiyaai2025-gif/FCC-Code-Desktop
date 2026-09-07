# P05 Owner Real-Target Observation — 2026-09-07

Status: `PENDING_EXACT_SHA_PROVENANCE`

This record captures sanitized owner-observed behavior from a real Windows FCC Code Desktop run with the installed `fcc-claude`/provider environment. It is **not** the canonical `P05_PHASE_EXIT_REAL_TARGET.json` PASS artifact and must not be used to change the owner queue or P05 phase gate until exact tested-repository SHA provenance is supplied and reviewed.

## Sanitized observations

The owner directly observed all of the following in the application:

- a genuine provider-backed task executed from the FCC Code Desktop conversation surface;
- structured runtime/tool activity was visible during execution;
- Stop transitioned the active task into stopping/cancellation behavior;
- Retry created a new run for the same logical task and incremented the attempt;
- the application closed and reopened successfully;
- the same session resumed with prior conversation/task state intact.

No prompt text, provider response content, credentials, environment variables, or user project contents are recorded here.

## Evidence boundary

Canonical policy requires the final owner evidence to identify the exact tested repository SHA. The current repository-side observer cannot prove which local commit produced the executable used for the owner's manual run. Therefore:

- `overallStatus=PASS` is **not** claimed here;
- `OWNER-P05-EXIT-REAL-TARGET` remains `QUEUED`;
- `P05_EXIT_GATE` remains `NOT_RUN`;
- `VERIFIED_FINAL_COMPLETE` remains false;
- no canonical acceptance state may be changed from this observation alone.

## Required final provenance

Run `git rev-parse HEAD` in the exact local FCC-Code-Desktop checkout used for the successful manual test (or rerun `tools/ui/run-p05-phase-exit-owner-validation.ps1` on the intended exact candidate). Once the 40-character SHA is known, a convergence worker can validate ancestry/applicability, create/review `evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json`, and perform canonical queue/gate reconciliation if all policy conditions hold.
