# P02 Common State Components

`FCCD-P02-008` establishes reusable shell-level presentation primitives for truthful empty, loading, information, success, warning, error, unavailable, offline, and blocked states.

## Contract

`CommonStateKind` is the presentation taxonomy. `CommonStateModel` carries concise title/message text, optional technical detail, and an optional action label/command pair. Loading is explicitly represented by `IsBusy`; detail and action visibility are derived from the model rather than inferred by individual views.

`CommonStateSurface` is the standard contained state panel. It uses the existing semantic theme brushes and typography roles, exposes an accessible name, renders a bounded indeterminate loading indicator only for the loading state, and exposes a keyboard-focusable action only when a command is present.

`CommonStatusBadge` is the compact companion for status rows and dense shell surfaces. Both controls communicate state with text plus a semantic indicator; color is never the only signal.

## Semantic mapping

- Empty: neutral surface and muted indicator.
- Loading / Info: information semantics.
- Success: success semantics.
- Warning / Unavailable / Offline: warning semantics.
- Error / Blocked: error semantics.

All colors are consumed through `DynamicResource` semantic brushes so dark/light theme replacement continues to work without view rewrites.

## Production shell integration

The existing Projects / Sessions / Tasks shell no longer renders an unexplained blank primary region when its later-phase content seam is null. `WorkspaceNavigationState` exposes a truthful per-section `SelectedEmptyState` and `HasSelectedContent`; `WorkspaceSectionSurface` displays the shared empty-state surface until real content is supplied.

This is a presentation/state seam only. It does not fabricate project, session, task, runtime, queue, permission, rate-limit, persistence, file, Git, terminal, Unity, Blender, or provider behavior before those owning phases.

## Validation

`tools/ui/validate-common-states.ps1` enforces:

- the state taxonomy and model invariants,
- semantic-theme-only visual consumption,
- accessible reusable surface/badge contracts,
- loading/detail/action visibility behavior,
- workspace empty-state composition,
- negative/recovery fixtures,
- a disposable Windows/WPF runtime fixture including dark/light theme parity.

The validator is part of the permanent Windows CI baseline. P02-008 does not close P02 or advance P03; task/phase closure remains evidence-driven under the canonical worker protocol.
