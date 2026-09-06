# P07-010 — Destructive-operation safeguards

## Scope

`FCCD-P07-010` establishes a fail-closed command-shape policy at the Git process boundary used by the current P07 mutation adapters. The policy exists to prevent accidental work-tree destruction, ref deletion, force push, broad staging, history rewrite, or other unreviewed Git mutation from being introduced through a later code change while P07 is active.

## Runtime boundary

`GitCommandSafetyPolicy` is evaluated immediately before `Process.Start()` by the existing index, branch, remote-sync, and commit/push adapters. A command shape that is not explicitly recognized is rejected before Git is launched.

The allowlist is intentionally narrow and matches the production command shapes already proven by P07-004 through P07-007:

- explicit literal-path `git add -- ...` staging only;
- index-only `git restore --staged -- ...`;
- unborn-repository index-only `git rm --cached --force --ignore-unmatch -- ...`;
- non-forced `git switch` and `git switch --create`;
- bounded non-pruning/non-forced fetch;
- `git merge --ff-only --no-edit FETCH_HEAD` only;
- staged-index commit with the existing no-hook/no-signing invocation shape;
- non-force current-HEAD push to a named local branch ref;
- read-only repository/ref/status support commands required by those adapters.

The `--force` token in the unborn-repository `git rm` path is a deliberate contextual exception: it is accepted only when `--cached` is present in the exact index-only shape, so work-tree bytes are not removed. A non-cached `git rm --force` is rejected.

## Explicitly blocked classes

The policy rejects unknown commands by default, including destructive or history-rewriting families such as `reset`, `clean`, force/discard checkout, work-tree restore, non-cached remove, `add -A`/`--all`, forced/pruning fetch, non-fast-forward merge, amend/fixup-style commit shapes, force/delete/mirror push shapes, `pull`, `rebase`, `stash`, ref mutation, and branch deletion.

It also rejects unknown global `-c` overrides. Only the existing `commit.gpgSign=false` override is accepted, preventing a caller from smuggling behavior-changing Git configuration through the process boundary.

Blocked exceptions report only the safety rule identifier and never echo command arguments, so commit messages, paths, or other potentially sensitive argument text are not reflected into diagnostics by the guard itself.

## Validation

Cloud validation consists of:

- direct unit coverage for every currently permitted mutation/query shape;
- negative coverage for destructive, broad, forced, deleting, and history-rewrite shapes;
- a no-argument-echo negative fixture;
- the complete pre-existing real disposable-Git mutation suites, which prove the newly wired guard does not break valid P07 stage/unstage, branch, fetch/pull, commit, or push workflows;
- the permanent Windows Release / Workspace Search / Large Workspace gates on the exact candidate and exact merged main.

No owner-only target is needed for this task because the safeguard is deterministic and fully exercised with the repository's real-Git Windows fixtures.

## Non-claims

This task does not implement new destructive Git operations, does not weaken dirty-worktree/provenance protection, does not close P07 by itself, does not authorize P08/P12, does not alter either deferred owner acceptance item, and does not imply release readiness or `VERIFIED_FINAL_COMPLETE=true`.
