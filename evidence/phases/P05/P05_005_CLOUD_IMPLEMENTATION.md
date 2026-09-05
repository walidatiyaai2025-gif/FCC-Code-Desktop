# FCCD-P05-005 — Cloud implementation evidence

Evidence class: `SELF_TEST_ONLY / CLOUD`

Task: `FCCD-P05-005 — Explicit task state machine`

Status: `IMPLEMENTED — exact PR-head CI, normal merge, and exact-main verification still required before CLOSED reconciliation`.

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

## Closure rule

Do not mark `FCCD-P05-005` `CLOSED` from this file alone. Closure requires:

1. exact implementation PR-head Windows CI SUCCESS;
2. normal merge commit into canonical `main`;
3. exact merge-SHA Windows CI SUCCESS;
4. integrated reconciliation updating the canonical task ledger/current-phase checkpoint without weakening owner-last governance.
