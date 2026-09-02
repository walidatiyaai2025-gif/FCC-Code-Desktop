# P00 Runtime and Compatibility Baseline

**Tasks:** `FCCD-P00-006`, `FCCD-P00-010`  
**State:** IMPLEMENTED pending closure of remaining target blockers.

## Primary runtime contract

The primary P04 runtime adapter will launch the installed `fcc-claude` executable as an owned process using argument arrays, `--print`, and `--output-format stream-json --verbose`. It will parse newline-delimited JSON into project-owned runtime events while preserving unknown event types and bounded sanitized raw evidence. It will obtain the session ID from observed structured events and use the documented `--resume <session-id>` surface only after successful continuation is verified.

The local `fcc-server` loopback health endpoint is a separate readiness signal. Health PASS does not imply provider readiness: the target returned healthy FCC responses while provider calls emitted structured 503 retry events.

The compatibility fallback remains a plain `--print` invocation with text or single JSON output behind the same project-owned `IAgentRuntime` boundary. It is not promoted to primary because stream-json is directly observed and exposes richer lifecycle/session/failure information. Successful fallback completion is still blocked by current upstream availability.

## Compatibility evidence taxonomy

P00 compatibility evidence uses these terms deliberately:

- `TESTED` — the stated component/version/behavior was exercised on the authoritative Windows target for the described scope.
- `DETECTED` — the component/version was discovered or reported by authoritative target evidence, but the relevant end-to-end behavior was not fully exercised for that row.
- `UNVERIFIED` — required compatibility behavior is not yet established by authoritative target evidence.
- `SUPPORTED` — an explicit product support commitment, not merely an observation from one target machine.
- `UNSUPPORTED` — authoritative evidence proves the component/version/behavior is incompatible with the declared contract.

`TESTED` is not a synonym for `SUPPORTED`. P00 currently records the narrow tested/detected baseline and must not silently turn one-machine observations into broader support ranges. Explicit supported ranges may be declared only when the evidence and closure criteria justify them.

## Observed compatibility baseline

| Component / behavior | Observed target baseline | Evidence classification | Notes |
|---|---|---|---|
| Windows | Windows 10 x64, build `19045` | TESTED | Authoritative target OS used for FCC and Unity contract evidence. |
| PowerShell | Windows PowerShell `5.1.19041.6456` | TESTED | Target runner environment observed on the Windows target. |
| Node.js | `20.11.1` | TESTED | Used by the repository-owned target probes. |
| .NET SDK | `10.0.400` | DETECTED | Present on target; P00 does not yet establish a general .NET SDK support range. |
| `fcc-claude` | Claude Code `2.1.251` | TESTED | Discovery/help/streaming/failure/cancellation behavior exercised. Successful provider-backed completion/resume remains separately UNVERIFIED. |
| `fcc-server` | loopback `8082`, `/health` returns 200 | TESTED | Health is a separate readiness signal and did not imply upstream provider availability. |
| Provider failure path | configured upstream returned structured HTTP `503` retries | TESTED | Provider-unavailable semantics are observed and classified. |
| Provider successful completion | no successful provider-backed turn in current target evidence | UNVERIFIED | Blocks successful session/resume and CLI-fallback completion evidence. |
| Model metadata / selection | model/runtime metadata observed in structured init evidence | DETECTED | Successful model-backed completion compatibility is not yet established. |
| Unity Hub | standard Program Files installation | DETECTED | Installation/discovery is recorded; this row does not declare a Hub support range. |
| Unity Editor `6000.5.8f1` | real disposable-project compile/test/automation/build contract passed | TESTED | This is the strongest Unity target baseline currently exercised. |
| Unity Editor `2022.3.75f1` | additional editor installation discovered | DETECTED | Do not infer the complete `6000.5.8f1` contract passed on this version without separate evidence. |
| Unity test framework | `1.7.0` minimum declared by Unity `6000.5.8f1` | DETECTED | Declaration/discovery is recorded separately from a broad package support commitment. |
| Blender | executable not installed/discoverable on the authoritative target | UNVERIFIED | No Blender version or real automation behavior may be marked TESTED/SUPPORTED yet. |

No component is classified `UNSUPPORTED` by current P00 evidence. No broader version range is classified `SUPPORTED` yet; doing so would require an explicit evidence-backed compatibility decision rather than inference from detection or a single tested machine.

## Open compatibility boundaries

- successful provider-backed completion and resume are blocked by observed upstream 503 responses;
- natural rate-limit behavior has not been observed and must not be forced with artificial traffic; `PG-002-P00-RATE-LIMIT-CLOSURE` records the unresolved closure-policy boundary;
- Blender versions and real automation behavior remain unknown until Blender is available on the authoritative target;
- successful assistant/tool/final stream event shapes remain additive unknown-event compatibility cases;
- the exact product `SUPPORTED` version ranges remain intentionally undeclared until the remaining target evidence and P00 closure criteria are satisfied.

The baseline is intentionally evidence-based and does not claim broader supported version ranges. `FCCD-P00-006` and `FCCD-P00-010` remain `IMPLEMENTED` until the remaining P00 target blockers are resolved and the exact-head exit gate passes.
