# FCC Runtime Health and Version Compatibility

**Task:** `FCCD-P04-006 — Runtime health/version compatibility service`  
**Phase:** P04 — FCC / `fcc-claude` runtime core

## Purpose

`FccRuntimeHealthCompatibilityService` turns the P04-001 discovery snapshot into an evidence-aware runtime health/version assessment without coupling UI code to FCC discovery details and without making a provider request.

The service intentionally keeps three questions separate:

1. Is `fcc-claude` locally discoverable so a runtime launch can be attempted?
2. Does the detected `fcc-claude` version exactly match the authoritative version exercised by the P00 target evidence?
3. What does the local FCC loopback health endpoint report?

A positive answer to question 3 does **not** establish provider readiness or successful prompt execution. P00 directly observed healthy FCC loopback behavior independently from provider failure and later provider-backed success. P04-008 and the P04 exact-head exit gate own the full real-runtime contract execution required for phase closure.

## Exact evidence baseline

The authoritative P00 compatibility baseline records `fcc-claude` / Claude Code `2.1.251` as the exact target-tested version for the exercised discovery, structured streaming, provider-backed completion, session/resume, CLI fallback, and failure/cancellation lanes.

P04-006 therefore uses `2.1.251` only as an **exact tested baseline**, not as a claimed supported range.

The service never converts one observed machine/version into a broad `SUPPORTED` declaration. A different parseable version is classified as detected but untested and requires compatibility smoke validation. A discovered executable whose version cannot be parsed is also treated as unverified and requires compatibility smoke validation.

## Classifications

### Runtime availability

- `Available` — `fcc-claude` was discovered. Runtime launch may be attempted.
- `Unavailable` — `fcc-claude` was not discovered. Runtime launch cannot be attempted.

Loopback state does not change this executable-availability classification because the P00 contract treats FCC loopback health as a separate readiness signal.

### Version evidence

- `TestedBaseline` — detected parsed version exactly equals `2.1.251`.
- `DetectedUntestedVersion` — a different parsed version is present; compatibility smoke validation is required.
- `UnverifiedVersion` — the executable is present but a numeric version was not established; compatibility smoke validation is required.
- `RuntimeMissing` — no `fcc-claude` executable was discovered.

`RequiresCompatibilitySmokeCheck` is true only for detected-but-unverified version states. This records the need for later compatibility validation without inventing a pass result.

## Health signal

The returned snapshot retains the complete `FccLoopbackHealth` result from P04-001 discovery and exposes `IsLoopbackHealthy` as a convenience. `Healthy`, `Unhealthy`, and `Unreachable` remain observable independently from executable/version compatibility.

The service does not infer:

- provider authentication state,
- provider/model readiness,
- provider rate-limit behavior,
- successful prompt completion,
- session/resume success,
- fallback-switch success.

Those behaviors require runtime execution evidence. In particular, real provider rate-limit semantics remain `NOT_OBSERVED_ON_TARGET` under the approved P00 rate-limit closure policy; P04-006 does not manufacture 429 traffic.

## Production flow

```text
FccEnvironmentDiscoveryService.DiscoverAsync
        ↓
FccEnvironmentSnapshot
        ↓
FccRuntimeHealthCompatibilityService.Evaluate
        ↓
FccRuntimeHealthCompatibilitySnapshot
```

`InspectAsync` performs the discovery-and-evaluate sequence for production callers. `Evaluate` is intentionally public so deterministic fixtures and later diagnostics can classify already-captured discovery snapshots without additional process/network probing.

## P04 boundaries

P04-006 does **not** implement or claim:

- P04-005 runtime event normalization,
- P04-007 start/stop/retry supervision,
- P04-008 provider-backed runtime contract suite,
- provider readiness from FCC loopback health,
- broad version support ranges,
- UI health-center presentation,
- later release compatibility commitments.

The full P04 exit gate remains open until every mandatory P04 task is CLOSED and the headless real-runtime harness passes on the exact candidate with canonical evidence.
