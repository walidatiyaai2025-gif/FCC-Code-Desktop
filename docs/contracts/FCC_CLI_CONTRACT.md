# FCC / `fcc-claude` Discovery and CLI Fallback Contract

**Related tasks:** `FCCD-P00-002`, `FCCD-P00-007`  
**Phase:** P00  
**Contract status:** TARGET-ENVIRONMENT EVIDENCE BLOCKED  
**Probe implementation:** `tools/contract-probes/fcc/probe.mjs`  
**Self-test:** `tools/contract-probes/fcc/self-test.mjs`

## 1. Decision boundary

This document records evidence for discovery and the CLI compatibility fallback only. It does **not** select the project's primary runtime transport and does not close `FCCD-P00-006`. It also does not claim the full streaming/session/failure matrices owned by `FCCD-P00-003`, `FCCD-P00-004`, or `FCCD-P00-005`.

## 2. Evidence-status vocabulary

- **VERIFIED** — directly exercised by the committed probe on the stated host.
- **OBSERVED BUT NOT GUARANTEED** — observed behavior that is not yet supported as a compatibility guarantee.
- **NOT VERIFIED** — required target behavior that could not be exercised in the available execution environment.
- **UNSUPPORTED** — the tested runtime explicitly lacks the behavior, when proven by target evidence.

No `NOT VERIFIED` item may be treated as a PASS.

## 3. Probe-host facts — VERIFIED

The worker execution host available for this run is Linux x64, not the project's Windows 10/11 x64 target environment. On this probe host:

- Node is available and can execute the repository-owned harness.
- Git and Python are available.
- PowerShell and .NET are not available on the probe host.
- `fcc`, `fcc-server`, `fcc-claude`, and `claude` are not installed/resolvable on the probe host.
- The explicit missing-`fcc-claude` path case returns probe exit code `2` and `BLOCKED_RUNTIME_NOT_FOUND`.
- fake `FCC_API_KEY` and `ANTHROPIC_AUTH_TOKEN` values injected by the self-test are persisted only as `[REDACTED]`; the raw fake secrets are absent from the JSON output.
- the probe output is structured JSON plus a concise stdout summary.
- temporary self-test files are removed after the test.

These facts prove the negative/missing-runtime and redaction behavior of the harness. They do **not** prove the owner's installed FCC contract.

## 4. Executable discovery contract implemented by the probe

The probe deterministically checks, without hard-coded personal paths:

- `fcc`
- `fcc-server`
- `fcc-claude`
- `claude`
- Git
- .NET
- Python
- PowerShell on Windows

For FCC/Claude executables it records every PATH-resolved candidate and bounded `--version`/`version`/`-V` plus `--help`/`help`/`-h` observations where the executable exists.

An explicit `--fcc-claude <path>` bypasses PATH discovery so a target run can prove direct-path invocation separately from PATH behavior.

On Windows, `.cmd`/`.bat` shims are launched through a constant PowerShell call-operator wrapper with user arguments passed as arguments rather than concatenated into a generated command string.

**Target status:** NOT VERIFIED until executed on the Windows machine containing the real installation.

## 5. Configuration discovery contract implemented by the probe

The probe records metadata, not configuration-file contents, for common FCC/Claude config locations derived from:

- the current user's home directory,
- `%APPDATA%`, `%LOCALAPPDATA%`, `%PROGRAMDATA%` where present,
- `FCC_CONFIG`, `FCC_CONFIG_PATH`, `FCC_HOME`, `FCC_CLAUDE_CONFIG`, and `CLAUDE_CONFIG_DIR` when present.

FCC/Claude/provider-related environment variable names may be recorded for presence. Values whose names or contents look like secrets are replaced with `[REDACTED]` before serialization.

The probe deliberately does not read and dump arbitrary config-file contents because this P00 evidence must not leak provider/FCC credentials.

**Actual target config files, authentication source, provider and model selection:** NOT VERIFIED.

## 6. FCC process, port, and health contract implemented by the probe

On Windows the probe obtains process ID / parent process ID / image-name metadata through `Win32_Process`, then correlates relevant FCC process IDs with TCP listeners from `netstat -ano -p tcp`. `FCC_PORT`, when present and numeric, is recorded separately.

Health probing is limited to loopback. An explicit `--health-url` must use `localhost`, `127.0.0.1`, or `::1`. Without an explicit URL, process-correlated ports are checked using bounded candidates `/health`, `/status`, and `/`. JSON health responses are recursively redacted by key before persistence; text responses pass through string redaction.

**Actual active FCC port, whether `fcc-server` must pre-exist, and actual health/status semantics on the target:** NOT VERIFIED.

## 7. CLI fallback invocation contract implemented by the probe

Provider-backed execution is opt-in with `--allow-live-prompt` so ordinary discovery cannot accidentally create provider traffic.

Before sending a prompt the harness inspects real help output. It only auto-selects a non-interactive invocation when help exposes one of these recognizable forms:

- `--print <prompt>`
- `--prompt <prompt>`
- `-p <prompt>`

If none is safely inferable, the probe returns an incomplete/blocking result instead of guessing. A verified local syntax can be supplied as `--cli-args-json`, with `{prompt}` as a placeholder, but the override must be justified by captured target help/version evidence.

**Actual target launch syntax:** NOT VERIFIED.

## 8. Working-directory behavior

A live target CLI run uses disposable fixture directories and attempts the same minimal prompt from:

1. a normal path,
2. a path containing spaces,
3. a Unicode/Arabic path (`مسار-اختبار`).

The harness never uses a valuable user repository for these path tests and removes its generated temporary tree when it owns that tree.

**Harness behavior:** VERIFIED.  
**Real `fcc-claude` behavior in these directories:** NOT VERIFIED.

## 9. stdout, stderr, streaming, and terminal completion

For live CLI runs the harness captures stdout and stderr independently as timestamped incremental chunks while retaining bounded final stdout/stderr text. It records:

- process ID,
- exit code,
- terminating signal where exposed,
- duration,
- timeout state,
- cancellation state,
- chunk timeline,
- coarse failure classification,
- session-like identifiers if exposed textually.

Terminal success is currently classified from process exit plus the live-run result. This is a P00 fallback viability probe, not the production runtime adapter.

**Harness output observability:** VERIFIED.  
**Actual FCC stdout/stderr format, incremental semantics, final-result extraction, and terminal-success markers:** NOT VERIFIED.

## 10. Exit-code and failure classification

Repository probe exit codes are stable:

- `0` — requested evidence completed for the tested runtime.
- `1` — probe infrastructure/error.
- `2` — runtime unavailable or required live evidence incomplete.
- `64` — usage error.

Within a live CLI result the harness distinguishes at least:

- `SUCCESS`
- `CANCELLED`
- `TIMEOUT`
- `RATE_LIMITED`
- `AUTH_OR_PROVIDER_ERROR`
- `MODEL_OR_PROVIDER_UNAVAILABLE`
- `FCC_UNAVAILABLE`
- `NON_ZERO_EXIT`
- `LAUNCH_OR_SIGNAL_FAILURE`

These are evidence classifications, not yet the production domain error model.

**Missing-runtime probe exit `2`: VERIFIED.**  
**Actual FCC/Claude exit codes for success and the listed target failures:** NOT VERIFIED.

## 11. Process ownership and cancellation

For a live run the harness snapshots the launcher and observed descendants using PID/PPID metadata. On cancellation/timeout it attempts:

1. graceful `SIGINT`/console-interrupt request through the launched process API,
2. bounded wait,
3. forced owned-tree termination if the launcher is still active (`taskkill /PID <owned-pid> /T /F` on Windows; process-group termination on the non-Windows probe host).

After termination it takes another process snapshot and records whether any observed owned PIDs remain. It never kills processes by executable name.

**Harness ownership/cancellation logic:** VERIFIED by code/self-review; missing-runtime self-test does not exercise a real child tree.  
**Real target `fcc-claude` launcher/child/supervisor topology, Ctrl+C handling, orphan behavior, and cleanup result:** NOT VERIFIED.

## 12. Sessions and continuation

The harness extracts obvious `session id` strings / UUID-like identifiers if exposed in stdout/stderr. It does not invent a resume syntax and does not auto-attempt continuation until the target help/runtime has exposed a stable contract.

**Session identifier exposure:** NOT VERIFIED.  
**CLI resume/continuation:** NOT VERIFIED.

## 13. Provider/model/authentication behavior

The available host has no target FCC installation, so this run cannot truthfully establish:

- FCC authentication resolution,
- provider resolution,
- configured model selection,
- invalid provider/model behavior,
- malformed target configuration behavior,
- network timeout behavior through the real runtime.

These remain **NOT VERIFIED**. The probe must not mutate valuable real configuration solely to fabricate a negative case.

## 14. Rate-limit behavior

**NOT OBSERVED IN THIS PROBE.**

No artificial request burst was generated to force HTTP/provider 429 behavior.

## 15. Current fallback conclusion

`FCCD-P00-007` cannot be closed from this run. The reusable harness exists and its safe negative/redaction behavior is verified, but the defining acceptance items — real launch, prompt transmission, target working-directory behavior, output semantics, completion/failure classification, cancellation and owned-tree cleanup — require execution against the actual Windows FCC/`fcc-claude` installation.

Likewise, `FCCD-P00-002` cannot be closed until the same target environment establishes actual installation/version/configuration/port/process/health behavior.

This is an environment-evidence blocker, not a plan gap and not permission to choose the primary runtime architecture.

## 16. Target reproduction

From the repository root on the real Windows target:

```powershell
node .\tools\contract-probes\fcc\self-test.mjs
node .\tools\contract-probes\fcc\probe.mjs --mode discovery --json .\tmp\fcc-discovery.json
node .\tools\contract-probes\fcc\probe.mjs --mode all --allow-live-prompt --json .\tmp\fcc-cli.json
```

If the real help output does not expose a safely recognized prompt switch, first inspect the sanitized discovery JSON/help output; only then supply an observed syntax via `--cli-args-json`.

Before committing target output, manually scan the sanitized file for secret-like material as required by the P00 task instructions.

## 17. Requirements for unblocking closure

`FCCD-P00-002` may move beyond BLOCKED only after target evidence records the actual FCC/fcc-claude executables, versions, config/auth discovery, process/server/port behavior and health/failure behavior required by the task.

`FCCD-P00-007` may move beyond BLOCKED only after a target live run proves launch, prompt transmission, the three working-directory cases, stdout/stderr observability, terminal result/failure classification, cancellation and owned-tree cleanup, with limitations recorded truthfully.

Until then, neither task is `CLOSED`.
