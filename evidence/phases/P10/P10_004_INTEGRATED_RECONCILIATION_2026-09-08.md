# FCCD-P10-004 — Unity process/project resource locking

Date: 2026-09-08
Phase: P10 — Unity first-class adapter
Task: `FCCD-P10-004 — Unity process/project resource locking`
Evidence classification: CLOUD_ACCEPTED / INTEGRATED

## Live-state selection

- Scheduling hint `FCCD-P19-002` was future work and was not executed because canonical `CURRENT_PHASE=P10`.
- Exact source main at claim: `bfe1ff3a2ff4d9f83430f53d9f89e0228828c406`.
- The live sweep found no open PR and no active P10-004 branch/claim before this task was claimed.
- Implementation branch: `worker/fccd-p10-004-unity-resource-locking`.
- Pull request: #239.

## Implementation

P10-004 reuses the provider-neutral P09 resource-locking contract instead of creating a second lock manager.

Each `UnityCliInvocation` declares two deterministic locks:

1. `unity:process:<project-id>` — a logical process slot keyed by the canonical `ProjectContext.ProjectId`.
2. `unity:project:<sha256>` — a physical Unity project-root lock keyed by a SHA-256 fingerprint of the canonicalized, Windows-case-folded full project path.

The raw project path is not embedded in the public lock key. The project-root key normalizes path casing and trailing separators so equivalent Windows path representations collide intentionally. The process and project locks are acquired through the shared P09 `IToolResourceLockManager`, preserving its deterministic ordering, cancellation behavior, partial-acquisition cleanup, and idempotent lease release. Unrelated Unity projects do not share either lock and are not globally serialized.

The Unity lock provider rejects non-Unity structured invocations at its generic provider boundary. P10-004 does not launch Unity, mutate a Unity project, parse Unity output, or claim later P10 compile/test/build semantics.

## Deterministic acceptance coverage

`tests/FCCCodeDesktop.UnityResourceLockingFixture` verifies:

- exactly two distinct locks per Unity invocation;
- same logical project ID with another root waits on the logical Unity process lock;
- different logical IDs targeting the same physical project root wait on the physical project lock;
- casing and trailing-separator variants of the same Windows project root resolve to the same physical-project lock;
- raw Unity project paths are not disclosed in physical-project lock keys;
- unrelated Unity projects acquire concurrently;
- cancellation propagates while waiting for either the process or project lock;
- cancellation after a partial multi-lock acquisition releases the earlier-acquired lock;
- lease disposal releases both Unity locks and allows deterministic reacquisition;
- the generic Unity lock-provider boundary rejects non-Unity invocations;
- Unicode, Arabic, spaces, and ordinary Windows path inputs remain supported.

Permanent validation is `tools/unity/validate-unity-resource-locking.ps1` plus `.github/workflows/p10-004-unity-resource-locking.yml`, using Windows Server 2025, exact .NET SDK `10.0.400`, locked restore, Release warnings-as-errors build, and fixture execution.

## Defect found and repaired

The initial implementation candidate `28ebb1a96bd3bcc74a5791b8e0f3ba7203622526` failed the dedicated P10-004 validation during Release fixture compilation because analyzer `CA1859` rejected three over-generalized fixture types. Product lock-source projects had already compiled successfully in that run.

The defect was repaired on the same branch without analyzer suppression, warning demotion, test removal, or safety weakening. Repair commit / exact accepted cloud candidate:

`5956e00286fdaff230c2b13f0e1919c208da60ed`

The repair uses the concrete `List<string>` type where appropriate and an explicit interface cast only where the fixture intentionally exercises `IExternalToolResourceLockProvider` dispatch.

## Exact accepted-candidate validation

All applicable checks on exact repaired candidate `5956e00286fdaff230c2b13f0e1919c208da60ed` completed SUCCESS:

- P10-004 Unity Resource Locking: run `34213834311` — SUCCESS.
- Windows CI: run `34213834508` — SUCCESS.
- Workspace Search Validation: run `34213834319` — SUCCESS.
- Large Workspace Safeguard Validation: run `34213834412` — SUCCESS.

No failed product check is deferred. The initial `CA1859` failure is retained here as repair provenance rather than hidden.

## Exact implementation-main validation

PR #239 was normally merged as `d38cc605e604e4c3e91d866b96bc470642c0144b`. All applicable push checks on that exact implementation main completed SUCCESS:

- P10-004 Unity Resource Locking: run `34214831778` — SUCCESS.
- Windows CI: run `34214831699` — SUCCESS.
- Workspace Search Validation: run `34214831670` — SUCCESS.
- Large Workspace Safeguard Validation: run `34214831640` — SUCCESS.

## Canonical reconciliation and exact-main regression recovery

PR #240 normally merged the P10-004 canonical reconciliation as `15c92bf8957a8dbdfd172db4687903f5984445d5`. The task row became canonically `CLOSED`, but the required exact-main verification exposed a real CI regression and therefore recovery retained priority over any new P10 work.

On exact main `15c92bf8957a8dbdfd172db4687903f5984445d5`:

- Workspace Search Validation: run `34216772265` — SUCCESS.
- Large Workspace Safeguard Validation: run `34216772279` — SUCCESS.
- P09 External Tool Gateway Exit: run `34216772157` — SUCCESS.
- Windows CI: run `34216772167` — FAILURE at `Validate P05-005 task state machine` after the full Windows Release baseline had completed SUCCESS.

The failing P05-005 runtime fixture timed out on its 30-second settlement wait while its final diagnostic already observed `State=Succeeded`, `IsActive=False`, `CanStop=False`, `CanRetry=False`, and no previously observed settling rejection. Earlier exact implementation-main Windows CI `34214831699` had passed the same P05-005 fixture. The failure was not deferred or reclassified as owner-only.

Recovery PR #241 used branch `recovery/p10-004-p05-005-settlement-flake`. The durable repair changes only `tools/ui/validate-task-state-machine.ps1`; production runtime behavior and P10 implementation are unchanged. The fixture repair:

- replaces wall-clock `DateTimeOffset.UtcNow` polling with monotonic `Stopwatch` elapsed time;
- keeps `ValidateCanStart()` as the fail-closed definition of fully settled state;
- retains the most recent genuine settling rejection for diagnostics;
- performs one final settlement probe after the monotonic deadline to eliminate the observed terminal-transition/deadline race;
- keeps a finite hosted-Windows settlement bound, widened from 30 seconds to 60 seconds rather than allowing an unbounded wait.

Exact repair candidate `8928b803f304d5c783abf2219f77520726d3fa71` passed every applicable PR-head check:

- Windows CI: run `34218416456` — SUCCESS. Its dedicated P05-005 task-state step completed SUCCESS, followed by all remaining Windows validation steps.
- Workspace Search Validation: run `34218416472` — SUCCESS.
- Large Workspace Safeguard Validation: run `34218416357` — SUCCESS.

The prior exact-main failure remains recorded here as repair provenance; no failing check is hidden, waived, or converted into manual evidence.

## Final recovery integration and exact-main validation

The final recovery PR candidate `de80d63130d2d18c6698d543a07a3ef665acd55d` added this repair provenance while retaining the same bounded validator repair. All applicable final-head checks completed SUCCESS:

- Windows CI: run `34219391553` — SUCCESS, including the dedicated P05-005 task-state step and all remaining P05/P06 validation steps.
- Workspace Search Validation: run `34219391367` — SUCCESS.
- Large Workspace Safeguard Validation: run `34219391485` — SUCCESS.

PR #241 was then normally merged, without squash, rebase, force-push, or safety-check weakening, as exact main `7b0bb6ccbaee00186d1f16967b3310bd81a85006`.

All applicable checks on that exact resulting main completed SUCCESS:

- Windows CI: run `34220329698` — SUCCESS. The full Windows Release baseline and the dedicated P05-005 task-state step both completed SUCCESS, followed by every remaining Windows validation step.
- Workspace Search Validation: run `34220329664` — SUCCESS.
- Large Workspace Safeguard Validation: run `34220329682` — SUCCESS.

This closes the discovered exact-main regression with real hosted-Windows evidence on the merged recovery commit. No product failure is deferred and no manual or owner-only result is fabricated.

## Owner-last / target boundary

No owner-only evidence is required for P10-004. Its resource-key derivation, lock contention, cancellation, cleanup, and concurrency semantics are fully exercised on hosted Windows without launching Unity.

No Unity runtime result, provider result, manual Windows result, or physical target evidence is fabricated. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner queue item and is not modified or duplicated by P10-004.

## Canonical reconciliation scope

P10-004 is durably `CLOSED` with its implementation, canonical reconciliation, exact-main regression repair, normal recovery integration, and exact resulting-main verification all recorded above. P10 remains `IN_PROGRESS`; P10-005 through P10-013 remain `PENDING`; `PHASE_EXIT_GATE=NOT_RUN`; P11+ implementation remains prohibited; `VERIFIED_FINAL_COMPLETE=false`.

This evidence-only provenance finalization changes no product code, task state, owner queue state, phase state, or future-phase authority.