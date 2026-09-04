# FCC Code Desktop — FCC Runtime Event Normalization

**Task:** `FCCD-P04-005 — Runtime event normalization`  
**Phase:** P04  
**Status:** production normalization contract

## Purpose

The primary structured FCC runtime must expose stable product-owned `AgentRuntimeEvent` semantics without binding later UI, persistence, or supervision code to raw FCC/Claude JSON. Normalization is deliberately loss-preserving: every recognized event retains the sanitized upstream frame payload and an upstream-derived `SourceType`, while unrecognized shapes remain explicit `Unknown` events rather than being dropped or guessed away.

This contract extends the P04-003 structured adapter. It does not alter the target-observed invocation surface `fcc-claude --print --output-format stream-json --verbose` and does not replace the P04-004 plain CLI fallback.

## Evidence classes

### TARGET_OBSERVED

The following mappings are directly grounded in authoritative P00 Windows target evidence:

- `system/init` with a non-empty session identity → `SessionIdentified`;
- `system/api_retry` → `Retry`, retaining sanitized fields such as `attempt`, `max_retries`, `retry_delay_ms`, `error_status`, `error`, session identity, and correlation identity in the payload.

The P00 target observed `system/api_retry` while the upstream provider returned HTTP 503. P04-005 does not convert that observation into a retry policy or a claim that provider recovery succeeded.

### COMPATIBILITY

Successful assistant/tool/final-result shapes were not observed in the P00 provider-backed target run. P04-005 therefore recognizes only explicit, structurally self-describing compatibility shapes and keeps their complete sanitized upstream payload:

- assistant text content blocks → `AssistantTextDelta`;
- assistant `tool_use` / `server_tool_use` content blocks → `ToolStarted`;
- user/assistant tool-result content blocks → `ToolResult`;
- `stream_event` text deltas → `AssistantTextDelta`;
- `stream_event` tool input JSON deltas → `ToolProgress`;
- `stream_event` tool block starts → `ToolStarted`;
- explicit usage objects → `Usage`;
- explicit result/completion frames → `Completion`;
- explicit error frames → `Error`;
- explicit status frames → `RuntimeStatus`.

These rules are compatibility mechanics, not new evidence that the owner's installed FCC version emitted those successful-provider shapes. `FCCD-P04-008` and the P04 exact-head exit gate retain ownership of real successful-provider contract validation.

## Unknown-event preservation

Any frame or nested content/delta/block shape that does not satisfy a recognized rule emits `AgentRuntimeEventKind.Unknown`. Its upstream-derived source type and sanitized payload remain available to diagnostics and future compatibility work. A frame may emit both recognized projections and an `Unknown` projection when it contains a future nested block beside recognized blocks.

No normalizer rule may silently discard a valid upstream frame solely because its schema is newer than the product.

## Ordering and correlation

- Event sequence numbers are monotonically increasing and contiguous within one `IAgentRuntimeExecution`.
- Multiple normalized projections from one upstream frame share one observation timestamp and one sanitized payload.
- Frame `uuid`/`id`, tool-use IDs, and tool-result correlation IDs are retained when explicitly present.
- Once a session ID has been observed, later normalized events may carry that known session identity even when an individual frame omits it.

## Security and bounded retention

Sanitized JSON payloads continue to redact credential-shaped property names such as token, secret, password, authorization, API-key, credential, and cookie fields before they reach product-owned events. Text projected into `AgentRuntimeEvent.Text` also redacts credential-assignment patterns and is bounded by the configured structured-runtime payload limit.

Normalization does not persist unsanitized upstream payloads and does not weaken the P04-003 bounded-capture contract.

## Terminal and supervision boundary

Runtime events are observations, not supervision decisions. In particular:

- a `Retry` event does not sleep, schedule, or launch another run;
- an `Error` event does not by itself override the adapter's terminal process/result classification;
- normalization does not implement backoff, duplicate-run prevention, graceful/forced escalation policy, cooldown, or global queue behavior;
- P04-007 owns start/stop/retry supervision;
- P14 owns global serial queue/cooldown/rate-limit coordination.

## Validation boundary

Permanent Windows CI uses a repository-owned fake `fcc-claude` executable to verify normalization mechanics, ordering, Unicode handling, correlation, unknown-shape preservation, redaction, and inherited P04-003 adapter behavior. That fixture is synthetic and does **not claim provider execution**.

Real FCC/provider successful-event acceptance remains `FCCD-P04-008` / the P04 exit gate. P04-005 makes no new provider/FCC target-execution claim.
