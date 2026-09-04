# FCC Code Desktop — CLI Fallback Runtime

**Task:** `FCCD-P04-004 — CLI fallback runtime adapter`  
**Phase:** P04 — FCC / `fcc-claude` runtime core

## Purpose

`FccCliFallbackAgentRuntime` provides the compatibility path required when the richer structured runtime surface is unavailable or unsuitable. It implements the same project-owned `IAgentRuntime` boundary as the primary adapter without coupling consumers to FCC CLI details.

The implementation is derived from the authoritative P00 Windows evidence and `docs/contracts/P00_RUNTIME_AND_COMPATIBILITY_BASELINE.md`. That evidence verified provider-backed fallback execution using the plain `fcc-claude --print <prompt>` surface from normal, space-containing, and Unicode/Arabic working directories, together with stdout/stderr observability, cancellation, and owned-process cleanup.

## Invocation contract

The fallback process is started with `ProcessStartInfo` and an argument array:

```text
fcc-claude
  --print
  <prompt>
```

`UseShellExecute=false`; stdout and stderr are redirected and read concurrently as UTF-8. Prompt text is passed as a single argument rather than concatenated into a shell command.

The P04-004 adapter deliberately does **not** add `--output-format stream-json`, `--verbose`, `--resume`, or guessed flags. Those richer semantics belong to the primary structured adapter or later authorized runtime work.

## Product-owned descriptor

The adapter advertises:

```text
RuntimeId: fcc.cli-fallback
Transport: AgentRuntimeTransport.CliFallback
SupportsStreaming: false
SupportsSessions: false
SupportsResume: false
SupportsCancellation: true
SupportsToolActivity: false
```

The conservative session capability is intentional. Although target help exposed broader session flags for `fcc-claude`, the compatibility fallback contract selected by P00 is plain `--print`; it must not silently claim structured session identity or resume behavior that this transport does not expose through the product-owned contract. A request containing `ResumeSessionId` is rejected explicitly.

## Output contract

The fallback captures stdout and stderr concurrently with a configured character bound so child pipes cannot deadlock and output retention cannot grow without limit.

A successful zero-exit run requires non-empty stdout. It emits one transport-neutral `AgentRuntimeEventKind.Unknown` compatibility event before terminal success:

- valid, untruncated JSON stdout uses source type `cli-fallback/json`; credential-shaped JSON properties are replaced with `[REDACTED]` while the remaining JSON is preserved;
- text or truncated stdout uses source type `cli-fallback/stdout`; obvious credential-assignment text is redacted, text is bounded, and metadata records whether truncation occurred plus the observed character count.

This is preservation, not rich event normalization. `FCCD-P04-005` owns normalized assistant/tool/retry/error event mapping.

## Terminal classification

Task-local classifications implemented here are intentionally narrow:

- missing/unstartable executable → `RuntimeNotFound`;
- non-zero process exit → `NonZeroExit`;
- successful process exit with no usable stdout → `UnknownFailure`;
- unexpected process/stream failure → `ProcessCrash`;
- requested owned-process termination → terminal `Cancelled`.

The adapter does not invent provider/model/rate-limit retry policy from opaque fallback text. `FCCD-P04-006` and `FCCD-P04-007` own compatibility/health and supervision/retry behavior.

## Cancellation and ownership

Cancellation targets only the process instance owned by the adapter and requests `Kill(entireProcessTree: true)`. It never kills by executable name. Broader graceful-to-forced escalation and retry supervision remain owned by `FCCD-P04-007`.

## Verification boundary

`tools/runtime/validate-fcc-cli-fallback-runtime.ps1` provides permanent static, negative, recovery, and Windows executable fixtures. The executable fixture builds a local fake runtime and verifies invocation, Unicode/space paths, text/JSON handling, redaction, output bounding, unsupported resume rejection, missing runtime/path handling, non-zero exit, empty output, cancellation, and recovery.

That fixture is synthetic and **does not claim provider execution**. It proves the production adapter mechanics on GitHub-hosted Windows. The authoritative P00 owner-target provider-backed fallback evidence remains an immutable architectural input. `FCCD-P04-008` and the P04 exact-head exit gate retain ownership of the full real-runtime contract suite and fresh phase-closure evidence.

## Explicit non-scope

P04-004 does not implement:

- `FCCD-P04-005` runtime event normalization;
- `FCCD-P04-006` runtime health/version compatibility policy;
- `FCCD-P04-007` start/stop/retry supervision or backoff;
- `FCCD-P04-008` full real-runtime contract suite;
- P05 conversation/session/task UX.
