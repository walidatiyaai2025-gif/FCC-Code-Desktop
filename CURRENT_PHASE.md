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
KNOWN_PHASE_BLOCKERS: 6
KNOWN_RELEASE_BLOCKERS: 0
VERIFIED_FINAL_COMPLETE: false
LAST_RECONCILED: 2026-09-01
```

## Active rule

Do not start P01 implementation until every mandatory P00 task is `CLOSED` and the P00 exit gate in `docs/EXECUTION_PLAN.md` is `PASS` with exact-head evidence.

Before any worker selects new work, it must apply `docs/WORKER_PROTOCOL.md`: repair broken canonical state, resolve blockers, recover abandoned/stale work, and finish integration-pending work before claiming an unrelated new task.

P00 target-dependent contract work must also follow `docs/P00_TARGET_MACHINE_VALIDATION.md`. Remote workers build and self-test deterministic probes; final target evidence must be collected on the owner's actual Windows machine and reconciled before target-dependent tasks can close.

## Current status

- `FCCD-P00-001` — CLOSED.
- `FCCD-P00-002` — BLOCKED only on real target-machine FCC/fcc-claude evidence after probe infrastructure was merged by PR #1.
- `FCCD-P00-003` — BLOCKED only on real target-machine structured-stream evidence after reusable recorder/parser/self-tests and target-runner integration were implemented by Worker 2.
- `FCCD-P00-004` — BLOCKED only on real target-machine session/resume evidence after reusable session extraction/resume probes and self-tests were implemented by Worker 2.
- `FCCD-P00-005` — BLOCKED only on real target-machine cancellation/failure evidence after reusable failure/cancellation probes and self-tests were implemented by Worker 2.
- `FCCD-P00-007` — BLOCKED only on real target-machine CLI fallback evidence after probe infrastructure was merged by PR #1.
- `FCCD-P00-008` — CLOSED after abandoned Worker 3 work was recovered, Windows probe defects were repaired, and the complete real Unity target contract passed.
- `FCCD-P00-009` — BLOCKED only on real target Blender execution after reusable probe infrastructure, 15/15 self-tests, contract docs, evidence, and unified-runner integration were completed on this Windows host where Blender is not installed.
- `FCCD-P00-006`, `010` — still require legitimate P00 convergence work.
- PR #1 FCC/CLI probe infrastructure and Worker 2 streaming/session/failure infrastructure remain preserved.

## Current objective

Complete P00 by:

1. obtaining real Blender target execution on a host with Blender installed,
2. reconciling the observed FCC/provider 503 evidence and closing eligible FCC contract tasks,
3. reconciling the primary runtime decision and compatibility baseline,
4. rerunning the complete unified suite when the external Blender/provider prerequisites are available,
5. running the complete P00 exit gate and fixing every failure before closure.

## Recommended remaining worker lanes inside P00

```text
LOCAL TARGET VALIDATION WORKER
  rerun Blender target evidence after Blender becomes available

W5 CONVERGENCE
  reconcile FCCD-P00-002/003/004/005/007 target evidence,
  close eligible blocked tasks,
  complete FCCD-P00-006 + FCCD-P00-010,
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
8. Preserve and reuse PR #1 FCC/CLI, Worker 2 streaming/session/failure, and Worker 3 Unity probe infrastructure.
9. Continue only legitimate non-overlapping work in `CURRENT_PHASE`.
10. Do not promote `NEXT_PHASE` until the current exit gate is recorded `PASS`.
