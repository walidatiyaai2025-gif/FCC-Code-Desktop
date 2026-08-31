# FCC Code Desktop

**Premium local AI development workbench for `fcc-claude` — designed as a practical Codex-style replacement on Windows.**

> **Target:** FCC Code Desktop v1.0.0 Production  
> **Platform:** Windows 10/11 x64  
> **Product rule:** complete, premium and reliable on the first public release  
> **Execution model:** AI builds and operates routine workflows; owner supervises outcomes  
> **Project memory:** this repository only

---

## START HERE — repository is the source of truth

This repository is the permanent authoritative reference for the entire project.

No chat, previous conversation, human memory, temporary prompt, scratchpad or undocumented assumption overrides repository state. If development stops at any point, a new AI worker must be able to resume from this repository alone.

Mandatory reading order:

1. [`AGENTS.md`](AGENTS.md) — binding constitution and autonomous-worker rules.
2. [`PROJECT_CONTROL.md`](PROJECT_CONTROL.md) — canonical scope, status, phases and continuation protocol.
3. [`docs/PRODUCT_SPEC.md`](docs/PRODUCT_SPEC.md) — full v1 product requirements.
4. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — architecture and integration boundaries.
5. [`docs/UI_UX_STANDARD.md`](docs/UI_UX_STANDARD.md) — premium visual/interaction standard.
6. [`docs/ENGINEERING_STANDARD.md`](docs/ENGINEERING_STANDARD.md) — code, testing, reliability, security and performance standard.
7. [`docs/RELEASE_POLICY.md`](docs/RELEASE_POLICY.md) — no-partial-release policy and installer/release gates.
8. [`docs/ACCEPTANCE_MATRIX.md`](docs/ACCEPTANCE_MATRIX.md) — mandatory exact-head acceptance tests.
9. [`docs/TASK_LEDGER.md`](docs/TASK_LEDGER.md) — authoritative implementation/closure inventory.
10. [`docs/DECISIONS.md`](docs/DECISIONS.md) — durable ADR-style project decisions.

If documents conflict, use the precedence above unless a newer explicit decision says it supersedes a specific prior rule.

---

## What the product is

FCC Code Desktop is **not a chat wrapper**. It is a local AI engineering workbench built around the user's existing `fcc-claude` / FCC runtime.

The target workflow is:

```text
Open project
  ↓
Ask AI to build/fix/debug/create
  ↓
Agent inspects code/files
  ↓
Agent edits
  ↓
Agent uses terminal/Git/build/test/debug tools
  ↓
Agent can use Unity and Blender when project work requires them
  ↓
Agent validates outputs, reads failures and iterates
  ↓
User reviews final changes/artifacts
  ↓
Commit / continue / resume later
```

Routine development should not require the owner to manually move between terminals, Unity, Blender and debugging utilities merely to let the AI continue.

---

## Mandatory v1 product capabilities

FCC Code Desktop v1.0.0 includes:

- Projects/workspaces
- FCC/`fcc-claude` runtime supervision
- Structured streaming agent conversations
- Tool-activity timeline
- Persistent sessions/resume
- Durable task state/recovery
- File explorer
- Code editor
- Workspace search
- Diff/change review
- Integrated terminal
- Git workflows
- Permission/safety profiles
- Global serial execution queue
- Default 15-second inter-run cooldown
- Rate-limit handling
- Crash/reboot recovery
- Local SQLite persistence and backup
- Structured logs and sanitized diagnostic bundles
- External Developer Tool Gateway
- **First-class Unity automation/debug/build/test adapter**
- **First-class Blender 3D creation/automation/render/export adapter**
- **Unity↔Blender AI asset pipeline**
- Premium dark/light UI
- High-DPI/keyboard/accessibility support
- Original professional AI-assisted visual identity/icon
- Premium setup executable
- Upgrade/repair/uninstall lifecycle
- Exact-head automated and clean-machine release verification

Nothing in this list is a post-v1 "nice to have".

---

## Unity + Blender requirement

This product is explicitly intended to let the AI work beyond ordinary source code.

### Unity

The agent must be able to detect the correct Unity project/version, find a compatible installed Editor, run supported batch/editor automation, collect logs, validate compilation/tests/builds, classify failures and iterate without routine manual operation.

### Blender

The agent must be able to detect Blender, run background/headless automation, execute Blender Python, create/modify 3D scenes/assets, import/export, render previews when useful, validate produced artifacts, classify failures and iterate.

### End-to-end 3D workflow

```text
AI task
  ↓
Blender creates/modifies 3D asset
  ↓
Artifact validated/exported
  ↓
Unity consumes/imports it
  ↓
Unity compile/test/build validation
  ↓
Agent evaluates evidence and fixes/iterates
```

Process exit code alone never proves success; expected outputs must also validate.

---

## Product architecture at a glance

```text
FCC Code Desktop
        │
        ├── Projects / Sessions / Tasks
        ├── Premium Chat / Agent Activity
        ├── Files / Editor / Search / Diff
        ├── Terminal / Git
        ├── Queue / Permissions / Recovery
        ├── Diagnostics / Settings
        └── External Tool Gateway
                ├── Unity Adapter
                ├── Blender Adapter
                └── Future tool adapters
        │
        ▼
     IAgentRuntime
        ├── FCC/Claude primary adapter
        └── FCC/Claude fallback adapter
        │
        ▼
     fcc-claude → FCC → configured provider/model
```

The UI/domain must never depend directly on brittle FCC, Unity or Blender internals.

---

## Technology baseline

Unless superseded by a documented ADR after evidence:

- Desktop: **C# / .NET 10 / WPF**
- Architecture: **MVVM + DI + modular clean boundaries**
- Persistence: **SQLite + versioned migrations + backups**
- Editor: **locally bundled Monaco-based surface where appropriate**
- Terminal: **Windows ConPTY**
- Git: **native Git CLI behind project-owned safety service**
- Embedded web surfaces: **WebView2 only where justified**
- Logging: **structured + correlation IDs + mandatory redaction**
- Testing: **unit, integration, runtime contract, tool contract, recovery, UI, installer**
- Installer: **professional branded Windows setup executable**

Dependencies must be justified, pinned where practical and isolated behind project-owned interfaces.

---

## Non-negotiable quality bar

FCC Code Desktop is **not** an MVP, proof of concept, mock, prototype or demo.

A technically working app with amateur UI fails. A beautiful app with unreliable runtime behavior fails.

Required simultaneously:

- premium UI/UX,
- maintainable coding quality,
- runtime reliability,
- data/workspace safety,
- security/privacy,
- performance,
- recovery,
- diagnostics,
- professional setup/branding,
- real external-tool validation.

No placeholder logo/icon, default WPF-looking primary UI, stock unfinished installer, fake success, hidden known blocker or "we will fix it later" primary feature may survive release closure.

---

## Autonomous execution policy

AI workers must inspect live repository state, choose the technically strongest normal implementation autonomously, verify it, document consequential decisions and update the task ledger.

Do not ask the owner to decide routine engineering details.

User intervention is reserved for genuine external blockers such as unavailable credentials/accounts, legal/licensing owner decisions, inaccessible hardware/environment or other facts AI cannot obtain or determine safely.

---

## No partial public releases

Internal builds/CI artifacts may exist, but the first public product release is:

```text
FCC Code Desktop v1.0.0
FCCCodeDesktop-Setup-1.0.0.exe
```

It cannot be published as complete until the exact candidate passes every mandatory row in `docs/ACCEPTANCE_MATRIX.md` and every release gate in `docs/RELEASE_POLICY.md`.

---

## Release invariant

At minimum the exact release candidate must have verified PASS evidence for:

```text
BUILD / ENGINEERING QUALITY
UNIT + INTEGRATION TESTS
FCC RUNTIME CONTRACT
STREAMING / TOOL EVENTS
SESSIONS / RESUME
FILES / EDITOR / SEARCH
DIFF / GIT SAFETY
TERMINAL / PROCESS SUPERVISION
QUEUE / COOLDOWN / RATE LIMIT
UNITY ADAPTER
BLENDER ADAPTER
UNITY↔BLENDER E2E
CRASH / REBOOT RECOVERY
DATABASE / BACKUP
SECURITY / REDACTION
PERFORMANCE
UI/UX / ACCESSIBILITY / DPI
INSTALLER / UPGRADE / UNINSTALL
CLEAN-MACHINE ACCEPTANCE
PROVENANCE / CHECKSUMS / DIAGNOSTICS
```

Only then may the project use:

```text
VERIFIED_FINAL_COMPLETE
```

---

## Current status

As of **2026-08-31**, the project control/specification baseline is established and implementation has not yet been credited as verified product completion.

The only authoritative implementation state is [`docs/TASK_LEDGER.md`](docs/TASK_LEDGER.md). Do not infer completion from commit count, file count, screenshots or code volume.