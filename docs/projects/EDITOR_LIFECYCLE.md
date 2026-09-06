# Editor Tabs, Save, Reload, and Dirty-State Lifecycle

`FCCD-P06-006` composes the P06-004 safe file service with the P06-005 native editor. It does not introduce a second filesystem path and does not bypass the large-workspace safeguards integrated by P06-008.

## Tab model

Selecting a normal text file in the project explorer opens one editor tab for that project-root/file pair. Selecting the same file again activates the existing tab rather than duplicating buffers. Each tab retains its original project root, normalized full path, relative path, encoding, newline policy, observed optimistic version token, and language label.

Changing the active project does not silently retarget existing tabs. A tab always saves back through the project root it was opened from, so a later project switch cannot redirect an unsaved buffer into another workspace.

## Safe open behavior

Before normal text materialization, `ProjectEditorWorkspace` calls `IProjectFileService.InspectAsync`. Binary and oversized files are refused before `ReadTextAsync`; reparse/root-containment and encoding policy remain owned by the file service. Opening a tab is read-only and does not mutate source content.

## Dirty state

A document becomes dirty when its editor buffer differs from the last loaded or successfully saved buffer. The tab label displays `*` while dirty. Dirty tabs are never silently discarded:

- reload requires explicit discard confirmation;
- close requires explicit discard confirmation;
- application shutdown is cancelled by default while dirty editor tabs exist unless the user explicitly confirms discarding them;
- changing the active project leaves existing tabs attached to their original roots;
- failed save/reload operations retain the current buffer.

## Save and conflict semantics

Save uses only `IProjectFileService.WriteTextAsync`. The request carries the tab's original encoding and the exact optimistic version token observed on load or the last successful save. If another process changes, deletes, or replaces the file, the safe file service fails closed with `ProjectFileConflictException`; the editor keeps the dirty buffer, marks the tab conflicted, and instructs the user to reload or reconcile the external change.

For files with a stable CRLF, LF, or CR newline style, source text is normalized into the WPF editor representation without creating false dirty state and is normalized back to that established source style before the safe write. Mixed-newline content is not silently rewritten by the lifecycle layer. The file service remains responsible for strict encoding, root containment, size ceilings, version verification, atomic replacement, and non-following of unsafe reparse paths.

## Reload and recovery

Reload re-runs the P06-008 inspection boundary before materialization. A file that became binary or too large is refused, and the existing buffer remains intact. A successful reload replaces text/encoding/newline/version metadata and clears dirty/conflict state. Cancellation and later retry are independent operations.

## UI composition

`ProjectWorkspaceSurface` preserves the canonical P06-007 search composition seam and adds a `ProjectEditorSurface` next to the lazy file explorer and workspace search. The editor surface uses the native `CodeEditorControl`, exposes Save/Reload/Close actions, displays multiple tabs, and presents inline lifecycle status/error information. Destructive dirty-buffer actions require an explicit Yes/No confirmation, and `MainWindow` applies the same fail-safe boundary when the application is closing.

## Validation

Focused unit tests cover tab reuse, version-aware save, conflict retention, reload/close dirty guards, large/binary refusal, project-root pinning, editor newline normalization, and source newline preservation. A real integration fixture composes `ProjectEditorWorkspace` with `FileSystemProjectFileService` against a Unicode/space-containing path and proves UTF-16BE preservation, LF round-trip integrity, external-change refusal, and explicit reload recovery. The permanent P06-006 validator also rejects removal of the application-shutdown dirty-buffer guard.

## Ownership and acceptance boundary

This task owns editor tabs, file loading/saving, reload, dirty-state and external-conflict UX. It relies on P06-004 for safe writes, P06-005 for editor rendering, P06-007 for search, and P06-008 for large/binary/tree limits.

All requirements are cloud-actionable. `FCCD-P06-006` adds no new owner-machine/manual/provider obligation and does not alter `FINAL_OWNER_ACCEPTANCE_QUEUE`. Existing `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain separate release blockers. P06/P07 phase advancement is not claimed by this document.
