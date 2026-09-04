# FCC Code Desktop — Agent Runtime Domain Contract

**Task:** `FCCD-P04-002 — IAgentRuntime domain contract`  
**Phase:** P04  
**Status:** implementation contract; transport adapters remain later P04 tasks

## Purpose

`IAgentRuntime` is the project-owned boundary between FCC Code Desktop product logic and unstable FCC/Claude transport details. UI, persistence, conversation, queue, and recovery features consume this contract rather than binding to `fcc-claude` command flags, stdout/stderr framing, process identifiers, or upstream JSON schemas.

This implements ADR-005 and ADR-017 without implementing the P04-003 structured adapter or P04-004 CLI fallback adapter.

## Execution model

A runtime exposes an immutable `AgentRuntimeDescriptor` and starts one `AgentRuntimeRequest` at a time.

A request carries:

- durable product task identity (`TaskId`),
- durable agent-run identity (`RunId`),
- prompt text without destructive normalization,
- working-directory context,
- optional previously observed runtime session identity for resume.

Starting returns an `IAgentRuntimeExecution` handle. The handle exposes:

- matching task/run correlation,
- an asynchronous normalized event stream,
- one terminal completion task,
- an explicit cancellation seam,
- asynchronous disposal for adapter-owned cleanup.

Expected runtime/provider failures are represented by a terminal `AgentRuntimeResult`; they are not required to escape as transport-specific exceptions. Adapter programming faults and invalid contract use may still throw normally.

## Adapter descriptors and capabilities

`AgentRuntimeDescriptor` identifies the adapter independently of the upstream installation. The current transport vocabulary is:

- `StructuredProcess` — primary structured process adapter selected by ADR-017,
- `CliFallback` — compatibility fallback,
- `Fixture` — repository-owned deterministic test implementation.

Capabilities declare whether an adapter supports streaming, session identity, resume, cancellation, and tool activity. Resume capability is invalid unless session identity is also supported.

A descriptor version is optional because version compatibility and health policy belong to `FCCD-P04-006`; absence of a version must not be silently treated as compatibility success.

## Normalized event envelope

`AgentRuntimeEvent` is transport-neutral and ordered by a non-negative sequence number. Stable normalized kinds are:

- `RuntimeStatus`,
- `AssistantTextDelta`,
- `ToolStarted`,
- `ToolProgress`,
- `ToolResult`,
- `SessionIdentified`,
- `Usage`,
- `Retry`,
- `Error`,
- `Completion`,
- `Unknown`.

The envelope can retain text, session identity, correlation identity, sanitized upstream `sourceType`, and sanitized upstream JSON payload.

Unknown upstream event types are never discarded. An `Unknown` normalized event is invalid unless the upstream source type is retained. This preserves the P00 streaming-contract rule that future FCC event shapes remain evidence rather than being guessed away.

`FCCD-P04-005` owns the concrete normalization rules from observed FCC frames into this envelope.

## Failure model

The domain failure taxonomy is based on the observed/safely testable P00 failure classes:

- `RuntimeNotFound`,
- `FccUnavailable`,
- `AuthenticationFailure`,
- `ModelUnavailable`,
- `ProviderUnavailable`,
- `ProviderBusyOrOverloaded`,
- `RateLimited`,
- `Timeout`,
- `MalformedStream`,
- `Interrupted`,
- `ProcessCrash`,
- `NonZeroExit`,
- `UnknownFailure`.

A failure may carry a positive status code and a sanitized source hint. Retryability and whether user action is required are tri-state values with `Unknown` as the default. This deliberately preserves the P00 requirement that those properties are not inferred without evidence.

No real rate-limit observation is claimed by this contract. `RateLimited` exists as a domain classification because deterministic classifier mechanics are required, while the P00 target evidence remains `NOT_OBSERVED_ON_TARGET` for provider 429 behavior.

## Terminal result invariants

- `Succeeded` cannot contain a failure.
- `Failed` must contain a classified failure.
- `Cancelled` is a terminal state independent of process implementation details; adapters may additionally emit an `Interrupted` failure event when supported by evidence.
- Session identity is optional and is populated only when actually observed/known.

Process exit code alone is not a domain success criterion. Later adapters are responsible for interpreting their transport contract and producing the truthful terminal result.

## Cancellation and ownership boundary

The domain contract exposes `CancelAsync` but does not prescribe how a process is interrupted. P04-003/P04-004 adapters and P04-007 supervision must implement the verified graceful-to-forced owned-process behavior without terminating unrelated same-name processes.

Disposing an execution is a cleanup boundary; it is not evidence that a run succeeded.

## Phase ownership boundaries

This task does **not** implement or claim:

- FCC/`fcc-claude` discovery (`FCCD-P04-001`),
- the structured `--print --output-format stream-json --verbose` process adapter (`FCCD-P04-003`),
- the single-result CLI fallback (`FCCD-P04-004`),
- concrete upstream-frame normalization (`FCCD-P04-005`),
- health/version compatibility decisions (`FCCD-P04-006`),
- retry/process supervision policy (`FCCD-P04-007`),
- real local FCC headless contract acceptance (`FCCD-P04-008` / P04 exit gate),
- P05 UI behavior,
- P14 global queue/cooldown/rate-limit coordination.

No provider prompt, real FCC target run, manual evidence, or external-runtime success is claimed by `FCCD-P04-002`.
