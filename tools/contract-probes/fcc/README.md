# FCC / `fcc-claude` P00 contract probes

These repository-owned probes support `FCCD-P00-002` and `FCCD-P00-007` only. They are evidence/de-risking tools, not production runtime implementation.

## Requirements

- Node.js 18+.
- Run on the real Windows target environment for closure evidence.
- Do not paste or hard-code secrets into arguments or files.

The probe redacts values whose names/content look like API keys, tokens, authorization headers, provider credentials or other secrets before writing JSON.

## Discovery

```powershell
node .\tools\contract-probes\fcc\probe.mjs --mode discovery --json .\tmp\fcc-discovery.json
```

The discovery probe records, where available:

- host OS/architecture and Node version,
- PowerShell/Git/.NET/Python presence and versions,
- `fcc`, `fcc-server`, `fcc-claude`, and `claude` executable discovery,
- version/help behavior,
- config-location metadata without reading secret values,
- FCC/Claude-related environment variable presence with secret redaction,
- relevant process presence,
- optional loopback health probe when `FCC_PORT` is present or `--health-url` is supplied.

An explicit executable path can be tested without depending on `PATH`:

```powershell
node .\tools\contract-probes\fcc\probe.mjs --mode discovery --fcc-claude "C:\path\to\fcc-claude.cmd" --json .\tmp\fcc-discovery-explicit.json
```

## CLI fallback

Live prompt transmission is intentionally opt-in so an ordinary discovery run cannot create provider traffic accidentally.

```powershell
node .\tools\contract-probes\fcc\probe.mjs --mode all --allow-live-prompt --json .\tmp\fcc-cli.json
```

The probe first reads real `fcc-claude --help` / version behavior. If it can safely infer a non-interactive prompt flag (`--print`, `--prompt`, or `-p`), it runs the same small prompt from disposable fixtures covering:

- a normal path,
- a path containing spaces,
- a Unicode/Arabic path.

It captures timestamped stdout/stderr chunks, exit code, duration, session-like IDs if exposed, common failure classifications, cancellation escalation, and whether forced process-tree cleanup was required.

If the local CLI syntax is different and cannot be safely inferred, do **not** guess. Verify local help first, then pass an observed argument contract explicitly:

```powershell
node .\tools\contract-probes\fcc\probe.mjs --mode all --allow-live-prompt --cli-args-json '["-p","{prompt}"]' --json .\tmp\fcc-cli.json
```

The override is evidence input, not an architectural assumption. Record the observed help/version output alongside it.

## Optional local health URL

Only loopback URLs are accepted:

```powershell
node .\tools\contract-probes\fcc\probe.mjs --mode discovery --health-url "http://127.0.0.1:3210/health" --json .\tmp\fcc-health.json
```

Non-loopback health URLs are refused by the probe.

## Self-test

This test makes no provider call. It forces a missing runtime path and injects fake secret values to prove deterministic missing-tool classification and redaction.

```powershell
node .\tools\contract-probes\fcc\self-test.mjs
```

Expected output:

```text
PASS self-test: deterministic missing-runtime classification + redaction.
```

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Requested evidence completed for the tested runtime. |
| `1` | Probe infrastructure/error. |
| `2` | Target runtime unavailable or required live evidence remains incomplete. |
| `64` | Usage error. |

An exit code of `2` is intentionally not converted into success. It is the durable signal that a P00 contract is not proven on that host.

## Evidence handling

Before committing real target-environment output:

1. Review the JSON for unexpected sensitive material.
2. Search for `token`, `api_key`, `Authorization`, `Bearer`, `ANTHROPIC`, and provider-specific credential names.
3. Do not commit raw output if any real secret is present.
4. Store sanitized evidence under the P00 evidence directories referenced by `docs/contracts/FCC_CLI_CONTRACT.md`.

## Scope boundaries

This probe does not close or implement `FCCD-P00-003`, `FCCD-P00-004`, `FCCD-P00-005`, or `FCCD-P00-006`. It may observe streaming/session/failure signals only to the extent required to decide whether the CLI fallback contract is viable.
