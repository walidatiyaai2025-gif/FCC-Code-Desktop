# P06-001 — Project Workspace Workflows

## Scope

`FCCD-P06-001` owns the production add/open/recent-project workflow for the existing local workspace model. It does not own technology detection, file-tree enumeration, file mutation, editor behavior, search, or Git operations; those remain P06-002 through P07.

## Durable project identity

Opening an existing folder is also the add-to-workspace operation:

1. normalize the selected path with the platform path API;
2. require the folder to exist;
3. derive a display name from the directory itself;
4. look up an existing project by canonical root path using the existing case-insensitive SQLite project identity;
5. create a new project only when that root has never been persisted;
6. otherwise preserve the existing project ID and `CreatedUtc` while refreshing mutable display metadata and `UpdatedUtc`;
7. list recent projects by most-recent `UpdatedUtc` first;
8. activate the persisted project in the existing session workspace.

The source tree is never copied, moved, enumerated, or modified by this workflow. Git is not a prerequisite: both Git and non-Git folders are valid project roots.

## Architecture

- `ProjectCatalogService` in `FCCCodeDesktop.Application` owns the use case.
- `IProjectCatalogStore` isolates durable catalog persistence.
- `IProjectDirectoryProbe` isolates operating-system path/directory behavior from orchestration.
- `SqliteProjectCatalogStore` uses the existing `Projects` table and existing case-insensitive unique root-path index; no schema migration is required.
- `SystemProjectDirectoryProbe` provides the concrete filesystem metadata probe without enumerating source content.
- `ProjectWorkspaceState` owns WPF presentation state and coordinates the already-existing `SessionWorkspaceState` after catalog persistence succeeds.
- `ProjectWorkspaceSurface` is a thin UI adapter for the Windows folder picker, recent-project selection, refresh, loading/disabled state, and inline actionable failures.

## Safety and error behavior

- blank paths are rejected;
- missing folders are rejected before any project metadata is written;
- repeated opens of the same root reuse durable identity instead of producing duplicates;
- a stale recent-project entry remains visible, but reopening it fails explicitly if the folder no longer exists;
- opening a project performs no source-file write and does not require `.git`;
- UI actions are disabled while another project operation is active;
- project errors are retained in presentation state for inline display rather than silently swallowed.

## Verification

`tools/projects/validate-project-workflows.ps1` statically enforces the production wiring and negative fixtures, then on Windows/.NET SDK `10.0.400` runs the focused `ProjectCatalogServiceTests` integration suite. The tests cover:

- persistence and recreation;
- reopen identity/creation-time preservation;
- deterministic recent-project order;
- spaces and non-ASCII paths;
- Git and non-Git folders;
- source-file non-mutation;
- missing-folder rejection;
- bounded recent-project queries.

This evidence is cloud/deterministic implementation evidence only. It does not alter or satisfy the queued P04/P05 owner-only acceptance obligations and does not claim the P06 phase exit gate.
