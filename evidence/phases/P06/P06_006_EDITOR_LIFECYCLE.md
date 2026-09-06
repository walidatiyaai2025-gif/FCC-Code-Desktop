# FCCD-P06-006 — Editor Tabs / Save / Reload / Dirty State

## Task

`FCCD-P06-006 — Editor tabs/save/reload/dirty state`

## Cloud implementation

- `ProjectEditorWorkspace` owns multi-tab editor lifecycle state over the canonical `IProjectFileService` contract.
- File open performs P06-008 inspection before normal text materialization and rejects binary/oversized content.
- Tabs are unique by original project-root/file pair and remain pinned to that original root across later project switches.
- Open/save/reload lifecycle operations are serialized through a workspace `SemaphoreSlim`; concurrent same-file opens recheck tab identity after the preceding operation completes and reuse one document instead of racing into duplicate buffers.
- Synchronous close acquires the same operation gate non-blockingly and fails closed while another lifecycle operation is in flight.
- Dirty state is derived from the current editor buffer versus the last loaded/successfully saved buffer.
- Save carries the exact observed `ProjectFileVersion` and original encoding through P06-004 `WriteTextAsync`; no direct filesystem write path was added.
- Stable CRLF/LF/CR source newline policy is normalized into the WPF editor representation without false dirty state and restored to the established source style before save; mixed-newline buffers are not silently rewritten by the lifecycle layer.
- External-change conflict keeps the dirty buffer and surfaces `ProjectFileConflictException`; it does not overwrite stale owner work.
- Reload and close reject dirty-buffer destruction unless the caller explicitly requests discard; WPF UI requires Yes/No confirmation.
- Application shutdown is fail-safe in both relevant states: dirty tabs require explicit discard confirmation, while an in-flight editor open/save/reload operation unconditionally cancels normal window close until the operation finishes.
- Successful reload refreshes text, encoding, newline metadata and version and clears conflict/dirty state.
- `ProjectEditorSurface` composes the native P06-005 `CodeEditorControl` beside the project explorer/search surface with Save/Reload/Close controls and status/error presentation.
- P06-007's canonical `AttachSearchSurface()` composition seam is preserved; P06-006 adds its editor surface separately instead of weakening the existing workspace-search contract.

## Automated validation

Permanent validation is `tools/ui/validate-editor-lifecycle.ps1`. It verifies the lifecycle/source/UI boundary, safe-file-service usage, dirty/conflict guards, large/binary inspection, operation serialization, concurrent-open regression coverage, application-shutdown dirty/in-flight guards, no direct write/process/network bypass, and destructive negative fixtures under the exact Windows/.NET 10.0.400 baseline.

The gate executes focused unit coverage whose names match `ProjectEditorWorkspaceTests`, including `ConcurrentOpenSameFileIsSerializedAndReusesSingleTab`, plus real composition coverage in `ProjectEditorWorkspaceIntegrationTests`. The concurrency fixture deliberately holds the first inspection open, starts a second same-file open, then proves one inspection/read, one tab, shared returned document identity, and restored non-busy state. The real integration fixture uses `FileSystemProjectFileService` against an actual Unicode/space-containing project path and proves UTF-16BE preservation, LF round-trip integrity, optimistic external-change conflict refusal, dirty-buffer retention, and explicit reload recovery.

The canonical `.github/workflows/windows-ci.yml` registers `Validate P06-006 editor lifecycle`, and `tools/ci/validate-windows-ci.ps1` fails closed if that permanent gate is removed. Inherited P06-007 and P06-008 dedicated workflows remain mandatory non-regression gates for the shared Projects workspace.

Exact accepted PR-head candidate `60aca82b36b046c7d5373cb8b4c807e0550e85e4` passed Windows CI run `34028644029` / run #343, P06-007 Workspace Search run `34028644082` / run #72, and P06-008 Large Workspace Safeguards run `34028644031` / run #52. PR #157 was normally merged as `8d204b9618be9d398d29668bc2b7f1ddec9f0ceb`; that exact canonical-main SHA passed Windows CI run `34028997094` / run #344, P06-007 Workspace Search run `34028996981` / run #73, and P06-008 Large Workspace Safeguards run `34028997023` / run #53. Canonical integration provenance is recorded in `evidence/phases/P06/P06_006_INTEGRATED_RECONCILIATION_2026-09-06.md`.

## Owner-last boundary

P06-006 is fully cloud-actionable and introduces no new owner-machine/manual/provider requirement. `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` remains unchanged; existing `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain the only queued owner obligations. No P06 closure, P07 authorization, phase-exit PASS, release eligibility, or `VERIFIED_FINAL_COMPLETE` is claimed by this task evidence.
