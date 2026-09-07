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
CURRENT_PHASE: P08
CURRENT_PHASE_NAME: Terminal/process supervision
CURRENT_PHASE_STATE: IN_PROGRESS
NEXT_PHASE: P09
PHASE_EXIT_GATE: NOT_RUN
KNOWN_RELEASE_BLOCKERS: 2
VERIFIED_FINAL_COMPLETE: false
OWNER_LAST_MODE: ACTIVE
DEFERRED_OWNER_ACCEPTANCE_COUNT: 2
DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET;OWNER-P05-EXIT-REAL-TARGET
DEFERRED_PHASE_GATES: P04=NOT_RUN;P05=NOT_RUN
VERIFIED_IMPLEMENTATION_COMPLETION: 0%
PUBLIC_RELEASE_ELIGIBLE: false
```

`CURRENT_PHASE.md` is the fast live resume checkpoint. `docs/EXECUTION_PLAN.md` is the canonical sequential execution contract. While `OWNER_LAST_MODE: ACTIVE`, `docs/OWNER_LAST_EXECUTION_POLICY.md` is the narrow owner-authorized scheduling amendment for genuinely environment-bound evidence only; it does not weaken task, phase, acceptance, or release criteria.

P00, P01, P02, and P03 are canonically CLOSED with their phase closure evidence retained under `evidence/phases/P00/CLOSURE.md`, `evidence/phases/P01/CLOSURE.md`, `evidence/phases/P02/CLOSURE.md`, and `evidence/phases/P03/CLOSURE.md`. P04 remains acceptance-unresolved solely because `FCCD-P04-008 — Runtime contract suite` still requires fresh genuine owner-Windows/provider `REAL_TARGET` evidence. `FCCD-P04-001` through `FCCD-P04-007` are CLOSED; `FCCD-P04-008` remains PENDING, its P04 exit gate remains `NOT_RUN`, and its owner-only obligation is queued one-to-one as `OWNER-P04-008-REAL-TARGET` in `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` with `releaseBlocking=true`.

P05 cloud implementation is complete and integrated: `FCCD-P05-001` through `FCCD-P05-008` are CLOSED, PR #140 normally merged as `6e85cc2941612937365bbaedc9e4370e9e1510e6`, and exact post-merge Windows CI run `33988198377` completed SUCCESS. The only remaining P05 exit-gate evidence requires genuine owner Windows/FCC/provider interaction: a real application task, structured execution, stop/retry, close/reopen, and durable session resume. That phase-gate obligation is queued as `OWNER-P05-EXIT-REAL-TARGET` with `releaseBlocking=true`; P05's exit gate remains `NOT_RUN` and no P05 phase PASS is claimed.

P06 is canonically CLOSED: `FCCD-P06-001` through `FCCD-P06-008` are CLOSED, dedicated exact-candidate P06 phase-exit run `34030997937` completed SUCCESS, closure PR #160 was normally merged as `38f01c2c07104b1e169a8fd4606f374e499cafc7`, and exact post-merge Windows CI run `34031863567`, Workspace Search run `34031863569`, and Large Workspace Safeguards run `34031863551` all completed SUCCESS. Closure evidence is `evidence/phases/P06/CLOSURE.md`.

P07 — Change review + Git — is canonically CLOSED: `FCCD-P07-001` through `FCCD-P07-011` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable task reconciliation. Exact immutable phase candidate `7561dd88b16531403a9f8f5667db17801105687f` passed pre-closure Windows CI `34068325212` / #431, Workspace Search `34068325218` / #160, and Large Workspace Safeguards `34068325246` / #144; dedicated P07 phase-exit run `34068796895` / job `101582228434` completed SUCCESS; closure PR #187 was normally merged as `e94f241b75ab7119bbb45f48872d24b78c5f9007`; and exact post-closure Windows CI `34069973813` / #433, Workspace Search `34069973830` / #162, and Large Workspace Safeguards `34069973823` / #146 all completed SUCCESS. Closure evidence is `evidence/phases/P07/CLOSURE.md`.

P08 — Terminal/process supervision — is now the single active cloud implementation/convergence phase. `FCCD-P08-001 — Process supervisor with owned process-tree tracking` is CLOSED after implementation PR #189, post-merge regression repair PR #190, and exact accepted-main Windows CI `34074668199`, Workspace Search `34074668196`, and Large Workspace Safeguards `34074668191` all completed SUCCESS on `ac54e739019e7264db5de3f9b26b700735924bc1`. `FCCD-P08-002 — Graceful→forced cancellation escalation` is CLOSED after implementation PR #192, post-merge regression recovery PR #193, and exact accepted-main Windows CI `34079056645`, Workspace Search `34079056639`, and Large Workspace Safeguards `34079056670` all completed SUCCESS on `4f80433830684966405c7d76aea50583ae4df75b`; the repair was limited to the bounded P05-005 hosted-Windows settlement fixture and did not weaken P08 production semantics. `FCCD-P08-003` through `FCCD-P08-008` remain PENDING. Workers must select dependency-valid unclaimed P08 work while preserving owned-process boundaries, bounded output, cancellation escalation, interactive terminal safety, and owner work. P09 and later implementation remain prohibited until P08 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes.

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

Only the phase recorded in `CURRENT_PHASE.md` is authorized for implementation. When owner-last mode is active, that means exactly one current **cloud implementation phase**; any earlier/current unresolved owner-only task or phase-gate obligation must be represented by a valid environment-bound `QUEUED`, `releaseBlocking=true` item under `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` according to `docs/OWNER_LAST_EXECUTION_POLICY.md`.

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

Ordinary phase closure still requires:

```text
ALL CURRENT-PHASE TASKS = CLOSED
AND PHASE EXIT GATE = PASS
AND EXACT-HEAD EVIDENCE RECORDED
AND MAIN = GREEN
AND KNOWN PHASE-LOCAL RELEASE BLOCKERS = 0
```

The only scheduling exception is `docs/OWNER_LAST_EXECUTION_POLICY.md`: after every cloud-actionable requirement in a source phase is integrated and green, genuinely environment-bound residual evidence may be deferred one-to-one into the canonical owner queue while the source task/gate remains truthfully unresolved. Cloud implementation then advances only to the next sequential phase; this never creates phase PASS or release PASS. P22 remains prohibited while any required owner queue item is `QUEUED`.

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