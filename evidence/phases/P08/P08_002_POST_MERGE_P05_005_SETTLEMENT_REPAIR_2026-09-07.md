# P08-002 Post-Merge P05-005 Settlement Regression Repair — 2026-09-07

## Classification

- Source task: `FCCD-P08-002 — Graceful→forced cancellation escalation`
- Integrated implementation merge: `3055b9f27baa047b3217b3256f2b229f78e53981` (PR #192)
- Regression class: cloud-repairable CI fixture timing defect in an earlier-phase permanent validator
- Owner-only evidence required: none
- Task closure claimed by this file: no

## Observed exact-main failure

The exact-main Windows CI run `34077646092` / #451 completed the full Windows Release baseline successfully: Release build produced 0 warnings / 0 errors, unit tests passed 140/140, integration tests passed 75/75, and the baseline reported PASS. The subsequent permanent `P05-005` task-state validator failed in its executable runtime fixture because `WaitForSettledAsync` used a fixed 10-second wall-clock deadline and reported `task did not fully settle before timeout`.

The companion exact-main Workspace Search run `34077646144` / #180 and Large Workspace Safeguards run `34077646165` / #164 both completed SUCCESS on the same merge SHA. The failure was therefore treated as a real CI reliability regression, not deferred and not reclassified as owner-only.

## Repair

The P05-005 executable fixture retains the same full-settlement contract: it still requires `TaskExecutionState` to become non-active and `ValidateCanStart()` to stop reporting the `still settling` guard before it can pass.

The fixture settlement window is now bounded at 30 seconds instead of 10 seconds to tolerate hosted-Windows scheduling variance while still failing closed on a genuinely stuck task. Timeout failure text now records lifecycle state, activity/control flags, and the last `ValidateCanStart` settlement rejection so any future recurrence is diagnosable rather than opaque.

No production `TaskExecutionState`, P08 cancellation semantics, owner-last policy, safety rule, or phase state is changed by this repair.

The guarded temporary patch orchestration self-deleted. Net repair scope before this evidence commit was exactly one modified file: `tools/ui/validate-task-state-machine.ps1`.

## Acceptance boundary

This repair must pass the normal permanent Windows CI, Workspace Search, and Large Workspace Safeguards gates before merge. After repair integration, P08-002 still requires exact-main green verification and canonical task reconciliation before it may become `CLOSED`.
