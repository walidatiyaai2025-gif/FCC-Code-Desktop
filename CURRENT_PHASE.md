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
KNOWN_PHASE_BLOCKERS: 1
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
- `FCCD-P00-009` — BLOCKED only on real target Blender execution after reusable probe infrastructure, 15/15 self-tests, contract docs, evidence, and unified-runner integration were completed on this Windows host where Blender is not installed.
- `FCCD-P00-006`, `010` — IMPLEMENTED from target evidence; closure awaits the remaining P00 blocker and exact-head gate. P00-010 compatibility terminology now explicitly separates TESTED, DETECTED, UNVERIFIED, SUPPORTED and UNSUPPORTED.
- `PG-002-P00-RATE-LIMIT-CLOSURE` — RESOLVED. `NOT_OBSERVED_ON_TARGET` remains distinct from PASS/actual observation, but is an accepted P00-005 closure boundary when deterministic classifier mechanics and the rest of the exact-head target contract pass without manufacturing provider load.
- PR #6 hardened the unified target runner to refuse non-Windows evidence, wrong repository roots, missing Git/Node prerequisites, and uncommitted executable-input changes that break exact-head provenance.
- PR #9 hardened FCC process ownership evidence so descendants created after the initial snapshot are observed before cancellation/timeout escalation.
- PR #13 made the unified target runner safely rerunnable by permitting only prior repository-owned target-evidence output changes while continuing to reject source/configuration/probe dirtiness.

## Current objective

Complete P00 by:

1. obtaining real Blender target execution on the owner's Windows target after Blender becomes available,
2. reconciling the primary runtime decision and compatibility baseline after the final target evidence,
3. rerunning the complete unified suite on the exact current head with no uncommitted source/configuration/probe changes,
4. running the complete P00 exit gate and fixing every failure before closure.

## Recommended remaining worker lanes inside P00

```text
LOCAL TARGET VALIDATION WORKER
  rerun Blender target evidence after Blender becomes available

W5 CONVERGENCE
  reconcile Blender target evidence,
  close eligible P00-009,
  close P00-006 + P00-010 after dependencies are satisfied,
  run full P00 exit gate.
```

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