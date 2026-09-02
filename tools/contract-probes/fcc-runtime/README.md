# FCC P00 Streaming / Session / Failure Probes

This directory implements the reusable remote-worker probe infrastructure for:

- `FCCD-P00-003` structured streaming
- `FCCD-P00-004` session/session-ID/resume behavior
- `FCCD-P00-005` cancellation and failure behavior

It extends, rather than replaces, the canonical PR #1 FCC discovery / CLI fallback infrastructure under `tools/contract-probes/fcc/`.

## Evidence boundary

Synthetic fixtures are `SELF_TEST_ONLY`. They prove parser, capture, cancellation, cleanup, timeout, redaction, session-extraction, and failure-classification mechanics. They do **not** prove actual FCC/fcc-claude event schemas, session semantics, cancellation semantics, or provider errors.

Real target behavior remains `TARGET_UNVERIFIED` until this probe is executed on the owner's Windows target machine.

## Files

- `probe.mjs` — target-capable streaming/session/failure recorder and analyzer.
- `self-test.mjs` — deterministic self-test suite.
- `fixture-process.mjs` — harmless owned child process used only by self-tests.

The existing PR #1 `probe.mjs` is intentionally left unchanged. It does not expose an importable shared-library API, and changing its command-line lifecycle solely for Worker 2 would create avoidable regression risk. Worker 2 therefore reuses the same safe process principles and limits duplicated compatibility logic to the minimum required by this specialized raw-stream recorder.

## Self-test

```text
node tools/contract-probes/fcc-runtime/self-test.mjs
```

The suite exercises:

- valid JSON event parsing,
- JSON split across multiple process chunks,
- UTF-8/Arabic output,
- stderr/stdout interleaving,
- malformed JSON without parser crash,
- unknown event type preservation,
- large event handling,
- abrupt EOF handling,
- raw frame order/stream/byte-length/hash capture,
- split-secret redaction across chunk boundaries,
- session ID candidate extraction,
- non-zero exit classification,
- synthetic rate-limit classification mechanics,
- timeout escalation,
- graceful interrupt then forced owned-tree termination,
- process-tree cleanup,
- missing runtime classification,
- persisted-output secret scan.

## Target probe

Safe discovery without provider-backed execution:

```text
node tools/contract-probes/fcc-runtime/probe.mjs --mode all --json tmp/fcc-runtime.json
```

Real target execution, only on the intended Windows FCC environment:

```text
node tools/contract-probes/fcc-runtime/probe.mjs --mode all --allow-live-prompt --json tmp/fcc-runtime.json
```

The probe never guesses structured-streaming, resume, or continue syntax. If target help/output establishes exact syntax, supply it explicitly:

```text
--stream-args-json '["<observed args>","{prompt}"]'
--resume-args-json '["<observed resume args>","{sessionId}","{prompt}"]'
--continue-args-json '["<observed continue args>","{prompt}"]'
```

`{prompt}` and `{sessionId}` are substituted as structured argument elements. No shell string concatenation is used.

## Streaming evidence model

Raw process chunks are recorded with:

- global sequence,
- stdout/stderr source,
- relative timestamp,
- byte length,
- SHA-256 of the original bytes,
- sanitized decoded text.

Secret masking for raw frames is applied against each complete stream before the sanitized stream is split back into the original decoded chunk boundaries. This prevents a credential split across two chunks from leaking into persisted raw-frame text.

Separately, line analysis records:

- `JSON_EVENT`,
- `TEXT_LINE`,
- `MALFORMED_JSON`,
- `EMPTY_LINE`,
- upstream `type`/`event`/`kind`/`name` value when present as an **event type hint**,
- key-based semantic hints,
- session-ID candidates,
- raw sanitized line,
- parsed sanitized JSON where valid.

These analytical hints do not assert an FCC schema. Unknown event types remain preserved.

## Session evidence model

The probe:

1. records candidate session IDs from observed JSON keys/text,
2. records help options whose names contain session/resume/continue/conversation/thread terminology,
3. does not execute resume without an explicit target-observed `--resume-args-json` template,
4. when supplied, starts a new process after the initial process has exited,
5. requires exact structured `FIRST_TURN_OK`, `RESUME_OK`, and post-invalid-session context markers,
6. uses a syntactically valid nonexistent UUID for the invalid-session case,
7. verifies the valid session still resumes after that negative case,
8. records target-observed `--continue` semantics when an explicit template is supplied,
9. records separate disposable initial/resume working directories and process-tree cleanup.

The probe does not forcibly restart FCC or disconnect/reconnect a provider merely to create failure cases.

## Failure evidence model

Observable classifications include:

- `RUNTIME_NOT_FOUND`
- `FCC_UNAVAILABLE`
- `AUTH_FAILURE`
- `MODEL_UNAVAILABLE`
- `PROVIDER_UNAVAILABLE`
- `PROVIDER_BUSY_OR_OVERLOADED`
- `RATE_LIMITED`
- `TIMEOUT`
- `MALFORMED_STREAM`
- `INTERRUPTED`
- `PROCESS_CRASH`
- `NONZERO_EXIT`
- `UNKNOWN_FAILURE`
- `SUCCESS`

`retryability` and `userActionRequired` remain `UNKNOWN` unless direct evidence supports a stronger conclusion. The probe never creates artificial request load to force a 429.

## Unified target runner

Worker 2 is integrated into:

```text
tools/contract-probes/run-target-validation.ps1
```

That script currently orchestrates the canonical PR #1 FCC probes plus Worker 2 probes. It remains non-zero/incomplete until the separate Unity and Blender worker lanes integrate their own target probes.
