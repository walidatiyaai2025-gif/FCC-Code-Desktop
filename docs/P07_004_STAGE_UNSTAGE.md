# P07-004 — Stage / unstage contract

`FCCD-P07-004` owns explicit Git index mutation only. It does not own branch changes, fetch/pull, commit/push, history, destructive reset/clean operations, or phase closure.

## Contract

- Application contract: `IGitIndexService`.
- Production adapter: `GitCliIndexService`.
- Operations: `StageAsync` and `UnstageAsync` over explicit repository-relative path collections.
- Result states are typed: success, non-repository, bare repository, Git unavailable, or query/mutation failure.
- Requested paths and effective paths are returned separately so rename-pair expansion is visible to callers.

## Safety invariants

- No broad `git add -A`, `git add .`, wildcard pathspec, shell command string, or work-tree reset/checkout is used.
- Every mutation path is normalized, repository-relative, literal, and rejects empty/current/parent segments plus `.git` metadata targeting.
- A request is bounded to 64 explicit paths and 12 KiB of requested path text; rename expansion is separately bounded.
- Stage uses `git add -- :(literal)<path>` only for the explicit effective set.
- Unstage with an existing `HEAD` uses `git restore --staged -- :(literal)<path>` and therefore changes the index only.
- Unstage in an unborn repository uses `git rm --cached --force --ignore-unmatch -- ...`; `--cached` intentionally preserves work-tree files.
- Rename status entries expand to both current and original paths so a selected rename is staged/unstaged atomically instead of leaving a half-staged delete/add pair.
- Git execution is non-interactive, UTF-8 decoded, timeout-bounded, cancellation-aware, and kills the owned process tree on cancellation/timeout.
- Failure stderr returned to the UI contract is trimmed and bounded.

## Cloud verification

Real disposable Git tests cover selective staging, unrelated owner-change preservation, modified-file unstage, deletion stage/unstage without recreation, rename-pair handling, unborn-repository unstage, Arabic/Unicode/space-containing paths, typed repository failure states, pathset safety limits, cancellation, and constructor timeout bounds.

The initial exact-head Release build surfaced only analyzer `CA1859` on two private helpers returning an interface while constructing `List<string>` values. The repair narrows those private return types to `List<string>` without changing mutation behavior; permanent CI must still pass on the resulting user-authored exact head.

This task adds no P07-005+ behavior and creates no owner-only acceptance requirement.
