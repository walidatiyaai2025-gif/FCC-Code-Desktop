# FCC Code Desktop — Project Control

**PROJECT_ID:** `FCC_CODE_DESKTOP`  
**DISPLAY_NAME:** `FCC Code Desktop`  
**REPOSITORY:** `walidatiyaai2025-gif/FCC-Code-Desktop`  
**DEFAULT_BRANCH:** `main`  
**TARGET_PLATFORM:** `Windows 10/11 x64`  
**TARGET_RELEASE:** `v1.0.0 Production`  
**SOURCE_OF_TRUTH:** this repository  
**INITIALIZED:** `2026-08-31`

---

## 1. Canonical mission

Deliver a complete premium Windows desktop application that uses the owner's existing local `fcc-claude` / FCC setup as the coding-agent runtime and provides a polished graphical AI development environment intended to replace day-to-day dependence on Codex-like tooling.

The product is not limited to editing source files. It must coordinate repositories, build systems, debuggers, game engines, 3D creation tools, device/emulator tooling, logs, tests and external developer applications through safe extensible integrations.

The owner supervises outcomes. AI workers research, choose, implement, verify and document normal technical decisions autonomously.

---

## 2. Current canonical status

```text
PROJECT_STATE: SPECIFICATION_AND_CONTROL_BASELINE
RELEASE_STATE: NOT_RELEASED
TARGET_VERSION: 1.0.0
CURRENT_PHASE: P04
CURRENT_PHASE_NAME: FCC / fcc-claude runtime core
CURRENT_PHASE_STATE: IN_PROGRESS
NEXT_PHASE: P05
PHASE_EXIT_GATE: NOT_RUN
KNOWN_RELEASE_BLOCKERS: 0
VERIFIED_FINAL_COMPLETE: false
VERIFIED_IMPLEMENTATION_COMPLETION: 0%
PUBLIC_RELEASE_ELIGIBLE: false
```

`CURRENT_PHASE.md` is the fast live resume checkpoint. `docs/EXECUTION_PLAN.md` is the canonical sequential execution contract.

P00, P01, P02, and P03 are canonically CLOSED with their phase closure evidence retained under `evidence/phases/P00/CLOSURE.md`, `evidence/phases/P01/CLOSURE.md`, `evidence/phases/P02/CLOSURE.md`, and `evidence/phases/P03/CLOSURE.md`. P03 closure was integrated by PR #85 as canonical merge `62d3162d31cad6ff8c1d52897cf81a93e57bceed`, and exact post-closure canonical main Windows CI run `33822291095` completed SUCCESS on that merge SHA. P04 is the sole legal implementation phase. `FCCD-P04-001` is CLOSED from validated implementation PR #91, exact candidate Windows CI run `33825468339`, normal merge `c7453dc64304ee149ea1a98b4736043fe644441c`, exact post-merge Windows CI run `33826581291`, and current-main non-regression Windows CI run `33826972327`; task evidence is `evidence/phases/P04/P04_001_INTEGRATED_RECONCILIATION_2026-09-04.md`. `FCCD-P04-002` is CLOSED from implementation PR #94 exact candidate `7b28a0bdbc76a092ae0df372cb780eb235ef525a`, Windows CI run `33826612463` / run #124, normal merge `0bc04b69838a390386e3cda17bf094ff7817e2ae`, exact post-merge Windows CI run `33826972327` / run #125, and current-main non-regression Windows CI run `33828658981` / run #127 on `e5b6c3e3f9ed9714358a0b402be0b961a9393d5b`; task evidence is `evidence/phases/P04/P04_002_INTEGRATED_RECONCILIATION_2026-09-04.md`. `FCCD-P04-003` is CLOSED from implementation PR #97 exact candidate `3a017c0eec34bd9c80d3dc6ef6e16ec564939e4f`, Windows CI run `33831874827` / run #131 attempt 2, normal merge `8fd24dc124aaca134f19499dae4df3021b63a2fb`, and exact post-merge Windows CI run `33833049188` / run #132; candidate and exact-main Release builds passed with 0 warnings/0 errors, unit tests 16/16, integration tests 37/37, and the permanent FCC structured-runtime static/negative/recovery/Windows executable fixture plus complete Windows baseline PASS. Task evidence is `evidence/phases/P04/P04_003_INTEGRATED_RECONCILIATION_2026-09-04.md`. `FCCD-P04-004` is CLOSED after implementation PR #106 exact repaired candidate `699749679fe9a4b970e94f3fa18992c12989fe8d` passed Windows CI run `33836177846` / run #137, was normally merged as `30df27e493cb0f4ef9c9d1de7afcb5158a7e7093`, and exact post-merge Windows CI run `33836542523` / run #138 completed SUCCESS on that merge SHA; candidate and exact-main Release builds passed with 0 warnings/0 errors, unit tests 16/16, integration tests 37/37, and the permanent FCC CLI-fallback static/negative/recovery/Windows executable fixture plus complete Windows baseline PASS. Earlier run #136 failed only because the disposable fake fallback fixture referenced nonexistent .NET API `Console.ErrorEncoding`; the fixture-only compile defect was repaired without weakening production behavior or validation. Task evidence is `evidence/phases/P04/P04_004_INTEGRATED_RECONCILIATION_2026-09-04.md`. `FCCD-P04-005` is CLOSED after implementation PR #108 initial exact head `ec173f27bb8a8676d2e227d884f812f7a78a9dd9` exposed a task-local static-validator false positive in Windows CI run `33839726434` / run #144 after Release build 0 warnings/0 errors, unit tests 16/16, and integration tests 37/37 had already passed. The static guard was repaired without weakening production redaction or executable redaction assertions; repaired exact head `5e733d7424a73e02d3c03a86abf5c076b64b4552` passed Windows CI run `33841968757` / run #147, was normally merged as `bba771de1e10ac702d73a6bdc20bb2143eddc526`, and exact post-merge canonical-main Windows CI run `33842288621` / run #148 completed SUCCESS. The permanent normalization static/negative/recovery/Windows executable fixture and the complete Windows baseline passed. Task evidence is `evidence/phases/P04/P04_005_INTEGRATED_RECONCILIATION_2026-09-04.md`. `FCCD-P04-006` through `FCCD-P04-008` remain PENDING unless separately and canonically reconciled. ADR-017 and the authoritative P00 FCC/`fcc-claude` target contract evidence govern runtime architecture, while P04 must produce its own full real-runtime exact-head contract/exit-gate evidence before closure. `PHASE_EXIT_GATE=NOT_RUN` and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.

---

## 3. Complete v1.0.0 boundary

The first public release includes all of the following domains:

1. Premium Windows product shell and design system
2. Professional original product identity, icon and setup branding
3. First-run environment/runtime diagnostics
4. FCC/`fcc-claude` compatibility and supervision
5. Agent runtime abstraction with a primary and fallback path
6. Streaming conversation UI and structured agent activity
7. Projects/workspaces
8. Persistent sessions and resume
9. Explicit task lifecycle/state machine
10. File explorer
11. Code editor
12. Workspace search
13. Change tracking and diff review
14. Integrated terminal
15. Git workflows
16. Permission profiles and destructive-operation safeguards
17. Global serial execution queue
18. Inter-run cooldown and rate-limit recovery
19. Cancellation and process-tree control
20. Crash/restart/reboot recovery
21. SQLite persistence with migrations and backup
22. Structured logging and sanitized diagnostics
23. Security/privacy and secret redaction
24. Large-repository and long-output performance controls
25. Keyboard/accessibility/high-DPI responsiveness
26. External Developer Tool Gateway
27. First-class Unity development/debug adapter
28. First-class Blender 3D creation/automation adapter
29. Unity↔Blender AI asset pipeline
30. Extensible adapters for build/test/debug/device/browser tooling
31. Professional first-public-version setup executable
32. Upgrade and uninstall behavior
33. Automated quality/runtime contract testing
34. Exact-head release verification
35. Clean-machine installer acceptance

These are mandatory v1 scope, not stretch goals.

---

## 4. Codex-replacement requirement

FCC Code Desktop is a complete local AI development workbench, not a chat wrapper.

The agent must be able, when tools are installed and a project requires them, to:

- discover supported local developer/content-creation tools,
- invoke builds and tests,
- start/stop controlled processes,
- inspect structured/textual logs,
- run debuggers or debugger adapters where integration permits,
- interact with device/emulator tools,
- control development applications through supported integration contracts,
- create/modify/validate 3D assets through Blender automation,
- receive machine-readable results back into the agent task,
- correlate errors with source/content changes,
- rerun validation automatically,
- expose actions and artifacts in the UI activity timeline.

All such capabilities use project-owned abstractions rather than one-off shell commands in UI code.

```text
Agent
  │
  ▼
Tool Gateway
  ├── Process/CLI Adapter
  ├── Build/Test Adapter
  ├── Debug Adapter
  ├── Browser Adapter
  ├── Device/Emulator Adapter
  ├── protocol seams where justified (DAP/MCP/etc.)
  └── First-class product adapters
        ├── Unity Adapter
        └── Blender Adapter
```

---

## 5. Unity v1 requirement

Unity is a mandatory first-class external-development-tool target in v1.

For a Unity repository, FCC Code Desktop must detect the project and installed Unity environment, expose Unity health, and allow the agent to perform supported automated development/debug loops without routine manual owner operation.

The baseline includes:

- detect Unity project and requested editor version,
- resolve compatible local/Hub-managed editor installations,
- launch with correct project path,
- controlled batch operations,
- dedicated log capture and classification,
- compile validation,
- EditMode and PlayMode tests where supported,
- project-owned Editor automation entry points,
- build target execution,
- exit/test/artifact validation,
- structured Unity tool events,
- project/editor resource locking,
- cancellation and recovery,
- version-specific contract tests.

Do not assume behavior is identical across Unity versions; test supported versions/contracts.

---

## 6. Blender v1 requirement

Blender is a mandatory first-class external tool in v1 because the product must support AI-driven game/content production, not source code alone.

The baseline includes:

- discover supported Blender installations/versions,
- interactive launch when visual inspection is required,
- background/headless deterministic execution,
- trusted generated/project-owned Python automation,
- scene/mesh/transform creation and modification,
- materials and scene configuration,
- camera/light operations where required,
- import/export through explicit adapters/scripts,
- still/animation rendering where required,
- console/log/debug capture,
- produced artifact manifests,
- `.blend`/export/render artifact validation,
- structured Python/process failure reporting,
- checkpoint/backup before risky replacement,
- target asset/resource locking,
- cancellation and recovery,
- contract tests against declared supported Blender versions.

Argument ordering and task-specific output validation must be handled structurally. A process exit code alone does not prove success.

---

## 7. Unity + Blender pipeline requirement

FCC Code Desktop must prove an AI-driven 3D workflow across both tools:

```text
Agent task
   ↓
Blender Adapter
   ↓
Create / modify / render / export 3D asset
   ↓
Artifact validation + manifest
   ↓
Unity project asset handoff
   ↓
Unity Adapter
   ↓
Import / compile / tests / build validation
   ↓
Structured results/artifacts
   ↓
Agent evaluates and iterates
```

Success requires validated outputs, not only successful process termination.

---

## 8. External-tool extensibility requirement

Unity and Blender are the first mandatory first-class adapters, not the last.

Architecture must allow later adapters for tools such as:

- Visual Studio / MSBuild / dotnet
- CMake / Ninja
- Node/npm/pnpm/yarn
- Python/pytest
- Java/Gradle
- Android SDK / adb / logcat / emulator
- Docker
- browsers/browser automation
- other game engines
- database CLIs
- local servers/services
- custom project commands

The core product remains usable when optional tools are absent.

---

## 9. Explicitly deferred beyond v1

Unless required for v1 quality:

- cloud sync,
- team accounts,
- multi-user collaboration,
- remote agents,
- mobile app,
- public plugin marketplace,
- SaaS telemetry/analytics,
- multi-machine synchronization,
- parallel multi-agent execution,
- full FCC Admin reimplementation,
- direct ownership of provider credentials already managed by FCC.

---

## 10. Hard invariants

### 10.1 Serial agent execution

```text
GLOBAL_AGENT_CONCURRENCY = 1
DEFAULT_INTER_RUN_COOLDOWN_SECONDS = 15
```

Only one active coding-agent run executes at a time by default. External resource locks may be stricter.

### 10.2 Single current project phase

```text
ACTIVE_PROJECT_PHASE_COUNT = 1
```

Only the phase recorded in `CURRENT_PHASE.md` is authorized for implementation.

Multiple workers may execute non-overlapping tasks inside that phase, but the project may not have implementation teams working ahead in later phases.

### 10.3 Repository continuity

The repository must always contain enough durable state for a new worker to continue without prior chat history.

### 10.4 No partial public product

Internal builds may be incomplete. Public product versions may not.

The first public version is `1.0.0` and is ineligible until every mandatory gate passes.

### 10.5 No silent destructive operations

User code/data/assets must not be destroyed to simplify automation.

### 10.6 External applications are controlled resources

Every controlled external tool requires lifecycle ownership, process identity, timeout/heartbeat strategy where applicable, logs, cancellation, result classification, resource locks, cleanup/recovery and artifact validation.

---

## 11. Strict stage-gated delivery sequence

The detailed phase contract and exit criteria live in `docs/EXECUTION_PLAN.md`.

The canonical sequence is:

```text
P00  Constitution + external contract de-risking
P01  Solution foundation + CI
P02  Premium design system + shell
P03  Persistence + canonical state model
P04  FCC/fcc-claude runtime core
P05  Conversation + session + task UX
P06  Projects + files + editor + search
P07  Change review + Git
P08  Terminal + process supervision
P09  External Tool Gateway
P10  Unity first-class adapter
P11  Blender first-class adapter
P12  Unity↔Blender AI asset pipeline
P13  Permissions + side-effect safety
P14  Global queue + cooldown + throttling
P15  Crash/reboot recovery + backups
P16  Diagnostics + security + performance
P17  Premium UX closure
P18  Product identity + professional setup
P19  Upgrade + uninstall + repair lifecycle
P20  Full regression + exact-head candidate verification
P21  Clean-machine + provenance acceptance
P22  v1.0.0 release closure
```

### Phase advancement invariant

```text
ALL CURRENT-PHASE TASKS = CLOSED
AND PHASE EXIT GATE = PASS
AND EXACT-HEAD EVIDENCE RECORDED
AND MAIN = GREEN
AND KNOWN PHASE-LOCAL RELEASE BLOCKERS = 0
```

Only then can `CURRENT_PHASE.md` advance.

There is no authorized cross-phase implementation parallelism.

If later work breaks a previously closed guarantee, forward advancement stops until that regression is fixed and impacted tests are rerun.

---

## 12. Resume protocol after interruption

A new worker must:

1. Fetch live `main`.
2. Read `AGENTS.md`.
3. Read `CURRENT_PHASE.md`.
4. Read `PROJECT_CONTROL.md` and `docs/EXECUTION_PLAN.md`.
5. Read the remaining canonical docs in `README.md` order.
6. Inspect `docs/TASK_LEDGER.md`.
7. Inspect recent commits, branches, PRs and issues.
8. Build a claim map.
9. Continue one legitimate incomplete task belonging to the current phase only.
10. Update durable repository state when material facts change.

Never restart from an old prompt when live repository state exists. Never advance to a later phase because old context is missing.

---

## 13. Phase closure evidence

Every phase requires a closure artifact using `docs/PHASE_CLOSURE_TEMPLATE.md` and stored under:

```text
evidence/phases/PXX/CLOSURE.md
```

It records the exact tested commit, mandatory tasks, commands/results, environment evidence, negative paths, recovery/safety results, known defects/regressions and explicit exit decision.

No evidence = no phase closure.

---

## 14. Final release condition

Final status may become:

```text
VERIFIED_FINAL_COMPLETE
```

only when:

- P00 through P22 are validly closed in sequence,
- the exact release commit passes every mandatory automated/manual gate,
- the exact installer from that commit passes clean-machine acceptance,
- no mandatory ledger task remains below `CLOSED`,
- no legitimate release blocker remains,
- branding/provenance/setup are final,
- FCC agent, Unity, Blender and Unity↔Blender workflows are operational and recoverable,
- version and installer provenance are recorded.