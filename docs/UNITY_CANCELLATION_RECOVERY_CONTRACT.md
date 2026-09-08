# Unity Cancellation / Recovery Contract

## Purpose

`FCCD-P10-012` defines fail-closed recovery semantics for an interrupted Unity automation operation without creating a second process supervisor. Runtime process-tree ownership remains the responsibility of the P08 Windows Job Object supervisor; same-project serialization remains the responsibility of the P09/P10 resource-lock layer.

## Durable checkpoint

A recovery checkpoint stores only:

- Unity operation ID;
- project ID;
- process-run ID;
- running vs cancellation-requested state;
- UTC start/update timestamps.

It deliberately does **not** persist executable paths, project paths, arguments, environment variables, stdout, stderr, prompts, tokens, credentials, or provider payloads.

## Recovery invariants

1. A running owned process retains its same-project lease. No retry may overlap it.
2. Cancellation may target a process only when its durable process-run identity matches the checkpoint. There is no kill-by-name behavior.
3. Foreign or mismatched processes are explicitly preserved and recovery blocks for ownership investigation.
4. Cancellation waits for owned process-tree exit before the project lease may be released.
5. A missing/stale/mismatched terminal artifact never proves success.
6. After the owned process is gone and its lease is released, a retry uses a fresh invocation/correlation identity.
7. Fresh correlated terminal evidence is reconciled rather than executing the operation a second time.
8. Unknown lease disposition blocks retry because same-project overlap cannot be ruled out.
9. Operation/project identity mismatch and time-regressing observations fail closed.

## Cloud acceptance

`tools/unity/validate-unity-recovery.ps1` runs on hosted Windows with .NET SDK `10.0.400`, locked restore, Release warnings-as-errors, static process-control checks, and a deterministic fixture covering owned cancellation, foreign-process preservation, stale evidence rejection, lease handling, terminal reconciliation, and fresh retry semantics.

This contract does not fabricate a successful real Unity provider/editor run. It verifies product-owned cancellation/recovery mechanics and their interaction boundary with already-integrated P08/P09 process ownership and resource locking.
