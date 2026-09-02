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

## VERIFIED_ON_WINDOWS_TARGET

Authoritative target validation completed on the owner's Windows environment.

Evidence:

```text
evidence/phases/P00/sessions/session-resume-target.json
```

Verified observations:

- tested source SHA: `8affdae59922f945576cc45fbd49d4fb68634b66`
- real provider-backed first turn succeeded
- authoritative session identifier was captured from `$.session_id`
- the initial client process exited before resume
- a new client process successfully resumed the specified session
- the first-turn continuity token was recovered without supplying it again
- continuity succeeded from a different working directory
- a generated nonexistent session was rejected as `INVALID_SESSION`
- the valid session remained usable after the invalid-session attempt
- owned-process cleanup passed

Target result:

```text
VERIFIED_SESSION_CONTINUITY_ON_WINDOWS_TARGET
```

FCC server restart and provider/model changes were not required by the task-local closure contract and were not forced.

`FCCD-P00-004` is CLOSED.