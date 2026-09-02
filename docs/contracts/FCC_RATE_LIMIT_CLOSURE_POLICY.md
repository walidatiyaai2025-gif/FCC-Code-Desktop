# FCC P00 Rate-Limit Closure Policy

**Decision:** `PG-002-P00-RATE-LIMIT-CLOSURE`  
**Related task:** `FCCD-P00-005`  
**Date:** 2026-09-02  
**Authority:** P00 planning / reconciliation

## Decision

P00 must not require intentionally generating provider load merely to force HTTP/provider `429` behavior.

For `FCCD-P00-005`, the rate-limit acceptance boundary is satisfied when all of the following are true:

1. the authoritative Windows target run is from the exact committed probe SHA,
2. provider-backed baseline execution is observed successfully,
3. cancellation/interrupt handling and owned-process cleanup are verified on the target,
4. the deterministic repository-owned fixture verifies `RATE_LIMITED` classifier mechanics from a synthetic 429 shape,
5. no natural target rate-limit event occurred, so the target observation remains explicitly `NOT_OBSERVED_ON_TARGET`, and
6. evidence records that no artificial provider load was generated solely to obtain a 429.

Under those conditions, `NOT_OBSERVED_ON_TARGET` is an accepted closure boundary for P00-005. It is not reclassified as `PASS`, and it must never be described as a real observed provider rate-limit event.

If a natural provider rate-limit event is observed later, its concrete event/output/exit semantics may be added as new compatibility evidence without reopening P00-005 unless that observation contradicts the implemented classifier contract.

## Rationale

The binding target-machine safety policy forbids intentional load merely to force rate limiting. Requiring a naturally occurring 429 as the only closure path would make P00 depend indefinitely on an external event the project is prohibited from manufacturing.

The safe closure policy therefore preserves the distinction between:

- actual target observation: `OBSERVED_ON_EXECUTION_HOST`,
- safe absence of a natural event: `NOT_OBSERVED_ON_TARGET`, and
- deterministic classifier coverage: `SELF_TEST_ONLY`.

This does not claim provider-specific retry semantics, wait durations, quota reset rules, or any other behavior that was not observed on the target.

## Evidence applied to the 2026-09-02 closure

Exact-head Windows evidence at tested source SHA `015ffd8c0e2a6e725e33ed153441ff51e7952556` records:

- baseline provider classification `SUCCESS`,
- cancellation classification `INTERRUPTED`,
- graceful interrupt attempted,
- five owned descendants observed,
- residual owned descendants terminated only by previously observed PID/identity,
- `processTreeCleanupObserved: true`,
- zero remaining owned processes,
- `RATE_LIMIT = NOT_OBSERVED_ON_TARGET`,
- no artificial provider load to force rate limiting,
- persisted secret scan PASS.

Supporting evidence:

- `evidence/phases/P00/failure/fcc-failure-target-exact-head.json`
- `evidence/phases/P00/failure/P00_005_TARGET_RERUN_2026-09-02.md`
- `tools/contract-probes/fcc-runtime/self-test.mjs`

## Closure effect

`PG-002-P00-RATE-LIMIT-CLOSURE` is RESOLVED.

With the exact-head Windows failure/cancellation evidence integrated, `FCCD-P00-005` is eligible for `CLOSED`.