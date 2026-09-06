# P06 Lazy File Explorer Contract

## Scope

`FCCD-P06-003` provides the read-only project tree used to browse an active workspace without eagerly walking the repository.

The explorer is intentionally narrower than the safe file mutation service in `FCCD-P06-004`: it enumerates file-system metadata only. It does not open file contents, save files, delete paths, rename paths, launch tools, or mutate the selected source tree.

## Lazy loading invariant

Opening a project creates one root node only. A directory is enumerated only when that specific node is expanded. Expanding one directory never recursively enumerates its descendants.

Directory enumeration runs away from the WPF dispatcher through the project-owned `IProjectFileExplorerService` abstraction. Results return to presentation state only after that one directory listing completes.

This behavior is required for large repositories: tree size must not translate into an eager startup scan or main-thread file-system walk.

## Bounded materialization

`FileSystemProjectFileExplorerService` limits one directory listing to `2048` entries by default. The supported implementation ceiling is `20000` entries per directory.

The implementation materializes at most one entry beyond the configured cap to determine whether truncation occurred. When the cap is reached, the UI reports that only the bounded prefix is being shown instead of silently pretending the directory is complete.

`FCCD-P06-008` remains responsible for broader large-tree protections and acceptance thresholds. P06-003 establishes the mandatory lazy and bounded enumeration primitive that later safeguards build upon.

## Workspace containment

Every requested directory is normalized and must remain lexically inside the active project root. Parent traversal and rooted paths that resolve outside the project are rejected before enumeration.

Non-ASCII names and paths containing spaces are supported through normal .NET path APIs; no shell command concatenation is used.

P06-008 centralizes the entry and traversal-depth limits. Generated/vendor directories and directories at the configured maximum depth remain visible with typed restriction metadata, but cannot be expanded. This preserves orientation while preventing an accidental deep or generated-tree walk.

## Reparse-point policy

Reparse-point entries may be displayed so the owner can see that they exist, but directories marked with `FileAttributes.ReparsePoint` are not expandable and are never traversed by the explorer service. This prevents a project tree from silently escaping its root through junctions or symbolic links.

## Failure and cancellation behavior

Missing directories fail explicitly. Access-denied and I/O failures are converted into actionable directory-specific messages. Per-entry attribute failures are skipped and counted rather than crashing the whole listing.

Cancellation is checked before work begins and throughout entry processing. The presentation state renders a local status/error child and requires an explicit tree refresh before retrying a failed or cancelled expansion.

## UI behavior

The Projects surface includes:

- a `Files` region for the active project;
- a recycling virtualized `TreeView`;
- an explicit `Refresh tree` action;
- directory expansion as the only trigger for child enumeration;
- inline loading, empty, bounded-result and error states;
- the existing Recent Projects empty state and project workflow controls unchanged.

The explorer is read-only in P06-003. Content opening/editing belongs to the later P06 file/editor tasks.

## Verification

Permanent validation is provided by `tools/projects/validate-lazy-file-explorer.ps1` and Windows CI. It verifies the static safety/wiring contract, negative fixtures, and executable integration tests covering:

- immediate-child-only enumeration;
- deterministic directories-first ordering;
- Unicode and space-containing paths;
- source-content non-mutation;
- project-root containment;
- missing-path failure;
- cancellation;
- bounded per-directory materialization.

This task requires no FCC/provider/manual target evidence. Its closure evidence is cloud/self-test and exact-head Windows CI evidence only; existing owner-last obligations for earlier phases remain unchanged.
