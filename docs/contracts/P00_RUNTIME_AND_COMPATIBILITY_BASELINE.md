# P00 Runtime and Compatibility Baseline

**Tasks:** `FCCD-P00-006`, `FCCD-P00-010`  
**State:** IMPLEMENTED pending closure of remaining target blockers.

## Primary runtime contract

The primary P04 runtime adapter will launch the installed `fcc-claude` executable as an owned process using argument arrays, `--print`, and `--output-format stream-json --verbose`. It will parse newline-delimited JSON into project-owned runtime events while preserving unknown event types and bounded sanitized raw evidence. It will obtain the session ID from observed structured events and use the documented `--resume <session-id>` surface only after successful continuation is verified.

The local `fcc-server` loopback health endpoint is a separate readiness signal. Health PASS does not imply provider readiness: the target returned healthy FCC responses while provider calls emitted structured 503 retry events.

The compatibility fallback remains a plain `--print` invocation with text or single JSON output behind the same project-owned `IAgentRuntime` boundary. It is not promoted to primary because stream-json is directly observed and exposes richer lifecycle/session/failure information. Successful fallback completion is still blocked by current upstream availability.

## Observed compatibility baseline

| Component | Observed target baseline | Status |
|---|---|---|
| Windows | Windows 10 x64, build `19045` | VERIFIED_ON_TARGET |
| PowerShell | Windows PowerShell `5.1.19041.6456` | VERIFIED_ON_TARGET |
| Node.js | `20.11.1` | VERIFIED_ON_TARGET |
| .NET SDK | `10.0.400` | VERIFIED_ON_TARGET |
| `fcc-claude` | Claude Code `2.1.251` | VERIFIED_ON_TARGET |
| `fcc-server` | loopback `8082`, `/health` returns 200 | VERIFIED_ON_TARGET |
| Unity Hub | standard Program Files installation | VERIFIED_ON_TARGET |
| Unity Editor | `6000.5.8f1`; `2022.3.75f1` also discovered | VERIFIED_ON_TARGET |
| Unity test framework | `1.7.0` minimum declared by Unity 6000.5.8f1 | VERIFIED_ON_TARGET |
| Blender | executable not installed/discoverable | BLOCKED_EXTERNAL |

## Open compatibility boundaries

- successful provider-backed completion and resume are blocked by observed upstream 503 responses;
- natural rate-limit behavior has not been observed and must not be forced with artificial traffic;
- Blender versions and real automation behavior remain unknown until Blender is available;
- successful assistant/tool/final stream event shapes remain additive unknown-event compatibility cases.

The baseline is intentionally evidence-based and does not claim broader supported version ranges. `FCCD-P00-006` and `FCCD-P00-010` remain `IMPLEMENTED` until the remaining P00 target blockers are resolved and the exact-head exit gate passes.
