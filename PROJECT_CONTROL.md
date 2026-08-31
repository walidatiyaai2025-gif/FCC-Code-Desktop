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

Deliver a complete premium Windows desktop application that uses the user's existing local `fcc-claude` / FCC setup as the coding-agent runtime and provides a polished graphical coding-agent environment intended to replace the owner's day-to-day dependence on Codex-like tooling.

The product must not be limited to editing source files. It must coordinate real development workflows across repositories, build systems, debuggers, game engines, 3D creation tools, device/emulator tooling, logs, tests and external developer applications through a safe extensible tool-integration layer.

The user supervises outcomes. AI workers are expected to research, choose, implement, verify and document routine technical decisions autonomously.

---

## 2. Current canonical status

```text
PROJECT_STATE: SPECIFICATION_AND_CONTROL_BASELINE
RELEASE_STATE: NOT_RELEASED
TARGET_VERSION: 1.0.0
VERIFIED_FINAL_COMPLETE: false
VERIFIED_IMPLEMENTATION_COMPLETION: 0%
PUBLIC_RELEASE_ELIGIBLE: false
```

Documentation/bootstrap commits do not count as implementation completion.

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
29. Extensible adapters for build/test/debug/device/browser tooling
30. Professional first-public-version setup executable
31. Upgrade and uninstall behavior
32. Automated quality/runtime contract testing
33. Exact-head release verification
34. Clean-machine installer acceptance

These are mandatory v1 scope, not stretch goals.

---

## 4. Codex-replacement requirement

FCC Code Desktop must be designed as a complete local AI development workbench, not a chat wrapper.

The agent must be able, when tools are installed and the project type requires them, to:

- discover supported local developer and content-creation tools,
- invoke build and test systems,
- start/stop controlled processes,
- inspect structured and textual logs,
- run debuggers or debugger adapters when integration permits,
- interact with device/emulator tools,
- open or control development applications through supported integration contracts,
- create/modify/validate 3D assets through supported Blender automation,
- receive machine-readable results back into the agent task,
- correlate errors with source/content changes,
- rerun validation automatically,
- expose all such actions in the UI activity timeline.

This capability must be implemented through project-owned abstractions rather than hard-coded one-off shell commands inside UI code.

Conceptual boundary:

```text
Agent
  │
  ▼
Tool Gateway
  ├── Process/CLI Adapter
  ├── MCP Adapter (when supported/useful)
  ├── Build/Test Adapter
  ├── Debug Adapter
  ├── Browser Adapter
  ├── Device/Emulator Adapter
  └── Product-specific Adapters
        ├── Unity Adapter (v1 first-class)
        └── Blender Adapter (v1 first-class)
```

---

## 5. Unity v1 requirement

Unity is a first-class external-development-tool target in v1.

For a Unity repository, FCC Code Desktop must detect the project and installed Unity environment, expose Unity-related health, and allow the agent to perform supported automated development/debug loops without requiring the owner to manually operate Unity for routine checks.

The Unity integration baseline includes:

- detect Unity projects from canonical project files,
- detect the project's requested Unity Editor version,
- resolve compatible local Unity installations / Unity Hub-managed editors,
- launch Unity with the correct project path,
- controlled batch-mode operations,
- capture/stream dedicated Unity log output,
- read and classify `Editor.log`/Player logs where appropriate,
- compile validation,
- EditMode and PlayMode automated test execution where the project supports tests,
- invoke explicitly project-owned Editor automation entry points through supported Unity command-line facilities,
- build target execution when requested by the task,
- collect exit codes, test-result files and logs,
- surface Unity failures as structured task/tool events,
- prevent competing automation from opening the same project in unsafe concurrent editor instances,
- support a project-local bridge/package later if richer live-editor communication is needed, without coupling the core UI to Unity internals.

Unity's command-line interfaces support project selection, batch execution and static Editor method execution. The product must validate behavior against the actual installed Unity version rather than assume all releases behave identically.

---

## 6. Blender v1 requirement

Blender is also a first-class external tool in v1 because FCC Code Desktop is intended to support AI-driven game/content production, not source code alone.

For Blender work, the agent must be able to automate routine 3D creation and validation workflows with minimal/no human interaction when the task is technically automatable.

The Blender integration baseline includes:

- detect supported local Blender installations and versions,
- launch Blender interactively when visual inspection is required,
- launch Blender in background/headless mode for deterministic automation,
- execute trusted generated/project-owned Python scripts through Blender's Python interface,
- create scenes and assets from scripts,
- create/modify mesh objects and transforms,
- create/update materials and scene configuration,
- operate cameras/lights where a task requires it,
- import supported source assets and export required game-pipeline formats through explicit adapters/scripts,
- render stills/animations for validation when requested,
- capture Blender console/log/debug output,
- collect produced files and machine-readable operation manifests,
- validate that expected `.blend`/export/render artifacts were produced and are non-empty/readable,
- surface script exceptions and Blender process failures as structured agent tool results,
- avoid overwriting valuable `.blend` files without checkpoint/backup policy,
- track process ownership and prevent unsafe concurrent writes to the same target asset/project,
- support a future project-owned Blender add-on/bridge for richer live-session control while keeping the core independent of Blender internals.

Blender's official CLI supports background operation, rendering, logging/debug controls and Python-driven automation. Argument ordering is significant, so the adapter must build and test commands structurally rather than concatenate arbitrary strings.

The initial adapter must include contract tests against at least one supported Blender LTS/current version and must degrade clearly when Blender is absent or incompatible.

---

## 7. Unity + Blender pipeline requirement

FCC Code Desktop must support an AI-driven Unity content pipeline in which Blender-generated assets can be produced, validated, exported and then consumed/validated inside a Unity project.

Conceptual loop:

```text
Agent task
   ↓
Blender Adapter
   ↓
Create / modify / render / export 3D asset
   ↓
Artifact validation + manifest
   ↓
Unity project Assets pipeline
   ↓
Unity Adapter
   ↓
Import / compile / tests / build or scene validation
   ↓
Logs + screenshots/artifacts where available
   ↓
Agent evaluates result and iterates
```

No adapter may silently report success merely because a process exited. Success requires task-specific artifact/output validation.

---

## 8. External-tool extensibility requirement

Unity and Blender are the first v1 first-class adapters, not the last.

Architecture must allow adapters for tools such as:

- Visual Studio / MSBuild / dotnet
- CMake / Ninja
- Node/npm/pnpm/yarn
- Python/pytest
- Java/Gradle
- Android SDK / adb / logcat / emulator
- Docker
- browsers and browser automation
- other game engines
- database CLIs
- local servers/services
- custom project commands

The core product must remain usable if optional tools are absent.

---

## 9. Explicitly deferred beyond v1

Unless required to satisfy v1 quality:

- cloud sync,
- team accounts,
- multi-user collaboration,
- remote agents,
- mobile app,
- public plugin marketplace,
- SaaS telemetry/analytics,
- multi-machine state synchronization,
- parallel multi-agent execution,
- full reimplementation of FCC Admin,
- direct ownership of provider credentials already managed by FCC.

---

## 10. Hard invariants

### 10.1 Serial execution

```text
GLOBAL_AGENT_CONCURRENCY = 1
DEFAULT_INTER_RUN_COOLDOWN_SECONDS = 15
```

Only one active agent run executes at a time by default. A second conversation remains queued until the prior run is terminal and cooldown expires.

External resource locks may impose stricter serialization (for example, the same Unity project or Blender target asset cannot be modified concurrently).

### 10.2 Repository continuity

The repository must always contain enough current state that a new AI worker can continue without previous chat history.

### 10.3 No partial public product

Internal builds may be incomplete. Public product versions may not.

The first public product version is `1.0.0` and is not eligible until every mandatory acceptance gate passes.

### 10.4 No silent destructive operations

User code/data/assets must not be destroyed to simplify automation.

### 10.5 External applications are controlled resources

Any external tool started by the product must have:

- explicit lifecycle ownership,
- process identity tracking,
- timeout/heartbeat strategy where applicable,
- log capture,
- cancellation strategy,
- result classification,
- concurrency/resource-lock rules,
- cleanup/recovery behavior,
- artifact validation.

---

## 11. Delivery phases

Phases are ordered for dependency risk reduction, but implementation may be parallelized only where ownership is non-overlapping.

```text
P00  Product constitution + runtime/tool contract probes
P01  Solution foundation + CI + quality gates
P02  Design system + premium shell
P03  Persistence + task/session state model
P04  FCC/fcc-claude supervisor + runtime adapter
P05  Streaming chat + tool activity + session resume
P06  Projects/files/editor/search
P07  Changes/diff + Git
P08  Terminal + process supervision
P09  Tool Gateway core
P10  Unity first-class adapter + Unity contract suite
P11  Blender first-class adapter + Blender contract suite
P12  Unity↔Blender asset-pipeline acceptance workflow
P13  Permissions + destructive-action safety
P14  Global queue + cooldown + rate-limit control
P15  Crash/reboot recovery + journaling/backups
P16  Diagnostics/security/performance hardening
P17  Premium UX closure + accessibility/high DPI
P18  Professional branding/icon + setup/bootstrapper
P19  Upgrade/uninstall/repair
P20  Full automated regression + exact-head CI
P21  Clean-machine acceptance + release provenance
P22  v1.0.0 release closure
```

Do not skip a phase's verification because later UI appears complete.

---

## 12. Resume protocol after interruption

A new worker must:

1. Fetch live `main`.
2. Read `AGENTS.md` and all canonical docs listed in `README.md`.
3. Inspect `docs/TASK_LEDGER.md`.
4. Inspect recent commits, branches, PRs and issues.
5. Reconcile landed code/evidence against ledger state.
6. Build a current claim map before selecting work.
7. Continue the next legitimate incomplete task.
8. Update repository documentation when a material fact/decision changes.

Never restart from an old prompt when live repository state exists.

---

## 13. Final release condition

Final status may become:

```text
VERIFIED_FINAL_COMPLETE
```

only when:

- the exact release commit passes all mandatory automated and manual gates,
- the exact installer built from that commit passes clean-machine acceptance,
- no mandatory ledger item remains below `CLOSED`,
- no legitimate known release blocker remains,
- branding/provenance/setup are final,
- primary workflows including FCC agent operation, Unity automation, Blender automation and Unity↔Blender validation are operational and recoverable.
