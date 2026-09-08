# FCCD-P10-006 — Compile validation

Date: 2026-09-08
Phase: P10 — Unity first-class adapter
Task: `FCCD-P10-006 — Compile validation`
Evidence classification: CLOUD_ACCEPTED / INTEGRATED

## Live-state recovery selection

- Scheduling hint `FCCD-P20-004 — Blender contract suite green` was future work and was not executed because canonical `CURRENT_PHASE=P10`.
- Exact source main at recovery selection was `3be87a5657bf0326d025425b8a59a6a3d8a9f77e`.
- No open pull request existed. P10-006 product implementation had already been normally merged by PR #245, while both `CURRENT_PHASE.md` and `docs/TASK_LEDGER.md` still recorded P10-006 as `PENDING`.
- Recovery/reconciliation of that already-integrated work therefore took priority over P10-007 or any future P20 work.

## Implementation boundary

P10-006 adds typed, fail-closed Unity compile outcome validation over the existing P09 process-result contract and P10-005 invocation-scoped Unity log evidence. It intentionally does not take ownership of P10-007+ EditMode, PlayMode, Editor automation, build-artifact, structured UI-event, or cancellation/recovery integration.

The implementation requires a successful controlled `unity.open-project` process plus observed, finalized, complete, contiguous, non-reset, non-corrupt log evidence. A zero exit code alone can never establish compile success. C# `error CS####` diagnostics and explicit Unity compilation-failure markers fail even when Unity exits zero. Cancellation is distinct; launch/process failures fail; missing/incomplete/reset/truncated/encoding-corrupt evidence remains `INDETERMINATE` instead of being guessed PASS. Compiler diagnostics are bounded while total error counts and sequence/compiler-code correlation are retained.

## Deterministic acceptance coverage

`tests/FCCCodeDesktop.UnityCompileValidationFixture` and `tools/unity/validate-unity-compile.ps1` cover success/warnings, exit-zero compiler failure, explicit failure markers, process/launch/cancellation/forced-termination outcomes, missing/empty/non-finalized/gapped/reset/truncated/invalid-UTF8 evidence, bounded diagnostics, snapshot immutability, and operation guards. Permanent validation is `.github/workflows/p10-006-unity-compile-validation.yml` using hosted Windows, exact .NET SDK `10.0.400`, locked restore, Release warnings-as-errors, and deterministic fixture execution.

## Defect exposed and repaired before acceptance

Initial implementation candidate `6f5dae574a8de4963a3bc46fd8e6aabecd9d496d` failed the dedicated gate because analyzer `CA1845` rejected `Substring`-based compiler-code extraction. The same branch was repaired to use span-based concatenation in `d6eceee3f9cb3c1b0ea282febb24abf5c1728ac5`. No analyzer suppression, warning demotion, test deletion, force-push, or safety weakening was used.

## Exact accepted implementation-candidate validation

All applicable checks on exact repaired candidate `d6eceee3f9cb3c1b0ea282febb24abf5c1728ac5` completed SUCCESS:

- P10-006 Unity Compile Validation: run `34230767921` — SUCCESS.
- Windows CI: run `34230767619` — SUCCESS.
- Workspace Search Validation: run `34230767687` — SUCCESS.
- Large Workspace Safeguard Validation: run `34230767648` — SUCCESS.

PR #245 was marked ready and normally merged using merge-commit flow guarded by the exact accepted head SHA.

## Exact implementation-main validation

Normal implementation merge / exact accepted implementation main:

`3be87a5657bf0326d025425b8a59a6a3d8a9f77e`

All applicable push checks on that exact main completed SUCCESS:

- P10-006 Unity Compile Validation: run `34231807438` — SUCCESS.
- Windows CI: run `34231807447` — SUCCESS.
- Workspace Search Validation: run `34231807449` — SUCCESS.
- Large Workspace Safeguard Validation: run `34231807641` — SUCCESS.

The Windows run completed the permanent Windows Release baseline and all current P05/P06 tail validators with no post-merge regression.

## Owner-last / target boundary

No new owner-only evidence is required for P10-006. The owned compile-outcome semantics are fully cloud-verifiable with deterministic hosted-Windows fixtures. No installed-Unity runtime result, FCC/provider result, or manual target evidence is fabricated. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner queue item and is neither modified nor duplicated.

## Canonical reconciliation scope

This reconciliation changes only canonical state/evidence for the already-integrated and exact-main-green P10-006 implementation:

- `CURRENT_PHASE.md`: P10-006 inventory `PENDING` → `CLOSED` plus integration provenance;
- `docs/TASK_LEDGER.md`: P10-006 `PENDING` → `CLOSED` and next-action pointer advances to a fresh live sweep before P10-007;
- this evidence file.

No product code, owner queue state, P10-007+ implementation, phase-exit state, or later-phase authority changes here. P10 remains `IN_PROGRESS`; P10-007 through P10-013 remain `PENDING`; `PHASE_EXIT_GATE=NOT_RUN`; P11+ implementation remains prohibited; `VERIFIED_FINAL_COMPLETE=false`.
