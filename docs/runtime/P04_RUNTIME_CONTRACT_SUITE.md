# P04 Runtime Contract Suite

**Task:** `FCCD-P04-008 — Runtime contract suite`  
**Phase:** P04 — FCC / `fcc-claude` runtime core

## Purpose

P04-001 through P04-007 establish the individual runtime contracts. P04-008 owns the aggregate headless contract suite that proves those pieces compose without UI coupling and prepares the authoritative real-target evidence required before P04 can close.

The suite deliberately uses two evidence classifications:

- `SELF_TEST_ONLY` — deterministic GitHub-hosted Windows fixture execution against a disposable fake FCC executable. This proves the harness, argument paths, normalized execution contract, negative path, cancellation mechanics and fallback composition. It **does not** claim provider or owner-machine execution.
- `REAL_TARGET` — execution on the owner's actual Windows FCC/`fcc-claude` environment from an exact clean source HEAD. This is the only classification accepted as authoritative target evidence for P04-008.

Synthetic and target evidence must never be conflated.

## Permanent cloud/CI suite

The permanent Windows CI stage is:

```powershell
.\tools\runtime\validate-fcc-runtime-contract-suite.ps1 -RunFixtures -RequireRuntime
```

The validator checks the tracked target harness and target-runner safety contract, then builds a disposable fake `fcc-claude` executable and executes the same headless harness used by the real-target lane. The synthetic fixture must PASS all five cross-path scenarios and must mark its evidence `SELF_TEST_ONLY`.

The previously integrated P04 component validators remain permanent CI stages as well. P04-008 does not replace or weaken their detailed negative/recovery coverage.

## Required aggregate scenarios

The harness executes these scenarios in order:

1. **structured success / streaming / session identity** — primary structured runtime completes successfully, emits an event stream and yields a session identity;
2. **resume** — a second structured request resumes using the prior session identity and completes successfully;
3. **real invalid-session failure** — an invalid resume identity must produce a truthful failed terminal result rather than false success;
4. **cancellation** — after the first structured event is observed, cancellation must terminate the owned execution and return `Cancelled`;
5. **fallback after structured failure** — the plain CLI fallback path must then complete independently, proving the compatibility path can be selected without UI coupling.

The target runner does not manufacture provider 429 traffic. Rate-limit evidence remains explicitly `NOT_INDUCED`; existing classifier behavior and historical P00 policy are not rewritten by this task.

## Authoritative target command

After the cloud implementation is integrated onto canonical `main`, run from the owner Windows target in an exact source checkout:

```powershell
pwsh -NoProfile -File .\tools\runtime\run-p04-runtime-target-validation.ps1
```

The target runner:

- refuses non-Windows execution;
- verifies the repository root and exact Git HEAD;
- refuses uncommitted executable-input changes, allowing only prior outputs under `evidence/phases/P04/runtime-contract/`;
- requires .NET SDK `10.0.400`;
- requires P04 to remain current and P04-008 to retain target-validation ownership;
- discovers the real installed `fcc-claude` through production discovery code;
- executes the tracked harness with `REAL_TARGET` classification;
- validates all five scenarios fail closed;
- writes sanitized JSON plus a Markdown summary under `evidence/phases/P04/runtime-contract/`;
- records the exact tested repository SHA;
- never writes raw prompts, raw event payloads, credentials or session identifiers to evidence (session identity is represented only by a short SHA-256-derived hash where needed).

A non-PASS scenario leaves P04-008 open. A cloud worker must never substitute `SELF_TEST_ONLY` results for this target execution.

## Closure boundary

`FCCD-P04-008` may become CLOSED only after:

1. the P04-008 implementation is integrated on canonical `main`;
2. exact-main Windows CI is green with the permanent aggregate synthetic suite;
3. the authoritative target runner is executed on the owner Windows machine from an exact source SHA;
4. sanitized `REAL_TARGET` evidence is integrated and ancestry/provenance is verified;
5. the task ledger/current-phase evidence are reconciled with no task-local regression.

Even after P04-008 closes, **the P04 exit gate is separate**. The exact-head P04 closure gate must still verify all mandatory P04 tasks, current target evidence, green canonical CI and zero P04-local blockers before advancing the phase.

**P05 remains prohibited** until canonical P04 closure records `PHASE_EXIT_GATE=PASS`. Therefore `FCCD-P05-007 — Markdown/code/diff content rendering` cannot begin merely because this contract-suite infrastructure exists.
