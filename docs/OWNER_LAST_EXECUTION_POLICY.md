# FCC Code Desktop — Owner-Last Execution Policy

**Status:** CANONICAL / OWNER-AUTHORIZED SCHEDULING AMENDMENT  
**Effective:** 2026-09-05  
**Scope:** scheduling of genuine owner-machine/manual/environment-bound evidence only

## 1. Authority and precedence

The owner explicitly authorizes an owner-last execution model so that cloud-actionable implementation, automated tests, repair, CI, and integration can continue before genuine owner-machine/manual/REAL_TARGET acceptance is executed.

This document is a narrow scheduling amendment. Where `AGENTS.md`, `docs/EXECUTION_PLAN.md`, or `docs/WORKER_PROTOCOL.md` would otherwise require a genuinely environment-bound evidence item to stop all later cloud implementation, this policy controls scheduling only.

It does **not** weaken or supersede:

- task closure criteria,
- phase functional requirements,
- exact-head evidence requirements,
- `docs/ACCEPTANCE_MATRIX.md`,
- `docs/RELEASE_POLICY.md`,
- security/data-integrity rules,
- real FCC/provider/Windows/Unity/Blender/installer/clean-machine/manual evidence requirements,
- the prohibition on fabricated or substituted evidence,
- the requirement that `VERIFIED_FINAL_COMPLETE` remain false until canonical P22 closure.

## 2. Core invariant

Owner-last changes **when** an environment-bound check is executed, never **whether** it is required.

A deferred source task is not `CLOSED` merely because its cloud work is complete. Deferred evidence remains an explicit release blocker in `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` until genuine evidence is executed, reviewed, integrated, and reconciled.

No new `docs/TASK_LEDGER.md` task state is introduced. Existing task states retain their meanings.

## 3. Eligibility for owner-last deferral

An item may be queued only when all of the following are true:

1. The remaining requirement genuinely depends on an owner-controlled or otherwise unavailable environment, installed application, hardware, provider/account state, manual visual/interaction inspection, installer lifecycle environment, or clean-machine environment.
2. All cloud-actionable implementation required to make the check meaningful is integrated.
3. Relevant automated tests and permanent CI are green on the canonical baseline.
4. No known code defect, failed CI, missing automated test, security defect, data-integrity defect, repairable repository problem, or missing implementation is being relabeled as owner-only.
5. A deterministic tracked runner exists when practical; otherwise the queue entry defines exact manual steps and evidence requirements.
6. The queue entry contains the source task/requirement, reason, prerequisites, command or manual procedure, expected evidence path, PASS criteria, and reconciliation rule.
7. Deferral does not cause later cloud implementation to rely on an unverified external assumption. If later correctness materially depends on the missing observation, that work remains blocked until the observation is real.

## 4. Cloud progression with deferred owner evidence

When an otherwise phase-blocking item meets section 3 and is recorded as `QUEUED` in the canonical final-owner queue:

- the source task and its final owner evidence obligation remain unresolved;
- the source phase is not represented as having genuine owner evidence that does not exist;
- workers may continue the next dependency-valid **cloud-actionable** phase work under this owner-authorized scheduling exception;
- workers must continue to preserve normal phase order for cloud implementation;
- only genuine queued owner evidence is deferred; ordinary blockers still follow `docs/WORKER_PROTOCOL.md` priority rules;
- any later regression of an earlier guarantee immediately regains repair priority.

A phase may therefore become **cloud-complete / owner-evidence-deferred** for scheduling purposes without being confused with final release acceptance. Durable state must explicitly name the deferred queue item whenever a phase transition uses this exception.

## 5. Canonical queue

`docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` is the only canonical owner-last evidence queue.

Queue item states are scheduling/evidence states, not task-ledger states:

- `QUEUED` — genuine owner/environment evidence still must be executed and integrated.
- `PASS_INTEGRATED` — genuine evidence has passed, has been reviewed, and is integrated on canonical history with its source task/acceptance reconciliation recorded.

A runner returning exit code 0 does **not** change a queue item to `PASS_INTEGRATED`. Only a repository reconciliation may do that after inspecting genuine evidence.

## 6. Failure semantics

Owner execution fails closed.

If an owner check exposes a product defect, test failure, unsafe behavior, missing evidence, unsupported environment, stale exact-head provenance, or malformed/sensitive evidence:

- do not manufacture PASS metadata;
- keep the queue item unresolved;
- reopen/repair the responsible product work under the earliest correct phase/task ownership;
- rerun affected automated verification;
- rerun the owner check on the new applicable exact candidate.

## 7. Exact-candidate rule

Final owner acceptance is executed against the then-frozen candidate required by the source acceptance contract. Any later source/config/packaging change invalidates affected owner evidence and requires rerun where `docs/RELEASE_POLICY.md` or the acceptance matrix demands exact-candidate proof.

Historical target evidence may inform architecture, but it cannot silently replace an exact-release-candidate acceptance row.

## 8. Final release hard stop

P22 release closure and `VERIFIED_FINAL_COMPLETE=true` are prohibited while any mandatory final-owner queue item is `QUEUED`, while any mandatory acceptance row is not genuine `PASS`, or while any mandatory task remains legitimately unresolved.

The owner-last model must never be used to tag, publish, or present an incomplete build as `v1.0.0`.

## 9. Queue growth rule

Future workers must append a queue item only when a concrete environment-bound requirement becomes genuinely eligible under section 3. Do not pre-classify future code work as owner-only merely because a future phase will eventually require target or manual acceptance.

Typical eligible categories can include real FCC/provider execution, Unity/Blender target execution, end-to-end installed-tool scenarios, required manual visual/accessibility checks, installer/upgrade/uninstall lifecycle checks, and clean-machine acceptance. Eligibility is determined by evidence, not by category name alone.

## 10. Enforcement

The permanent Windows CI baseline must run `tools/final-acceptance/validate-owner-last-policy.ps1`. That validator must fail when the queue/policy/runner contract is missing or malformed, when a queued source task is falsely marked `CLOSED`, when an invalid defect-like classification is used, or when the P04 target runner loses its queue-authorized fail-closed execution guard.

The consolidated owner runner is `tools/final-acceptance/run-final-owner-acceptance.ps1`. It executes only tracked queue commands, validates produced evidence, and never mutates queue state to PASS.