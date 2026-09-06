# P07-008 — Git history

## Scope

`FCCD-P07-008` adds a bounded, read-only local Git history surface. It deliberately does not mutate refs, the index, the work tree, repository configuration, or remotes, and it does not implement dirty-change provenance, destructive-operation safeguards, or conflict-scenario closure from P07-009+.

## Contract

`IGitHistoryService` exposes `GetHistoryAsync(path, query)` with a typed `GitHistoryQuery` and `GitHistoryResult`.

The result contains bounded structured commit records with:

- full and abbreviated object IDs;
- parent object IDs;
- author name/email/date;
- commit subject;
- a stable exclusive continuation cursor when more history exists.

Typed query outcomes distinguish success, empty repository, non-repository, Git unavailable, invalid query/cursor, bounded-output overflow, and generic query failure.

## Safety and bounds

The CLI adapter:

- delegates repository detection to the existing read-only `IGitService`;
- uses `ProcessStartInfo.ArgumentList`, never a shell command string;
- uses only local read-only `rev-parse` and `git log` operations;
- disables interactive prompts, optional locks, and pagers;
- decodes stdout/stderr explicitly as UTF-8;
- bounds commit count to 100 records per page;
- bounds repository-relative path filters and rejects rooted/traversal/`.git` metadata segments;
- uses Git literal pathspec magic so wildcard-like file names are never expanded;
- validates continuation cursors as full SHA-1/SHA-256 object IDs and verifies they resolve to commits;
- bounds materialized stdout and returns `TooLarge` rather than retaining unbounded history text;
- applies a bounded timeout/caller cancellation and terminates only its owned process tree.

Bare repositories are valid read-only history sources. An unborn/empty repository returns the typed `EmptyRepository` state rather than being treated as a failure.

## Cloud validation

Real disposable-Git tests cover:

- newest-first history ordering, parent linkage, author metadata and Unicode commit subjects;
- stable bounded pagination using the last visible commit as an exclusive continuation cursor;
- literal path filtering that does not over-match wildcard-like names;
- bare-repository history;
- empty/non-repository/Git-unavailable states;
- invalid bounds, traversal/metadata path filters, malformed and unknown cursors;
- bounded-output overflow;
- preservation of dirty work-tree bytes and raw index bytes;
- caller cancellation and constructor timeout/output safety limits.

The permanent Windows Release CI remains the authoritative integration gate. No owner-only acceptance item is required for P07-008 because the complete behavior is reproducible with disposable local Git repositories in cloud Windows CI.

## Non-claims

This task does not implement or close P07-009 dirty/pre-existing-change provenance, P07-010 destructive-operation safeguards, P07-011 conflict integration scenarios, the P07 exit gate, P08/P11 authorization, either existing owner-last obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE=true`.
