# FCC Runtime Start / Stop / Retry Supervision

**Task:** `FCCD-P04-007 — Start/stop/retry supervision`  
**Phase:** P04 — FCC / fcc-claude runtime core

## Purpose

`AgentRuntimeSupervisor` is a transport-neutral `IAgentRuntime` decorator. It supervises one logical runtime execution without coupling application/UI code to FCC process details.

The supervisor owns only the P04 lifecycle policy required to:

- start one logical runtime execution through the selected `IAgentRuntime`;
- preserve the caller's task/run identity across supervised attempts;
- forward normalized runtime events with one monotonic sequence across attempts;
- stop the active execution through `IAgentRuntimeExecution.CancelAsync`;
- make repeated stop requests idempotent;
- suppress retry after cancellation;
- serialize retries so attempts never overlap;
- bound the number of attempts;
- emit an explicit product-owned `Retry` event before a retry attempt.

## Evidence-bounded retry rule

Automatic retry is intentionally conservative.

A failed attempt is eligible only when all of these are true:

1. automatic retry is enabled;
2. the configured maximum attempt count has not been reached;
3. cancellation has not been requested;
4. the terminal state is `Failed`;
5. `AgentRuntimeFailure.Retryability == Retryable`;
6. `AgentRuntimeFailure.UserAction == NotRequired`.

`Unknown` retryability is **not** promoted to retryable by the supervisor. This preserves the P00 failure contract, which requires retryability/user-action semantics to remain unknown unless target evidence supports a stronger classification.

The supervisor does not interpret `system/api_retry` frames as permission to relaunch a process. Those frames remain normalized runtime observations; P04-005 deliberately does not own relaunch policy.

## Cancellation boundary

The supervisor does not target operating-system processes directly. It calls the active transport's `CancelAsync` once and waits for the supervised pump to reach terminal state. Concrete FCC adapters remain responsible for their owned-process cancellation implementation.

The P00 target contract remains authoritative for FCC cancellation evidence: graceful interrupt was attempted, only observed owned process identities were eligible for residual cleanup, and zero owned processes remained after the authoritative target run. No code in this task kills by executable name.

The general reusable process-supervision/escalation framework remains P08 scope.

## Explicit non-goals / later-phase ownership

P04-007 does **not** implement:

- global durable queue ownership or concurrency policy (`P14`);
- the 15-second inter-run cooldown (`P14`);
- provider/rate-limit sleep or exponential backoff (`P14`);
- queue reordering or cross-session scheduling (`P14`);
- crash/reboot recovery and startup reconciliation (`P15`);
- WPF stop/retry controls (`P05` and later UI integration);
- fabricated provider/FCC retryability classifications;
- the full real-runtime contract suite or P04 exit gate (`P04-008`).

## Validation

Deterministic unit coverage proves:

- an explicitly retryable/no-user-action failure retries and then succeeds;
- retry attempts are strictly serial (maximum concurrent attempt count is one);
- logical task/run identity is preserved;
- forwarded/retry events receive monotonic sequences;
- unknown retryability does not trigger a retry;
- required user action blocks automatic retry;
- retry count is bounded;
- cancellation is idempotent, reaches the active execution once, and suppresses retry;
- automatic retry can be disabled;
- unsafe attempt-count bounds are rejected.

These are repository-owned fixture results, not claims about a live provider. Full target-backed start/stop/resume/fallback/error behavior remains owned by P04-008 and the exact-head P04 exit gate.
