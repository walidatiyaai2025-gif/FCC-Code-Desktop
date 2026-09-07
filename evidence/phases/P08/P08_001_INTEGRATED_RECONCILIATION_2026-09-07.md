# P08-001 Integrated Reconciliation — 2026-09-07

## Classification

- Task: `FCCD-P08-001 — Process supervisor with owned process-tree tracking`
- Phase: `P08 — Terminal/process supervision`
- Final task state after this reconciliation: `CLOSED`
- Evidence class: cloud / Windows CI + canonical integration provenance
- Owner-only evidence required for this task: none

## Implementation

P08 was activated by PR #188, normal merge `36cd7984c87e3ef9e627d0bf424b414f2237f374`.

PR #189 implemented Runtime-owned `IProcessSupervisor` / `ISupervisedProcess` contracts, one private Windows Job Object per launched tree with `KILL_ON_JOB_CLOSE`, active-process ownership snapshots, full owned-tree completion semantics, bounded non-shell launch arguments/environment, and an owned-handle-only forced-tree termination primitive. Real Windows fixtures cover descendant cleanup and preservation of an unrelated unowned sentinel.

Exact implementation candidate: `5915ce7f21d8b487346acf7334b34bd4523a215a`.

PR #189 exact-head permanent gates:
- Windows CI `34072739503` / #438 — SUCCESS
- P06-007 Workspace Search `34072739496` / #167 — SUCCESS
- P06-008 Large Workspace Safeguards `34072739498` / #151 — SUCCESS

PR #189 normally merged as `d0df56e60ec62e05db793184c5bc0d53b7c65d9b`.

## Post-merge regression and repair

Exact-main repeated Windows validation exposed a real lifecycle race: `ISupervisedProcess.Completion` could publish before the supervisor removed the owned process from its active registry. Workspace Search `34073251587` / #168 failed. This was treated as a cloud-repairable product/test defect; P08-001 was not reconciled CLOSED at that point.

PR #190 repaired `ObserveTreeExitAsync` so the owned entry is removed from `_active` before successful or failed completion is published. The forced-tree fixture also moved the controlled process CWD to `Environment.SystemDirectory` while retaining descendant-PID evidence in the disposable fixture directory, removing irrelevant Windows directory-handle coupling without weakening descendant-termination or unowned-sentinel assertions.

Exact repair candidate: `e3d6ecdc14f01be5460ca1656d6f6ba2b6535460`.

PR #190 exact-head permanent gates:
- Windows CI `34074218833` / #446 — SUCCESS
- P06-007 Workspace Search `34074218827` / #175 — SUCCESS
- P06-008 Large Workspace Safeguards `34074218830` / #159 — SUCCESS

PR #190 normally merged as `ac54e739019e7264db5de3f9b26b700735924bc1`.

Exact accepted-main permanent gates:
- Windows CI `34074668199` / #447 — SUCCESS
- P06-007 Workspace Search `34074668196` / #176 — SUCCESS
- P06-008 Large Workspace Safeguards `34074668191` / #160 — SUCCESS

## Reconciliation boundary

This evidence closes only `FCCD-P08-001`. P08 remains `IN_PROGRESS`, `PHASE_EXIT_GATE=NOT_RUN`, and `FCCD-P08-002` through `FCCD-P08-008` remain PENDING. P09 and later implementation remain prohibited until P08 truthfully closes.

The canonical final-owner queue is unchanged: `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain `QUEUED`, release-blocking obligations. No real-target/manual evidence is fabricated or reclassified, and `VERIFIED_FINAL_COMPLETE=false` remains mandatory.

Reconciliation was applied by guarded run `34075323776`, which required exact base `ac54e739019e7264db5de3f9b26b700735924bc1`, verified the four-file durable scope, and removed its temporary orchestration before producing the reconciliation candidate.
