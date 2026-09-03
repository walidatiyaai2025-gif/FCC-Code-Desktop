# FCC Code Desktop — Command Palette and Keyboard Framework

**Task:** `FCCD-P02-007`  
**Phase:** P02 — Premium design system and shell  
**Status:** production shell framework

## Purpose

P02 establishes one discoverable command surface before feature screens multiply. The command palette is a shell-owned presentation and registration framework; it does not implement later-phase project, editor, Git, terminal, runtime, persistence, or external-tool behavior.

Feature phases can register their own commands through `CommandPaletteState.RegisterCommand(...)` without replacing the palette UI or global keyboard entry point.

## Production composition

`MainWindow` owns one shared `CommandPaletteState` resource and composes one `CommandPalette` overlay above the titlebar/workspace grid.

The initial registry exposes only commands backed by already-integrated P02 state:

- Show Projects
- Show Sessions
- Show Tasks
- Toggle Bottom Panel

No command fabricates feature data or invokes a later-phase subsystem.

## Keyboard contract

Global shell shortcuts:

| Gesture | Behavior |
|---|---|
| `Ctrl+Shift+P` | Open command palette |
| `F1` | Open command palette |
| `Ctrl+J` | Toggle the existing bottom tool panel |

When the palette is open:

| Key | Behavior |
|---|---|
| `Up` / `Down` | Move the selected command with wraparound |
| `Enter` | Execute the selected command |
| `Esc` | Close without executing |
| Typing | Filter by command title, category, or stable command id |

Filtering is case-insensitive. Opening the palette starts from a clean filter and deterministic first selection. An empty result set has no selected command and cannot accidentally execute stale state.

## Command registration contract

Each `ShellCommandDescriptor` provides:

- stable id,
- user-facing title,
- category,
- optional shortcut display text,
- an `ICommand`,
- optional command parameter.

Registration rejects duplicate ids case-insensitively. Invalid blank ids/titles/categories and null commands are rejected. Commands can be unregistered safely, which gives later feature modules a lifecycle seam without mutating the palette itself.

The palette checks the wrapped command's `CanExecute` state before execution. A successful execution closes the palette; an unavailable or missing selection does not.

## Focus and accessibility

The palette:

- captures the previously focused input element when opened,
- moves keyboard focus to the filter box,
- restores prior focus when dismissed,
- exposes automation names for the palette, search box, and result list,
- uses visible semantic focus/selection states,
- supports mouse double-click as a secondary execution path,
- remains fully operable from the keyboard.

## Visual contract

The palette consumes the existing P02 semantic brushes and typography/geometry tokens. It introduces no raw palette colors and remains compatible with runtime dark/light theme switching.

The overlay constrains its maximum width/height while stretching within the host window, avoiding a fixed-width requirement that would force later architectural replacement at narrower supported shell sizes.

## Deferred scope

The following remain owned by later phases:

- command persistence/user customization,
- project/file/editor commands,
- Git commands,
- terminal/process commands,
- runtime/agent commands,
- Unity/Blender commands,
- settings/keybinding persistence,
- user-editable key maps.

P02-007 establishes the stable shell seam only.

## Deterministic verification

`tools/ui/validate-command-palette.ps1` verifies:

- production composition,
- command registration contract,
- global shortcut wiring,
- keyboard navigation/dismissal wiring,
- case-insensitive filtering and duplicate rejection,
- semantic-theme usage,
- later-phase boundary protection,
- negative/recovery fixtures,
- Windows/WPF runtime behavior against the production `MainWindow`.

The permanent Windows CI runner invokes this validator with deterministic fixtures and a runtime lane.
