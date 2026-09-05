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

A deferred source task is not `CLOSED` merely because its cloud work is complete. A deferred source phase is not given a fabricated `EXIT_GATE=PASS`. Deferred evidence remains an explicit release blocker in `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` until genuine evidence is executed, reviewed, integrated, and reconciled.

No new `docs/TASK_LEDGER.md` task state is introduced. Existing task states retain their meanings.

## 3. Eligibility for owner-last deferral

An item may be queued only when all of the following are true:

1. The remaining requirement genuinely depends on an owner-controlled or otherwise unavailable environment, installed application, hardware, provider/account state, manual visual/interaction inspection, installer lifecycle environment, or clean-machine environment.
2. All cloud-actionable implementation required to make the check meaningful is integrated.
3. Relevant automated tests and permanent CI are green on the canonical baseline.
4. No known code defect, failed CI, missing automated test, security defect, data-integrity defect, repairable repository problem, or missing implementation is being relabeled as owner-only.
5. A deterministic tracked runner exists when practical; otherwise the queue entry defines an exact tracked evidence procedure.
6. The queue entry contains the source task/requirement, reason, prerequisites, command/procedure, expected evidence path, PASS criteria, and reconciliation rule.
7. Deferral does not cause later cloud implementation to rely on an unverified external assumption. If later correctness materially depends on the missing observation, that work remains blocked until the observation is real.
8. The source task remains unresolved in the canonical ledger and the source phase gate is not represented as PASS solely because of the deferral.

## 4. One current cloud phase with deferred earlier evidence

Owner-last preserves sequential **cloud implementation** order. It does not permit arbitrary phase skipping or parallel cross-phase feature work.

A cloud-phase transition past an owner-deferred source phase is legal only when:

```text
ALL CLOUD-ACTIONABLE WORK IN THE SOURCE PHASE IS INTEGRATED
AND PERMANENT CI IS GREEN
AND EVERY REMAINING SOURCE-PHASE OBLIGATION IS GENUINELY ENVIRONMENT-BOUND
AND EVERY EARLIER NON-CLOSED TASK HAS EXACTLY ONE VALID QUEUED OWNER ITEM
AND EVERY QUEUED ITEM IS releaseBlocking=true
AND NO LATER CLOUD WORK MATERIALLY DEPENDS ON THE MISSING OBSERVATION
AND CURRENT_PHASE.md RECORDS OWNER_LAST_MODE=ACTIVE + THE DEFERRED ITEM IDS
```

Then `CURRENT_PHASE` may advance to the next sequential cloud implementation phase while the earlier source task and gate remain truthfully unresolved.

At all times:

- exactly one current cloud implementation phase exists;
- a worker may implement only that cloud phase unless performing higher-priority repair/recovery;
- ordinary phase order is preserved: P05 → P06 → ...; no phase may be skipped merely because an owner item is queued;
- a regression in an earlier guarantee immediately regains Priority 1/2 repair status;
- no earlier unresolved task may exist unless it is represented one-to-one by a valid `QUEUED` owner item;
- a `PASS_INTEGRATED` queue item is no longer an excuse for leaving its source task unreconciled;
- `KNOWN_RELEASE_BLOCKERS` in `CURRENT_PHASE.md` must be at least the count of unresolved release-blocking queue items.

## 5. Canonical queue

`docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` is the only canonical owner-last evidence queue.

Queue item states are scheduling/evidence states, not task-ledger states:

- `QUEUED` — genuine owner/environment evidence still must be executed and integrated.
- `PASS_INTEGRATED` — genuine evidence passed, was reviewed, and is integrated on canonical history with source task/acceptance reconciliation recorded.

A runner returning exit code 0 does **not** change a queue item to `PASS_INTEGRATED`. Only repository reconciliation may do that after inspecting genuine evidence.

Future eligible items are appended only after their cloud prerequisites are actually integrated and green. Do not pre-classify future code work as owner-only.

## 6. Required queue fields and classification discipline

Each queued item must durably record at minimum:

- unique item ID;
- source task and source phase;
- environment-bound classification;
- why owner-only;
- cloud-complete evidence;
- exact tracked command/procedure;
- prerequisites;
- expected evidence path;
- PASS criteria;
- reconciliation rule;
- `releaseBlocking=true`.

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

Before P22 activation, all deferred owner items must be genuinely executed, reviewed, integrated, and reconciled as `PASS_INTEGRATED`, with their source tasks/phases/acceptance obligations reconciled according to the canonical contracts.

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
- `OWNER-P04-008-REAL-TARGET` is the one canonical queued release blocker for that obligation.
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
- an earlier non-CLOSED task with no one-to-one queued owner item;
- current cloud phase advanced without `OWNER_LAST_MODE=ACTIVE` and deferred item IDs;
- release-blocker count below unresolved owner queue count;
- false `PASS_INTEGRATED` without integrated evidence;
- false `VERIFIED_FINAL_COMPLETE=true` while queue unresolved;
- P22 activation while queue unresolved;
- removal of target-runner queue authorization or final-runner fail-closed behavior.

The validator is a loophole-prevention mechanism. It must never generate owner evidence or auto-close a task.