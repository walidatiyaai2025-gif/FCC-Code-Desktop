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

P00 workers must converge their probes behind a single repository-owned Windows entry point before final target validation.

Canonical intended path:

```text
tools/contract-probes/run-target-validation.ps1
```

The implementation may internally call Node/PowerShell/Python or tool-specific modules, but the owner/local worker receives one entry point.

The target runner must eventually orchestrate all P00 target-dependent evidence that exists on `main`, including:

- FCC / `fcc-claude` discovery and health,
- CLI fallback,
- structured streaming,
- sessions/resume,
- cancellation/error behavior,
- Unity contract probes,
- Blender contract probes.

It must be safe to rerun.

---

## 5. Target runner safety requirements

The runner must:

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

## 8. Closure semantics for currently blocked tasks

A task such as `FCCD-P00-002` or `FCCD-P00-007` that has complete reusable probe infrastructure but lacks actual target observation remains `BLOCKED`.

After target evidence is integrated, a convergence worker must:

1. rerun/review the relevant self-tests,
2. verify the target evidence came from the expected machine/repo SHA,
3. reconcile observed behavior into contract documentation,
4. fix probes/docs if observations differ from assumptions,
5. mark the task `VERIFIED`, then `CLOSED` only when all task-local criteria are satisfied.

Target evidence must never be treated as automatic closure without reconciliation.

---

## 9. Parallel P00 worker strategy

P00 remains one current phase, but non-overlapping probe construction may proceed in parallel.

Recommended lanes:

```text
W1  FCC discovery + CLI fallback       P00-002 / P00-007
W2  streaming + sessions + failures    P00-003 / P00-004 / P00-005
W3  Unity contract probes              P00-008
W4  Blender contract probes            P00-009

then

LOCAL TARGET VALIDATION WORKER
  runs unified target suite on owner's Windows machine

then

W5 CONVERGENCE
  P00-006 / P00-010 + reconcile blocked tasks + full P00 exit gate
```

W1-W4 may merge probe infrastructure even when target evidence is still blocked, provided their changes are isolated, self-tested, truthful, and do not falsely close target-dependent tasks.

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

## 11. Immediate implication after PR #1

PR #1 validly created the FCC discovery/CLI fallback probe infrastructure and correctly left `FCCD-P00-002` and `FCCD-P00-007` blocked because its worker host did not contain the owner's actual FCC environment.

That result should be preserved.

The project should now continue building the remaining non-overlapping P00 probes (streaming/session/failure, Unity, Blender), converge them behind the unified target runner, and perform one consolidated local target-validation pass rather than repeatedly discovering the same remote-environment limitation worker by worker.
