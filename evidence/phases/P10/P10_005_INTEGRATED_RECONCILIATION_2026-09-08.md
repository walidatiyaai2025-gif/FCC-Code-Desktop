# FCCD-P10-005 — Dedicated Unity log capture/parser

Date: 2026-09-08
Phase: P10 — Unity first-class adapter
Task: `FCCD-P10-005 — Dedicated Unity log capture/parser`
Evidence classification: CLOUD_ACCEPTED / INTEGRATED

## Live-state recovery selection

- Scheduling hint `FCCD-P20-001 — All non-environment automated suites green` was future work and was not executed because canonical `CURRENT_PHASE=P10`.
- Exact source main at recovery selection was `c89759b6726d86fe05b7dbb42e15e9f0f7bf7c32`.
- The live sweep found one legitimate integration-pending current-phase PR: #243 on `worker/fccd-p10-005-unity-log-capture-parser`.
- Recovery of that PR therefore took priority over any new P10 claim or future P20 work.

## Implementation boundary

P10-005 adds a dedicated bounded Unity `-logFile` capture/parser layer. It owns incremental log-file reading and structural line classification only; it does not claim later P10 compile, EditMode, PlayMode, Editor automation, build, structured UI-event, or cancellation/recovery outcome semantics.

The implementation provides:

- an immutable continuation cursor with byte offset, monotonic sequence and log generation;
- bounded line size, batch size and read buffer configuration;
- reading while Unity may still hold the file open via read/write/delete sharing;
- partial line preservation across polling boundaries, including split UTF-8/Arabic content;
- UTF-8 BOM handling that does not consume the configured content-byte budget;
- strict UTF-8 decoding with replacement-decoded diagnostic text plus explicit encoding-error metadata;
- deterministic reset/generation handling when the log is truncated below the cursor;
- caller-controlled flush of a final unterminated line after Unity termination;
- structural `Info`, `Warning`, `Error`, `Exception`, and `Assert` classification, including compiler-looking log lines, without treating those classifications as compile/test/build success or failure decisions.

The implementation uses only an explicit fully-qualified log path and does not add secret/provider handling or owner-environment assumptions.

## Deterministic acceptance coverage

`tests/FCCCodeDesktop.UnityLogCaptureFixture` and `tools/unity/validate-unity-log-capture.ps1` cover:

- parser classification and recognized timestamp handling;
- non-timestamp bracket prefixes remaining intact;
- option/path validation and missing-file behavior;
- incremental complete-line emission with partial UTF-8/Arabic preservation;
- deterministic monotonic sequence behavior;
- log truncation/reset generation behavior;
- bounded batch continuation without duplicate/lost lines;
- oversized-line bounding with exact dropped-byte accounting;
- malformed UTF-8 surfaced structurally instead of crashing;
- final-line flush behavior;
- cancellation propagation.

Permanent validation is `.github/workflows/p10-005-unity-log-capture.yml` on hosted Windows with exact .NET SDK `10.0.400`, locked restore, Release warnings-as-errors build, and deterministic fixture execution.

## Defects exposed and repaired before acceptance

The first implementation validation exposed five analyzer `CA1512` errors in guard code. They were repaired with the framework `ArgumentOutOfRangeException.ThrowIf*` helpers. No analyzer suppression, warning demotion, test removal, or safety weakening was used.

The repaired build then reached the runtime fixture, which exposed a second genuine defect: a UTF-8 BOM at the start of a generated log consumed three bytes of the bounded line-content budget, causing incorrect retained length and dropped-byte accounting. The production reader was repaired so BOM transport bytes are excluded from content-budget accounting. The fixture assertions were retained unchanged and subsequently passed.

## Exact accepted implementation-candidate validation

All applicable checks on exact candidate `f7b9dd7663546d233c6140c6aa8c6911ee863512` completed SUCCESS:

- P10-005 Unity Log Capture Parser: run `34224412169` — SUCCESS.
- Windows CI: run `34224411973` — SUCCESS.
- Workspace Search Validation: run `34224411970` — SUCCESS.
- Large Workspace Safeguard Validation: run `34224411975` — SUCCESS.

PR #243 was then marked ready and normally merged using merge-commit flow, guarded by the exact accepted head SHA. No force-push, squash, rebase, waiver, or safety-check weakening was used.

## Exact implementation-main validation

Normal implementation merge / exact accepted implementation main:

`af7f79ed81ca57b5412bba4dbb2cd11c2cd100c9`

All applicable push checks on that exact main completed SUCCESS:

- P10-005 Unity Log Capture Parser: run `34225327300` — SUCCESS.
- Windows CI: run `34225327482` — SUCCESS.
- Workspace Search Validation: run `34225327290` — SUCCESS.
- Large Workspace Safeguard Validation: run `34225327291` — SUCCESS.

The exact-main Windows run completed the complete permanent Windows Release baseline plus all current P05/P06 validation steps. No post-merge product regression was found.

## Owner-last / target boundary

No owner-only evidence is required for P10-005. Its capture/parser contract is fully cloud-verifiable with deterministic hosted-Windows file fixtures and does not require an installed Unity editor to prove the owned parsing semantics.

No Unity-runtime, FCC/provider, manual Windows, or physical-target result is fabricated. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner queue item and is neither modified nor duplicated.

## Canonical reconciliation scope

This reconciliation changes only canonical state/evidence for the already-integrated and exact-main-green P10-005 implementation:

- `CURRENT_PHASE.md`: P10-005 inventory `PENDING` → `CLOSED` plus integration provenance;
- `docs/TASK_LEDGER.md`: P10-005 `PENDING` → `CLOSED` and the next-action pointer moves to a fresh live sweep before P10-006;
- this evidence file.

No product code, owner queue state, P10-006+ implementation, phase-exit state, or later-phase authority changes here. P10 remains `IN_PROGRESS`; P10-006 through P10-013 remain `PENDING`; `PHASE_EXIT_GATE=NOT_RUN`; P11+ implementation remains prohibited; `VERIFIED_FINAL_COMPLETE=false`.
