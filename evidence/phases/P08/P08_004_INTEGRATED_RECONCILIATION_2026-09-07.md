# P08-004 — Integrated reconciliation

**Task:** `FCCD-P08-004 — ConPTY terminal host`
**Canonical status:** `CLOSED`
**Current phase after reconciliation:** `P08 — Terminal/process supervision` / `IN_PROGRESS`
**Phase exit gate:** `NOT_RUN`

## Implementation

PR #196 implemented the Application-owned ConPTY contracts and production Windows host. The exact implementation candidate was `218cc7d93fafb690a5e2cf7192970bf6be0caa1f`.

The implementation uses native ConPTY creation/resize, `STARTUPINFOEX`, atomic `PROC_THREAD_ATTRIBUTE_JOB_LIST` assignment to a private `KILL_ON_JOB_CLOSE` Job Object, isolated standard handles, managed interactive input/output streams, deterministic completion/disposal, and owned process-tree cleanup without targeting unrelated processes. Shell selection remains outside P08-004; the hosted acceptance uses the exact resolved CMD profile contract.

Exact implementation-head gates completed SUCCESS:

- Windows CI #534 / run `34119217635`
- P06-007 Workspace Search #263 / run `34119217493`
- P06-008 Large Workspace Safeguards #247 / run `34119217492`
- P08-004 ConPTY Terminal Host #29 / run `34119217478`

The dedicated hosted-Windows ConPTY fixture proves negative/cancellation handling, a real CMD-profile launch, actual injected-input execution, independently proven output, live resize, clean exit/output EOF, owner-data preservation, dispose-time owned-descendant termination, and unrelated-process isolation.

PR #196 was normally merged as `da9caf9fb1542b7e080ddd768b77f73e653bb08b`.

## Canonical-main verification

On exact implementation-merge main `da9caf9fb1542b7e080ddd768b77f73e653bb08b`:

- P08-004 ConPTY Terminal Host #30 / run `34119961238` — SUCCESS
- P06-007 Workspace Search #264 / run `34119961241` — SUCCESS
- P06-008 Large Workspace Safeguards #248 / run `34119961210` — SUCCESS
- Windows CI #535 / run `34119961204` — CANCELLED after a later main push; it is not counted as PASS evidence.

A temporary no-content repository-history correction then left canonical main at `f86e8bc5e08b4def96fc50fc2cc3d3819621aa68`. GitHub compare reports that head as two commits ahead of `da9caf9f...` with zero changed files, and both commits resolve to the exact same Git tree `baded42ef7666d85016c1788d92a50b412744d83`. The current exact main then completed the full applicable non-regression set:

- Windows CI #537 / run `34120686533` — SUCCESS
- P06-007 Workspace Search #266 / run `34120686443` — SUCCESS
- P06-008 Large Workspace Safeguards #250 / run `34120686425` — SUCCESS

The content accepted by the successful dedicated ConPTY run and by the successful current-main non-regression runs is therefore the same product tree. No cancelled run is represented as successful evidence.

## Closure boundary

P08-004 is therefore CLOSED. P08 remains `IN_PROGRESS`; P08-007 and P08-008 remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`. P09, P15, and later phases remain prohibited until P08 closes through normal governance.

P08-004 requires no owner-only/manual evidence: its Windows-specific acceptance is covered by the real GitHub-hosted Windows ConPTY fixture. This reconciliation does not modify `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md`, does not waive any existing owner obligation, does not claim release eligibility, and does not set `VERIFIED_FINAL_COMPLETE=true`.
<!-- FCCD-P08-004-FINAL-CLOSURE-PROVENANCE -->
## FCCD-P08-004 final canonical closure provenance

- Task: `FCCD-P08-004 — ConPTY terminal host` — `CLOSED`.
- Implementation PR: #196; exact implementation candidate `218cc7d93fafb690a5e2cf7192970bf6be0caa1f`; normal implementation merge `da9caf9fb1542b7e080ddd768b77f73e653bb08b`.
- Canonical closure reconciliation PR: #203; exact reconciliation head `d09653191cf36a9f1b428d785ec572c5d1183382`; normal reconciliation merge / exact resulting main `7228fac7f32abf1d80a39ac697120c2d16306044`.
- Exact resulting-main Windows CI: run `34122854398` / #539 — `SUCCESS`.
- Exact resulting-main P06-007 Workspace Search: run `34122854368` / #268 — `SUCCESS`.
- Exact resulting-main P06-008 Large Workspace Safeguards: run `34122854364` / #252 — `SUCCESS`.
- These exact-main runs completed after PR #203 merged and exposed no post-reconciliation regression. They are the final task-closure baseline and supersede the earlier pre-reconciliation `f86e8bc5...` main only as the canonical closure endpoint; the earlier implementation/integration provenance remains valid historical evidence.
- P08 remains `IN_PROGRESS`; `FCCD-P08-007` and `FCCD-P08-008` remain `PENDING`; `PHASE_EXIT_GATE=NOT_RUN`; no later phase is authorized by this task closure.
- No owner-only evidence is required for P08-004. Existing owner-last obligations are unchanged and `VERIFIED_FINAL_COMPLETE=false`.

