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
- post-launcher-exit cleanup of residual descendants by previously observed PID/identity,
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

When no natural rate-limit event occurs on the target:

```text
RATE_LIMIT = NOT_OBSERVED_ON_TARGET
```

Synthetic `429 Too Many Requests` fixture output proves classifier mechanics only and is labeled `SELF_TEST_ONLY`.

Planning/reconciliation decision `PG-002-P00-RATE-LIMIT-CLOSURE` is RESOLVED by `docs/contracts/FCC_RATE_LIMIT_CLOSURE_POLICY.md`: for P00-005, `NOT_OBSERVED_ON_TARGET` plus verified deterministic rate-limit classifier mechanics is an acceptable safe closure boundary when the rest of the exact-head target contract passes and no artificial provider load is generated solely to obtain a 429. `NOT_OBSERVED_ON_TARGET` remains distinct from `PASS` and must never be represented as an actual provider rate-limit observation.

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

PR #9 changed the target-relevant ownership evidence contract: `captureProcess` refreshes the descendant set immediately before cancellation/timeout escalation, and a deterministic late-spawn fixture requires such descendants to be observed and cleaned.

The authoritative target rerun then exposed a Windows-specific residual-process case: the launcher could exit while previously observed descendants remained alive beyond the original cleanup window. The probe was hardened again to use a bounded Windows post-exit settling window and, only when required, terminate residual processes by previously observed PID/identity rather than executable name. Deterministic self-tests now include `post-exit-owned-descendant-residual-cleanup`.

## VERIFIED_ON_WINDOWS_TARGET — 2026-09-02

Authoritative exact-head target validation completed at tested source SHA `015ffd8c0e2a6e725e33ed153441ff51e7952556`.

Evidence:

- `evidence/phases/P00/failure/fcc-failure-target-exact-head.json`
- `evidence/phases/P00/failure/P00_005_TARGET_RERUN_2026-09-02.md`

Verified target observations:

- Windows x64 execution host,
- real `fcc-claude` runtime found,
- provider-backed baseline classification `SUCCESS`,
- cancellation triggered,
- cancellation classification `INTERRUPTED`,
- graceful interrupt attempted,
- five owned descendants observed,
- residual termination exercised against previously observed PID/identity,
- zero remaining owned processes,
- `processTreeCleanupObserved: true`,
- missing-runtime classification `RUNTIME_NOT_FOUND`,
- persisted secret scan PASS,
- `RATE_LIMIT = NOT_OBSERVED_ON_TARGET`,
- no artificial provider load generated to force rate limiting.

The rate-limit non-observation is accepted under the explicit PG-002 closure policy and is not represented as a real 429 observation.

`FCCD-P00-005` is CLOSED.