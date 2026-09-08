# FCCD-P10-007 — EditMode test integration

Date: 2026-09-08
Phase: P10 — Unity first-class adapter
Task: `FCCD-P10-007 — EditMode test integration`
Evidence classification: CLOUD_ACCEPTED / INTEGRATED

## Live-state recovery selection

- Slot hint `FCCD-P20-007 — Freeze exact release candidate SHA` was future work and was not executed because canonical `CURRENT_PHASE=P10`.
- The slot recovered the legitimate integration-pending P10-007 lane rather than starting duplicate or later-phase work. At recovery start, implementation PR #247 was still pending final regression convergence on exact head `d5c9c7c780cf0a05a46ab69d3520ad5183b95cce`.
- No second P10 task was selected.

## Implementation boundary

P10-007 adds fail-closed Unity EditMode result integration over the controlled P09 process/result contract and the typed Unity CLI action. P00 genuine target evidence established NUnit3 `test-run` output with a nonzero EditMode test count, so this layer validates that contract rather than inventing one. It intentionally does not own P10-008 PlayMode semantics or P10-009+ automation/build/event/cancellation work.

A PASS requires a successful controlled `unity.run-tests.editmode` process plus a fresh result artifact. Pre-run fingerprinting prevents stale XML reuse; P09 safe artifact validation is reused; result size is capped at 16 MiB; length/SHA-256 are rechecked to detect TOCTOU changes; DTD/entities are rejected; counters must be coherent with nonzero discovery; cancellation stays distinct; a passing XML cannot override process failure; and retained failure details are bounded. Missing, empty, oversized, stale, malformed, inconsistent, zero-test, or changed artifacts fail closed.

## Deterministic acceptance coverage

`tests/FCCCodeDesktop.UnityEditModeTestFixture`, locked restore data, `tools/unity/validate-unity-editmode-tests.ps1`, and permanent `.github/workflows/p10-007-unity-editmode-tests.yml` cover pass/fail NUnit results, Unicode/Arabic values, process/launch/cancellation disagreement, missing/empty/stale/oversized artifacts, malformed XML, DTD/entity rejection, zero tests, inconsistent counters, bounded failure diagnostics, operation/path guards, Release warnings-as-errors, and hosted-Windows deterministic execution.

## Defect exposed and repaired before acceptance

Initial candidate `7ea751bcd95023124792a256cb4669377e850ec6` built with zero warnings/errors but the runtime fixture exposed an invalid ordering assumption: a failed NUnit suite may be retained before its failed test case. The same lane was repaired to assert bounded failure message/stack evidence independently of XML node ordering, keeping all production limits unchanged. No analyzer suppression, warning demotion, test removal, force-push, or safety weakening was used. Temporary repair orchestration was removed from the durable diff.

## Exact accepted implementation-candidate validation

Exact accepted candidate: `d5c9c7c780cf0a05a46ab69d3520ad5183b95cce`.

- P10-007 Unity EditMode Test Integration `34237835082` — SUCCESS.
- Windows CI `34237835044` — SUCCESS.
- Workspace Search `34237835184` — SUCCESS.
- Large Workspace Safeguards `34237835056` — SUCCESS.

PR #247 was then marked ready and normally merged with expected-head guarding.

## Exact implementation-main validation

Normal implementation merge / exact accepted implementation main: `cf8f2ef42459b11157107ee46d4319592a19ba70`.

- P10-007 Unity EditMode Test Integration `34239031180` — SUCCESS.
- Windows CI `34239031262` — SUCCESS, including the Release baseline and all current P05/P06 tail validators.
- Workspace Search `34239031249` — SUCCESS.
- Large Workspace Safeguards `34239031144` — SUCCESS.

## Owner-last / target boundary

No new owner-only evidence is required for P10-007. P00 already contains genuine installed-Unity contract evidence; P10-007's owned parser/integration semantics are cloud-verifiable with deterministic hosted-Windows fixtures. No installed-Unity, FCC/provider, or manual evidence is fabricated. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner queue item and is not changed or duplicated.

## Canonical reconciliation scope

This reconciliation changes only `CURRENT_PHASE.md`, `docs/TASK_LEDGER.md`, and this evidence file for the already-integrated exact-main-green P10-007 implementation. No product code, owner queue state, P10-008+ implementation, phase exit, or later-phase authority changes. P10 remains `IN_PROGRESS`; P10-008 through P10-013 remain `PENDING`; `PHASE_EXIT_GATE=NOT_RUN`; P11+ implementation remains prohibited; `VERIFIED_FINAL_COMPLETE=false`.
