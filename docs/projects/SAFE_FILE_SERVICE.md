# Safe File Service

`FCCD-P06-004` establishes the bounded project-owned text file read/write boundary used by later editor features. The task deliberately stops below editor tabs, dirty-state UX, content rendering, rename/delete, and search behavior.

## Ownership and layering

- `FCCCodeDesktop.Application` owns `IProjectFileService` plus encoding, newline, version, snapshot, write-request, write-result, and conflict contracts.
- `FCCCodeDesktop.Files` owns the concrete filesystem adapter.
- The active project root is the trust anchor. Callers may supply either a path relative to that root or a fully qualified path, but the normalized target must remain inside the project.
- Nested reparse-point directories are not traversed, and reparse-point files are rejected. The service does not use a reparse point below the project root to escape project ownership.

## Bounded text reads

Reads are asynchronous and cancellable. `FileSystemProjectFileService` limits materialized file content to `8 MiB` by default and enforces a supported ceiling of `128 MiB`; the default can be lowered for focused callers/tests. P06-008 still owns broader large-file/tree product safeguards, so this task does not claim that later task complete.

Text decoding is fail-closed:

- UTF-8 without BOM;
- UTF-8 with BOM;
- UTF-16 little-endian when identified by BOM;
- UTF-16 big-endian when identified by BOM.

Invalid UTF-8 or non-BOM legacy encodings are rejected instead of guessed. This avoids silently converting bytes with an assumed system code page. A read returns the exact decoded text plus encoding metadata, detected newline style (`CRLF`, `LF`, `CR`, mixed, or none), whether the text ends in a newline, normalized/relative paths, and an optimistic version token.

## Version-aware safe writes

A file version contains byte length, last-write UTC ticks, and SHA-256 of the bytes observed by the service. Existing files are never overwritten without the caller supplying that observed version. If the file changed, disappeared, or no longer matches the supplied version, `ProjectFileConflictException` is raised rather than overwriting external work.

The service checks the expected version before preparing a save and checks it again immediately before commit. This is optimistic conflict detection; it is intentionally fail-closed for normal concurrent editor/process changes without claiming a cross-process filesystem transaction lock.

New files may be created without an expected version only when the target does not already exist. If another process creates the target before commit, the save fails as a conflict.

## Atomic commit boundary

Writes are encoded explicitly with the requested supported encoding and bounded before touching the target. Content is written to a unique `.fccd-*.tmp` file in the target directory with asynchronous write-through I/O, flushed, then moved over the target in the same directory. Temporary files are cleaned up on failure/cancellation on a best-effort basis.

Writing the exact text supplied by the caller means newline sequences are not normalized implicitly. A later editor can preserve the read snapshot's newline convention by retaining the text/newline semantics it received; this service does not rewrite line endings behind the caller's back.

## Explicit non-scope

P06-004 does not implement file rename/delete, recursive mutation, directory creation/deletion, editor tabs, dirty/reload UX, source-control operations, workspace search, or UI-thread file access. It does not shell out or start processes.

## Evidence boundary

Validation for this task is cloud/self-test evidence only: static/negative contract fixtures plus executable Windows/.NET integration tests. P06-004 introduces no FCC/provider/manual/owner `REAL_TARGET` requirement and does not alter the existing owner-last queue or claim P06 phase closure.
