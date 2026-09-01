# FCC Session / Resume Contract

**Task:** `FCCD-P00-004`  
**Phase:** P00  
**Probe:** `tools/contract-probes/fcc-runtime/probe.mjs`

## SELF_TEST_VERIFIED

The probe can extract session-ID candidates from:

- JSON keys shaped like `session_id`, `sessionId`, or equivalent session+ID keys,
- textual `session id` forms,
- UUID-like identifiers retained as candidates rather than automatically asserted to be FCC session IDs.

It also scans actual target help output for option-name hints containing session/resume/continue/conversation/thread terminology.

## Resume safety boundary

Resume syntax is never guessed.

A target worker may provide an exact syntax already established from target help/runtime evidence through:

```text
--resume-args-json <JSON array using {sessionId} and {prompt} placeholders>
```

Only then does the probe:

1. use a session candidate observed from the initial process,
2. confirm the initial process exited,
3. start a new process using the explicit resume template,
4. send a unique continuation marker prompt,
5. record the exact sanitized argument array,
6. record stdout/stderr/events/process result,
7. mark continuation confirmed only if the unique marker is returned successfully,
8. execute an invalid-session case with the same explicit syntax and a generated nonexistent ID,
9. optionally exercise duplicate resume when explicitly requested.

## TARGET_UNVERIFIED

The actual target still must establish:

- whether FCC exposes a session ID,
- authoritative session-ID source and shape,
- whether the ID persists after launcher exit,
- continuation/resume support,
- exact resume syntax,
- whether working directory/project path affect resume,
- invalid/missing session behavior,
- duplicate resume behavior,
- behavior after client restart,
- behavior after FCC restart where safe,
- behavior after provider reconnect where safe,
- whether model/provider changes affect resume.

The probe deliberately does not restart FCC, corrupt a session, or disconnect providers solely to manufacture negative evidence.

## Target observation — 2026-09-02

Real `system/init` and `system/api_retry` events consistently exposed UUID-shaped `session_id` values, and target help exposed `--session-id`, `--resume`, and `--continue`. This proves session identifier exposure and documents the invocation surface.

The upstream provider returned HTTP 503 retries before any successful first turn. A successful resumable session and continuation marker therefore could not be established. `FCCD-P00-004` remains `BLOCKED` on provider availability rather than probe implementation.
