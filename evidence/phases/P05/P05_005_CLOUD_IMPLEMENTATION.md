# FCCD-P05-005 — Cloud implementation evidence

Evidence class: `SELF_TEST_ONLY / CLOUD`

Task: `FCCD-P05-005 — Explicit task state machine`

Status: `INTEGRATED — exact PR-head CI, normal merge, and exact-main verification succeeded; see integrated reconciliation evidence for canonical closure provenance`.

## Recovered implementation

The legitimate P05-005-only commits from the stale mixed convergence branch were recovered onto `worker/fccd-p05-005-recovery`, preserving ancestry from canonical main `1f3f73ac1137720497a2b798f2a0d17895a0b614` while excluding later P05-006/P05-007/P05-008 implementation.

Recovered and hardened P05-005 scope:

- explicit `TaskLifecycleState` and fail-closed transition matrix;
- one active or still-settling logical task per workspace;
- task/run/session identity ownership;
- P03 SQLite task/agent/event journal integration;
- durable terminal state written before terminal UI projection;
- foreign-key-safe cancellation/failure recovery ordering;
- cleanup of runtime executions that fail identity/startup handoff;
- normalized runtime event projection into the existing conversation state;
- durable assistant output and runtime-session binding;
- bounded task failure diagnostics;
- Tasks workspace status surface;
- production FCC discovery + structured runtime + supervision composition;
- per-execution zero-based/contiguous source-sequence validation;
- conversation-facing monotonic event sequencing across successive runtime executions.

## Cloud validation

Permanent gate:

```powershell
.\tools\ui\validate-task-state-machine.ps1 -RunFixtures -RequireRuntime
```

The Windows CI policy itself requires that workflow invocation, with a negative fixture that rejects removal.

The executable fixture uses controlled local `IAgentRuntime` implementations; it does not contact an external provider. It validates:

- two consecutive successful logical tasks with distinct task/run IDs;
- source sequences restarting at zero per execution while presentation sequence remains monotonic;
- durable P03 task/event journal rows;
- durable P05 user/assistant conversation history;
- runtime session ID persistence;
- active/settling task rejection;
- classified failure with bounded diagnostic output;
- source sequence origin and gap corruption fail closed;
- mismatched runtime execution identity is cleaned up and fails closed;
- unavailable runtime is rejected before task creation;
- production WPF task surface construction.

Static and negative fixtures additionally protect terminal persistence ordering, execution cleanup ownership, task navigation composition, semantic resource usage, and removal of raw provider payload persistence from the task journal.

## Safety boundaries

- No raw provider `PayloadJson` is persisted into the task journal.
- Cross-session runtime output is stopped rather than attached to the wrong session.
- Runtime execution ownership is cleaned before a failed startup can permit another task.
- Terminal UI success is not projected before durable terminal state is recorded.
- No provider 429, FCC target execution, target Windows installation, or manual evidence is fabricated.
- Existing `OWNER-P04-008-REAL-TARGET` remains queued and release-blocking.
- No new owner-only obligation is introduced by P05-005 cloud mechanics.
- P15 retains ownership of crash/reboot reconciliation for interrupted work.

## Integrated provenance

- Exact implementation candidate: `cb7edc6909235a275949b6e184ceabb2a8340859`.
- Exact PR-head Windows CI: run `33953673037` / run #217 — SUCCESS.
- PR #132 was normally merged as `7ee9feab02a5691246452d4e472d110cd420e443`.
- Exact post-merge canonical-main Windows CI: run `33953912542` / run #218 — SUCCESS on exact merge SHA `7ee9feab02a5691246452d4e472d110cd420e443`.
- Canonical integration evidence: `evidence/phases/P05/P05_005_INTEGRATED_RECONCILIATION_2026-09-05.md`.

This file remains cloud/self-test evidence. It does not convert the queued P04 REAL_TARGET obligation into provider-backed PASS.