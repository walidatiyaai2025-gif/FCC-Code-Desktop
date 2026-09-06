# P06-008 Large Workspace Safeguards — Cloud Recovery Evidence

## Task

- Task: `FCCD-P06-008 — Large file/tree safeguards`.
- Canonical cloud phase at recovery start: `P06 — Projects + files + editor + search`.
- Evidence class: cloud implementation/test/integration evidence only.
- This file does not claim P06-008 CLOSED, P06 phase-exit PASS, release eligibility, or `VERIFIED_FINAL_COMPLETE` until exact canonical-main validation is complete.

## Recovery provenance

- Recovery start canonical main: `887e85a16f60c7b6404b7facf5c96ccf73759980`.
- Stale Draft PR recovered: #152 — `FCCD-P06-008: harden large workspace boundaries`.
- Recovered source head: `13fdca9ce60fec9064231b64def243328ed50fe2`.
- Lane-B recovery branch: `worker-b/fccd-p06-008-large-workspace-recovery`.
- Ancestry-preserving recovery merge: `435e10ae77e7dd48ed9c5f55d3e19fe38d138c26`.
- Recovery merge parents: canonical main `887e85a16f60c7b6404b7facf5c96ccf73759980` and recovered source head `13fdca9ce60fec9064231b64def243328ed50fe2`.
- No rebase, squash, force-push, or write to the recovered worker branch was used.

## Production scope

- Central typed `WorkspaceScalePolicy` with finite defaults and supported ceilings for directory materialization, traversal depth, files examined, search results, per-file matches, text/search file bytes, previews, binary probes, and generated/vendor exclusions.
- Lazy explorer hardening: bounded materialization, typed generated/depth/reparse restrictions, cancellation, project-root containment, deterministic presentation ordering, and truthful truncation metadata.
- File-service hardening: bounded read-only inspection that classifies text/binary/too-large files, returns bounded previews, preserves encoding safety, and leaves normal bounded conflict-aware save semantics intact.
- Workspace-search hardening: the recovered draft documented central policy use but still retained hard-coded exclusion/preview/probe behavior and lacked traversal-depth/per-file match enforcement. Recovery corrects that defect by consuming `WorkspaceScalePolicy`, enforcing configured ceilings, reporting effective typed limits, and retaining regex timeout/cancellation/root/reparse protections.
- P06-006 ownership is not taken: tabs, load/save orchestration, reload, external-change UX, and dirty-state lifecycle remain outside P06-008.

## Permanent validation

- New dedicated validator: `tools/projects/validate-large-workspace-safeguards.ps1`.
- Validator includes static contract checks, destructive/unbounded-operation exclusions, negative fixtures, exact .NET SDK enforcement, focused workspace-policy unit tests, and explorer/file/search integration fixtures.
- Canonical Windows CI is updated to require `Validate P06-008 large workspace safeguards` with `-RunFixtures -RequireRuntime`.
- `tools/ci/validate-windows-ci.ps1` is updated so removal of the P06-008 gate is itself rejected.
- The inherited P06-007 workspace-search validator is strengthened to require the centralized policy-based search implementation rather than the superseded hard-coded limits.

## Validation state

- Starting exact-main Windows CI #293 / run `34020853699`: SUCCESS on `887e85a16f60c7b6404b7facf5c96ccf73759980`.
- Starting exact-main P06-007 Workspace Search #22 / run `34020853716`: SUCCESS on `887e85a16f60c7b6404b7facf5c96ccf73759980`.
- Recovered source head had previously passed Windows CI #282 / run `34018672946` and P06-007 Workspace Search #11 / run `34018672954`, but those runs did not contain the completed P06-008 permanent gate or the recovered search-policy repair and are not treated as final P06-008 acceptance.
- Exact recovery-head validation: PENDING until the Lane-B pull request workflow completes.
- Exact post-merge canonical-main validation: PENDING until normal integration and resulting main workflows complete.

## Owner-last boundary

- P06-008 introduces no genuine owner-only/manual/FCC/provider/Unity/Blender evidence requirement.
- `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` remains unchanged.
- Existing `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain the only queued release-blocking owner obligations.
- No fabricated provider or target-machine PASS is claimed.
