# P07-009 — Dirty / pre-existing change provenance

## Scope

`FCCD-P07-009` establishes a read-only provenance boundary for repository changes that were already dirty before an autonomous operation began. The purpose is to prevent later Git automation from silently treating owner work as agent-owned simply because both touched the same repository.

The feature is exposed through Application-owned `IGitChangeProvenanceService` and implemented by `GitChangeProvenanceService` on top of the existing read-only `IGitService` status contract.

## Provenance model

The service has two explicit operations:

1. `CaptureBaselineAsync(path)` records the repository root, capture time, and every dirty status entry visible at the baseline, including staged/work-tree state, conflicts, and rename source/target aliases.
2. `CompareAsync(path, baseline)` queries the current dirty state and classifies each current path as either:
   - `PreExistingDirty` — the current path or rename alias intersects a dirty baseline path; or
   - `CreatedSinceBaseline` — no baseline dirty path or alias intersects it.

Baseline entries that no longer intersect current dirty status are returned separately as `ResolvedPreExistingChanges`.

## Conservative ownership rule

This service deliberately performs **path-lineage provenance**, not byte-level actor attribution.

If a path was dirty at baseline, that path remains owner-sensitive during comparison even when additional edits occur later. The service does not claim that later bytes inside an overlapping file belong to the agent. P07-010 destructive-operation safeguards must therefore treat `PreExistingDirty` overlap as protected owner work unless a stronger, separately verified ownership mechanism exists.

Rename/copy lineage is conservative as well: both `Path` and `OriginalPath` are baseline aliases. If a staged rename later appears as a deleted source plus untracked destination after index changes, both sides continue to intersect the pre-existing baseline rather than being misclassified as newly agent-created work.

## Safety and boundedness

- The implementation performs no Git mutation. It delegates only to the existing read-only `IGitService.GetStatusAsync` contract.
- It does not run `reset`, `restore`, `checkout`, `switch`, `clean`, `add`, `commit`, `push`, fetch/pull, ref mutation, config mutation, or shell commands.
- Repository roots must match between baseline and comparison; cross-repository baselines fail with `BaselineRepositoryMismatch`.
- Dirty-path materialization is bounded (`4096` default, `65536` hard maximum). Exceeding the configured limit fails closed with `TooManyChanges`; entries are never silently dropped.
- Caller cancellation propagates before or during the underlying bounded status query.
- Repository-relative paths are normalized to `/` for stable matching. Repository-root comparison is case-insensitive on Windows and ordinal on case-sensitive platforms.

## Cloud validation

The real disposable-Git suite covers:

- a clean baseline followed by a Unicode/Arabic path classified as `CreatedSinceBaseline`;
- an owner-modified path that remains `PreExistingDirty` after additional edits, while a new path is classified separately;
- resolved pre-existing dirty work;
- rename alias continuity when Git status shape changes from a staged rename to deleted-source + untracked-target;
- cross-repository baseline rejection;
- typed non-repository, bare-repository, and Git-unavailable outcomes;
- fail-closed dirty-path bounds;
- caller cancellation and constructor bound validation;
- read-only preservation of owner file bytes during capture/comparison.

The permanent Windows Release CI remains authoritative.

## Non-claims

This task does not implement destructive-operation policy, conflict workflow closure, P07 phase closure, P08/P11/P12 authorization, owner-only target evidence, or release readiness. It supplies the provenance input needed by later P07 safeguards while preserving `VERIFIED_FINAL_COMPLETE=false`.
