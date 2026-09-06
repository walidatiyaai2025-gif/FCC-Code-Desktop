# P07-006 — Fetch/pull

## Scope

`FCCD-P07-006` adds bounded Git remote synchronization behind a dedicated Application-owned `IGitRemoteService`. It does not add commit, push, history, reset, clean, rebase, autostash, destructive checkout, or later-phase behavior.

## Contract

The service exposes two explicit operations:

- `FetchAsync(path, remoteName)` fetches a configured remote while leaving local `HEAD`, the index, and the work tree unchanged.
- `PullFastForwardAsync(path, remoteName, remoteBranchName)` performs a safety-first pull equivalent: validate repository/remote/branch, require attached `HEAD` and a clean index/work tree, fetch the requested remote branch, prove `HEAD` is an ancestor of `FETCH_HEAD`, then perform only `git merge --ff-only FETCH_HEAD`.

Typed outcomes distinguish success, repository/tool failures, unsafe/invalid targets, missing remotes, detached `HEAD`, dirty owner work, non-fast-forward divergence, remote failures, pull refusal, and verification failure.

## Safety boundary

Remote commands use `ProcessStartInfo.ArgumentList`, UTF-8 redirected streams, non-interactive credential behavior, bounded timeout/cancellation, and owned-process-tree cleanup.

Pull deliberately refuses instead of repairing or rewriting owner state when:

- the index or work tree is dirty;
- `HEAD` is detached;
- the local and fetched histories are not fast-forward compatible;
- branch/HEAD or work-tree state changes concurrently between fetch and merge;
- Git refuses the fast-forward merge.

There is no fallback to `reset`, `clean`, `checkout --force`, `rebase`, merge commits, autostash, or conflict auto-resolution. Fetch may update remote-tracking refs and `FETCH_HEAD`, but verifies that local `HEAD` did not move.

## Cloud validation

Disposable real-Git fixtures cover:

- fetch updating `origin/main` without changing local `HEAD` or work-tree bytes;
- clean fast-forward pull updating the branch to the fetched commit;
- dirty-tree refusal preserving owner bytes and local `HEAD`;
- diverged-history refusal preserving the local commit/work tree;
- missing/invalid remote and branch targets;
- detached-HEAD refusal;
- non-repository, bare-repository, Git-unavailable and cancellation paths;
- constructor timeout bounds.

The fixtures use a local bare remote, so no external network/provider evidence is required. Permanent Windows Release CI is the authoritative cloud gate.

## Non-claims

This task does not implement commit/push, history, dirty-change provenance, destructive-operation safeguards, P07 conflict closure, P07 phase closure, P08/P11 authorization, new owner-only acceptance, release readiness, or `VERIFIED_FINAL_COMPLETE=true`.
