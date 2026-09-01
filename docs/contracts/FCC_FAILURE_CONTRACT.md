# FCC Cancellation / Failure Contract

**Task:** `FCCD-P00-005`  
**Phase:** P00  
**Probe:** `tools/contract-probes/fcc-runtime/probe.mjs`

## SELF_TEST_VERIFIED

Repository-owned fixture processes prove the mechanics for:

- graceful interrupt request,
- bounded graceful wait,
- forced owned-tree termination fallback,
- post-exit polling until observed owned PIDs disappear,
- timeout classification,
- interrupted-stream handling,
- non-zero exit classification,
- malformed stream classification,
- runtime-not-found classification,
- synthetic rate-limit classification mechanics,
- secret redaction before persisted output.

All child processes used by these tests are created by the fixture itself. No process is killed by executable name.

## Failure classification model

The probe can emit observable categories:

```text
RUNTIME_NOT_FOUND
FCC_UNAVAILABLE
AUTH_FAILURE
MODEL_UNAVAILABLE
PROVIDER_UNAVAILABLE
PROVIDER_BUSY_OR_OVERLOADED
RATE_LIMITED
TIMEOUT
MALFORMED_STREAM
INTERRUPTED
PROCESS_CRASH
NONZERO_EXIT
UNKNOWN_FAILURE
SUCCESS
```

Each classification records its evidence source. `retryability` and `userActionRequired` remain `UNKNOWN` unless direct target evidence supports a stronger statement.

## TARGET_UNVERIFIED

The actual target must still establish the real FCC/fcc-claude behavior for:

- graceful interrupt handling,
- whether a wrapper/Claude child survives launcher interruption,
- forced cleanup requirements,
- interrupted session recoverability,
- FCC unavailable while invoking the real runtime,
- malformed/invalid configuration where safely reproducible,
- provider unavailable,
- model unavailable,
- authentication failure where safely observable,
- network/provider timeout,
- unexpected process crash/exit,
- actual non-zero exit codes,
- overloaded/provider-busy shapes,
- rate-limit output/event/exit behavior.

## Rate-limit rule

No artificial traffic is generated to force HTTP/provider 429 behavior.

Until naturally observed on the target:

```text
RATE_LIMIT = NOT_OBSERVED_ON_TARGET
```

Synthetic `429 Too Many Requests` fixture output proves classifier mechanics only and is labeled `SELF_TEST_ONLY`.

## Unsafe negative cases deliberately skipped

The probe does not:

- revoke real credentials,
- corrupt valuable configuration,
- destroy real sessions,
- spam providers,
- terminate unrelated processes,
- mutate valuable repositories.
