# P05 phase-exit convergence — cloud complete / owner REAL_TARGET required

```text
PHASE: P05
PHASE_NAME: Conversation + session + task experience
CLOUD_BASELINE_SHA: 47fabb4aa9ea7e29d7526374ed6120d76c4e16d4
CLOUD_BASELINE_WINDOWS_CI_RUN: 33986684958
CLOUD_BASELINE_WINDOWS_CI_RESULT: SUCCESS
MANDATORY_P05_TASKS: 8/8 CLOSED
P05_EXIT_GATE: NOT_RUN
OWNER_EVIDENCE_REQUIRED: true
OWNER_EVIDENCE_CLASSIFICATION: REAL_TARGET
VERIFIED_FINAL_COMPLETE: false
```

## Purpose

This record captures the strongest honest cloud-side convergence available for the P05 phase exit gate. It does **not** close P05 and does not claim that GitHub-hosted CI can substitute for a real owner Windows/FCC/provider interaction.

The canonical P05 exit criterion requires a user to open a project session, issue a **real** task through the local FCC/Claude environment, observe structured execution, exercise stop/retry where applicable, close/reopen the application, and resume without losing durable state. That final interaction cannot be truthfully proven in GitHub-hosted CI because the permanent cloud validators use deterministic fixtures/self-test mechanics rather than the owner's installed provider runtime.

## Integrated cloud evidence

Before this convergence work began, canonical `main` at `47fabb4aa9ea7e29d7526374ed6120d76c4e16d4` already contained all eight P05 mandatory tasks in `CLOSED` state:

- FCCD-P05-001 — Streaming chat rendering
- FCCD-P05-002 — Structured tool activity timeline
- FCCD-P05-003 — Composer/attachments/context
- FCCD-P05-004 — Session create/history/resume
- FCCD-P05-005 — Explicit task state machine
- FCCD-P05-006 — Stop/cancel/retry UX
- FCCD-P05-007 — Markdown/code/diff content rendering
- FCCD-P05-008 — Conversation virtualization/performance

The exact canonical-main Windows CI run `33986684958` completed `SUCCESS` on that SHA. The permanent baseline includes locked restore, format verification, Release build, unit/integration tests, owner-last governance validation, the P04 deterministic runtime contract suite, and the P05 conversation/session/task validators. The workflow additionally runs the P05-005, P05-006, P05-007 and P05-008 validators explicitly.

This proves the cloud-actionable implementation/test/CI side is integrated and green. It does not prove provider-backed interaction on the owner's machine.

## Owner-last preparation

A tracked fail-closed runner is added at:

```text
tools/ui/run-p05-phase-exit-owner-validation.ps1
```

The runner requires:

- authoritative owner Windows environment;
- Git and PowerShell 7;
- exact .NET SDK `10.0.400`;
- installed `fcc-claude` available on `PATH`;
- the exact intended canonical candidate;
- successful deterministic P05 validators before interaction;
- two real application launches on the same exact build;
- a harmless genuine provider-backed task;
- observable streaming/structured activity;
- stop then retry behavior;
- close/reopen and durable session resume;
- sanitized JSON evidence containing booleans/provenance only, never prompt/provider content or credentials.

Expected evidence path:

```text
evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json
```

A PASS from the runner still requires repository review/integration/reconciliation. The runner never changes the queue, phase gate, task ledger, or release state.

## Exit decision

```text
P05_CLOUD_ACTIONABLE_WORK_COMPLETE: true
P05_CLOUD_BASELINE_GREEN: true
P05_REAL_TARGET_OWNER_CHECK_REQUIRED: true
P05_EXIT_GATE: NOT_RUN
P05_PHASE_STATE: IN_PROGRESS
KNOWN_CLOUD_BLOCKERS: 0
VERIFIED_FINAL_COMPLETE: false
```

The sole remaining P05 exit-gate evidence is genuinely owner/environment-bound. Under owner-last governance it must remain an explicit release blocker until genuinely executed, reviewed, integrated and reconciled. No product defect or failed CI is being deferred by this record.
