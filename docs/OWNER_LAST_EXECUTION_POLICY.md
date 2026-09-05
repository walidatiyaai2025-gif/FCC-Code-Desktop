# FCC Code Desktop — Owner-Last Execution Policy

**Status:** CANONICAL / OWNER-AUTHORIZED SCHEDULING AMENDMENT  
**Effective:** 2026-09-05  
**Scope:** scheduling of genuine owner-machine/manual/environment-bound evidence only

## 1. Authority and precedence

The owner explicitly authorizes an owner-last execution model so that cloud-actionable implementation, automated tests, repair, CI, and integration can continue before genuine owner-machine/manual/REAL_TARGET acceptance is executed.

This document is a narrow scheduling amendment. It controls **scheduling only** where the ordinary sequential lock text in `AGENTS.md`, `CURRENT_PHASE.md`, `PROJECT_CONTROL.md`, `docs/EXECUTION_PLAN.md`, `docs/WORKER_PROTOCOL.md`, or `docs/TASK_LEDGER.md` would otherwise stop later dependency-valid cloud implementation solely because a genuinely environment-bound requirement is waiting for the final owner lane.

It does **not** weaken or supersede:

- task closure criteria;
- phase functional requirements;
- phase exit-gate PASS criteria;
- exact-head evidence requirements;
- `docs/ACCEPTANCE_MATRIX.md`;
- `docs/RELEASE_POLICY.md`;
- security/data-integrity rules;
- real FCC/provider/Windows/Unity/Blender/installer/clean-machine/manual evidence requirements;
- the prohibition on fabricated, substituted, assumed, stale-SHA, or self-test evidence being called real acceptance;
- the requirement that `VERIFIED_FINAL_COMPLETE` remain false until canonical P22 final closure.

When `OWNER_LAST_MODE: ACTIVE` is recorded in `CURRENT_PHASE.md`, that file identifies the **one current cloud implementation phase**. Earlier phases may remain acceptance-unresolved only through the fail-closed queue rules below. Historical prose that says a later phase was prohibited before owner-last activation remains valid historical evidence but no longer controls scheduling by itself.

## 2. Core invariant

Owner-last changes **when** an environment-bound check is executed, never **whether** it is required.

A deferred source task is not `CLOSED` merely because its cloud work is complete. A deferred source phase or phase-gate requirement is not given a fabricated `EXIT_GATE=PASS`. Deferred evidence remains an explicit release blocker in `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` until genuine evidence is executed, reviewed, integrated, and reconciled.

No new `docs/TASK_LEDGER.md` task state is introduced. Existing task states retain their meanings. A phase-gate queue entry is an acceptance/scheduling record only; it does not invent a hidden ledger task.

## 3. Eligibility for owner-last deferral

An item may be queued only when all of the following are true:

1. The remaining requirement genuinely depends on an owner-controlled or otherwise unavailable environment, installed application, hardware, provider/account state, manual visual/interaction inspection, installer lifecycle environment, or clean-machine environment.
2. All cloud-actionable implementation required to make the check meaningful is integrated.
3. Relevant automated tests and permanent CI are green on the canonical baseline.
4. No known code defect, failed CI, missing automated test, security defect, data-integrity defect, repairable repository problem, or missing implementation is being relabeled as owner-only.
5. A deterministic tracked runner exists when practical; otherwise the queue entry defines an exact tracked evidence procedure.
6. The queue entry contains the source task/requirement, reason, prerequisites, command/procedure, expected evidence path, PASS criteria, and reconciliation rule.
7. Deferral does not cause later cloud implementation to rely on an unverified external assumption. If later correctness materially depends on the missing observation, that work remains blocked until the observation is real.
8. For a task-sourced item, the source task remains unresolved in the canonical ledger. For a phase-gate-sourced item, every mandatory task for that phase is already `CLOSED`, the phase's cloud-actionable validation is complete and green, and the phase exit gate remains truthfully unresolved rather than being represented as PASS.

## 4. One current cloud phase with deferred earlier evidence

Owner-last preserves sequential **cloud implementation** order. It does not permit arbitrary phase skipping or parallel cross-phase feature work.

A cloud-phase transition past an owner-deferred source phase is legal only when:

```text
ALL CLOUD-ACTIONABLE WORK IN THE SOURCE PHASE IS INTEGRATED
AND PERMANENT CI IS GREEN
AND EVERY REMAINING SOURCE-PHASE OBLIGATION IS GENUINELY ENVIRONMENT-BOUND
AND EVERY EARLIER NON-CLOSED TASK HAS EXACTLY ONE VALID QUEUED OWNER TASK ITEM
AND EVERY DEFERRED UNRESOLVED PHASE GATE HAS EXACTLY ONE VALID QUEUED OWNER PHASE-GATE ITEM
AND EVERY QUEUED ITEM IS releaseBlocking=true
AND NO LATER CLOUD WORK MATERIALLY DEPENDS ON THE MISSING OBSERVATION
AND CURRENT_PHASE.md RECORDS OWNER_LAST_MODE=ACTIVE + THE DEFERRED ITEM IDS + DEFERRED_PHASE_GATES
```

The canonical enforcement statement is: every earlier non-CLOSED task has exactly one valid `QUEUED` task-sourced owner item, and every phase gate explicitly recorded in `DEFERRED_PHASE_GATES` has exactly one valid `QUEUED` phase-gate-sourced owner item.

Then `CURRENT_PHASE` may advance to the next sequential cloud implementation phase while the earlier source task and/or phase gate remain truthfully unresolved.

At all times:

- exactly one current cloud implementation phase exists;
- a worker may implement only that cloud phase unless performing higher-priority repair/recovery;
- ordinary phase order is preserved: P05 → P06 → ...; no phase may be skipped merely because an owner item is queued;
- a regression in an earlier guarantee immediately regains Priority 1/2 repair status;
- no earlier unresolved task may exist unless it is represented one-to-one by a valid `QUEUED` task-sourced owner item;
- no deferred unresolved phase gate may exist unless it is represented one-to-one by a valid `QUEUED` phase-gate-sourced owner item;
- a `PASS_INTEGRATED` queue item is no longer an excuse for leaving its source task or phase-gate reconciliation stale;
- `KNOWN_RELEASE_BLOCKERS` in `CURRENT_PHASE.md` must be at least the count of unresolved release-blocking queue items.

## 5. Canonical queue

`docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` is the only canonical owner-last evidence queue.

Queue item states are scheduling/evidence states, not task-ledger states:

- `QUEUED` — genuine owner/environment evidence still must be executed and integrated.
- `PASS_INTEGRATED` — genuine evidence passed, was reviewed, and is integrated on canonical history with source task/phase-gate/acceptance reconciliation recorded.

A runner returning exit code 0 does **not** change a queue item to `PASS_INTEGRATED`. Only repository reconciliation may do that after inspecting genuine evidence.

Future eligible items are appended only after their cloud prerequisites are actually integrated and green. Do not pre-classify future code work as owner-only.

## 6. Required queue fields and classification discipline

Each queued item must durably record at minimum:

- unique item ID;
- `sourceKind` identifying `TASK` or `PHASE_GATE`;
- source phase;
- source task when `sourceKind=TASK`, or explicit phase-gate requirement when `sourceKind=PHASE_GATE`;
- environment-bound classification;
- why owner-only;
- cloud-complete evidence;
- exact tracked command/procedure;
- prerequisites;
- expected evidence path;
- PASS criteria;
- reconciliation rule;
- `releaseBlocking=true`.

A source task or phase-gate requirement is therefore explicit and validator-controlled; neither kind may be inferred from prose alone.

Allowed environment-bound classifications are deliberately narrow and validator-controlled. Classification names never convert failed code/CI/security/data work into owner work.

## 7. Failure semantics

Owner execution fails closed.

If an owner check exposes a product defect, test failure, unsafe behavior, missing evidence, unsupported environment, stale exact-head provenance, malformed evidence, or sensitive evidence:

- do not manufacture PASS metadata;
- keep the queue item unresolved;
- reopen/repair the responsible product work under the earliest correct phase/task ownership;
- rerun affected automated verification;
- rerun the owner check on the new applicable exact candidate.

A failed owner check is product/recovery work, not a waiver request.

## 8. Exact-candidate rule

Final owner acceptance is executed against the then-frozen candidate required by the source acceptance contract and final release policy. Any later source/config/packaging change invalidates affected owner evidence and requires rerun where `docs/RELEASE_POLICY.md` or `docs/ACCEPTANCE_MATRIX.md` demands exact-candidate proof.

Historical target evidence may inform architecture, but it cannot silently replace exact-release-candidate acceptance.

## 9. Consolidated final-owner runner

The consolidated runner is:

```powershell
.\tools\final-acceptance\run-final-owner-acceptance.ps1
```

It must fail closed. It may execute only commands tracked by the canonical queue, must enforce repository/exact-head/source-input provenance, must require expected evidence, must validate evidence classification/SHA/PASS status where machine-readable, must perform sanitization checks, and must never mutate queue state to PASS.

Successful execution ends in **reconciliation required**, not release completion.

## 10. P22 and final release hard stop

P22 is the final release/acceptance closure phase and is **not eligible to become the current cloud implementation phase while any required owner queue item remains `QUEUED`**.

Before P22 activation, all deferred owner items must be genuinely executed, reviewed, integrated, and reconciled as `PASS_INTEGRATED`, with their source tasks/phases/phase gates/acceptance obligations reconciled according to the canonical contracts.

`VERIFIED_FINAL_COMPLETE=true` is prohibited while:

- any owner queue item is `QUEUED`;
- any mandatory task is legitimately unresolved;
- any mandatory phase gate is not genuinely PASS where required for final closure;
- any mandatory acceptance row is not genuine PASS on the required exact release candidate;
- any release blocker remains.

The owner-last model must never be used to tag, publish, or present an incomplete build as `v1.0.0`.

## 11. Current P04 → P05 scheduling activation

The first owner-last transition is intentionally narrow:

- `FCCD-P04-001` through `FCCD-P04-007` are canonically CLOSED.
- `FCCD-P04-008` cloud implementation is integrated and green but fresh owner-Windows/provider `REAL_TARGET` evidence remains required.
- `OWNER-P04-008-REAL-TARGET` is the canonical queued release blocker for that task obligation.
- P04 remains acceptance-unresolved; its gate remains `NOT_RUN`.
- P05 is activated only as the next sequential **cloud implementation phase**.
- If the eventual P04 real-target run reveals a defect, P05+ forward work stops as necessary to repair the earliest affected guarantee and rerun impacted verification.

Activation evidence: `evidence/governance/OWNER_LAST_P05_CLOUD_ACTIVATION_2026-09-05.md`.

## 12. Enforcement

The permanent Windows CI baseline must run `tools/final-acceptance/validate-owner-last-policy.ps1`.

That validator must fail for at least:

- missing/malformed queue or policy;
- non-environment/defect-like classification;
- missing tracked command/evidence metadata;
- queued source task falsely marked `CLOSED`;
- an earlier non-CLOSED task with no one-to-one queued task item;
- a deferred phase gate with no one-to-one queued phase-gate item;
- a queued phase-gate requirement represented as `PASS`;
- current cloud phase advanced without `OWNER_LAST_MODE=ACTIVE`, deferred item IDs, and deferred phase-gate state;
- release-blocker count below unresolved owner queue count;
- false `PASS_INTEGRATED` without integrated evidence;
- false `VERIFIED_FINAL_COMPLETE=true` while queue unresolved;
- P22 activation while queue unresolved;
- removal of target-runner queue authorization or final-runner fail-closed behavior.

The validator is a loophole-prevention mechanism. It must never generate owner evidence or auto-close a task or phase gate.

## 13. Current P05 phase-exit deferral boundary

P05 demonstrates the phase-gate form of owner-last deferral. `FCCD-P05-001` through `FCCD-P05-008` must already be integrated and `CLOSED`, the permanent Windows cloud baseline must be green, and a tracked fail-closed owner runner must exist before the P05 exit requirement can be queued.

The P05 source is `sourceKind=PHASE_GATE`, `sourceRequirement=P05_EXIT_GATE`, not a fabricated ninth P05 task. Its required real interaction remains `REAL_TARGET`: issue a genuine task through FCC Code Desktop on the owner Windows/FCC/provider environment, observe structured execution, exercise stop/retry, close/reopen, and verify durable session resume. While that item is `QUEUED`, P05's exit gate remains `NOT_RUN`; no `CLOSURE.md` PASS is permitted.

After this cloud convergence is integrated and exact-head CI is green, a separate transition may activate only P06 as the next sequential cloud implementation phase while preserving both P04 and P05 unresolved owner obligations as release blockers. This does not authorize P07 or any later phase until P06 cloud work is itself complete under the same sequential rules.
