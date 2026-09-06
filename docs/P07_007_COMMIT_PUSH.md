# P07-007 — Commit/push

## Scope

`FCCD-P07-007` adds bounded local commit and non-force push operations behind a dedicated Application-owned mutation contract. It deliberately does not implement history browsing, dirty/pre-existing-change provenance, destructive-operation safeguards, force push, reset, clean, rebase, amend, branch deletion, or any P08/P11 work.

## Contract

`IGitCommitPushService` exposes:

- `CommitAsync(path, commitMessage)` — creates a commit from the **already staged index only**.
- `PushAsync(path, remoteName)` — pushes the current attached local branch to the same branch name on a configured remote.

Typed results distinguish success, repository/unavailable failures, detached HEAD, nothing staged, missing identity, invalid commit messages/remotes, missing remotes, rejected pushes, and generic query failures.

## Commit safety boundary

The service never stages files implicitly. It checks the staged index first and returns `NothingStaged` when no staged paths exist, leaving unstaged owner bytes untouched.

Commit execution uses an explicit message and disables GPG signing and repository commit hooks (`--no-gpg-sign`, `--no-verify`) so bounded desktop automation does not unexpectedly invoke an editor, signing agent, or repository-supplied executable. No amend/reset/clean/rebase operation is available from this service.

The service verifies a new `HEAD` commit was actually created and returns its SHA.

## Push safety boundary

Push requires an attached branch and an existing configured remote. It uses a single explicit refspec:

`HEAD:refs/heads/<current-branch>`

and never supplies force, force-with-lease, delete, mirror, prune, or history-rewrite options. Repository pre-push hooks are disabled with `--no-verify` to avoid implicit execution of repository-supplied programs.

A non-fast-forward or other Git refusal is returned as `PushRejected`. No fallback retries with destructive options are attempted.

## Cloud validation

Disposable real-Git tests cover:

- committing staged content while preserving unrelated unstaged owner bytes;
- refusing a commit when only unstaged changes exist;
- invalid commit-message rejection before mutation;
- publishing the current branch to a local bare remote;
- non-fast-forward push rejection while preserving both local and remote heads;
- missing-remote and detached-HEAD outcomes;
- non-repository, bare-repository, Git-unavailable, cancellation, and timeout bounds.

The bare local remote is intentionally used so Git push semantics are exercised without external networking, credentials, or fabricated provider evidence.

## Non-claims

This task does not close P07, authorize P08/P11, implement Git history, force/destructive operations, satisfy either owner-last release blocker, or imply release readiness / `VERIFIED_FINAL_COMPLETE=true`.
