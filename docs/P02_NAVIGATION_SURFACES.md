# P02 Navigation / Projects / Sessions / Tasks Surface Contract

## Scope

This document records the P02 presentation contract for `FCCD-P02-005`.

The shell exposes three first-class navigation destinations:

- Projects
- Sessions
- Tasks

P02 owns the production interaction and composition seams only. P03 owns persistence and durable entity state. P04 owns the real FCC runtime. P05 owns complete conversation/session/task experience.

## Production composition

`MainWindow` creates one shared `WorkspaceNavigationState` and injects it into both:

- `NavigationSurface` in `WorkspaceLayout.LeftContent`
- `WorkspaceSectionSurface` in `WorkspaceLayout.PrimaryContent`

The navigation control changes `WorkspaceNavigationState.SelectedSection` through `SelectSectionCommand`. The primary surface binds its title, description and content host to the same state instance. This prevents the shell from becoming a static mock while avoiding fabricated project/session/task records.

## State seams

`WorkspaceNavigationState` exposes independent content seams:

- `ProjectsContent`
- `SessionsContent`
- `TasksContent`

`SelectedContent` follows `SelectedSection`. Later phases may inject their production views/view models without replacing the P02 shell architecture.

No file IO, SQLite, FCC runtime calls, process execution, registry access or persistence is permitted inside this P02 state object or its controls.

## Accessibility and interaction

Each primary navigation destination has an explicit automation name and keyboard-focus treatment. Selected, hover, pressed, disabled and focus states use semantic theme resources rather than hard-coded colors.

The navigation surface and selected section surface share the dark/light semantic theme contract from P02-002.

## Verification

The permanent Windows CI baseline runs:

```powershell
pwsh -NoProfile -File .\tools\ui\validate-navigation-surfaces.ps1 -RunFixtures -RequireRuntime
```

The validator checks static composition, semantic-theme usage, accessibility names, state/command seams and phase-boundary violations. Its runtime WPF fixture verifies shared state, Projects/Sessions/Tasks switching, independent content seams, command execution, invalid-section rejection with state preservation, named control creation and dark/light dynamic theme behavior.

`FCCD-P02-005` is eligible for task closure only after its exact PR head passes Windows CI, the PR is normally merged, and exact resulting `main` remains green. This document does not claim P02 phase closure or authorize P03 work.
