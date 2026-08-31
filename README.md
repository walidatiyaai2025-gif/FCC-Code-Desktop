# FCC Code Desktop

**Production-grade local AI coding desktop for `fcc-claude`.**

> Repository status: **PRODUCT CONSTITUTION / IMPLEMENTATION BASELINE**  
> Target: **FCC Code Desktop v1.0.0 Production**  
> Platform: **Windows 10/11 x64**  
> Product principle: **Premium, complete, reliable from the first public release.**

---

## 1. Repository is the source of truth

This repository is the permanent authoritative reference for the entire FCC Code Desktop project.

No conversation, chat history, human memory, temporary prompt, local note, or undocumented decision is authoritative unless it has been reconciled into this repository.

If work stops at any point, a new AI worker must be able to continue from the repository alone.

Authoritative reading order:

1. [`AGENTS.md`](AGENTS.md) — project constitution and autonomous-worker rules.
2. [`PROJECT_CONTROL.md`](PROJECT_CONTROL.md) — canonical project state, scope, completion definition, and continuation protocol.
3. [`docs/PRODUCT_SPEC.md`](docs/PRODUCT_SPEC.md) — complete product requirements.
4. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — system architecture and hard technical decisions.
5. [`docs/UI_UX_STANDARD.md`](docs/UI_UX_STANDARD.md) — premium design and interaction standard.
6. [`docs/ENGINEERING_STANDARD.md`](docs/ENGINEERING_STANDARD.md) — coding, testing, reliability, security, and performance standard.
7. [`docs/RELEASE_POLICY.md`](docs/RELEASE_POLICY.md) — versioning, installer, release gates, and no-partial-release policy.
8. [`docs/ACCEPTANCE_MATRIX.md`](docs/ACCEPTANCE_MATRIX.md) — mandatory acceptance tests.
9. [`docs/TASK_LEDGER.md`](docs/TASK_LEDGER.md) — canonical work inventory and closure ledger.
10. [`docs/DECISIONS.md`](docs/DECISIONS.md) — architectural/product decisions and rationale.

When documents conflict, the precedence above applies unless a newer explicit ADR in `docs/DECISIONS.md` states that it supersedes a specific earlier rule.

---

## 2. Product mission

Build a premium Windows desktop application that provides a complete graphical coding-agent workspace on top of the user's existing local `fcc-claude` / FCC environment.

The application must provide one coherent product surface for:

- Projects/workspaces
- Claude/FCC agent conversations
- Real-time streaming output
- Tool activity
- Session persistence and resume
- File explorer
- Code editing
- Workspace search
- Change review and diff
- Integrated terminal
- Git workflows
- Permission controls
- Global serial execution queue
- Rate-limit protection
- Crash/reboot recovery
- Runtime/FCC health monitoring
- Diagnostics and sanitized support bundles
- Local persistence and backup
- Professional installer, iconography, first-run experience, upgrade and uninstall

The user should be able to install FCC Code Desktop, open an existing project, ask the agent to perform work, observe what it does, review changes, use terminal/Git, close the application, reopen it, and continue safely.

---

## 3. Non-negotiable product standard

FCC Code Desktop is **not** an MVP, proof of concept, UI mock, prototype, or demo project.

The first public version must already be a complete premium product within the declared v1 scope.

The project must optimize for:

- **UI/UX quality:** polished, coherent, accessible, responsive desktop experience.
- **Engineering quality:** maintainable architecture, strong typing, modular boundaries, tests and diagnostics.
- **Runtime reliability:** deterministic lifecycle, explicit state machines, graceful cancellation, retries and recovery.
- **Safety:** no silent destructive Git/file operations; secrets must never leak into logs or support bundles.
- **Performance:** large repositories, long sessions and large outputs must remain usable.
- **Installability:** professional setup executable from the first public release.
- **Continuity:** repository state must make autonomous continuation possible after any interruption.

A visually impressive application with broken runtime behavior is a failure. A technically correct application with amateur UI is also a failure. Both product surface and engineering must pass.

---

## 4. Product architecture at a glance

```text
FCC Code Desktop
        │
        ├── Projects / Sessions / Tasks
        ├── Premium Chat + Agent Activity
        ├── Files / Editor / Search / Diff
        ├── Terminal / Git
        ├── Queue / Permissions / Recovery
        └── Diagnostics / Settings
                │
                ▼
          IAgentRuntime
          ├── FCC/Claude primary adapter
          └── FCC/Claude CLI fallback adapter
                │
                ▼
           fcc-claude
                │
                ▼
             FCC Proxy
                │
                ▼
          Configured Provider
```

The desktop UI must never be tightly coupled to unstable FCC internals. All external-agent integration passes through a compatibility/runtime layer.

---

## 5. Initial technology baseline

Unless superseded by a documented ADR:

- **Desktop:** C# / .NET 10 / WPF
- **Architecture:** MVVM + dependency injection + modular clean boundaries
- **Persistence:** SQLite with versioned migrations and backup/recovery
- **Editor:** locally bundled Monaco-based editor surface where appropriate
- **Terminal:** Windows ConPTY-based terminal surface
- **Git:** native Git CLI integration behind a safe service boundary
- **Embedded web surfaces:** WebView2 when justified
- **Logging:** structured logging with mandatory secret redaction
- **Testing:** unit + integration + runtime contract + recovery + UI automation + installer/upgrade tests
- **Installer:** professional Windows setup executable with versioning, icon, upgrade and uninstall support

No dependency may be introduced merely because it is convenient. Dependencies must be justified, version-pinned where practical, and isolated behind project-owned interfaces.

---

## 6. First public release means production v1.0.0

There will be no public `0.x`, beta-looking, partially functional, or "we will fix it later" release presented as the product.

Development builds and CI artifacts may exist internally, but they are **not releases** and must not be presented to the user as finished versions.

The first public product release is:

```text
FCC Code Desktop v1.0.0
FCCCodeDesktop-Setup-1.0.0.exe
```

It must pass every required gate in `docs/RELEASE_POLICY.md` and `docs/ACCEPTANCE_MATRIX.md`.

---

## 7. Premium from day one

The v1 installer and application must include:

- Professional product naming and visual identity
- Original AI-produced icon and application artwork, with licensing/provenance recorded
- Premium setup/bootstrapper UI
- Consistent typography, spacing, iconography and interaction states
- Dark and light appearance
- Proper high-DPI behavior
- Keyboard navigation and accessibility considerations
- Empty, loading, error, offline, rate-limited, blocked and recovery states
- Professional first-run diagnostics
- Version displayed in product surfaces and installer metadata

No placeholder icon, default WPF chrome, default installer wizard appearance, temporary logo, debug-looking status text, or developer-only UI may survive release closure.

---

## 8. Autonomous execution policy

The project is intended to be built primarily by AI workers while the user supervises outcomes.

Workers must:

- Inspect live repository state before acting.
- Read the canonical documents before implementation.
- Choose the technically strongest option consistent with the constitution.
- Avoid asking the user to choose routine technical details.
- Resolve normal engineering ambiguity autonomously using evidence, tests and documented tradeoffs.
- Document material decisions in the repository.
- Never hide uncertainty behind a release.
- Never claim completion without evidence.
- Never lower quality or scope silently to make a task appear finished.

User intervention is reserved for genuine external blockers such as credentials, unavailable accounts/services, legal/licensing decisions requiring the owner, or hardware/environment access that AI cannot obtain.

---

## 9. Release invariant

A version may be released **only** when the exact release commit has verified evidence for all required gates:

```text
BUILD                    PASS
STATIC/QUALITY CHECKS    PASS
UNIT TESTS               PASS
INTEGRATION TESTS        PASS
FCC RUNTIME CONTRACT     PASS
STREAMING                PASS
TOOL EVENTS              PASS
SESSIONS / RESUME        PASS
FILES / EDITOR / SEARCH  PASS
DIFF / CHANGE SAFETY     PASS
TERMINAL                 PASS
GIT                      PASS
QUEUE / RATE LIMIT       PASS
CRASH RECOVERY           PASS
DATABASE / BACKUP        PASS
SECURITY / REDACTION     PASS
PERFORMANCE              PASS
INSTALLER                PASS
UPGRADE                  PASS
UNINSTALL                PASS
CLEAN-MACHINE TEST       PASS
UI/UX ACCEPTANCE         PASS
ACCESSIBILITY CHECKS     PASS
DIAGNOSTICS              PASS
PROVENANCE               PASS
```

Anything else is an internal build, not a finished FCC Code Desktop release.

---

## 10. Current canonical state

As of **2026-08-31**, this repository is being initialized as the permanent project control plane and product specification.

Implementation status and the only authoritative task inventory are maintained in [`docs/TASK_LEDGER.md`](docs/TASK_LEDGER.md).

Do not infer completion percentage from file count, commit count, UI screenshots, or code volume. Completion is acceptance-gate based.
