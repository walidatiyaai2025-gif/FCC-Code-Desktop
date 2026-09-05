# FCCD-P05-005 — Cloud implementation evidence

Evidence class: `SELF_TEST_ONLY / CLOUD`

Task: `FCCD-P05-005 — Explicit task state machine`

Status at creation: `IMPLEMENTED — exact PR-head CI, normal merge, and exact-main verification still required before CLOSED reconciliation`.

## Recovered implementation

The legitimate P05-005-only commits from the stale mixed convergence branch were recovered onto `worker/fccd-p05-005-recovery`, preserving ancestry from canonical main `1f3f73ac1137720497a2b798f2a0d17895a0b614` while excluding later P05-006/P05-007/P05-008 implementation.

Recovered P05-005 scope:

- explicit `TaskLifecycleState` and fail-closed transition matrix;
- one active logical task per workspace;
- task/run/session identity ownership;
- P03 SQLite task/agent/event journal integration;
- normalized runtime event projection into the existing conversation state;
- durable assistant output and runtime-session binding;
- Tasks workspace status surface;
- production FCC discovery + structured runtime + supervision composition;
- conversation-facing monotonic event sequencing across successive runtime executions.

## Cloud validation

Permanent gate:

```powershell
.\tools\ui\validate-task-state-machine.ps1 -RunFixtures -RequireRuntime
```

The Windows CI policy itself requires that workflow invocation, with a negative fixture that rejects removal.

The executable fixture uses a controlled local `IAgentRuntime`; it does not contact an external provider. It validates:

- two consecutive successful logical tasks with distinct task/run IDs;
- source sequences restarting per execution while presentation sequence remains monotonic;
- durable P03 task/event journal rows;
- durable P05 user/assistant conversation history;
- runtime session ID persistence;
- one-active-task rejection;
- classified failed terminal state;
- corrupted source sequence fails closed;
- unavailable runtime is rejected before task creation;
- production WPF task surface construction.

## Safety boundaries

- No raw provider `PayloadJson` is persisted into the task journal.
- Cross-session runtime output is stopped rather than attached to the wrong session.
- No provider 429, FCC target execution, target Windows installation, or manual evidence is fabricated.
- Existing `OWNER-P04-008-REAL-TARGET` remains queued and release-blocking.
- No new owner-only obligation is introduced by P05-005 cloud mechanics.

## Closure rule

Do not mark `FCCD-P05-005` `CLOSED` from this file alone. Closure requires:

1. exact implementation PR-head Windows CI SUCCESS;
2. normal merge commit into canonical `main`;
3. exact merge-SHA Windows CI SUCCESS;
4. integrated reconciliation updating the canonical task ledger/current-phase checkpoint without weakening owner-last governance.
