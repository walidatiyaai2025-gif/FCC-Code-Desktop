# P04-008 Cloud-Complete / Target-Validation-Required Handoff — 2026-09-04

## Scope

This record is the durable handoff for `FCCD-P04-008 — Runtime contract suite` after its cloud-actionable implementation was integrated and verified on canonical `main`.

This record does **not** close `FCCD-P04-008`, does **not** run or pass the P04 phase exit gate, does **not** advance to P05, and does **not** set `VERIFIED_FINAL_COMPLETE=true`.

## Canonical implementation provenance

- Pre-implementation canonical base: `11d64600a316719ab73fbe6204ab5245782a53ff`.
- Implementation/recovery PR: #115 — `P04-008: add aggregate runtime contract suite`.
- Worker branch: `worker/fccd-p04-008-runtime-contract-suite`.
- Initial candidate `4c55dda48b4fe43f7b305ef6e5a3af6133210453` reached Windows CI run `33854390285` / run #161. Production Release build, unit/integration tests, and prior P04 validators passed, but the new aggregate harness failed analyzer enforcement only (`CA1859` concrete collection recommendations and `CA1869` cached `JsonSerializerOptions`).
- Analyzer-only repair commit: `d2f2512d4708c0d064ff9dd2b83a5080da6af1d3`. The repair cached evidence JSON serializer options and used the concrete `List<AgentRuntimeEvent>` type already produced by the harness; no runtime contract, assertion, safety boundary, or evidence classification was weakened.
- PR #115 synthetic merge tested by GitHub-hosted Windows CI: `424f59aced4d700e0f41398f8c8dd6277766379d` = repaired candidate merged into base `11d64600a316719ab73fbe6204ab5245782a53ff`.
- Repaired candidate Windows CI run `33855016920` / run #162: **SUCCESS**.
- PR #115 normal merge: `16f848f403e41fda8c315bdbc0c7d65c80589c7b`, with parents `11d64600a316719ab73fbe6204ab5245782a53ff` and `d2f2512d4708c0d064ff9dd2b83a5080da6af1d3`; tested ancestry was preserved.
- Exact post-merge canonical-main Windows CI run `33855389026` / run #163: **SUCCESS** on `16f848f403e41fda8c315bdbc0c7d65c80589c7b`.

## Cloud validation result

The repaired PR candidate and exact integrated main passed the permanent Windows CI baseline. Candidate run #162 recorded:

- Release build: **0 warnings, 0 errors**.
- Unit tests: **24 passed, 0 failed**.
- Integration tests: **37 passed, 0 failed**.
- FCC environment discovery: PASS.
- FCC runtime health/version compatibility: PASS.
- FCC structured runtime adapter: PASS.
- FCC runtime event normalization: PASS.
- FCC CLI fallback runtime adapter: PASS.
- P04 aggregate runtime contract static/negative validation: PASS.
- P04 aggregate runtime contract synthetic happy/negative/cancel/resume/fallback fixture: PASS.
- All inherited shell/design/DPI validators: PASS.
- Overall Windows CI baseline: PASS.

The aggregate suite uses the production `FccStructuredAgentRuntime` and `FccCliFallbackAgentRuntime` behind a tracked headless harness while substituting a controlled fake FCC executable for deterministic cloud validation. This proves the project-owned orchestration/classification mechanics but is **SELF_TEST_ONLY** evidence, not authoritative provider-backed target evidence.

## Integrated implementation

P04-008 now includes cloud-integrated infrastructure for:

- structured success/event streaming/session identity;
- session resume scenario;
- invalid-session failure scenario;
- explicit cancellation scenario;
- compatibility fallback scenario after structured-path failure;
- monotonic event sequencing checks;
- bounded scenario timeouts;
- sanitized evidence output with session identity represented only by a short SHA-256-derived hash where needed;
- explicit `RATE_LIMIT = NOT_INDUCED` safety classification;
- permanent Windows CI enforcement plus negative fixtures that reject removal of resume coverage, rate-limit safety, exact-worktree provenance, or phase-lock enforcement;
- authoritative-target runner `tools/runtime/run-p04-runtime-target-validation.ps1` with Windows-only, exact repository/HEAD, clean-worktree/executable-input, and exact .NET SDK `10.0.400` guards.

## Evidence classification

### Verified in GitHub-hosted Windows CI

`SELF_TEST_ONLY`

This classification covers deterministic harness mechanics, synthetic runtime success/failure/resume/cancellation/fallback behavior, production adapter integration, analyzers, tests, and the permanent Windows CI policy.

### Still required

`REAL_TARGET`

A fresh authoritative owner-Windows run must execute the integrated P04 runtime contract runner against the owner's actual installed `fcc-claude` / FCC/provider environment. Cloud CI is not substituted for this requirement.

The required command surface is the tracked runner:

```powershell
.\tools\runtime\run-p04-runtime-target-validation.ps1
```

The runner is responsible for discovering the actual installed runtime, enforcing exact-head/clean-worktree provenance, running the real structured success/stream/session/resume/failure/cancellation/fallback scenarios, and writing sanitized `REAL_TARGET` evidence under:

```text
evidence/phases/P04/runtime-contract/
```

Do not manufacture or deliberately trigger provider 429/rate-limit traffic. If no natural rate limit occurs, retain the explicit non-induced/non-observed classification required by the repository's evidence policy.

## Canonical task/phase state after this handoff

- `FCCD-P04-001` through `FCCD-P04-007` — CLOSED.
- `FCCD-P04-008` — remains unresolved in the canonical ledger until fresh authoritative `REAL_TARGET` evidence is generated, reviewed, integrated, and reconciled.
- `CURRENT_PHASE` — P04.
- `CURRENT_PHASE_STATE` — IN_PROGRESS.
- `PHASE_EXIT_GATE` — NOT_RUN.
- P04 phase closure — NOT CLAIMED.
- P05 implementation — PROHIBITED until P04-008 is CLOSED and the separate exact-head P04 exit gate passes with canonical closure evidence.
- `VERIFIED_FINAL_COMPLETE` — false.

## Next legitimate action

On the authoritative owner Windows target, start from the then-current canonical `main`, verify there is no newer legitimate P04 recovery work, run `tools/runtime/run-p04-runtime-target-validation.ps1` exactly as tracked, and integrate only genuine sanitized `REAL_TARGET` evidence. If the target run exposes a product defect, repair P04-008 and rerun the affected exact-head validation; do not convert target failure into success metadata.

Only after P04-008 has real closure evidence may the P04 exact-head phase exit gate be run. P05-008 or any other P05 implementation remains out of order until that gate is canonically PASS.
