# FCC Structured Streaming Contract

**Task:** `FCCD-P00-003`  
**Phase:** P00  
**Probe:** `tools/contract-probes/fcc-runtime/probe.mjs`  
**Self-test:** `tools/contract-probes/fcc-runtime/self-test.mjs`

## Status vocabulary

- `VERIFIED_ON_TARGET` — directly observed on the owner's intended Windows FCC environment.
- `SELF_TEST_VERIFIED` — verified only with repository-owned synthetic fixtures.
- `TARGET_UNVERIFIED` — target observation is still required.
- `NOT_OBSERVED` — a safe target run occurred but did not expose the behavior.
- `UNSUPPORTED` — target evidence explicitly proves the runtime lacks the behavior.
- `UNKNOWN` — evidence is insufficient to classify further.

Synthetic fixtures are never FCC behavior evidence.

## SELF_TEST_VERIFIED — recorder/parser mechanics

The repository probe verifies that it can:

- preserve global process-chunk order,
- distinguish stdout and stderr,
- record byte length and SHA-256 for each original process chunk,
- sanitize persisted chunk text even when a credential-shaped value is split across chunks,
- reconstruct UTF-8 safely across chunk boundaries,
- retain JSON events and plain text separately,
- process JSON split across multiple process chunks,
- preserve unknown upstream event types as hints rather than discard them,
- record malformed JSON without aborting the whole capture,
- handle Arabic/Unicode text,
- handle a large event,
- flush an unterminated final line at abrupt EOF,
- preserve parsed sanitized payloads separately from raw sanitized evidence.

The parser's stable transport-level classifications are intentionally generic:

```text
JSON_EVENT
TEXT_LINE
MALFORMED_JSON
EMPTY_LINE
```

When valid JSON contains a string `type`, `event`, `kind`, or `name`, its value is retained as `eventTypeHint`. Matching keys may create analytical hints such as delta/tool/result/progress/error/usage/session-like keys. These are discovery aids, not an invented FCC event schema.

## TARGET_UNVERIFIED — actual FCC streaming contract

Target execution must still determine:

- whether the installed FCC/fcc-claude path exposes structured streaming at all,
- the exact target invocation syntax,
- actual event schema and event-type values,
- text/assistant delta semantics,
- tool-use start/payload/result semantics,
- progress/final result metadata,
- error event shape,
- usage/token information if present,
- session identifiers if present,
- real stdout/stderr distribution,
- real frame/order/timestamp behavior,
- whether malformed/truncated output occurs in normal/error paths.

The probe does not guess `--output-format`, `stream-json`, or similar flags. It records streaming-related help-option hints. Exact target-observed syntax may be supplied via `--stream-args-json`.

## Raw evidence requirements

A target evidence file must retain sanitized:

- raw frame sequence/source/timing,
- original frame byte length and SHA-256,
- sanitized frame text,
- line-level classification,
- parsed sanitized JSON where valid,
- unknown event-type hints,
- malformed frame details,
- final stdout/stderr and process status.

If output exceeds the probe's bounded capture size, `outputTruncated=true` is evidence of an incomplete capture and must not be silently treated as complete structured-stream verification.
