# Large Workspace Safeguards

`FCCD-P06-008` centralizes the scale and safety boundaries used by project file, explorer, and search operations. The policy is owned by `FCCCodeDesktop.Application`; filesystem adapters consume it without moving I/O or traversal decisions into WPF.

## Policy contract

`WorkspaceScalePolicy` is the typed source of production defaults and supported ceilings. Construction fails closed when a limit or excluded directory name is invalid. Callers may lower limits for a specific operation, but cannot raise them above the policy ceiling.

The production defaults are:

- 2,048 materialized entries for one directory listing;
- 64 directory levels below the project root;
- 20,000 files examined by one traversal operation;
- 500 search results total and 100 matches from one file;
- 8 MiB for normal text-file materialization;
- 4 MiB for a content-search candidate;
- 240 characters for a search or text preview;
- a 4 KiB binary-content probe;
- generated/vendor directories such as `.git`, `bin`, `obj`, `node_modules`, `packages`, `dist`, `build`, Unity `Library`, `Temp`, and `Logs` excluded from recursive traversal.

Supported ceilings remain finite and are validated in one place. They permit focused tests and deliberate future configuration without allowing an unbounded allocation or scan.

## Tree behavior

The explorer remains lazy: it lists only the directory the user expands. Per-directory results are bounded, deterministically ordered, and report `LimitReached` when the listing is truncated. Generated/vendor and reparse-point directories may remain visible for orientation, but they are not traversable. Entries at the configured depth boundary are likewise visible but not expandable, with typed restriction metadata so presentation can remain truthful.

Enumeration and attribute work runs off the WPF dispatcher, observes cancellation, stays inside the canonical project root, and records inaccessible or unstable entries as skipped instead of failing the entire usable listing.

## File behavior

Normal text reads refuse files above the policy's materialization limit before allocating a full payload. A bounded inspection path classifies an input as text, binary, or too large and can return only a bounded preview. Inspection never writes, truncates, or changes timestamps intentionally. Strict UTF-8 and BOM-identified UTF-16 remain the accepted text encodings.

This seam is intended for editor/open UX without implementing P06-005 editor rendering or P06-006 tab/save/reload/dirty lifecycle.

## Search behavior

Workspace search consumes the same traversal exclusions and supported ceilings. It adds a maximum traversal depth and maximum matches per file so one pathological file cannot consume the entire result payload. File, total-result, per-file-match, content-size, preview, and binary-probe bounds are reported in typed result metadata. Reaching any configured bound produces a truthful partial/truncated result rather than a false complete state.

Regular-expression evaluation remains time-bounded and all traversal/read work remains cancellable and off the UI thread.

## Safety invariants

- Project-root containment is checked before traversal or file reads.
- Reparse-point files and directories are not followed.
- Generated/vendor directory exclusions are case-insensitive and deterministic.
- Binary and oversized inputs are classified without full-file allocation.
- Missing and inaccessible inputs fail or skip explicitly according to operation scope.
- No safeguard operation mutates searched, inspected, or enumerated project files.
- Cancellation and a later retry are independent; a failed or cancelled operation does not poison service state.

## Ownership boundary

This task does not implement or replace the locally bundled editor, editor tabs, save/reload/dirty conflict UX, or workspace-search presentation. It supplies reusable limits and hardens the filesystem services only. Existing P04/P05 owner-last queue obligations are unchanged.

## Verification

Permanent validation is provided by `tools/projects/validate-large-workspace-safeguards.ps1` and the Windows CI P06-008 gate. Automated coverage includes invalid policy construction, wide/deep trees, generated directories, total traversal and per-file result budgets, large/binary/empty files, bounded previews, Unicode/Arabic/space-containing paths, inaccessible and missing paths, cancellation/recovery, deterministic ordering/truncation, containment/reparse safety, non-mutation, and synthetic larger-workspace sanity.
