# P00 Runtime and Compatibility Baseline

**Tasks:** `FCCD-P00-006`, `FCCD-P00-010`  
**State:** VERIFIED from the complete P00 evidence set; final CLOSED transition awaits the exact-head P00 exit gate.

## Primary runtime contract

The primary P04 runtime adapter will launch the installed `fcc-claude` executable as an owned process using argument arrays, `--print`, and `--output-format stream-json --verbose`. It will parse newline-delimited JSON into project-owned runtime events while preserving unknown event types and bounded sanitized raw evidence. The authoritative Windows session lane verified session identifiers from structured output and successful continuation through the documented `--resume <session-id>` surface in a new process.

The local `fcc-server` loopback health endpoint remains a separate readiness signal. Health PASS does not by itself imply provider readiness: historical target evidence captured healthy FCC loopback responses while the configured upstream emitted structured 503 retry events. Newer target evidence independently proves provider-backed successful completion, so the historical 503 observation is a tested failure mode rather than the current completion boundary.

The compatibility fallback remains a plain `--print` invocation with text or single JSON output behind the same project-owned `IAgentRuntime` boundary. It is not promoted to primary because stream-json is directly observed and exposes richer lifecycle/session/failure information. Authoritative Windows evidence now proves provider-backed fallback completion across normal, space-containing, and Unicode/Arabic working directories, together with stdout/stderr observability, graceful cancellation, and owned-process cleanup.

The failure/cancellation contract is also exact-head verified for its exercised scope: a provider-backed baseline completed successfully, cancellation classified `INTERRUPTED`, graceful interruption was attempted, late/residual owned descendants were observed and cleaned by previously observed PID/identity, and zero owned processes remained.

## Compatibility evidence taxonomy

P00 compatibility evidence uses these terms deliberately:

- `TESTED` â€” the stated component/version/behavior was exercised on the authoritative Windows target for the described scope.
- `DETECTED` â€” the component/version was discovered or reported by authoritative target evidence, but the relevant end-to-end behavior was not fully exercised for that row.
- `UNVERIFIED` â€” required compatibility behavior is not yet established by authoritative target evidence.
- `SUPPORTED` â€” an explicit product support commitment, not merely an observation from one target machine.
- `UNSUPPORTED` â€” authoritative evidence proves the component/version/behavior is incompatible with the declared contract.

`TESTED` is not a synonym for `SUPPORTED`. P00 records narrow, scope-specific tested/detected observations and must not silently turn one-machine observations into broader support ranges. Explicit supported ranges may be declared only when the evidence and closure criteria justify them.

## Observed compatibility baseline

| Component / behavior | Observed target baseline | Evidence classification | Notes |
|---|---|---|---|
| Windows | Windows 11 Pro x64, `10.0.26100` in the newer FCC closure evidence; earlier Unity evidence recorded Windows x64 `10.0.19045` | TESTED | Both are target observations for their exercised lanes; neither establishes a general Windows support range. |
| PowerShell | Windows PowerShell `5.1.26100.8875` | TESTED | Observed by the successful Windows FCC CLI closure evidence. |
| Node.js | `22.23.2` in the newer FCC closure evidence; Unity target evidence used `20.11.1` | TESTED | Versions are scope-specific target observations, not a declared Node support range. |
| .NET SDK | `10.0.400` | DETECTED | Present in the successful FCC CLI closure evidence; P00 does not establish a general .NET SDK support range. |
| `fcc-claude` | Claude Code `2.1.251` | TESTED | Discovery/help, structured streaming, provider-backed successful completion, session/resume continuity, CLI fallback, and failure/cancellation were exercised in their respective target lanes. |
| `fcc-server` | loopback `8082`, `/health` returns 200 | TESTED | Health is a separate readiness signal; provider success and provider failure were established independently in later/earlier target runs. |
| Provider failure path | configured upstream returned structured HTTP `503` retries | TESTED | Provider-unavailable/retry semantics were directly observed in historical Windows streaming evidence. |
| Provider successful completion | real provider-backed successful turns in session/resume, CLI fallback, and the exact-head failure baseline | TESTED | Classification is limited to the exercised prompts/runtime configuration and is not a provider-wide support guarantee. |
| Session / resume continuity | first turn succeeded; authoritative session ID captured; new-process resume recovered prior context; invalid-session rejection and valid-session recovery passed | TESTED | Evidence also covers continuation from a different working directory and owned-process cleanup. |
| Failure / cancellation | exact-head Windows baseline `SUCCESS`; cancellation `INTERRUPTED`; graceful interrupt; residual owned-process cleanup; zero remaining owned processes | TESTED | Tested source SHA `015ffd8c0e2a6e725e33ed153441ff51e7952556`. |
| Provider rate-limit event semantics | `NOT_OBSERVED_ON_TARGET`; deterministic synthetic-429 classifier mechanics verified separately as `SELF_TEST_ONLY` | UNVERIFIED | `PG-002-P00-RATE-LIMIT-CLOSURE` is RESOLVED. This is an accepted P00-005 closure boundary, not an observed provider 429 and not a PASS classification for real rate-limit behavior. |
| Model metadata / selection | model/runtime metadata observed on successful structured target execution | DETECTED | The exercised configured model produced successful turns, but P00 does not declare a model/provider compatibility range or general selection guarantee. |
| Unity Hub | standard Program Files installation | DETECTED | Installation/discovery is recorded; this row does not declare a Hub support range. |
| Unity Editor `6000.5.8f1` | real disposable-project creation, compile negative/recovery, EditMode/PlayMode tests, automation, Windows x64 build, lock, cancellation, and cleanup contract passed | TESTED | This is the Unity version on which the complete P00 target contract was exercised. |
| Unity Editor `2022.3.75f1` | additional editor installation discovered | DETECTED | Do not infer the complete `6000.5.8f1` contract passed on this version without separate evidence. |
| Unity test framework | `1.7.0` minimum declared by Unity `6000.5.8f1` | DETECTED | Declaration/discovery is recorded separately from a broad package support commitment. |
| Blender `5.2.0` | real discovery/version, background/factory startup, Python automation, `.blend` save, PNG render, OBJ export, controlled Python failure, cancellation and cleanup passed | TESTED | Complete P00 Blender contract exercised on the authoritative Windows target at source SHA `e6932783b30ab0bdbb596c7959e03143753bff9a`; this is a tested version, not a declared general support range. |

No component is classified `UNSUPPORTED` by current P00 evidence. No broader version range is classified `SUPPORTED` yet; doing so would require an explicit evidence-backed compatibility decision rather than inference from detection or a single tested machine.

## Open compatibility boundaries

- real provider rate-limit event/output/exit/retry semantics remain `NOT_OBSERVED_ON_TARGET`; the classifier mechanics are verified, and `PG-002-P00-RATE-LIMIT-CLOSURE` is resolved without manufacturing a 429;
- Blender `5.2.0` is now `TESTED` for the complete exercised P00 target contract; broader Blender version ranges remain intentionally undeclared;
- successful provider-backed structured execution is now observed, but P00 does not claim exhaustive coverage of every possible assistant/tool/result event variant; unknown event types remain an additive compatibility case that the adapter must preserve;
- the exact product `SUPPORTED` version ranges remain intentionally undeclared until the remaining target evidence and P00 closure criteria justify a support decision.

The baseline is intentionally evidence-based and does not claim broader supported version ranges. `FCCD-P00-006` and `FCCD-P00-010` are now `VERIFIED`: all task-local evidence dependencies, including real Blender execution, are resolved. Their final transition to `CLOSED` is reserved for the exact-head P00 exit-gate closure record.
