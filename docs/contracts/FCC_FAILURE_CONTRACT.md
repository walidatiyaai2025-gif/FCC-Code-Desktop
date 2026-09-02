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
- ownership observation for descendants created after the initial process snapshot,
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

## Target evidence requirements

The authoritative Windows target contract covers, where safely observable:

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

Existing target observations are evidence only for the behaviors actually exercised. Unsafe negative cases are not manufactured merely to fill the matrix.

## Rate-limit rule

No artificial traffic is generated to force HTTP/provider 429 behavior.

Until naturally observed on the target:

```text
RATE_LIMIT = NOT_OBSERVED_ON_TARGET
```

Synthetic `429 Too Many Requests` fixture output proves classifier mechanics only and is labeled `SELF_TEST_ONLY`.

`PG-002-P00-RATE-LIMIT-CLOSURE` records the unresolved planning question of whether this safe `NOT_OBSERVED_ON_TARGET` boundary can satisfy P00 closure or whether a natural observation remains mandatory. Ordinary workers must not decide that policy implicitly.

## Unsafe negative cases deliberately skipped

The probe does not:

- revoke real credentials,
- corrupt valuable configuration,
- destroy real sessions,
- spam providers,
- terminate unrelated processes,
- mutate valuable repositories.

## Historical target observation — 2026-09-02

The Windows target produced structured provider-failure events with HTTP status `503`, retry attempt/count, retry delay, session ID, and UUID. Target runs also exercised timeout/cancellation behavior. The separate CLI target lane recorded successful cleanup of the observed owned launcher tree after interruption, while some structured runtime captures retained transient observed console-host processes long enough to record `processTreeCleanupObserved: false`.

Those observations remain useful historical evidence, but they predate PR #9's strengthened ownership tracking for descendants created after the initial snapshot.

## Probe hardening after target observation

PR #9 changed the target-relevant ownership evidence contract: `captureProcess` now refreshes the descendant set immediately before cancellation/timeout escalation, and a deterministic late-spawn fixture requires such descendants to be observed and cleaned. Because this changes the evidence-producing probe after the previous Windows run, the exact-head rule requires a new authoritative Windows target rerun before `FCCD-P00-005` can be considered VERIFIED again.

Current task state is therefore `BLOCKED` on:

1. an exact-head Windows rerun of the hardened cancellation/failure probe, and
2. planning/reconciliation resolution of `PG-002-P00-RATE-LIMIT-CLOSURE` unless a natural rate-limit event is observed first.
