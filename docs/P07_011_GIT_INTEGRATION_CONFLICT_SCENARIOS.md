# P07-011 — Git integration tests and conflict scenarios

## Scope

`FCCD-P07-011` is the final P07 cloud acceptance fixture. It does not introduce a new production Git mutation primitive. Instead it composes the already integrated P07 services against disposable real Git repositories and bare remotes so the P07 exit criterion is exercised as a workflow rather than as isolated unit contracts.

## Acceptance matrix

The integration suite proves four cross-service scenarios:

1. **Clean standard workflow** — a clean client fast-forward pulls a remote change, stages one explicit path through `IGitIndexService`, commits only staged work through `IGitCommitPushService`, pushes without force, and finishes with the local and bare-remote branch heads equal and status clean.
2. **Dirty checkout refusal + provenance** — a pre-existing owner modification that would be overwritten by checkout causes `IGitBranchService` to return `CheckoutBlocked`; the branch and exact owner bytes remain unchanged and `IGitChangeProvenanceService` continues to classify the path as `PreExistingDirty`.
3. **Real merge conflict visibility** — a disposable fixture deliberately creates a genuine Git content conflict. `IGitService.GetStatusAsync` must report the conflicted path as `Unmerged`/`IsConflicted`, status inspection must not rewrite the conflict bytes, and `GitCommandSafetyPolicy` must continue to reject hard reset, clean, forced checkout, and discard-changes switch shapes.
4. **Diverged remote refusal** — local and remote histories diverge. Fast-forward pull returns `NonFastForward`; a subsequent push returns `PushRejected`; neither local nor remote head is silently moved and local bytes remain intact.

## Safety boundary

Raw Git commands in this file are fixture-construction operations inside disposable temporary repositories only. They create commits, remotes, and the intentional merge conflict needed to test the production surfaces. They are not new production command paths and do not weaken `GitCommandSafetyPolicy`.

The production behavior under test remains bounded by the contracts already integrated in P07-001 through P07-010:

- repository/status/diff reads are non-mutating;
- index mutation is explicit-path only;
- checkout never forces or discards owner changes;
- pull is clean-tree fast-forward only;
- push is non-force and rejects non-fast-forward updates;
- pre-existing dirty provenance remains distinguishable from later agent changes;
- destructive command shapes remain blocked.

The controlling invariant is that conflict/error handling must fail visibly and must not silently destroy owner work.

## Cloud validation

The authoritative validation is Windows Release CI on the exact candidate head, including the complete unit/integration suite and the permanent Workspace Search / Large Workspace companion gates. Because all scenarios use disposable local repositories and local bare remotes, no owner-only Windows/FCC/provider/Unity/Blender evidence is required for this task.

## Non-claims

Completing this task closes only `FCCD-P07-011` after normal integration and exact-main verification. It does not by itself mark the P07 phase exit gate `PASS`, authorize P08 or P12 implementation, resolve the queued P04/P05 owner obligations, or set `VERIFIED_FINAL_COMPLETE=true`. P07 phase closure must still be reconciled separately against the exact integrated candidate under canonical governance.
