# FCC Code Desktop

**Production-grade local AI coding desktop for `fcc-claude`, designed as a premium Codex-style replacement with first-class external-tool automation.**

> Target: **FCC Code Desktop v1.0.0 Production**  
> Platform: **Windows 10/11 x64**  
> Product principle: **Premium, complete, reliable from the first public release.**  
> Execution principle: **One current phase; no phase advancement before verified closure.**

---

## 1. Repository is the source of truth

This repository is the permanent authoritative memory for the entire project.

No chat, old prompt, local note, worker memory or undocumented decision is authoritative unless reconciled here.

If work stops at any point, a new AI worker must be able to continue from this repository alone.

### Mandatory reading order

1. [`AGENTS.md`](AGENTS.md) — binding project constitution and worker rules.
2. [`CURRENT_PHASE.md`](CURRENT_PHASE.md) — exact live resume checkpoint and current authorized phase.
3. [`PROJECT_CONTROL.md`](PROJECT_CONTROL.md) — canonical product scope and project state.
4. [`docs/EXECUTION_PLAN.md`](docs/EXECUTION_PLAN.md) — strict P00→P22 sequential stage-gated plan.
5. [`docs/PRODUCT_SPEC.md`](docs/PRODUCT_SPEC.md) — complete product requirements.
6. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — system architecture and technical boundaries.
7. [`docs/UI_UX_STANDARD.md`](docs/UI_UX_STANDARD.md) — premium design and interaction standard.
8. [`docs/ENGINEERING_STANDARD.md`](docs/ENGINEERING_STANDARD.md) — coding, testing, reliability, security and performance standard.
9. [`docs/RELEASE_POLICY.md`](docs/RELEASE_POLICY.md) — versioning, setup, release gates and no-partial-release policy.
10. [`docs/ACCEPTANCE_MATRIX.md`](docs/ACCEPTANCE_MATRIX.md) — mandatory acceptance scenarios.
11. [`docs/TASK_LEDGER.md`](docs/TASK_LEDGER.md) — canonical mandatory work inventory.
12. [`docs/DECISIONS.md`](docs/DECISIONS.md) — architectural/product decision log.
13. [`docs/PHASE_CLOSURE_TEMPLATE.md`](docs/PHASE_CLOSURE_TEMPLATE.md) — mandatory evidence format for phase advancement.

When documents conflict, `AGENTS.md` and the current explicit phase/gate controls take precedence; consequential changes must be documented rather than silently interpreted.

---

## 2. Product mission

Build a premium Windows desktop application on top of the owner's existing local FCC / `fcc-claude` environment.

It must operate as a real local AI development workbench, not a chat wrapper. The owner should be able to ask the agent to implement and debug work while the application coordinates source code, terminal processes, Git, builds, tests, external developer tools and recoverable sessions.

Core v1 surface includes:

- Projects/workspaces
- Real FCC/Claude conversations with streaming
- Structured agent/tool activity
- Durable sessions and resume
- File explorer, editor and search
- Diff/change review
- Integrated terminal
- Safe Git workflows
- Permissions and side-effect safety
- Global serial execution queue and rate-limit protection
- Crash/reboot recovery
- Diagnostics, logs and sanitized support bundles
- External Developer Tool Gateway
- First-class Unity automation/debug/build support
- First-class Blender 3D creation/automation/render/export support
- Unity↔Blender AI asset pipeline
- Professional identity, icon and setup executable
- Upgrade/uninstall lifecycle
- Exact-head automated and clean-machine release acceptance

---

## 3. Strict sequential execution

The complete execution sequence is defined in [`docs/EXECUTION_PLAN.md`](docs/EXECUTION_PLAN.md):

```text
P00 Contract de-risking
 ↓
P01 Solution / CI
 ↓
P02 Premium design system / shell
 ↓
P03 Persistence / state
 ↓
P04 FCC/Claude runtime
 ↓
P05 Conversation / sessions / task UX
 ↓
P06 Projects / files / editor / search
 ↓
P07 Changes / Git
 ↓
P08 Terminal / process supervision
 ↓
P09 External Tool Gateway
 ↓
P10 Unity adapter
 ↓
P11 Blender adapter
 ↓
P12 Unity↔Blender pipeline
 ↓
P13 Permissions / safety
 ↓
P14 Queue / cooldown / throttling
 ↓
P15 Crash/reboot recovery / backups
 ↓
P16 Diagnostics / security / performance
 ↓
P17 Premium UX closure
 ↓
P18 Branding / professional setup
 ↓
P19 Upgrade / uninstall lifecycle
 ↓
P20 Full regression / exact-head candidate
 ↓
P21 Clean-machine / provenance
 ↓
P22 v1.0.0 release closure
```

### Non-negotiable gate

```text
ALL CURRENT-PHASE TASKS = CLOSED
AND EXIT_GATE = PASS
AND EXACT-HEAD EVIDENCE RECORDED
AND NO KNOWN PHASE-LOCAL BLOCKER
```

Only then may `CURRENT_PHASE.md` advance to the next phase.

Multiple AI workers may work in parallel only on non-overlapping tasks **inside the same current phase**. They may not independently advance the project into later phases.

---

## 4. Product standard

FCC Code Desktop is **not** an MVP, prototype, mock or demo.

The first public release must already be production quality within the declared v1 boundary.

The project optimizes simultaneously for:

- premium UI/UX,
- maintainable architecture,
- runtime reliability,
- deterministic recovery,
- user data safety,
- secret hygiene,
- performance on real repositories,
- external-tool robustness,
- accessibility/high-DPI behavior,
- professional installation and lifecycle management.

A beautiful application with unreliable runtime behavior fails. A technically correct application with amateur UI also fails.

---

## 5. Architecture at a glance

```text
FCC Code Desktop
        │
        ├── Projects / Sessions / Tasks
        ├── Premium Chat + Agent Activity
        ├── Files / Editor / Search / Diff
        ├── Terminal / Git
        ├── Queue / Permissions / Recovery
        ├── Diagnostics / Settings
        └── External Tool Activity / Artifacts
                │
        ┌───────┴───────────────────────────┐
        ▼                                   ▼
  IAgentRuntime                     External Tool Gateway
  ├─ FCC/Claude primary             ├─ Unity Adapter
  └─ CLI fallback                   ├─ Blender Adapter
        │                            └─ future adapters
        ▼
   fcc-claude
        │
        ▼
     FCC Proxy
```

The UI must never be tightly coupled to unstable FCC, Claude, Unity or Blender internals. External systems are isolated behind project-owned typed contracts and compatibility tests.

---

## 6. Technology baseline

Unless superseded by a documented decision:

- **Desktop:** C# / .NET 10 / WPF
- **Architecture:** MVVM + dependency injection + modular clean boundaries
- **Persistence:** SQLite with migrations and backups
- **Editor:** locally bundled Monaco-based surface where appropriate
- **Terminal:** Windows ConPTY
- **Git:** native Git CLI behind a safe service boundary
- **Embedded web:** WebView2 only where justified
- **Logging:** structured logging with mandatory secret redaction
- **Testing:** unit + integration + runtime contract + external-tool + recovery + UI + installer lifecycle
- **Installer:** professional Windows setup executable with branding, versioning, upgrade and uninstall

Dependencies must be justified, pinned where practical and isolated behind project-owned abstractions.

---

## 7. Unity + Blender are mandatory v1 capabilities

### Unity

The agent must be able to detect a Unity project/editor, execute controlled compile/test/build automation, collect logs/results, classify failures, cancel safely and recover after interruption.

### Blender

The agent must be able to discover Blender, run it headlessly, execute Python automation, create/modify 3D content, save `.blend`, render previews, import/export assets, validate actual artifacts, surface errors and recover safely.

### Cross-tool pipeline

A required end-to-end v1 flow is:

```text
AI task
  ↓
Blender create/modify asset
  ↓
validate .blend / render / exported artifact
  ↓
manifested handoff
  ↓
Unity import
  ↓
compile/test/build validation
  ↓
AI receives structured result
  ↓
fix/retry until accepted or truthfully failed
```

A zero process exit code is never enough when required output artifacts are missing, corrupt or semantically invalid.

---

## 8. Autonomous execution policy

The owner supervises outcomes; AI workers are expected to research, choose, implement, test, fix and document normal engineering decisions autonomously.

Workers must not ask the owner to choose routine libraries, class structures, retry algorithms, layout details, naming, test organization, installer internals or similar implementation details.

Owner intervention is reserved for genuine external blockers such as unavailable credentials/account authorization, hardware access, licensing/legal decisions or target environments the worker cannot access.

Technical difficulty is not a blocker.

---

## 9. First public release

There is no public partial `0.x` product standing in for the requested result.

Development/CI artifacts may exist internally, but the first finished public product target is:

```text
FCC Code Desktop v1.0.0
FCCCodeDesktop-Setup-1.0.0.exe
```

No installer may be presented as the finished product until every required phase and acceptance gate has closed.

---

## 10. Release invariant

Final release requires the exact release commit and exact installer to pass, at minimum:

```text
BUILD / QUALITY
UNIT / INTEGRATION
FCC RUNTIME CONTRACT
STREAMING / SESSIONS / RESUME
FILES / EDITOR / SEARCH
DIFF / GIT SAFETY
TERMINAL / PROCESS CONTROL
TOOL GATEWAY
UNITY CONTRACT
BLENDER CONTRACT
UNITY↔BLENDER E2E
PERMISSIONS / QUEUE / RATE LIMIT
CRASH / REBOOT RECOVERY
DATABASE / BACKUP
SECURITY / REDACTION
PERFORMANCE
PREMIUM UI/UX / DPI / ACCESSIBILITY
INSTALL / UPGRADE / UNINSTALL
CLEAN-MACHINE ACCEPTANCE
PROVENANCE
```

Only after P22 closure may the final status become:

```text
VERIFIED_FINAL_COMPLETE
```

---

## 11. Current state

The authoritative live checkpoint is [`CURRENT_PHASE.md`](CURRENT_PHASE.md).

As of **2026-08-31**, the authorized phase is **P00 — Constitution + external-contract de-risking**.

Do not start P01 implementation before P00 is closed with recorded evidence.