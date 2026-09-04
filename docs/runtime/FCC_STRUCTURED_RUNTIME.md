# FCC Structured Runtime Adapter

**Task:** `FCCD-P04-003 — Primary FCC/Claude structured runtime adapter`  
**Phase:** P04 — FCC / `fcc-claude` runtime core  
**Decision:** implements existing ADR-017 and the closed P00 runtime/stream/session contracts.

## Production boundary

`FccStructuredAgentRuntime` is the primary `IAgentRuntime` implementation for the observed local `fcc-claude` process surface. It launches the discovered executable as an owned non-shell process using the exact P00-observed primary argument contract:

```text
--print --output-format stream-json --verbose <prompt>
```

When `AgentRuntimeRequest.ResumeSessionId` is present, the adapter uses the target-verified new-process continuation surface:

```text
--print --output-format stream-json --verbose --resume <session-id> <prompt>
```

Arguments are added through `ProcessStartInfo.ArgumentList`; the prompt and session identifier are never shell-concatenated. The requested working directory is applied directly to the owned process.

## Structured stream framing

Standard output is treated as newline-delimited JSON. P04-003 owns transport framing and the minimum semantics required to keep the domain contract truthful:

- each non-empty stdout line must parse as a JSON object;
- the target-observed `system/init` shape (including the equivalent `type=system`, `subtype=init` form) extracts `session_id` and emits `SessionIdentified`;
- other valid JSON frames are preserved as `Unknown` with their upstream source type rather than mapped to invented assistant/tool/result semantics;
- common upstream correlation identities such as `uuid`/`id` are retained when present;
- unknown fields survive in sanitized bounded `PayloadJson`;
- a missing structured frame set or any malformed non-empty frame produces `MalformedStream` rather than treating process exit code zero as sufficient success;
- a non-zero process exit after a valid stream produces `NonZeroExit`;
- a process-launch failure for the configured executable produces `RuntimeNotFound`.

This intentionally leaves richer event normalization to `FCCD-P04-005`. Successful assistant delta, tool start/progress/result, usage, provider retry, and terminal event mappings are not invented here merely because the domain enum already has those stable product categories.

## Payload safety and bounds

Persistable upstream JSON is rewritten before it enters `AgentRuntimeEvent.PayloadJson`:

- property names containing token, secret, password, authorization, API-key, credential, or cookie markers are replaced with `[REDACTED]`;
- payloads are bounded by `FccStructuredAgentRuntimeOptions.MaximumPayloadCharacters` (64 KiB default, constrained to 1 KiB–1 MiB);
- an oversized sanitized JSON object is replaced by a small JSON truncation envelope with a bounded sanitized preview.

This is a task-local ingress safeguard. It does not claim the later P16 sink-boundary secret-redaction/security-hardening gate.

## Cancellation boundary

`CancelAsync` stops only the process owned by this execution using `Kill(entireProcessTree: true)` and classifies the execution as `Cancelled`. P04-003 does **not** implement the later P04-007 graceful-interrupt escalation, retry/backoff, cooldown, rate-limit supervision, or restart coordination policy.

## Deliberate phase boundaries

This task does **not** implement:

- `FCCD-P04-004` — CLI fallback runtime adapter;
- `FCCD-P04-005` — complete runtime event normalization policy;
- `FCCD-P04-006` — health/version compatibility policy;
- `FCCD-P04-007` — start/stop/retry supervision;
- `FCCD-P04-008` — complete runtime contract suite and real local-provider harness;
- P05 conversation/UI behavior.

The production adapter is therefore executable infrastructure, not the P04 phase exit gate by itself.

## Verification

`tools/runtime/validate-fcc-structured-runtime.ps1` performs static contract checks, deterministic negative/recovery checks, and a Windows executable fixture. The fixture builds a repository-external fake `fcc-claude` executable and verifies:

- exact primary argument ordering and prompt preservation;
- exact `--resume <session-id>` argument ordering;
- session extraction from `system/init`;
- Unicode/Arabic payload preservation;
- unknown event-type preservation;
- credential-shaped JSON property redaction;
- bounded payload truncation;
- malformed-stream failure;
- non-zero exit failure;
- missing executable failure;
- missing working-directory rejection;
- owned process-tree cancellation.

These checks are **fixture-only** and deliberately do not contact the owner's FCC environment or any provider. P04-003 therefore does not claim provider execution, owner-target execution, real completion, real cancellation, real resume, or new external behavior beyond the already-closed P00 evidence.
