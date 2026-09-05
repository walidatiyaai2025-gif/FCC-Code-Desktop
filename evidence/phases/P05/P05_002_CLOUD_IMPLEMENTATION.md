# FCCD-P05-002 — Cloud Implementation Evidence

**Task:** `FCCD-P05-002 — Structured tool activity timeline`  
**Phase:** P05  
**Evidence class:** `SELF_TEST_ONLY / CLOUD`  
**Status:** `IMPLEMENTED — PR CI AND INTEGRATION REQUIRED`

## Implemented scope

- structured `ToolActivityState` presentation over normalized `AgentRuntimeEvent` values;
- `ToolStarted` / `ToolProgress` / `ToolResult` projection with correlation-aware row updates;
- neutral `ResultReceived` state that does not infer success from result text;
- unmatched progress/result preservation instead of silent event loss;
- correlation reuse without rewriting prior completed activity history;
- bounded conversation-side tool timeline with semantic dark/light resources;
- dispatcher-safe latest-activity scrolling through the existing conversation surface;
- permanent static, negative, recovery, and executable Windows/WPF validator;
- permanent Windows CI registration plus CI-policy guard;
- contract documentation at `docs/conversation/STRUCTURED_TOOL_ACTIVITY_TIMELINE.md`.

## Safety boundaries

P05-002 consumes typed normalized events only. It does not inspect `PayloadJson`, parse FCC/provider wire formats, start processes, execute tools, or claim target/provider acceptance. Normalized text is the only runtime text projected into the timeline.

Synthetic tool events in the Windows fixture are product-mechanics evidence only and must not be represented as genuine FCC/provider execution.

## Validation required before integration

The implementation PR must pass the canonical Windows CI baseline including:

```powershell
.\tools\ci\validate-windows-ci.ps1 -RequireDotNet
.\tools\ci\run-windows-ci.ps1
```

and specifically:

```powershell
.\tools\ui\validate-tool-activity-timeline.ps1 -RunFixtures -RequireRuntime
```

After normal merge, exact canonical-main Windows CI must pass before integrated reconciliation may mark `FCCD-P05-002` CLOSED.

## Owner-last status

No new owner-only evidence is introduced by P05-002. The existing `OWNER-P04-008-REAL-TARGET` item remains queued, genuine, unresolved, and release-blocking under `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md`.
