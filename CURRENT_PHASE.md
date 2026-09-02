# FCC Code Desktop — Current Phase

This file is the fastest canonical resume checkpoint. It must be updated only when durable live project state changes.

```text
PROJECT_ID: FCC_CODE_DESKTOP
TARGET_RELEASE: 1.0.0
CURRENT_PHASE: P00
CURRENT_PHASE_NAME: Constitution + external-contract de-risking
CURRENT_PHASE_STATE: IN_PROGRESS
NEXT_PHASE: P01
PHASE_EXIT_GATE: NOT_RUN
KNOWN_PHASE_BLOCKERS: 0
KNOWN_RELEASE_BLOCKERS: 0
VERIFIED_FINAL_COMPLETE: false
LAST_RECONCILED: 2026-09-02
```

## Active rule

Do not start P01 implementation until every mandatory P00 task is `CLOSED` and the P00 exit gate in `docs/EXECUTION_PLAN.md` is `PASS` with exact-head evidence.

Before any worker selects new work, it must apply `docs/WORKER_PROTOCOL.md`: repair broken canonical state, resolve blockers, recover abandoned/stale work, and finish integration-pending work before claiming an unrelated new task.

P00 target-dependent contract work must also follow `docs/P00_TARGET_MACHINE_VALIDATION.md`. Remote workers build and self-test deterministic probes; final target evidence must be collected on the owner's actual Windows machine and reconciled before target-dependent tasks can close.

## Current status

- `FCCD-P00-001` — CLOSED.
- `FCCD-P00-002` — CLOSED from Windows executable/version/help and live loopback health evidence.
- `FCCD-P00-003` — CLOSED from real structured `system/init` and `system/api_retry` target frames with sanitized raw/parsed evidence.
- `FCCD-P00-004` - CLOSED from authoritative Windows provider-backed first-turn and new-process session-resume continuity evidence, including invalid-session rejection, valid-session recovery after the negative case, and owned-process cleanup.
- `FCCD-P00-005` — CLOSED from authoritative exact-head Windows failure/cancellation evidence at tested source SHA `015ffd8c0e2a6e725e33ed153441ff51e7952556`: provider baseline SUCCESS, cancellation INTERRUPTED, graceful interrupt, hardened descendant observation, residual owned-process cleanup by previously observed PID/identity, zero remaining owned processes, and explicit `RATE_LIMIT = NOT_OBSERVED_ON_TARGET` under the resolved PG-002 safe closure policy. No artificial 429 traffic was generated.
- `FCCD-P00-007` - CLOSED from authoritative Windows CLI fallback evidence covering provider-backed completion across normal, spaced, and Unicode/Arabic working directories, stdout/stderr observability, graceful cancellation, and owned-process cleanup.
- `FCCD-P00-008` — CLOSED after abandoned Worker 3 work was recovered, Windows probe defects were repaired, and the complete real Unity target contract passed.
- `FCCD-P00-009` — CLOSED from authoritative Windows Blender `5.2.0` execution at tested source SHA `e6932783b30ab0bdbb596c7959e03143753bff9a`: discovery/version, background/Python automation, save/render/export artifact validation, controlled failure, owned cancellation/cleanup, and 29/29 deterministic self-tests passed; evidence was integrated by PR #40.
- `FCCD-P00-006`, `010` — VERIFIED from the complete reconciled P00 evidence set. Their task-local contracts are resolved; final transition to CLOSED awaits only the exact-head P00 exit-gate record.
- `PG-002-P00-RATE-LIMIT-CLOSURE` — RESOLVED. `NOT_OBSERVED_ON_TARGET` remains distinct from PASS/actual observation, but is an accepted P00-005 closure boundary when deterministic classifier mechanics and the rest of the exact-head target contract pass without manufacturing provider load.
- PR #6 hardened the unified target runner to refuse non-Windows evidence, wrong repository roots, missing Git/Node prerequisites, and uncommitted executable-input changes that break exact-head provenance.
- PR #9 hardened FCC process ownership evidence so descendants created after the initial snapshot are observed before cancellation/timeout escalation.
- PR #13 made the unified target runner safely rerunnable by permitting only prior repository-owned target-evidence output changes while continuing to reject source/configuration/probe dirtiness.

## Current objective

Complete P00 by:

1. integrating the Blender and compatibility reconciliation candidate;
2. running the complete non-provider P00 exit gate on the exact merged candidate head;
3. transitioning FCCD-P00-006 and FCCD-P00-010 to CLOSED only if that gate passes;
4. recording evidence/phases/P00/CLOSURE.md with the exact candidate SHA;
5. opening P01 only after every mandatory P00 task is CLOSED and the phase exit gate is PASS.

## Recommended remaining worker lane inside P00

W5 FINAL P00 EXIT GATE:
- verify exact candidate head;
- run the complete non-provider P00 verification suite;
- verify integrated target evidence and provenance;
- close P00-006 and P00-010 only if the gate passes;
- write P00 CLOSURE.md;
- transition the phase only with zero blockers and regressions.

No additional provider, Unity, or Blender target rerun is required solely for this convergence step.

Workers must inspect live claims before taking a lane and must not duplicate active work.

## Resume procedure

1. Read `AGENTS.md`.
2. Read `PROJECT_CONTROL.md`.
3. Read `docs/EXECUTION_PLAN.md`.
4. Read `docs/WORKER_PROTOCOL.md`.
5. Read `docs/P00_TARGET_MACHINE_VALIDATION.md`.
6. Read `docs/TASK_LEDGER.md` and `docs/PLAN_GAPS.md`.
7. Fetch live branches/PRs/issues/commits, CI/evidence, and build a claim + recovery map.
8. Preserve and reuse merged FCC/CLI, streaming/session/failure, Unity, Blender and unified-runner probe infrastructure.
9. Continue only legitimate non-overlapping work in `CURRENT_PHASE`.
10. Do not promote `NEXT_PHASE` until the current exit gate is recorded `PASS`.