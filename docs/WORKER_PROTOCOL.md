# FCC Code Desktop — Worker Continuation and Recovery Protocol

**Status:** CANONICAL / BINDING  
**Applies to:** every Codex/AI worker, reviewer, recovery worker, phase lead, and continuation session  
**Source of truth:** live repository state

---

## 1. Purpose

This protocol prevents workers from inventing work, abandoning partially completed work, duplicating active work, or selecting a new task while an earlier task is blocking the current phase.

The owner does not manually assign technical tasks or reconstruct history. Workers must recover the correct next action from the repository.

The governing rule is:

```text
RECOVER LIVE STATE
      ↓
FIND BLOCKING / ABANDONED / FAILED CURRENT-PHASE WORK
      ↓
FINISH OR REPAIR IT FIRST
      ↓
ONLY IF NONE EXISTS, CLAIM NEXT UNCLAIMED CURRENT-PHASE TASK
      ↓
IMPLEMENT → TEST → VERIFY → EVIDENCE → CLOSE
```

---

## 2. Mandatory startup reconciliation

Before selecting any task, every worker must fetch LIVE state and inspect at minimum:

1. `AGENTS.md`
2. `CURRENT_PHASE.md`
3. `PROJECT_CONTROL.md`
4. `docs/EXECUTION_PLAN.md`
5. `docs/WORKER_PROTOCOL.md`
6. `docs/TASK_LEDGER.md`
7. `docs/ACCEPTANCE_MATRIX.md`
8. `docs/DECISIONS.md`
9. current `main` HEAD
10. open PRs
11. active/recent worker branches
12. recent commits
13. open issues relevant to the current phase
14. phase evidence
15. CI/test status where available

The worker must reconcile what actually exists with what the ledger says. Repository reality wins over stale prose.

---

## 3. Build a claim and recovery map

Before choosing work, classify every non-closed current-phase task and related branch/PR into one of these categories:

```text
ACTIVE_VALID
ABANDONED_OR_STALE
BLOCKING_FAILED
INTEGRATION_PENDING
UNCLAIMED_PENDING
CLOSED
```

### ACTIVE_VALID
Another worker is demonstrably still working on the task and its claim is live. Do not duplicate it.

### ABANDONED_OR_STALE
Work exists but the prior worker stopped, disappeared, left an incomplete branch/PR, stopped updating evidence, or otherwise no longer has a trustworthy live claim.

This work must be recovered before unrelated new work is selected when it blocks current-phase closure or is the earliest incomplete dependency.

### BLOCKING_FAILED
Implementation or integration exists but tests, CI, contract checks, merge, acceptance, or required evidence fail.

This has priority over new feature work.

### INTEGRATION_PENDING
Implementation is substantially complete but still needs review, conflict resolution, merge/rebase/convergence, exact-head verification, evidence, or ledger closure.

Finish this before starting unrelated work if it blocks the current phase.

### UNCLAIMED_PENDING
No valid implementation/claim exists. This is eligible only after higher-priority recovery work is cleared.

---

## 4. Strict work-selection priority

Every worker must choose work in this exact priority order:

### Priority 1 — Restore broken canonical state

If `main`, required CI, current-phase acceptance, or an earlier closed guarantee is broken, fix that first.

Do not start new work on top of a knowingly broken canonical baseline unless the new work is explicitly the repair.

### Priority 2 — Resolve current-phase blockers

Any `BLOCKED` task, failing dependency, broken contract, failed test, merge conflict, missing required environment evidence, or integration problem that prevents phase closure has priority over new work.

### Priority 3 — Recover abandoned/stale current-phase work

If another worker started legitimate current-phase work and stopped before closure:

- inspect the existing branch/PR/commits,
- determine what is usable,
- preserve correct work,
- fix incomplete or incorrect work,
- finish tests/evidence/integration,
- close the task if justified.

Do **not** restart from scratch merely because the original worker is gone.

Restart is allowed only when evidence shows the existing implementation is unsafe, fundamentally incorrect, or more expensive to repair than replace. Record the reason.

### Priority 4 — Finish integration-pending work

Complete merge/rebase/conflict resolution, canonical integration, verification, evidence, and ledger reconciliation for otherwise finished tasks.

### Priority 5 — Select next unclaimed current-phase task

Only when Priorities 1–4 contain no legitimate work may a worker select a new task.

Choose the earliest/dependency-critical legitimate task in the current phase, not the easiest or most visually attractive one.

---

## 5. Stale claim detection

A task must not remain permanently untouchable merely because an old branch or ledger row says `CLAIMED` or `IN_PROGRESS`.

A worker must use live evidence to determine whether a claim is still active.

Signals that a claim may be stale include:

- previous worker explicitly stopped or returned without closure,
- branch/PR contains incomplete work and no active worker is demonstrably continuing it,
- task is blocking the phase while its worker is no longer active,
- the claim conflicts with newer canonical state,
- prior work ended with a blocker that is now resolvable,
- user started a new continuation worker specifically because previous execution stopped.

Do not use an arbitrary clock timeout as the only stale-claim criterion.

When reclaiming, record the takeover in the ledger/PR/commit/evidence as appropriate so another worker does not duplicate the recovery.

---

## 6. No orphaned blocking work

No worker may leave a discovered legitimate current-phase blocker unowned while starting unrelated new work.

If the worker can resolve it autonomously, resolve it.

If resolution genuinely requires an external owner action that cannot be obtained by the worker:

1. finish all unaffected prerequisite/recovery work,
2. record the blocker precisely,
3. include reproduction/evidence,
4. mark the affected task `BLOCKED`,
5. keep the current phase open,
6. do not move to a later phase.

Technical difficulty, failing tests, unfamiliar code, merge conflicts, or a hard bug are not external blockers.

---

## 7. Related work discovered during a task

If completing the selected task exposes additional work, classify it immediately.

### Required to make the selected task correct

It is part of the current task. Finish it before closure.

### Required for the current phase exit gate

Add it to `docs/TASK_LEDGER.md` in the current phase and ensure it is completed before phase advancement.

### Regression in earlier closed work

Stop forward progress and restore the earlier guarantee first. Rerun affected downstream verification.

### Legitimate later-phase enhancement

Record it in the proper later phase if missing, but do not implement it now unless it is required to unblock the current phase.

### Optional/non-required idea

Do not expand scope automatically. Do not implement speculative features just because they seem useful.

---

## 8. Preserve useful previous work

When taking over incomplete work:

- read the diff before changing it,
- understand the previous implementation and tests,
- preserve correct code,
- preserve relevant evidence,
- avoid destructive resets,
- do not discard user or worker changes merely to get a clean tree,
- distinguish pre-existing changes from takeover changes,
- reconcile conflicts deliberately.

A takeover is continuation, not permission to erase history.

---

## 9. Task closure requirement

A recovered or newly selected task is not complete until:

```text
IMPLEMENTATION COMPLETE
AND TESTS PASS
AND ERROR/RECOVERY PATHS PASS WHERE APPLICABLE
AND REQUIRED UI STATES PASS WHERE APPLICABLE
AND EVIDENCE EXISTS
AND CANONICAL INTEGRATION IS COMPLETE
AND LEDGER IS RECONCILED
AND NO TASK-LOCAL REGRESSION REMAINS
```

Only then may the task become `CLOSED`.

Do not stop at `IMPLEMENTED` or `VERIFIED` if the worker's assigned scope is to close the task end-to-end.

---

## 10. Worker must not invent

A worker must not invent:

- a new product direction,
- a new phase order,
- a replacement architecture without evidence and required ADR reconciliation,
- reduced quality criteria,
- missing PASS results,
- fictional external-tool behavior,
- fake completion percentages,
- arbitrary new features,
- a different UI/UX doctrine,
- a later-phase task just because the current work is difficult.

If the canonical plan is genuinely missing something required for correctness, reliability, security, product completeness, or the declared user goal:

1. prove why it is missing,
2. add/reconcile the requirement in the canonical project documents,
3. place the work in the correct phase,
4. do not silently redesign the project.

---

## 11. Owner interaction rule

The owner supervises outcomes and should not be required to decide routine engineering details or manually identify the next task.

Workers must make normal technical decisions using:

1. canonical repository requirements,
2. live evidence,
3. authoritative documentation,
4. tests/contract probes,
5. platform best practice,
6. maintainability/reliability/security/performance,
7. premium product quality.

Ask the owner only for genuine external requirements that cannot be derived or obtained autonomously.

---

## 12. End-of-worker handoff

Before a worker stops for any reason, it must make repository state as durable as possible.

At minimum:

- commit or clearly preserve legitimate completed work,
- update task state truthfully,
- record tests run and current failures,
- record blockers precisely,
- record any active branch/PR,
- update evidence when applicable,
- do not claim closure without proof.

If interrupted before doing this, the next worker must recover from live Git state using this protocol.

---

## 13. Canonical next-action algorithm

Every worker can determine the next action using this algorithm:

```text
FETCH LIVE STATE

IF canonical baseline is broken:
    repair canonical baseline
ELSE IF current-phase blocking failure exists:
    resolve blocking failure
ELSE IF abandoned/stale blocking current-phase work exists:
    take over and finish it
ELSE IF integration-pending current-phase work exists:
    integrate + verify + close it
ELSE IF unclaimed current-phase work exists:
    claim highest-priority dependency-valid task
    implement + test + verify + close it
ELSE:
    run complete current-phase exit gate
    fix every failure
    record exact-head closure evidence
    close phase only if PASS
    advance CURRENT_PHASE only after legal closure
```

This algorithm is mandatory. It removes the need for the owner to invent or manually assign work.
