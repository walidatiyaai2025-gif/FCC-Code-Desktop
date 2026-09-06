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
- `4 MiB per content-searched file`.

The service rejects caller limits above its production safety ceilings instead of allowing an accidental unbounded scan. Reaching a file or result cap is reported in the result set and surfaced to the user.

P06-008 centralizes these defaults and adds a `64`-level traversal-depth cap, a `100`-match per-file cap, a bounded per-directory materialization cap, and typed limit reasons. A depth-limited, unusually wide, or match-heavy workspace therefore returns an explicit partial result instead of silently consuming unbounded time or allowing one pathological file to monopolize the result payload. Directory entries and resulting matches are ordered deterministically within every bounded listing.

`FCCD-P06-008` still owns the broader large-file/tree policy for the full workspace. The P06-007 limits are search-specific safety invariants and do not close or replace P06-008.

## Filesystem safety

Workspace search is read-only. It does not call file-write/delete APIs, launch processes, or mutate project metadata. Traversal stays inside the selected root, does not follow reparse-point entries, skips common generated directories by default, rejects oversized content candidates before reading them, skips binary-looking data, uses strict UTF-8 fallback with BOM-based Unicode detection, and bounds regular-expression evaluation with a timeout.

Content and regex modes operate line by line. Regex matches intentionally do not span newline boundaries.

## Validation

Permanent validation is `tools/projects/validate-workspace-search.ps1`. It checks background/cancellation/reparse/bounds safeguards, WPF search composition, virtualized results, explicit cancel behavior, production integration tests and destructive negative fixtures.

The integration fixture covers literal content, file-name matching, regex matching and syntax rejection, Unicode/spaces, BOM text, generated-directory exclusion, binary/oversized-file skipping, result/file caps, cancellation, missing roots and invalid bounds.

This task has no owner-only evidence, provider/FCC requirement, Unity/Blender requirement, clean-machine requirement or manual target acceptance. Its evidence class is cloud/self-test and `FINAL_OWNER_ACCEPTANCE_QUEUE` is unchanged.
