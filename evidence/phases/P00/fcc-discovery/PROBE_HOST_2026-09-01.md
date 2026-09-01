# P00 FCC Discovery Evidence — Probe Host

**Task:** `FCCD-P00-002`  
**Date:** 2026-09-01  
**Target platform required by project:** Windows 10/11 x64  
**Baseline main SHA at worker start:** `f46afb3179369ffd14d1af8bbf8a2a062f07a434`  
**Probe implementation blob:** `991081b07ebe30d5b03416fa39a57a05a8601a14` (`tools/contract-probes/fcc/probe.mjs`)  
**Self-test blob:** `d3e1e95f16052777dffce26912531015f1985efd`

## Scope

This artifact records real execution available to this worker. It deliberately does not present the Linux probe host as the project's target Windows environment.

## Host actually exercised

```text
platform=linux x64
node=v22.16.0
git=git version 2.47.3
python=Python 3.13.5
pwsh=NOT_FOUND
powershell=NOT_FOUND
dotnet=NOT_FOUND
fcc=NOT_FOUND
fcc-server=NOT_FOUND
fcc-claude=NOT_FOUND
claude=NOT_FOUND
```

## Commands executed

The committed probe source was syntax-checked and the repository self-test logic was executed against an explicit nonexistent `fcc-claude` path. A second evidence run injected fake FCC/Anthropic credential-shaped environment values and wrote JSON output to a disposable temp directory.

Representative commands:

```text
node --check tools/contract-probes/fcc/probe.mjs
node tools/contract-probes/fcc/self-test.mjs
node tools/contract-probes/fcc/probe.mjs --mode all --fcc-claude <disposable-missing-path> --json <disposable-output>
```

The evidence-only run used fake values; no real credential was supplied.

## Verified results on the probe host

```text
probe_exit=2
self_test=PASS
secret_scan=PASS
fallback=BLOCKED_RUNTIME_NOT_FOUND
fccClaudeFound=false
FCC_API_KEY=[REDACTED]
ANTHROPIC_AUTH_TOKEN=[REDACTED]
```

### PASS — reusable negative discovery

An explicit missing executable is classified as absent and the probe exits `2`, not `0`.

### PASS — secret redaction

Fake secret values matching FCC/Anthropic credential names were not present in the persisted JSON. Their values were `[REDACTED]`.

### PASS — non-destructive execution

The test used disposable OS temp paths and removed generated test data after execution. No valuable user repository was used for path/failure tests.

## Target-environment results

The following are **NOT VERIFIED** because the worker environment has no access to the owner's Windows installation containing FCC/`fcc-claude`:

- Windows version and PowerShell version on the target,
- actual `fcc`, `fcc-server`, `fcc-claude`, and Claude executable locations,
- real versions/help behavior,
- real PATH behavior versus explicit-path behavior,
- actual FCC config locations and required non-secret inputs,
- provider/model selection behavior,
- authentication resolution,
- active FCC server process and parent/child relationships,
- active FCC port,
- health/status endpoint behavior,
- whether `fcc-server` must already be running,
- target startup dependencies,
- unavailable/malformed configuration behavior of the real installation.

## Blocker decision

`FCCD-P00-002 = BLOCKED`.

Reason: the task explicitly requires measured behavior from the real installed FCC/`fcc-claude` target environment. The available execution host is Linux and does not contain or reach that installation. Substituting assumptions or generic documentation would violate P00 evidence rules.

The reusable target probe is committed and ready to execute when a worker has access to the actual Windows environment.

## Reproduction on target

```powershell
node .\tools\contract-probes\fcc\self-test.mjs
node .\tools\contract-probes\fcc\probe.mjs --mode discovery --json .\tmp\fcc-discovery.json
```

Then review the sanitized JSON and commit only target evidence proven free of secrets.
