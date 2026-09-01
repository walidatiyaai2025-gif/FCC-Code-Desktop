# P00 Session / Resume Evidence — Remote Probe Host

**Task:** `FCCD-P00-004`  
**Date:** 2026-09-01  
**Baseline main SHA:** `7d9b91324ec4bffeabc241f052921c5bc57a5f1f`

## SELF_TEST_VERIFIED

The repository fixture verifies session-ID candidate extraction from valid JSON and text without asserting that synthetic IDs represent FCC behavior.

The target-capable probe records real help-option hints and real observed candidate IDs. Resume is not attempted unless target evidence supplies exact syntax through `--resume-args-json` / unified-runner `-ResumeArgsJson`.

When supplied, the probe starts a new process after the initial process exits, records the resume invocation/result, uses a unique continuation marker, records invalid-session behavior, and can optionally record duplicate-resume behavior.

## Target status

`FCCD-P00-004 = BLOCKED` pending target execution.

Actual session creation, authoritative ID source, persistence, continuation, resume syntax, invalid session behavior, project/CWD effects, FCC restart behavior, provider reconnect behavior, and provider/model-change semantics remain `TARGET_UNVERIFIED`.

## Unified target command

```powershell
.\tools\contract-probes\run-target-validation.ps1 -AllowLivePrompt
```
