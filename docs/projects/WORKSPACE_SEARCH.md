# Workspace Search

`FCCD-P06-007 — Workspace content/file/regex search` adds a read-only, cancellable search surface to the active project workspace.

## User-visible behavior

The workspace exposes filename, literal-content, and line-based regular-expression modes. Search supports case-sensitive or case-insensitive matching, Unicode paths/content, one-based line/column locations for content matches, bounded previews, keyboard Enter to run, and Escape/Cancel to stop active work.

Search results are virtualized in the WPF surface. Changing the active project cancels any in-flight search and clears stale results so matches from one workspace cannot leak into another workspace view.

## Responsiveness and bounds

Search runs on a background worker rather than enumerating or reading files on the WPF dispatcher thread. Every operation accepts cancellation and checks it during directory traversal and line scanning.

Default per-search bounds are:

- `500` matches;
- `20,000` examined files;
- `4 MiB per content-searched file`;
- `100` matches from one file;
- `64` directory levels below the project root.

P06-008 centralizes these defaults in `WorkspaceScalePolicy`. The search request may lower an operational bound, but `FileSystemProjectSearchService` rejects a request above the active injected policy instead of silently widening the scan. Result metadata records total-result, file-count, file-size, per-file-match, traversal-depth, preview, and binary-probe bounds. Reaching a traversal or match budget is reported as a truthful partial result.

The broader P06-008 policy also supplies the case-insensitive generated/vendor directory exclusions, binary probe size, and preview size used by search, keeping tree/file/search scale decisions consistent while preserving P06-007 search semantics.

## Filesystem safety

Workspace search is read-only. It does not call file-write/delete APIs, launch processes, or mutate project metadata. Traversal stays inside the selected root, does not follow reparse-point entries, skips common generated directories by default, rejects oversized content candidates before reading them, skips binary-looking data, uses strict UTF-8 fallback with BOM-based Unicode detection, and bounds regular-expression evaluation with a timeout.

Content and regex modes operate line by line. Regex matches intentionally do not span newline boundaries.

## Validation

Permanent validation is `tools/projects/validate-workspace-search.ps1`. It checks background/cancellation/reparse/bounds safeguards, WPF search composition, virtualized results, explicit cancel behavior, shared-policy enforcement, production integration tests and destructive negative fixtures.

The integration fixture covers literal content, file-name matching, regex matching and syntax rejection, Unicode/spaces, BOM text, generated-directory exclusion, binary/oversized-file skipping, result/file/per-file/depth caps, policy-overrun rejection, cancellation, missing roots and invalid bounds.

This task has no owner-only evidence, provider/FCC requirement, Unity/Blender requirement, clean-machine requirement or manual target acceptance. Its evidence class is cloud/self-test and `FINAL_OWNER_ACCEPTANCE_QUEUE` is unchanged.
