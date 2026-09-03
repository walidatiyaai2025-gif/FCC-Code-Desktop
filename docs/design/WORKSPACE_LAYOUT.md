# FCC Code Desktop — Resizable Workspace Layout

**Task:** `FCCD-P02-004`  
**Phase:** P02 — Premium design system and shell  
**Status:** implementation contract

## Purpose

The main workbench uses a stable three-region shell beneath the P02 application chrome:

```text
Navigation / project context | Primary work surface | Context / explorer
```

The regions are production composition seams. P02-004 deliberately does not fabricate projects, sessions, tasks, tool output, or editor data before the tasks that own those surfaces.

## Layout contract

`WorkspaceLayout` owns:

- left, primary, and right content seams,
- two keyboard-focusable horizontal `GridSplitter` controls,
- semantic background/divider styling from P02-002,
- a `WorkspaceLayoutState` object for layout geometry,
- side-pane collapse, restore, reset, and bounded resize state,
- dark/light runtime parity,
- accessible region and splitter names.

Default geometry is intentionally dense and usable at the P02 target desktop size:

- left pane: 240 DIP,
- right pane: 300 DIP,
- primary pane: star-sized with a 320 DIP minimum,
- splitters: 4 DIP,
- side-pane resize bounds: 160–480 DIP.

`WorkspaceLayoutState` is presentation state only. P03 will decide how window/layout state is persisted; P02 does not write files, registry values, SQLite rows, or user settings.

## Integration boundary

`MainWindow.WorkspaceHost` remains the stable P02-003 composition boundary and now contains one `WorkspaceLayoutHost`. Later P02 tasks fill the content seams rather than replacing `MainWindow` or the workspace grid:

- P02-005 supplies navigation/project/session/task presentation,
- P02-006 supplies the bottom tool-panel framework within the primary work surface,
- P02-007 supplies command/keyboard interaction,
- P02-008 supplies common state components,
- P02-009 closes responsive/DPI behavior.

## Safety and recovery

Resize state is bounded. Invalid non-absolute `GridLength` values are rejected. Collapse preserves the last valid expanded width and restore reapplies a bounded value. `Reset()` returns both side panes to canonical defaults.

No process, filesystem, registry, Git, persistence, FCC, Unity, Blender, or owner-data action is performed by the layout state.

## Verification

`tools/ui/validate-workspace-layout.ps1` enforces:

- production composition through `WorkspaceHost`,
- three content seams and two real splitters,
- semantic-theme resources with no hard-coded palette,
- accessibility names,
- two-way layout-state bindings,
- default/bounded geometry,
- collapse/restore/reset behavior,
- presentation-only dependency boundary,
- deterministic negative/recovery fixtures.

On Windows CI, the runtime fixture creates the real `MainWindow`, resolves `WorkspaceLayoutHost`, exercises clamping, collapse/restore/reset, injects disposable content through all three seams, verifies splitters/hosts, and proves dark→light semantic background update. The fixture lives only under the runner temporary directory.
