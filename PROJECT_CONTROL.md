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

The product must not be limited to editing source files. It must be able to coordinate real development workflows across repositories, build systems, debuggers, game engines, device/emulator tooling, logs, tests and external developer applications through a safe extensible tool-integration layer.

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
28. Extensible adapters for build/test/debug/device/browser tooling
29. Professional first-public-version setup executable
30. Upgrade and uninstall behavior
31. Automated quality/runtime contract testing
32. Exact-head release verification
33. Clean-machine installer acceptance

These are mandatory v1 scope, not stretch goals.

---

## 4. Codex-replacement requirement

FCC Code Desktop must be designed as a complete local AI development workbench, not a chat wrapper.

The agent must be able, when tools are installed and the project type requires them, to:

- discover supported local developer tools,
- invoke build and test systems,
- start/stop controlled processes,
- inspect structured and textual logs,
- run debuggers or debugger adapters when integration permits,
- interact with device/emulator tools,
- open or control development applications through supported integration contracts,
- receive machine-readable results back into the agent task,
- correlate errors with source changes,
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
        └── Unity Adapter (v1 first-class)
```

---

## 5. Unity v1 requirement

Unity is a first-class external-development-tool target in v1.

For a Unity repository, FCC Code Desktop must be capable of detecting the project and installed Unity environment, exposing Unity-related health, and allowing the agent to perform supported automated development/debug loops without requiring the owner to manually operate Unity for routine checks.

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

Unity's documented command-line interfaces support project selection, log redirection and static Editor method execution, making headless/automation loops practical; the product must validate the exact installed Unity version behavior with contract tests rather than assuming all releases behave identically.

---

## 6. External-tool extensibility requirement

Unity is the first first-class adapter, not the last.

Architecture must allow later or automatic adapters for tools such as:

- Visual Studio / MSBuild / dotnet
- CMake / Ninja
- Node/npm/pnpm/yarn
- Python/pytest
- Java/Gradle
- Android SDK / adb / logcat / emulator
- Docker
- browsers and browser automation
- game engines other than Unity
- database CLIs
- local servers/services
- custom project commands

The core product must remain functional if none of these optional tools are installed.

---

## 7. Explicitly deferred beyond v1

Unless required to satisfy v1 quality:

- cloud sync,
- team accounts,
- multi-user collaboration,
- remote agents,
- mobile app,
- plugin marketplace,
- SaaS telemetry/analytics,
- multi-machine state synchronization,
- parallel multi-agent execution,
- full reimplementation of FCC Admin,
- direct ownership of provider credentials already managed by FCC.

---

## 8. Hard invariants

### 8.1 Serial execution

```text
GLOBAL_AGENT_CONCURRENCY = 1
DEFAULT_INTER_RUN_COOLDOWN_SECONDS = 15
```

Only one active agent run executes at a time by default. A second conversation remains queued until the prior run is terminal and cooldown expires.

### 8.2 Repository continuity

The repository must always contain enough current state that a new AI worker can continue without previous chat history.

### 8.3 No partial public product

Internal builds may be incomplete. Public product versions may not.

The first public product version is `1.0.0` and is not eligible until every mandatory acceptance gate passes.

### 8.4 No silent destructive operations

User code/data must not be destroyed to simplify automation.

### 8.5 External applications are controlled resources

Any external tool started by the product must have:

- explicit lifecycle ownership,
- process identity tracking,
- timeout/heartbeat strategy where applicable,
- log capture,
- cancellation strategy,
- result classification,
- concurrency rules,
- cleanup/recovery behavior.

---

## 9. Delivery phases

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
P11  Permissions + destructive-action safety
P12  Global queue + cooldown + rate-limit control
P13  Crash/reboot recovery + journaling/backups
P14  Diagnostics/security/performance hardening
P15  Premium UX closure + accessibility/high DPI
P16  Professional branding/icon + setup/bootstrapper
P17  Upgrade/uninstall/repair
P18  Full automated regression + exact-head CI
P19  Clean-machine acceptance + release provenance
P20  v1.0.0 release closure
```

Do not skip a phase's verification because later UI appears complete.

---

## 10. Resume protocol after interruption

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

## 11. Final release condition

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
- primary user workflows including Unity/tool integration are operational and recoverable.
