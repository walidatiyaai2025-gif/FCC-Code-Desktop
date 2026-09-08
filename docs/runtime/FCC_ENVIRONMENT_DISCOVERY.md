# FCC environment discovery

**Task:** `FCCD-P04-001 — FCC/fcc-claude environment discovery`  
**Phase:** P04 — FCC / `fcc-claude` runtime core

## Purpose

P04-001 converts the authoritative P00 target observations into a production discovery boundary that later P04 runtime services can consume without coupling discovery to the UI or to provider execution.

The service discovers the local `fcc-claude` and `fcc-server` executables, obtains bounded `fcc-claude` version evidence, and probes the configured FCC loopback health endpoint. It never sends a prompt and it does not establish provider readiness.

## Executable discovery

`FccEnvironmentDiscoveryService` resolves `fcc-claude` and `fcc-server` from an explicit path when one is supplied, otherwise from PATH plus PATHEXT. Malformed PATH entries are ignored rather than crashing discovery. No user-specific installation path is hard-coded.

For `fcc-claude`, discovery tries the bounded version-only forms observed/probed during P00:

1. `--version`
2. `version`
3. `-V`

Direct executables are invoked with `ProcessStartInfo.ArgumentList`. Windows `.cmd`/`.bat` shims are invoked through the built-in Windows command processor using fixed `/D`, `/S`, and `/C` arguments plus a constant wrapper. The executable path and fixed version argument are passed through child-process environment variables and quoted by that constant wrapper rather than interpolated into a dynamically constructed command string. This avoids a separate Windows PowerShell cold start for every version candidate while preserving the no-user-command-concatenation boundary.

Version probes default to a bounded 15-second timeout and kill only the process tree created by that discovery probe if the probe exceeds its timeout or the caller cancels it. The hosted-Windows validation fixture gives its disposable `.cmd` shim a 5-second probe budget and verifies both PATH and explicit-path version parsing, which protects exact-main CI against regressions back to heavyweight shell startup while remaining comfortably above normal `cmd.exe` launch latency. The production options still reject process timeouts above one minute. This is discovery cleanup only; the richer agent-run ownership/supervision contract remains owned by later P04 work.

## FCC loopback health

The health probe is independent from executable discovery and independent from provider readiness.

Endpoint selection is:

1. explicit loopback `HealthUri` when supplied;
2. explicit `FccServerPort` when supplied;
3. numeric `FCC_PORT` from the process environment when valid;
4. the P00-tested default `http://127.0.0.1:8082/health`.

Only absolute HTTP(S) loopback URIs are accepted. Redirect following and proxy use are disabled so a discovery health request cannot be redirected away from loopback. Response bodies are not persisted or surfaced by this task.

Health results are classified as:

- `Healthy` — HTTP 2xx;
- `Unhealthy` — a non-2xx loopback response;
- `Unreachable` — connection/transport failure or timeout.

A healthy FCC loopback endpoint must never be interpreted as proof that the configured provider/model can complete a task. P00 observed healthy FCC loopback responses independently from provider failure and later provider-backed success.

## Scope boundary

P04-001 intentionally does **not** implement:

- `IAgentRuntime` (`FCCD-P04-002`);
- structured prompt execution (`FCCD-P04-003`);
- CLI fallback prompt execution (`FCCD-P04-004`);
- runtime event normalization (`FCCD-P04-005`);
- version support policy / compatibility decisions (`FCCD-P04-006`);
- full agent start/stop/retry supervision (`FCCD-P04-007`);
- the complete real-runtime contract suite (`FCCD-P04-008`).

The P00 compatibility baseline remains the authoritative external-contract input. Product support ranges are not inferred from one discovered version.

## Validation

`tools/runtime/validate-fcc-environment-discovery.ps1` provides static contract checks, negative/recovery fixtures, and a Windows/.NET runtime fixture using disposable fake `fcc-claude.cmd` / `fcc-server.cmd` shims plus a local one-shot loopback HTTP server. The fixture exercises successful PATH discovery/version parsing/health, missing-runtime behavior, explicit-path override with independent version parsing, loopback-only validation, invalid port rejection, rejection of a PowerShell batch-wrapper regression, and bounded shim startup without contacting a provider.
