# P04-007 Integrated Task Reconciliation — 2026-09-04

## Task

`FCCD-P04-007 — Start/stop/retry supervision`

## Reconciliation purpose

This record closes only the already-implemented and canonically integrated P04-007 task. It does not close P04, run the P04 exit gate, advance to P05, or set `VERIFIED_FINAL_COMPLETE=true`.

## Live integration provenance

- Current phase at reconciliation: `P04 — FCC / fcc-claude runtime core`.
- Implementation PR: #113, `P04-007: supervise runtime start stop and retry`.
- Exact implementation worker head: `a1e0d023e8450692aea2bf6f634323e1898c7b96`.
- Candidate PR synthetic merge tested by GitHub-hosted Windows CI: `6a7511e74954bcfbb64a6d04d6eca9b80821f9c9`.
- PR #113 normal merge commit on canonical `main`: `9e0dc4e805913a5beceeb20224d3b726581d449c`.
- The normal merge preserves exact worker head `a1e0d023e8450692aea2bf6f634323e1898c7b96` as a parent.

## Implemented contract

P04-007 adds transport-neutral lifecycle supervision on the project-owned `IAgentRuntime` boundary:

- `AgentRuntimeSupervisor` decorates `IAgentRuntime` without coupling the application/UI layer to FCC process details;
- preserves logical task/run identity across supervised attempts;
- forwards normalized runtime events with one monotonic sequence across attempts;
- stop/cancel is idempotent and delegates cancellation to the currently active runtime execution exactly once;
- cancellation suppresses retry;
- retries are serialized so supervised attempts never overlap;
- retry count is bounded by `AgentRuntimeSupervisionOptions`;
- automatic retry is allowed only for a terminal `Failed` result explicitly classified `Retryable` with `UserAction=NotRequired`;
- `Unknown` retryability is not promoted to retryable;
- an explicit product-owned `AgentRuntimeEventKind.Retry` event is emitted before a retry attempt;
- normalized `system/api_retry` observations are not treated as permission to relaunch;
- global queue/cooldown/rate-limit backoff remains P14, generalized OS process supervision remains P08, crash/reboot recovery remains P15, WPF stop/retry UX remains P05, and fresh full real-runtime contract/exit-gate evidence remains P04-008/P04 closure scope.

## Exact candidate validation

Authoritative cloud validation for the implementation candidate:

- Exact worker head: `a1e0d023e8450692aea2bf6f634323e1898c7b96`.
- Windows CI run: `33849646661` / run #155 — **SUCCESS**.
- Runner: GitHub-hosted Windows Server 2025.
- .NET SDK: `10.0.400`.
- Release build: **0 warnings, 0 errors**.
- Unit tests: **24 passed, 0 failed**.
- Integration tests: **37 passed, 0 failed**.
- Dependency, build-metadata, nullable/analyzer/style quality, and test-infrastructure policy: **PASS**.
- FCC environment discovery, runtime health/version compatibility, structured runtime, runtime event normalization, and CLI fallback permanent validators: **PASS**.
- Inherited P02 static/negative/recovery/runtime validators: **PASS**.
- Complete permanent Windows CI baseline: **PASS**.

Deterministic supervision coverage verifies retry-success flow, non-retry of unknown retryability, user-action blocking, bounded attempts, cancellation/idempotence, disabled automatic retry, non-overlap, identity preservation, monotonic event sequence, and unsafe attempt-count rejection.

## Exact post-merge validation

- Canonical merge SHA: `9e0dc4e805913a5beceeb20224d3b726581d449c`.
- Exact post-merge Windows CI run: `33850126499` / run #156 — **SUCCESS**.
- Windows Release job completed successfully on that exact canonical-main SHA.

## Evidence classification and boundaries

This task-level evidence is **GitHub-hosted Windows deterministic/runtime-fixture evidence plus canonical integration provenance**. It does not create or claim a fresh real provider/FCC turn, provider readiness, a real provider 429, fresh session/resume success, fresh fallback switching, owner-target manual evidence, P04 exit-gate success, or P05 UI behavior.

Authoritative P00 target evidence remains immutable architecture input. `FCCD-P04-008` and the P04 exact-head exit gate retain ownership of the fresh full real-runtime contract suite required for P04 closure.

## Reconciliation decision

`FCCD-P04-007` satisfies task-level closure criteria:

- implementation exists and is integrated on canonical `main`;
- exact candidate Windows CI passed;
- exact post-merge canonical-main Windows CI passed;
- task-specific deterministic lifecycle fixtures passed;
- no task-local regression is known;
- durable evidence and canonical governance reconciliation are recorded by this branch.

Therefore the canonical task state may be reconciled to **CLOSED** when this reconciliation branch is normally merged and the exact resulting `main` remains green.

P04 remains `IN_PROGRESS`; `PHASE_EXIT_GATE=NOT_RUN`; `FCCD-P04-008` remains `PENDING`; P05 remains prohibited; `VERIFIED_FINAL_COMPLETE=false`.
