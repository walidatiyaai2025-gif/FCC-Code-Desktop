# FCC Code Desktop — P00 Target-Machine Validation Lane

**Status:** CANONICAL / BINDING P00 SUPPLEMENT  
**Reason:** Remote/cloud coding workers cannot directly observe the owner's local Windows FCC/`fcc-claude`, Unity, or Blender installations. P00 therefore requires a deliberate local-evidence lane rather than allowing remote workers to guess target behavior.

---

## 1. Problem resolved by this document

P00 requires real target-environment evidence for FCC/`fcc-claude`, streaming, sessions, cancellation/failures, Unity, and Blender. A remote worker can build and self-test deterministic probes, but it cannot truthfully close target-dependent contracts when the target executables are not present in its execution environment.

This is not permission to weaken P00. It is a delivery-path requirement.

The project uses two evidence lanes:

```text
REMOTE/CLOUD WORKERS
  build + self-test reusable probes
                ↓
CANONICAL PROBE SUITE ON main
                ↓
TARGET WINDOWS VALIDATION WORKER
  runs locally where FCC/Unity/Blender actually exist
                ↓
SANITIZED TARGET EVIDENCE
                ↓
CONVERGENCE WORKER
  reconciles contracts + closes eligible P00 tasks
                ↓
FULL P00 EXIT GATE
```

---

## 2. Remote-worker responsibilities

Remote workers assigned P00 contract tasks must:

- implement deterministic repository-owned probes,
- self-test missing-runtime and error handling,
- redact secrets before writing evidence,
- document exact expected target observations,
- merge valid probe infrastructure to canonical `main`,
- mark target-dependent tasks `BLOCKED` rather than inventing results when the real Windows environment is unavailable.

A remote worker is successful when it has removed all code/tooling uncertainty it can remove and has made the remaining target check executable by a local worker.

`BLOCKED` due solely to missing target access is acceptable intermediate state. It is not phase closure.

---

## 3. Target Windows Validation Worker

After the relevant remote probes exist on canonical `main`, one worker must run **inside the owner's actual Windows target environment**.

Preferred executor order:

1. the user's local `fcc-claude` environment acting as a local autonomous worker,
2. another trusted local coding agent with direct access to the same Windows machine,
3. a repository-owned one-command PowerShell target runner executed by the owner exactly as documented.

The owner must not be required to interpret results or make technical decisions. If owner interaction is unavoidable, it must be reduced to executing one explicit command or pasting one explicit local-worker prompt.

---

## 4. Required unified target runner

The repository-owned Windows entry point is:

```text
tools/contract-probes/run-target-validation.ps1
```

The runner orchestrates the current canonical P00 target-dependent lanes on `main`:

- FCC / `fcc-claude` discovery and health,
- CLI fallback,
- structured streaming,
- sessions/resume,
- cancellation/error behavior,
- Unity contract probes,
- Blender contract probes.

The implementation may internally call Node/PowerShell/Python or tool-specific modules, but the owner/local worker receives one entry point. It must remain safe to rerun.

---

## 5. Target runner safety and provenance requirements

The runner must:

- run only on Windows for authoritative P00 target evidence,
- refuse a dirty Git worktree before evidence generation so the evidence can be attributed to the recorded exact HEAD SHA,
- verify that the checkout resolved by Git is the repository containing the runner,
- verify required probe prerequisites such as Git and Node before execution,
- perform no destructive workspace operations,
- never intentionally generate load merely to force a rate limit,
- never overwrite valuable Unity or Blender assets,
- use disposable fixtures where mutation is required,
- redact tokens, API keys, authorization headers, provider credentials, FCC secrets, and sensitive environment values,
- keep raw unsanitized secrets out of Git history,
- distinguish `PASS`, `FAIL`, `BLOCKED`, `NOT_INSTALLED`, and `NOT_OBSERVED`,
- return a non-zero exit code when mandatory evidence is incomplete,
- preserve logs necessary for debugging while sanitizing them,
- record executable paths and versions safely,
- record the exact repository SHA used for the run,
- record command/probe versions and timestamps,
- clean up owned temporary processes/files.

PR #6 hardened these provenance requirements in the canonical runner by rejecting non-Windows execution, dirty worktrees, wrong repository roots, and missing Git/Node prerequisites before target evidence is written.

---

## 6. Target evidence output

The unified target run must produce a compact machine-readable manifest plus human-readable evidence under P00 evidence paths.

At minimum:

```text
evidence/phases/P00/target/P00_TARGET_EVIDENCE.json
evidence/phases/P00/target/P00_TARGET_EVIDENCE.md
```

Tool-specific supporting files may also be produced.

The manifest must identify:

- tested repo SHA,
- Windows version/architecture,
- discovered tool versions,
- probe result per P00 contract,
- exact PASS/FAIL/BLOCKED reason,
- artifact paths,
- sanitized error summaries,
- whether each result was observed on the actual target machine.

---

## 7. Publication of local evidence

Target evidence becomes canonical only after it is brought back into the repository.

Preferred methods, in order:

1. local worker creates a focused branch/PR containing sanitized evidence only,
2. local runner commits/pushes a dedicated evidence branch when Git authentication is already safely available,
3. owner supplies the generated sanitized evidence files to a repository-capable worker for integration.

The runner must not require the owner to copy secrets or manually interpret logs.

---

## 8. Closure semantics for target-dependent tasks

A task with complete reusable probe infrastructure but incomplete authoritative target observation remains `BLOCKED`.

After target evidence is integrated, a convergence worker must:

1. rerun/review the relevant self-tests,
2. verify the target evidence came from the expected machine and exact repository SHA,
3. verify the evidence-producing probe is the same version represented by that SHA,
4. reconcile observed behavior into contract documentation,
5. fix probes/docs if observations differ from assumptions,
6. mark the task `VERIFIED`, then `CLOSED` only when all task-local criteria are satisfied.

Target evidence must never be treated as automatic closure without reconciliation. A target-relevant probe change after a Windows run invalidates exact-head verification for the affected guarantee until that updated probe is rerun on the authoritative target.

---

## 9. Parallel P00 worker strategy

P00 remains one current phase. Cloud workers may operate on non-overlapping probe defects, regression coverage, evidence consistency, and convergence preparation while target-only work remains blocked.

Current responsibility split is conceptually:

```text
CLOUD / REMOTE WORKERS
  maintain and harden canonical probes,
  self-test deterministic mechanics,
  reconcile contracts/evidence/state,
  never manufacture target evidence

LOCAL TARGET VALIDATION WORKER
  run the unified suite on the owner's Windows target

PLANNING / RECONCILIATION AUTHORITY
  resolve explicit plan gaps such as PG-002 when policy—not implementation—is blocking closure

CONVERGENCE WORKER
  reconcile new target evidence,
  close eligible P00 tasks,
  run the complete P00 exit gate
```

Workers must inspect live branches/PRs/claims before taking a lane and must not duplicate active work.

---

## 10. Phase-lock remains unchanged

This lane does not weaken sequential execution.

P01 remains forbidden until:

```text
ALL P00 MANDATORY TASKS = CLOSED
AND TARGET EVIDENCE IS INTEGRATED
AND P00 EXIT GATE = PASS
AND EXACT-HEAD EVIDENCE IS RECORDED
AND MAIN IS GREEN
```

If required target evidence cannot be obtained, P00 remains open.

---

## 11. Current canonical integration status

All mandatory P00 probe families now have repository-owned infrastructure integrated behind the unified runner:

- FCC discovery/health and CLI fallback,
- structured streaming,
- session/resume,
- cancellation/failure,
- Unity,
- Blender.

Historical milestones such as PR #1 and Worker 2 remain useful provenance, but they are not the current integration boundary. Unity and Blender are no longer separate unintegrated lanes: the runner invokes both current probe families and produces their target evidence paths.

The most recent cloud hardening relevant to target execution includes:

- PR #6 — target-runner provenance guards for Windows, exact clean HEAD, repository identity, Git and Node;
- PR #9 — FCC runtime ownership evidence refreshes descendants immediately before cancellation/timeout escalation and covers late-spawned children with deterministic regression tests.

Because PR #9 changed the evidence-producing cancellation/process-ownership probe after the last Windows run, `FCCD-P00-005` requires a new exact-head Windows target run before it may regain `VERIFIED` status.

---

## 12. Current target blockers and next authoritative run

At the current P00 state:

- `FCCD-P00-004` requires a successful provider-backed session and resume/continuity run;
- `FCCD-P00-005` requires the hardened exact-head Windows cancellation/failure rerun and resolution of `PG-002-P00-RATE-LIMIT-CLOSURE` unless a natural rate-limit observation occurs first;
- `FCCD-P00-007` requires successful provider-backed CLI fallback completion;
- `FCCD-P00-009` requires real Blender execution on the authoritative Windows target;
- `FCCD-P00-006` and `FCCD-P00-010` remain convergence-dependent until the target blockers are removed.

The next authoritative local run must use a clean checkout of the current canonical `main`, the one-command runner, bounded provider traffic, sanitized evidence, and no fabricated replacement for unavailable provider or Blender behavior. After evidence is integrated, a convergence worker reconciles all affected contracts/task states and only then evaluates the P00 exit gate.
