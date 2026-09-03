# P02 Bottom Tool Panel Framework Contract

## Scope

This document records the production shell contract for `FCCD-P02-006 — Bottom tool panel framework`.

P02 owns the bottom-panel interaction, resizing, selection and future-content seams only. It does not implement terminal process hosting, diagnostics, runtime output collection, persistence or later-phase feature behavior.

## Production composition

`MainWindow` creates shared production presentation state for the workspace layout and bottom tool panel. The same `WorkspaceLayoutState` instance is injected into both `WorkspaceLayout` and `BottomToolPanel`.

The center workspace region is vertically composed as:

1. primary workspace content,
2. keyboard-focusable horizontal resize splitter,
3. bottom tool-panel region.

The panel has an expanded default height, bounded resize range and a collapsed header-only height. Collapse/restore is command-driven through `WorkspaceLayoutState.ToggleBottomPanelCommand`; the last valid expanded height is preserved for restoration.

## Tool-panel content seams

`BottomToolPanelState` exposes three structural destinations:

- Output
- Problems
- Terminal

These are framework slots, not claims that the corresponding later-phase engines already exist. Each destination has an independent content seam (`OutputContent`, `ProblemsContent`, `TerminalContent`) and `SelectedContent` follows the selected destination. Later phases can inject production views/view models without replacing the P02 shell.

`Terminal` is only a UI composition seam in P02-006. ConPTY, process supervision and interactive terminal behavior remain owned by P08.

## Visual and accessibility contract

- The panel consumes semantic dark/light resources only; no hard-coded palette is permitted.
- Output, Problems and Terminal are keyboard-focusable controls with explicit automation names.
- Selected, hover, pressed, disabled and keyboard-focus states use the shared semantic interaction resources.
- The resize splitter and collapse/restore control expose automation names.
- The collapsed state retains the panel header so the user can restore the panel without depending on a later command-palette feature.

## Phase boundaries

P02-006 must not introduce:

- SQLite or settings persistence (P03),
- FCC runtime output wiring (P04/P05),
- standardized empty/loading/error components (P02-008),
- command-palette/global keyboard architecture (P02-007),
- ConPTY/process/terminal implementation (P08),
- DPI closure work owned by P02-009.

## Verification

The permanent Windows CI baseline runs:

```powershell
pwsh -NoProfile -File .\tools\ui\validate-bottom-tool-panel.ps1 -RunFixtures -RequireRuntime
```

The validator enforces production composition, semantic styling, accessibility names, resize/collapse state, tool selection/content seams, phase boundaries and deterministic negative/recovery cases. Its Windows/WPF runtime fixture verifies shared layout state, min/max height clamping, collapse/restore preservation, Output/Problems/Terminal switching, invalid selection rejection with state preservation, named controls and live dark/light theme parity.

`FCCD-P02-006` becomes eligible for task closure only after its exact PR head passes the complete Windows Release baseline, the implementation is normally merged, and exact resulting `main` remains green. This contract does not close P02 or authorize P03.
