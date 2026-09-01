# P00 CLI Fallback Evidence — Probe Host

**Task:** `FCCD-P00-007`  
**Date:** 2026-09-01  
**Baseline main SHA at worker start:** `f46afb3179369ffd14d1af8bbf8a2a062f07a434`  
**Probe implementation blob:** `991081b07ebe30d5b03416fa39a57a05a8601a14`  
**Target required for closure:** real Windows FCC/`fcc-claude` environment

## Implemented reusable harness

`tools/contract-probes/fcc/probe.mjs` contains the CLI fallback viability harness. It is deliberately an evidence probe, not the later production `IAgentRuntime` fallback adapter.

When a real runtime is present and live prompts are explicitly allowed, the harness can exercise:

- non-interactive runtime launch using syntax observed from real help output,
- prompt transmission,
- disposable working-directory selection,
- normal path,
- path with spaces,
- Unicode/Arabic path,
- separate incremental stdout/stderr chunk capture,
- final stdout/stderr capture,
- exit code / signal / duration,
- timeout/failure classification,
- PID/PPID process-tree snapshots,
- graceful interrupt attempt,
- forced owned-tree termination fallback,
- post-run check for observed owned PIDs still present,
- session-like identifier extraction if textually exposed.

Provider-backed prompt execution is opt-in (`--allow-live-prompt`) to avoid unintended calls.

## Real execution performed in this run

The available worker host has no `fcc-claude`. The committed harness therefore exercised the real missing-runtime branch rather than simulating a successful FCC run.

Result:

```text
fccClaudeFound=false
fallbackAssessment=BLOCKED_RUNTIME_NOT_FOUND
probeExitCode=2
selfTest=PASS
secretScan=PASS
```

This proves that the fallback probe does not convert an unavailable runtime into success and that the reusable evidence path is safe to rerun.

## CLI fallback acceptance status

| Requirement | Status in this run |
|---|---|
| Runtime launch through real `fcc-claude` | NOT VERIFIED |
| Prompt transmission | NOT VERIFIED |
| Normal working directory | NOT VERIFIED against FCC |
| Path containing spaces | Harness implemented; FCC behavior NOT VERIFIED |
| Unicode/Arabic path | Harness implemented; FCC behavior NOT VERIFIED |
| stdout observable | Capture mechanism implemented; real FCC output NOT VERIFIED |
| stderr observable | Capture mechanism implemented; real FCC output NOT VERIFIED |
| Incremental output | Capture mechanism implemented; real FCC streaming NOT VERIFIED |
| Final completion/result extraction | NOT VERIFIED |
| Exit-code model for actual FCC success/failure | NOT VERIFIED |
| Missing runtime classification | VERIFIED — exit `2` / `BLOCKED_RUNTIME_NOT_FOUND` |
| Graceful interruption | Harness implemented; real FCC behavior NOT VERIFIED |
| Forced termination fallback | Harness implemented; real FCC behavior NOT VERIFIED |
| Owned process-tree cleanup | Harness implemented; real FCC behavior NOT VERIFIED |
| Session ID exposure | NOT VERIFIED |
| Resume/continuation | NOT VERIFIED; no syntax guessed |
| FCC unavailable during real invocation | NOT VERIFIED |
| Provider/model unavailable | NOT VERIFIED |
| Network timeout | NOT VERIFIED through real runtime |
| Rate limit | NOT OBSERVED IN THIS PROBE |

## Rate-limit safety

No attempt was made to create excessive provider calls or force a 429. Rate-limit behavior remains `NOT OBSERVED IN THIS PROBE`.

## Blocker decision

`FCCD-P00-007 = BLOCKED`.

The task's closure criteria require a real invocation that proves prompt transmission, working-directory semantics, observable output, completion/failure classification, cancellation and process-tree cleanup. The worker host cannot run the owner's local Windows FCC installation, so those criteria cannot be honestly satisfied here.

## Target reproduction

Run on the actual Windows environment:

```powershell
node .\tools\contract-probes\fcc\self-test.mjs
node .\tools\contract-probes\fcc\probe.mjs --mode all --allow-live-prompt --json .\tmp\fcc-cli.json
```

If real help does not expose one of the recognized non-interactive flags, do not guess. Capture help/version evidence and supply the actually observed syntax through `--cli-args-json`.

Once a live run exists, reconcile `docs/contracts/FCC_CLI_CONTRACT.md`, this evidence directory, and the canonical task ledger from the observed behavior.
