# P00 Structured Streaming Evidence — Remote Probe Host

**Task:** `FCCD-P00-003`  
**Date:** 2026-09-01  
**Baseline main SHA:** `7d9b91324ec4bffeabc241f052921c5bc57a5f1f`  
**Target required for closure:** owner's Windows FCC/fcc-claude environment

## Probe host

```text
platform=linux x64
node=v22.16.0
git=2.47.3
python=3.13.5
pwsh=NOT_FOUND
dotnet=NOT_FOUND
fcc-claude=NOT_AVAILABLE_ON_PROBE_HOST
```

## SELF_TEST_VERIFIED

`node tools/contract-probes/fcc-runtime/self-test.mjs` passed repeatedly on the remote probe host.

The synthetic `SELF_TEST_ONLY` stream fixture verifies:

- partial JSON across chunks,
- stdout/stderr interleaving,
- invalid JSON without parser crash,
- unknown event-type retention,
- Arabic/Unicode handling,
- large payload handling,
- abrupt EOF flush,
- raw frame metadata/order,
- split-secret masking across chunk boundaries,
- session-candidate extraction.

## Target status

`FCCD-P00-003 = BLOCKED` pending target execution.

No actual FCC structured event schema, streaming option, tool event, usage event, final-result shape, or stderr convention is claimed by this remote evidence.

## Unified target command

```powershell
.\tools\contract-probes\run-target-validation.ps1 -AllowLivePrompt
```

If target help proves dedicated structured-stream arguments, the local validation worker supplies them through `-StreamArgsJson`; the owner is not expected to infer them.
