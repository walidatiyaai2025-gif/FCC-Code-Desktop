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
KNOWN_RELEASE_BLOCKERS: 0
VERIFIED_FINAL_COMPLETE: false
LAST_RECONCILED: 2026-08-31
```

## Active rule

Do not start P01 implementation until every mandatory P00 task is `CLOSED` and the P00 exit gate in `docs/EXECUTION_PLAN.md` is `PASS` with exact-head evidence.

Before any worker selects new work, it must apply `docs/WORKER_PROTOCOL.md`: repair broken canonical state, resolve blockers, recover abandoned/stale work, and finish integration-pending work before claiming an unrelated new task.

## Current objective

Complete P00 by proving and recording the real contracts for:

- local FCC / `fcc-claude` discovery, version and health,
- structured streaming,
- sessions/resume,
- cancel/error/rate-limit behavior,
- primary runtime adapter,
- CLI fallback,
- Unity CLI/test/build integration,
- Blender CLI/background/Python/render/export integration,
- supported compatibility baseline.

## Resume procedure

1. Read `AGENTS.md`.
2. Read `PROJECT_CONTROL.md`.
3. Read `docs/EXECUTION_PLAN.md`.
4. Read `docs/WORKER_PROTOCOL.md`.
5. Read `docs/TASK_LEDGER.md`.
6. Fetch live branches/PRs/issues/commits, CI/evidence, and build a claim + recovery map.
7. First resolve broken/blocking/abandoned/integration-pending work in `CURRENT_PHASE` according to `docs/WORKER_PROTOCOL.md`.
8. Only when no such work exists, claim the next legitimate unclaimed task in `CURRENT_PHASE`.
9. Do not promote `NEXT_PHASE` until the current exit gate is recorded `PASS`.
