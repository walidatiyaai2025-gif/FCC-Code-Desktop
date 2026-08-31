# FCC Code Desktop — Sequential Execution Plan

**Status:** CANONICAL  
**Target:** `v1.0.0 Production`  
**Execution model:** strict stage-gated sequential delivery  
**Owner role:** outcome supervision; routine technical decisions are autonomous  
**Source of truth:** this repository

---

## 1. Purpose

This document defines the only valid end-to-end execution order for FCC Code Desktop v1.0.0.

The project must not drift into partially completed subsystems, attractive but disconnected UI, unverified integrations, or release packaging built on unfinished foundations.

The rule is simple:

```text
ONE CURRENT PHASE
      ↓
IMPLEMENT
      ↓
TEST
      ↓
FIX ALL PHASE DEFECTS
      ↓
VERIFY
      ↓
RECORD EVIDENCE
      ↓
CLOSE PHASE
      ↓
ONLY THEN OPEN NEXT PHASE
```

A later phase may be researched only when necessary to remove a blocker in the current phase. It may not become active implementation work, may not be used to inflate progress, and may not be treated as product completion before its turn.

---

## 2. Supreme phase-lock rule

At any moment there is exactly one **CURRENT_PHASE**.

A phase transition is legal only when all mandatory tasks belonging to the current phase are `CLOSED` and the phase exit gate passes.

Forbidden:

- starting feature implementation from a later phase because it is easier or visually attractive,
- leaving known defects behind with the intention of returning later,
- marking a phase complete because most tasks pass,
- accepting an integration with untested error/recovery paths,
- shipping an installer while runtime or tool integrations remain incomplete,
- using screenshots as a substitute for functional verification,
- moving forward with a `BLOCKED`, `IN_PROGRESS`, `IMPLEMENTED`, or merely `VERIFIED` mandatory task in the current phase,
- changing scope silently to make a phase close.

Required phase transition condition:

```text
ALL CURRENT-PHASE TASKS = CLOSED
AND PHASE EXIT GATE = PASS
AND EVIDENCE RECORDED
AND MAIN IS GREEN
AND NO KNOWN PHASE-LOCAL RELEASE BLOCKER
```

Only then:

```text
CURRENT_PHASE = NEXT_PHASE
```

---

## 3. Phase lifecycle

Every phase follows the same lifecycle.

### 3.1 OPEN

The phase becomes current only after the previous phase is closed.

Actions:

1. Fetch live `main`.
2. Reconcile repository state.
3. Read all canonical documents.
4. Confirm previous phase closure evidence.
5. Build the current phase task list from `docs/TASK_LEDGER.md`.
6. Record any newly discovered mandatory work in the ledger before implementation.

### 3.2 IMPLEMENT

Implement only work necessary for the current phase.

Rules:

- production architecture from the first line of code,
- no throwaway production implementation,
- tests are implemented with the feature,
- error paths and cancellation are part of the feature,
- visible UI work must include its required states,
- external contracts are probed rather than guessed.

### 3.3 VERIFY

Run the complete phase-specific verification suite.

Verification must include:

- happy path,
- negative/error paths,
- cancellation where applicable,
- restart/recovery where applicable,
- data integrity where applicable,
- performance sanity where applicable,
- UI state completeness where applicable.

### 3.4 FIX

Any failure reopens the relevant task.

There is no concept of "known acceptable failure for now" inside a phase exit gate unless the canonical specification explicitly declares it non-mandatory.

### 3.5 CLOSE

Before advancing, create a durable closure record under:

```text
evidence/phases/PXX/CLOSURE.md
```

The closure record must contain at minimum:

```text
PHASE
CANDIDATE_SHA
DATE
MANDATORY_TASKS
TEST_COMMANDS
TEST_RESULTS
MANUAL/ENVIRONMENT EVIDENCE WHEN REQUIRED
KNOWN_BLOCKERS = NONE
KNOWN_REGRESSIONS = NONE
EXIT_GATE = PASS
```

The evidence must identify the exact commit tested.

---

## 4. Progress accounting

There are two different progress concepts and they must never be mixed.

### Verified phase progress

A phase contributes to verified implementation progress only after its exit gate is `PASS` and it is closed on the canonical baseline.

### Task activity

Tasks may be `IN_PROGRESS`, `IMPLEMENTED`, or `VERIFIED`, but these states do not make the phase complete.

The project must not report "almost done" from code volume or screenshots.

Canonical progress is acceptance/gate based.

---

# 5. Full sequential v1 plan

---

## P00 — Constitution + external-contract de-risking

### Goal

Remove foundational unknowns before product implementation depends on them.

### Mandatory outcomes

- Repository governance is complete and internally consistent.
- `fcc-claude` discovery/version/health behavior is measured on the real target environment.
- Real streaming behavior is captured.
- Session ID/create/resume behavior is captured.
- Cancel/interrupt/failure/rate-limit behavior is captured.
- Primary agent runtime contract is selected from evidence.
- CLI fallback is proven.
- Unity CLI/test/build/version behavior is probed.
- Blender CLI/background/Python/render/export behavior is probed.
- Compatibility baseline is recorded.

### P00 exit gate

`PASS` only if no P01 architecture decision depends on an unverified assumption about FCC/Claude, Unity, or Blender.

### Deliverable

Contract probes + compatibility report + finalized architectural contracts.

---

## P01 — Solution foundation + CI + engineering guardrails

### Goal

Create the production codebase and prevent quality drift from the first implementation commit.

### Mandatory outcomes

- .NET 10 solution structure.
- Clean project/module boundaries.
- Nullable reference types enforced.
- Analyzer/style policy enforced.
- Dependency version/locking strategy.
- Unit/integration test infrastructure.
- Windows Release CI build/test pipeline.
- Build/version/provenance metadata service.
- Reproducible local build instructions.

### P01 exit gate

Fresh checkout can restore, build and test successfully using documented commands; CI validates the same baseline.

---

## P02 — Premium design system + application shell

### Goal

Establish the final-quality visual and interaction foundation before feature screens multiply.

### Mandatory outcomes

- semantic design tokens,
- typography hierarchy,
- dark/light themes,
- app chrome/titlebar strategy,
- main resizable workspace,
- navigation/project/session/task surfaces,
- bottom tool-panel framework,
- command palette/keyboard framework,
- standardized loading/empty/error/offline/blocked states,
- DPI/responsive foundation.

### Important rule

P02 is not a static mock. Shell interactions must be wired to production view models/state abstractions even if feature data is not yet implemented.

### P02 exit gate

The shell meets the UI standard at target minimum resolution and DPI baseline with no placeholder styling that would require architectural replacement later.

---

## P03 — Persistence + canonical state model

### Goal

Make projects, sessions, tasks, events and recovery durable before runtime complexity is added.

### Mandatory outcomes

- SQLite initialization,
- versioned migrations,
- projects/sessions/messages persistence,
- tasks/agent/tool/process event journal,
- queue persistence,
- settings persistence,
- integrity checks,
- backup rotation,
- migration/recovery tests.

### P03 exit gate

Create → persist → close → reopen → reconcile works for all phase entities, including migration and corruption/backup test cases defined for this phase.

---

## P04 — FCC / `fcc-claude` runtime core

### Goal

Establish a reliable agent runtime independent of UI implementation details.

### Mandatory outcomes

- environment discovery,
- `IAgentRuntime`,
- primary structured runtime adapter,
- CLI fallback adapter,
- normalized runtime events,
- health/version compatibility service,
- start/stop/retry supervision,
- runtime contract test suite.

### P04 exit gate

A headless integration harness can send a real task through the local FCC/Claude environment, stream events, complete, fail, cancel, resume where supported, and switch to the proven fallback path without UI coupling.

---

## P05 — Conversation + session + task experience

### Goal

Turn the proven runtime into a complete usable agent conversation surface.

### Mandatory outcomes

- streamed assistant output,
- structured tool timeline,
- composer/attachments/context foundation,
- create/history/resume sessions,
- explicit task state machine,
- stop/cancel/retry,
- Markdown/code/diff rendering,
- conversation virtualization.

### P05 exit gate

A user can open a project session, issue a real task, observe structured execution, stop/retry where applicable, close/reopen the app and resume without losing durable state.

---

## P06 — Projects + files + editor + search

### Goal

Provide a production workspace surface around agent execution.

### Mandatory outcomes

- add/open/recent projects,
- project technology detection,
- lazy file explorer,
- safe file service,
- locally bundled editor,
- tabs/save/reload/dirty handling,
- content/file/regex search,
- large-file/tree protections.

### P06 exit gate

Real repositories including large trees can be browsed, searched and edited without corrupting files or freezing the main UI under the defined acceptance thresholds.

---

## P07 — Change review + Git

### Goal

Make agent changes inspectable and repository workflows safe.

### Mandatory outcomes

- repository detection,
- status/changed files,
- diff viewer,
- stage/unstage,
- branch create/checkout,
- fetch/pull,
- commit/push,
- history,
- provenance for pre-existing dirty changes,
- destructive-operation safeguards,
- conflict/error tests.

### P07 exit gate

Standard Git workflows and defined conflict/dirty-tree scenarios pass without silently destroying owner work.

---

## P08 — Terminal + process supervision

### Goal

Provide real interactive development-process execution with controlled lifecycle.

### Mandatory outcomes

- owned process-tree supervisor,
- graceful → forced cancellation escalation,
- bounded streaming logs,
- ConPTY terminal,
- PowerShell/CMD profiles,
- optional Git Bash/WSL detection,
- interactive terminal UI,
- process safety tests.

### P08 exit gate

Interactive and non-interactive process scenarios execute, stream, resize, cancel and clean up without orphaned owned processes in the defined test set.

---

## P09 — External Tool Gateway

### Goal

Create the stable extensibility boundary that makes the product a Codex replacement rather than a chat wrapper.

### Mandatory outcomes

- `IExternalToolAdapter`,
- discovery/capability registry,
- typed invocation/result contracts,
- resource locking,
- artifact manifest/validation,
- tool health/diagnostics,
- generic CLI/process primitives,
- protocol seams such as DAP/MCP where justified without core coupling.

### P09 exit gate

A fixture adapter can be discovered, invoked, cancelled, locked, produce validated artifacts/results and expose structured diagnostics through the same contracts Unity and Blender will use.

---

## P10 — Unity first-class adapter

### Goal

Provide autonomous Unity development/debug/build loops.

### Mandatory outcomes

- Unity project/version detection,
- installed editor/Hub resolution,
- strongly typed CLI invocation,
- project/editor resource locks,
- log capture/parser,
- compile validation,
- EditMode tests,
- PlayMode tests,
- project-owned editor automation entry points,
- build execution and artifact validation,
- structured Unity events,
- cancellation/recovery,
- contract fixture/suite.

### P10 exit gate

A controlled Unity fixture can be opened headlessly/appropriately, compiled, tested, automated and built, with failures surfaced structurally and no unsafe competing project instance.

---

## P11 — Blender first-class adapter

### Goal

Allow the AI agent to create, modify, validate, render and export 3D assets autonomously.

### Mandatory outcomes

- Blender installation/version resolution,
- ordered typed CLI builder,
- background/headless runner,
- correlated Python runner,
- scene/mesh/material automation fixture,
- import/export,
- preview/render,
- console/debug parsing,
- `.blend`/export/render artifact validation,
- asset checkpoint/backup,
- resource locking,
- structured events/artifact preview,
- cancellation/recovery,
- contract suite.

### P11 exit gate

A fixture task can create or modify a 3D asset, save `.blend`, render a preview, export the requested artifact and validate the actual outputs; missing/corrupt outputs fail even if Blender exits successfully.

---

## P12 — Unity ↔ Blender AI asset pipeline

### Goal

Prove end-to-end autonomous 3D production rather than isolated tool adapters.

### Mandatory outcomes

- cross-tool orchestration,
- approved artifact manifest/handoff,
- Unity import verification of Blender output,
- broken/missing artifact negative tests,
- end-to-end AI 3D fixture.

### P12 exit gate

Blender-generated/modified content is handed to Unity, imported and verified by the Unity side, with traceable artifacts and deterministic failure when the handoff is invalid.

---

## P13 — Permissions + side-effect safety

### Goal

Make autonomous power safe and understandable.

### Mandatory outcomes

- permission profiles/mapping,
- permission request UX,
- high-risk full-access warnings,
- side-effect classification for files/Git/tools,
- workspace checkpoints,
- unsafe path/argument guards.

### P13 exit gate

Defined high-risk operations cannot bypass policy accidentally, while normal autonomous workflows remain usable.

---

## P14 — Global queue + cooldown + rate limiting

### Goal

Prevent overlapping agents and provider/resource thrashing.

### Mandatory outcomes

- durable global coordinator,
- default concurrency = 1,
- default inter-run cooldown = 15 seconds,
- queue inspect/reorder/cancel,
- rate-limit classification,
- bounded backoff/retry,
- restart reconciliation without duplicate launches,
- stress tests.

### P14 exit gate

Stress and restart tests prove that no second agent launches while another active run owns the global slot and that throttling/backoff cannot create duplicate work.

---

## P15 — Crash/reboot recovery + backups

### Goal

Make interruption recoverable instead of surprising.

### Mandatory outcomes

- durable recovery journal,
- startup reconciliation,
- interrupted agent-run recovery,
- interrupted file/Git mutation recovery,
- interrupted Unity recovery,
- interrupted Blender recovery,
- crash/reboot fault-injection tests,
- automatic DB backup retention/recovery.

### P15 exit gate

Injected crashes/restarts across defined critical operations recover to a truthful safe state with no duplicate agent launch and no silent loss of owner data.

---

## P16 — Diagnostics + security + performance hardening

### Goal

Make failures diagnosable, secrets protected and long-running real use stable.

### Mandatory outcomes

- correlated structured logs,
- sink-boundary secret redaction,
- health/diagnostics center,
- sanitized diagnostic ZIP,
- no-telemetry verification,
- large-repository/search tests,
- long-chat/log memory tests,
- Unity/Blender high-output tests,
- dependency/security review.

### P16 exit gate

Defined load tests pass within thresholds, diagnostics contain actionable evidence, and secret-leak tests find no plaintext protected credentials.

---

## P17 — Premium UX closure

### Goal

Close all visual/interaction debt after the functional product is real.

### Mandatory outcomes

- all component states,
- keyboard/focus/accessibility pass,
- 1366×768 acceptance,
- 1920×1080 acceptance,
- 4K/high-DPI acceptance,
- dark/light parity,
- Unity UX polish,
- Blender/artifact-preview polish,
- perceived-latency polish.

### P17 exit gate

No placeholder/debug/default-WPF experience remains and the full primary product flow passes the visual/interaction acceptance matrix at required sizes and DPI settings.

---

## P18 — Product identity + professional setup

### Goal

Produce release-grade identity and installation experience, not developer packaging.

### Mandatory outcomes

- original premium AI-assisted identity,
- production multi-size `.ico`,
- provenance record,
- installer/bootstrapper architecture,
- branded setup UI,
- application/start-menu/taskbar/version metadata,
- first-run environment checks.

### Important rule

Installer engineering may use internal test packages, but no installer may be presented as a product release before final closure.

### P18 exit gate

A clean install presents professional setup/identity and launches the exact product with environment checks and correct metadata.

---

## P19 — Upgrade + uninstall + repair lifecycle

### Goal

Ensure v1 is maintainable after installation.

### Mandatory outcomes

- in-place upgrade path,
- data-preserving migration/rollback behavior,
- app-only uninstall default,
- explicit safe product-data removal option,
- installer lifecycle automation tests.

### P19 exit gate

Install → use → upgrade → repair/reinstall where supported → uninstall scenarios pass without unintended data destruction.

---

## P20 — Full regression + exact-head candidate verification

### Goal

Stop feature work and prove one immutable release candidate.

### Mandatory outcomes

- all non-environment suites green,
- FCC runtime contracts green,
- Unity contracts green,
- Blender contracts green,
- Unity↔Blender E2E green,
- UI/accessibility automation green,
- freeze candidate SHA,
- rerun all required gates against that exact SHA.

### P20 exit gate

One exact immutable SHA has a fully green required automated verification set. Any code change invalidates this gate and returns the project to the appropriate earlier phase/regression work.

---

## P21 — Clean-machine + provenance acceptance

### Goal

Prove the product outside the development environment.

### Mandatory outcomes

- build production installer from the frozen exact candidate SHA,
- verify installer hash/provenance,
- install on clean supported Windows environment without development SDK assumptions,
- discover/use supported external FCC environment correctly,
- execute primary coding workflow,
- execute persistence/restart workflow,
- execute representative Unity workflow on a suitably provisioned validation machine,
- execute representative Blender workflow on a suitably provisioned validation machine,
- verify upgrade/uninstall behavior as required,
- capture final acceptance evidence.

### P21 exit gate

The production installer built from the exact candidate SHA passes all mandatory clean-machine acceptance rows with zero legitimate release blocker.

---

## P22 — v1.0.0 release closure

### Goal

Perform final reconciliation and release only what has actually been proven.

### Mandatory outcomes

- reconcile every mandatory ledger item,
- all mandatory tasks `CLOSED`,
- acceptance matrix fully green,
- no open legitimate release blocker,
- exact release SHA recorded,
- installer SHA-256 recorded,
- version `1.0.0` consistent everywhere,
- release notes/provenance complete,
- repository state documents updated,
- final release/tag created only after all previous conditions pass.

### P22 exit gate

Only when every condition passes may project status become:

```text
VERIFIED_FINAL_COMPLETE
```

and the finished product artifact be presented as:

```text
FCCCodeDesktop-Setup-1.0.0.exe
```

---

# 6. Regression rule after phase closure

Closing a phase does not permit later work to break it.

If a later phase introduces a regression in an earlier closed phase:

1. Stop advancement.
2. Mark the affected acceptance gate failed.
3. Open a regression task in the canonical ledger.
4. Fix and reverify the affected earlier contract.
5. Rerun all downstream tests impacted by the change.
6. Only then resume the current phase.

The historical phase does not need to be cosmetically renumbered as current, but its guarantees must be restored before forward progress resumes.

---

# 7. Blocker rule

A blocker is genuine only when it cannot be resolved autonomously with available repository/code/tool/environment access.

Examples:

- required credential unavailable,
- required physical device/hardware unavailable,
- required local FCC/Unity/Blender target environment inaccessible to the executing worker,
- owner-only legal/license/account authorization.

A technical difficulty, failing test, unclear library choice or complicated bug is **not** a blocker; it is work inside the current phase.

When genuinely blocked:

```text
CURRENT_PHASE remains unchanged
BLOCKED task is recorded
exact missing prerequisite is documented
no later phase is promoted to CURRENT_PHASE
```

---

# 8. Multi-worker rule under sequential execution

Multiple workers may be used only inside the **same current phase**, with non-overlapping task ownership.

Before claiming work each worker must build a live claim map from branches/PRs/ledger.

Parallel workers must not allow the project to advance into different phases independently.

A phase controller/lead reconciles all current-phase outputs to the canonical baseline, reruns the full phase gate, records evidence, and closes the phase.

---

# 9. Required status block

`PROJECT_CONTROL.md` must always make the current phase visible using a block equivalent to:

```text
CURRENT_PHASE: P00
CURRENT_PHASE_STATE: OPEN | IN_PROGRESS | VERIFYING | BLOCKED | CLOSED
NEXT_PHASE: P01
PHASE_EXIT_GATE: NOT_RUN | FAIL | PASS
KNOWN_RELEASE_BLOCKERS: <count>
VERIFIED_FINAL_COMPLETE: false
```

Workers must update durable state when the phase changes.

---

# 10. Day-one execution start

The project starts here:

```text
CURRENT_PHASE = P00
```

No product implementation phase is authorized until P00 contract de-risking closes.

The very first implementation-oriented activity therefore is not random UI or feature coding. It is completing the real FCC/Claude, Unity and Blender contract probes and freezing the compatibility/adapter decisions that the rest of the product will depend on.

This is intentional: eliminate architectural surprises first, then build forward once.