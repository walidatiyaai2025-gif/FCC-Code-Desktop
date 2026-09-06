# FCCD-P06-006 — Editor Tabs / Save / Reload / Dirty State

## Task

`FCCD-P06-006 — Editor tabs/save/reload/dirty state`

## Cloud implementation

- `ProjectEditorWorkspace` owns multi-tab editor lifecycle state over the canonical `IProjectFileService` contract.
- File open performs P06-008 inspection before normal text materialization and rejects binary/oversized content.
- Tabs are unique by original project-root/file pair and remain pinned to that original root across later project switches.
- Dirty state is derived from the current editor buffer versus the last loaded/successfully saved buffer.
- Save carries the exact observed `ProjectFileVersion` and original encoding through P06-004 `WriteTextAsync`; no direct filesystem write path was added.
- Stable CRLF/LF/CR source newline policy is normalized into the WPF editor representation without false dirty state and restored to the established source style before save; mixed-newline buffers are not silently rewritten by the lifecycle layer.
- External-change conflict keeps the dirty buffer and surfaces `ProjectFileConflictException`; it does not overwrite stale owner work.
- Reload and close reject dirty-buffer destruction unless the caller explicitly requests discard; WPF UI requires Yes/No confirmation.
- Application shutdown is also fail-safe: when dirty editor tabs exist, `MainWindow` defaults to cancelling close unless the user explicitly confirms discarding those unsaved buffers.
- Successful reload refreshes text, encoding, newline metadata and version and clears conflict/dirty state.
- `ProjectEditorSurface` composes the native P06-005 `CodeEditorControl` beside the project explorer/search surface with Save/Reload/Close controls and status/error presentation.
- P06-007's canonical `AttachSearchSurface()` composition seam is preserved; P06-006 adds its editor surface separately instead of weakening the existing workspace-search contract.

## Automated validation

Permanent validation is `tools/ui/validate-editor-lifecycle.ps1`. It verifies the lifecycle/source/UI boundary, safe-file-service usage, dirty/conflict guards, large/binary inspection, the application-shutdown dirty-buffer guard, no direct write/process/network bypass, and destructive negative fixtures under the exact Windows/.NET 10.0.400 baseline.

The gate executes both focused unit coverage in `ProjectEditorWorkspaceTests` and real composition coverage in `ProjectEditorWorkspaceIntegrationTests`. The real integration fixture uses `FileSystemProjectFileService` against an actual Unicode/space-containing project path and proves UTF-16BE preservation, LF round-trip integrity, optimistic external-change conflict refusal, dirty-buffer retention, and explicit reload recovery.

The canonical `.github/workflows/windows-ci.yml` registers `Validate P06-006 editor lifecycle`, and `tools/ci/validate-windows-ci.ps1` fails closed if that permanent gate is removed. Inherited P06-007 and P06-008 dedicated workflows remain mandatory non-regression gates for the shared Projects workspace.

Exact PR-head and post-merge run IDs are intentionally not preclaimed here; they must be recorded only after GitHub reports terminal SUCCESS on the exact candidate/main SHA.

## Owner-last boundary

P06-006 is fully cloud-actionable and introduces no new owner-machine/manual/provider requirement. `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` remains unchanged; existing `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain the only queued owner obligations. No P06 closure, P07 authorization, phase-exit PASS, release eligibility, or `VERIFIED_FINAL_COMPLETE` is claimed by this task evidence.
