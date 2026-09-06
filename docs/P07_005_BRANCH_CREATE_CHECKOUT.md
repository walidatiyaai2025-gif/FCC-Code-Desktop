# P07-005 — Branch create/checkout

## Scope

`FCCD-P07-005` adds bounded **local** Git branch creation and checkout behind an Application-owned mutation contract. It deliberately does not add fetch, pull, commit, push, history, reset, clean, forced checkout, or any remote/network behavior.

## Contract

`IGitBranchService` exposes two explicit operations:

- `CreateAndCheckoutAsync(path, branchName)` creates a new local branch from the current HEAD/unborn branch position and switches to it.
- `CheckoutAsync(path, branchName)` switches only to an already-existing local branch.

Both return `GitBranchMutationResult`, including operation kind, typed status, requested branch, repository root, previous/current branch names when known, and bounded Git failure text.

Typed outcomes distinguish success, non-repository, bare repository, Git unavailable, invalid branch name, missing branch, existing branch, Git-refused checkout, and other query failure.

## Safety boundary

The implementation uses `ProcessStartInfo.ArgumentList`; branch names are never interpolated into a shell command. Names are bounded before Git invocation and validated by `git check-ref-format --branch` before any mutation.

Branch mutation uses only:

- `git switch --create <branch>` for create + checkout;
- `git switch <branch>` for checkout.

It never supplies `--force`, `--discard-changes`, `reset`, `clean`, or any equivalent destructive option. If Git refuses a switch because owner changes would be overwritten, the service returns `CheckoutBlocked` and leaves Git responsible for preserving the current branch/index/work tree. No fallback attempts to discard or rewrite owner work.

Repository detection remains delegated to the read-only `IGitService`; branch writes are isolated behind `IGitBranchService`, preserving the read/write boundary established by earlier P07 tasks.

Git execution is non-interactive, UTF-8, bounded by timeout/cancellation, and cleans up only its owned process tree. Remote commands are outside this service.

## Cloud validation

The real disposable-Git unit suite covers:

- create + checkout of a Unicode/Arabic hierarchical branch while preserving dirty owner bytes;
- checkout of an existing branch while carrying an unrelated safe dirty change;
- a conflicting dirty-tree checkout that Git rejects while the current branch and owner bytes remain unchanged;
- invalid, already-existing, and missing branch handling without hidden fallback mutation;
- typed non-repository, bare-repository, and Git-unavailable outcomes;
- caller cancellation before mutation;
- timeout-bound constructor validation.

The permanent Windows Release CI remains the authoritative integration gate. P07-005 introduces no owner-only acceptance item: its behavior is fully testable with disposable real Git repositories in cloud Windows CI.

## Non-claims

This task does not close P07, authorize P08/P11, implement fetch/pull or later Git tasks, satisfy either existing owner-last release blocker, or imply release readiness / `VERIFIED_FINAL_COMPLETE=true`.
