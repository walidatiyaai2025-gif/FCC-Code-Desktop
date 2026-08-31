# FCC Code Desktop — Complete v1.0.0 Product Specification

## Product promise

FCC Code Desktop is a premium, local-first Windows AI development workbench driven by the user's local `fcc-claude`/FCC environment. It is intended to replace day-to-day Codex-style coding-agent usage with a richer desktop experience that can work across source code, terminals, Git, build/test systems, Unity and Blender.

The owner supervises results. Routine implementation, debugging, tool selection and iteration are performed by the AI without requiring the owner to manually operate developer tooling.

The product must be coherent and complete on the first public release. Internal development artifacts may be incomplete, but nothing is released as the product until the full v1 acceptance matrix passes.

---

## Primary user journey

1. Install `FCCCodeDesktop-Setup-1.0.0.exe`.
2. Launch the product.
3. First-run health verifies FCC, `fcc-claude`, Git and required application runtime components.
4. Open an existing project folder.
5. Product auto-detects project technologies and compatible external developer tools.
6. Start or resume an AI session.
7. Ask the agent to implement/debug/build/test/create assets.
8. Watch structured activity in real time.
9. Permission-sensitive or destructive operations are governed by policy.
10. Agent can use files, terminal, Git, builds, debuggers, Unity and Blender as applicable.
11. User can inspect diffs, logs and generated artifacts.
12. Close/restart Windows or the app and resume safely.
13. Commit/push finished work from the same workspace.

---

## Functional domains

### A. Premium desktop shell

- Native Windows desktop experience.
- Dark/light appearance.
- High-DPI support.
- 1366×768 minimum usable layout; 1920×1080 and 4K optimized.
- Project sidebar, central agent conversation, contextual workspace panel, bottom terminal/changes/problems/output/logs surface.
- Command palette and keyboard navigation.
- Persistent panel sizes/layout preferences.
- Explicit status for FCC, model/runtime, Git and external tools.

### B. Projects

Each project stores metadata without copying the source tree:

- canonical path,
- display name,
- Git repo metadata,
- technology detections,
- external tool detections,
- sessions/tasks,
- permission profile,
- tool policy,
- workspace-specific settings,
- last-known health and recovery state.

Support Git and non-Git folders.

### C. Agent conversations

- Streaming assistant text.
- Structured tool timeline.
- Rich Markdown/code/diff rendering.
- Stop/cancel.
- Queue status.
- Retry/recovery states.
- Attach/reference files, folders, images and clipboard context where supported.
- Session titles/history/search.
- Resume previous sessions.
- Preserve complete user-visible history locally.

### D. Agent activity model

Events are typed, not opaque text. Required event classes include:

- user message,
- assistant message,
- runtime status,
- tool started/progress/result,
- file read/write/delete intent,
- command start/output/end,
- test/build result,
- Unity operation,
- Blender operation,
- Git operation,
- permission request,
- rate limit,
- recoverable error,
- fatal error,
- completion summary.

### E. Files/editor/search

- Lazy file explorer.
- Ignore common generated directories by default.
- Local code editor with tabs, syntax highlighting, line numbers, search/replace, go-to-line, save/reload, dirty-state indicator and encoding safety.
- Workspace filename/content/regex search.
- Large-file protection and cancellable background search.

### F. Changes/diff

- Current modified/added/deleted files.
- Diff viewer.
- Open/review/revert a file with safeguards.
- Stage/unstage.
- Change summaries linked to agent turns where possible.
- Never silently discard pre-existing user changes.

### G. Terminal

- Real Windows pseudoconsole integration (ConPTY baseline).
- PowerShell and CMD minimum; detect Git Bash/WSL when available.
- ANSI/UTF-8, resize, copy/paste, Ctrl+C, scrollback and interactive programs.
- Process ownership and cancellation integrated into task lifecycle.

### H. Git

Required workflows:

- status,
- branches,
- checkout,
- create branch,
- fetch,
- pull,
- stage/unstage,
- commit,
- push,
- history,
- diff.

High-risk operations such as force push, hard reset, clean or history rewrite require product-level safeguards and cannot be silently executed.

### I. Queue and throttling

Default invariant:

```text
GLOBAL_AGENT_CONCURRENCY = 1
INTER_RUN_COOLDOWN = 15 seconds
```

- Only one coding-agent run actively drives tools at once by default.
- Other conversations remain queued.
- Provider/FCC throttling pauses new launches.
- Backoff is bounded and observable.
- Queue survives app restart.

### J. Permissions

Product exposes understandable permission profiles mapped safely to underlying agent/runtime capabilities.

Required product profiles:

- Plan/read-only oriented,
- Ask/default,
- Edit-enabled,
- Automated guarded,
- Full access / high-risk mode with explicit warning.

Never misrepresent the effective underlying permission mode.

### K. Persistence/recovery

Local SQLite database with versioned migrations.

Persist:

- projects,
- sessions,
- messages,
- tasks,
- event journal,
- queue,
- tool runs,
- external process records,
- Git checkpoints,
- settings,
- diagnostics metadata.

On interruption/crash/reboot:

- detect abandoned running tasks,
- inspect owned process survival,
- reconcile workspace/Git state,
- recover conversation/session IDs,
- provide resume/review/cancel choices as applicable,
- never report a task complete when its final verification was interrupted.

### L. FCC/Claude runtime

- Detect `fcc-claude` and relevant FCC health/version information.
- Runtime integration behind `IAgentRuntime`.
- Primary structured integration plus fallback compatible CLI path.
- Streaming and session/continuation support must be contract-tested against actual installed versions.
- Version change triggers compatibility smoke checks.
- FCC/provider configuration remains owned by FCC unless a stable explicit contract is intentionally integrated.

### M. External Developer Tool Gateway

A project-owned extensibility layer lets the agent operate installed developer/content tools without embedding brittle tool logic in the UI.

Required capabilities:

- discovery,
- capability description,
- invocation,
- structured result,
- streaming logs,
- cancellation,
- process/resource locks,
- artifact collection,
- diagnostics,
- version compatibility checks.

Adapters may use safe CLI, process APIs, official automation APIs/protocols, DAP/MCP when justified, project-owned bridges or other stable contracts.

### N. Unity first-class adapter

Must support at minimum:

- Unity project detection and project version detection,
- local Editor/Hub installation resolution,
- correct-project launching,
- batch/headless automation when supported,
- isolated log capture,
- compile validation,
- EditMode/PlayMode test execution where project tests exist,
- Editor automation entry points,
- builds/target selection as required by tasks,
- structured error/result collection,
- project-instance locking,
- safe cancellation/recovery,
- artifact/log validation.

Routine AI debugging loop target:

```text
inspect → edit → Unity compile/test → parse errors → fix → retest → validate
```

### O. Blender first-class adapter

Must support at minimum:

- Blender installation/version discovery,
- `.blend`/3D-workspace awareness,
- interactive launch when visual inspection is needed,
- background/headless execution,
- Blender Python script execution,
- procedural scene/mesh/material/camera/light operations through scripts,
- asset import/export automation,
- still/animation rendering when validation requires it,
- console/log/debug capture,
- process/resource locking,
- checkpoint/backup before destructive asset replacement,
- output artifact manifests,
- file/readability/non-empty validation,
- structured Python/process errors,
- safe cancellation/recovery.

Routine AI 3D loop target:

```text
specify asset → generate/modify in Blender → validate scene/artifact → render preview if needed → export → verify output
```

### P. Unity↔Blender AI pipeline

Required integrated acceptance scenario:

```text
User asks for a game asset/change
        ↓
Agent decides 3D work is required
        ↓
Blender adapter creates/modifies asset
        ↓
Blender validates and exports
        ↓
Unity receives/imports asset
        ↓
Unity compiles/tests/validates
        ↓
Agent inspects results
        ↓
Iterates until acceptance or explicit blocker
```

The user must not be required to manually shuttle routine artifacts between applications.

### Q. Diagnostics

Visible health center for:

- application,
- database,
- FCC,
- `fcc-claude`,
- Git,
- runtime adapter,
- Unity,
- Blender,
- optional detected toolchains.

Sanitized diagnostic export ZIP must exclude secrets and include versions/logs/environment summaries needed to reproduce failures.

### R. Security/privacy

- Local-first.
- No telemetry by default.
- No cloud account required by FCC Code Desktop.
- Secrets redacted from persisted logs and diagnostics.
- Product-owned secrets, if ever necessary, use Windows-protected storage rather than plaintext SQLite.
- Commands and file paths passed through structured process APIs; avoid unsafe shell concatenation.
- External tool adapters validate paths/arguments and declare side effects.

### S. Performance

The product must remain responsive with:

- large source trees,
- long chat histories,
- high-volume build/log streams,
- large diffs,
- lengthy Unity/Blender automation.

Use virtualization, lazy loading, bounded in-memory buffers, cancellable background work and disk-backed logs where needed.

### T. Setup/branding/release

First public version ships as a polished setup executable:

`FCCCodeDesktop-Setup-1.0.0.exe`

Required:

- original professional icon,
- premium setup experience,
- product version metadata,
- Start menu/taskbar identity,
- repair/upgrade/uninstall behavior,
- keep-user-data default on uninstall,
- no Visual Studio/.NET SDK/Node/Python requirement merely to run the desktop app unless an external project/tool itself needs them,
- dependency detection with actionable UI,
- clean-machine acceptance.

---

## Out of v1 product scope

- cloud collaboration,
- accounts/team workspaces,
- public plugin marketplace,
- parallel coding agents by default,
- mobile client,
- remote worker fleet,
- replacing FCC's provider-admin responsibilities.

---

## Product completion test

The product is not complete because it opens, chats, edits code, or looks premium. It is complete only when the exact v1.0.0 candidate passes `docs/ACCEPTANCE_MATRIX.md` and `docs/RELEASE_POLICY.md` with no mandatory unresolved item.