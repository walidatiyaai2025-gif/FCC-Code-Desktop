# FCC Code Desktop — Premium UI/UX Standard

## 1. Product experience target

FCC Code Desktop must feel like a deliberate professional development product, not a skinned WPF utility. Visual quality, interaction quality, information architecture and system-state clarity are release requirements.

The reference quality bar is modern premium developer tooling: dense enough for professionals, calm enough for long sessions, keyboard-friendly, predictable and fast.

---

## 2. Core layout

Default desktop composition:

```text
┌────────────────────────────────────────────────────────────────────┐
│ Product / Project / Branch / Runtime / Tool Health / Window Actions│
├───────────────┬────────────────────────────────┬───────────────────┤
│ Projects      │ Conversation / Agent Activity  │ Context / Explorer│
│ Sessions      │                                │ Search / Outline  │
│ Tasks         │                                │ Tool Health       │
├───────────────┴────────────────────────────────┴───────────────────┤
│ Terminal | Changes | Problems | Output | Logs | Artifacts          │
├────────────────────────────────────────────────────────────────────┤
│ Composer / Permission Mode / Attachments / Send-Queue State        │
└────────────────────────────────────────────────────────────────────┘
```

Panels must be resizable, collapsible where appropriate and restorable.

Minimum supported usable viewport: **1366×768 @ 100% scaling**.

Must also be validated at common 125%, 150%, 175%, 200% DPI/scaling and 4K displays.

---

## 3. Visual system

Before feature UI proliferates, establish reusable design tokens for:

- spacing scale,
- corner radii,
- typography roles,
- foreground/background hierarchy,
- border/divider hierarchy,
- accent/action semantics,
- success/warning/error/info states,
- focus rings,
- selection,
- hover/pressed/disabled states,
- shadows/elevation only where functional.

No arbitrary per-screen colors/spacing.

Support dark and light appearance from the same semantic tokens.

---

## 4. Typography

Typography must prioritize code-heavy readability.

Define roles such as:

- Display/product heading
- Section heading
- Body
- Secondary metadata
- Compact status
- Code/monospace

Avoid oversized marketing typography inside the working surface. The product is an IDE/workbench, not a landing page.

Text must remain readable under DPI scaling and Windows text settings.

---

## 5. Iconography and branding

Release uses one coherent professional icon family.

Forbidden for final release:

- emoji as functional icons,
- mixed unrelated icon packs,
- default Windows/WPF placeholder symbols,
- copied third-party product marks,
- temporary initials logo.

The application icon must be original and professionally generated/refined via AI-assisted design, then exported/tested at all required Windows sizes.

Required product identity surfaces:

- executable,
- installer,
- Start menu,
- taskbar,
- title bar/app shell,
- About,
- release assets.

Record asset provenance.

---

## 6. Conversation experience

The conversation surface must distinguish clearly between:

- user prompt,
- agent answer,
- streamed text,
- tool actions,
- command output,
- builds/tests,
- Unity operations,
- Blender operations,
- warnings/errors,
- permission requests,
- completion evidence.

Long tool sequences collapse intelligently but remain inspectable.

The user should understand what the agent is doing without reading raw JSON or console noise.

Each active task displays explicit state and elapsed progress context where meaningful.

---

## 7. State completeness

Every major component must define and design:

- normal,
- empty,
- loading,
- disabled,
- unavailable,
- offline,
- queued,
- active,
- waiting permission,
- rate limited,
- retrying,
- cancelled,
- interrupted,
- failed,
- recovered,
- success states.

No primary workflow may fall back to an unstyled exception dialog or indefinite spinner.

---

## 8. Queue UX

Since one active coding agent is the default invariant, queue state must be obvious.

Example:

```text
RUNNING   Project A / Fix auth issue
QUEUED 1  Project B / Build Unity scene
QUEUED 2  Project C / Generate Blender asset

Next run available after cooldown: 12s
```

Users can inspect, reorder or cancel queued work when safe, but queued work cannot bypass global safety policy silently.

---

## 9. Tool health UX

A unified tool-health surface shows:

```text
FCC          Ready
fcc-claude   Ready
Git          Ready
Unity        6000.x detected / project compatible
Blender      4.x detected
Terminal     Ready
Database     Ready
```

Missing optional tools are informative, not fatal to unrelated projects.

A Unity project with missing compatible Unity version should surface a specific actionable issue. Same for Blender-dependent tasks.

---

## 10. Unity UX

Unity operations should render structured summaries:

```text
Unity • Compile
✓ Project opened in batch mode
✓ Script compilation
✗ 2 compiler errors

Assets/Scripts/PlayerMove.cs:42
CS0103 ...
```

Test/build outputs should be navigable to relevant files/logs/artifacts.

Do not flood the conversation with entire `Editor.log` by default; summarize with expandable/raw access.

---

## 11. Blender UX

Blender operations should render structured summaries:

```text
Blender • Generate Asset
✓ Blender 4.x
✓ Script executed
✓ Scene saved
✓ FBX exported
✓ Preview rendered

Artifacts
• character.blend
• character.fbx
• preview.png
```

When render/image artifacts exist, expose convenient preview/open actions.

Errors link to script/log context where possible.

---

## 12. Changes/diff UX

- Clear file-status grouping.
- Side-by-side or unified diff where implementation supports it cleanly.
- Line-level highlighting.
- Easy jump from agent activity to affected file.
- Pre-existing user changes visually distinguishable from changes associated with current task when provenance can be established.
- Dangerous discard/revert actions clearly scoped.

---

## 13. Terminal UX

Terminal must feel native and responsive.

Requirements:

- proper keyboard focus,
- selection/copy/paste,
- ANSI color support,
- readable scrollback,
- tab/session labeling,
- process-running indication,
- safe close behavior,
- no text-input lag during heavy agent streaming.

---

## 14. First-run experience

First launch should be concise and confidence-building:

```text
FCC Code Desktop

Environment check
✓ Application runtime
✓ FCC
✓ fcc-claude
✓ Git
○ Unity — optional, not required for this workspace
○ Blender — optional, not required for this workspace

[Open Project]
```

Do not force a tutorial carousel before the user can work.

If required dependencies are missing, explain exactly what is blocked versus what remains usable.

---

## 15. Installer UX

Setup is part of the premium product.

Must include:

- branded icon/artwork,
- product name/version,
- clean hierarchy and typography,
- sensible install path default,
- no developer jargon unless needed,
- progress that does not freeze,
- actionable failure state,
- launch-after-install option,
- repair/upgrade/uninstall consistency.

A stock-looking default installer with placeholder branding is a release blocker.

---

## 16. Accessibility and keyboard

At minimum:

- logical tab/focus order,
- visible focus state,
- keyboard access to primary actions,
- command palette shortcuts,
- screen-reader accessible names for non-text controls,
- adequate contrast,
- no meaning communicated by color alone,
- scalable text/layout without clipping.

---

## 17. Responsiveness/perceived performance

UI thread must remain responsive during:

- agent streaming,
- large file search,
- large logs,
- Unity import/build,
- Blender rendering,
- Git operations.

Use virtualization and progressive rendering.

Provide feedback quickly for user actions even when underlying work is long-running.

---

## 18. Copy standard

System language should be concise, specific and technical when needed.

Prefer:

`Unity compilation failed: 2 errors`

over:

`Something went wrong.`

Never claim success until verification evidence exists.

---

## 19. Premium release blockers

Any of the following blocks release:

- placeholder logo/icon,
- inconsistent spacing or typography across main surfaces,
- clipped controls at supported resolutions/scales,
- invisible keyboard focus,
- broken dark/light theme,
- major unstyled dialogs,
- raw JSON as normal user-facing state,
- indefinite spinner for a failed/hung task,
- inaccessible primary actions,
- installer visually inconsistent with product,
- obvious developer/debug UI in production,
- unreadable high-volume logs/diffs.

---

## 20. Visual acceptance evidence

Release candidate requires screenshot evidence at minimum for:

- first run,
- main workspace idle,
- active streaming task,
- queued task,
- permission request,
- rate-limit state,
- files/editor,
- diff,
- terminal,
- Git changes,
- Unity operation/result,
- Blender operation/artifacts,
- crash recovery,
- settings/diagnostics,
- installer,
- dark/light mode,
- 1366×768,
- 1920×1080,
- high-DPI scenario.

Screenshots are evidence of presentation only; they do not replace functional tests.