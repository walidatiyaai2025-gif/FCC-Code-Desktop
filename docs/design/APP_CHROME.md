# FCC Code Desktop — Premium Application Chrome

**Task:** `FCCD-P02-003`  
**Phase:** P02 — Premium design system and shell  
**Status:** implementation contract

## Purpose

FCC Code Desktop uses a deliberate application-owned title bar while preserving Windows-native window lifecycle behavior. The chrome must feel integrated with the P02 design system, respond to dark/light theme switching, remain keyboard/accessibility aware, and provide a stable shell seam for later P02 workspace surfaces.

The implementation is intentionally narrow:

```text
App.xaml
  -> MainWindow
      -> AppTitleBar
      -> WorkspaceHost (empty extension seam for P02-004+)
```

No project/session/task feature UI is fabricated by P02-003.

## Ownership boundary

`FCCD-P02-003` owns:

- the production root WPF `MainWindow`,
- custom app-owned title/titlebar presentation,
- native-resizable `WindowChrome` composition,
- minimize/maximize/restore/close actions,
- caption drag/double-click/snap behavior through WPF `WindowChrome`,
- semantic titlebar background/divider/foreground states,
- vector caption-control glyphs owned by this repository,
- hover/pressed/focus/disabled caption-control styling,
- keyboard-focus visibility and accessible names for caption controls,
- titlebar extension seams for future context and status content,
- dark/light runtime parity of the visible chrome,
- deterministic static and Windows runtime validation.

It does **not** own:

- the main resizable workspace layout (`FCCD-P02-004`),
- navigation/projects/sessions/tasks (`FCCD-P02-005`),
- bottom tool panels (`FCCD-P02-006`),
- command palette behavior (`FCCD-P02-007`),
- common loading/error/status components (`FCCD-P02-008`),
- DPI/layout acceptance closure (`FCCD-P02-009`),
- persisted window/layout/theme settings (P03),
- final application icon/product artwork (P18).

## Window strategy

`MainWindow` uses:

- `WindowStyle=None`,
- `ResizeMode=CanResize`,
- WPF `System.Windows.Shell.WindowChrome`,
- a 40-DIP caption region,
- a 6-DIP native resize border,
- application-owned caption buttons,
- `UseAeroCaptionButtons=False`.

The application does **not** use `AllowsTransparency=True`. That avoids replacing the native window composition path with a layered-window path that can degrade rendering behavior and complicate resize/snap semantics.

The caption region remains a real `WindowChrome` caption region. Non-interactive title area therefore preserves native drag, double-click maximize/restore, and Windows snap interactions. Interactive controls opt into `WindowChrome.IsHitTestVisibleInChrome` explicitly.

## Application identity boundary

The visible chrome uses the product name `FCC Code Desktop` as text. P02-003 deliberately does not introduce a temporary initials badge, copied third-party mark, stock logo, or placeholder application icon.

The original production application icon and full identity system remain owned by P18. This prevents a temporary P02 asset from becoming de facto release branding while still giving the titlebar a finished textual hierarchy.

## Titlebar extension seams

`AppTitleBar` exposes dependency properties:

```csharp
string ProductName
object? ContextContent
object? StatusContent
```

`ContextContent` and `StatusContent` are production presentation seams, not mocked feature data. Later P02 tasks can inject project/branch/runtime/tool-health presentation without replacing the window/chrome architecture.

The default P02-003 window leaves those seams empty rather than displaying fictional readiness, repository, branch, runtime, or tool values.

## Visual contract

Component geometry lives in `DesignSystem/AppChrome.xaml` and composes after:

1. `DesignTokens.xaml`,
2. `Typography.xaml`,
3. the active semantic theme.

The chrome defines component-only resources such as caption height, caption-button width, resize-border thickness, padding, and original vector window-control geometry.

Theme appearance always comes from P02-002 semantic resources:

- `FccBrushCanvas`,
- `FccBrushSurface`,
- `FccBrushBorder`,
- `FccBrushDivider`,
- `FccBrushTextPrimary`,
- `FccBrushTextSecondary`,
- `FccBrushHoverOverlay`,
- `FccBrushPressedOverlay`,
- `FccBrushFocus`,
- `FccBrushErrorBackground`,
- `FccBrushError`.

No chrome-specific color palette is introduced.

Theme-dependent resources use `DynamicResource`, allowing the already-instantiated titlebar to update when `ThemeService` replaces the dark/light theme dictionary.

## Caption controls

The titlebar provides four explicit control identities:

- Minimize,
- Maximize,
- Restore,
- Close.

Maximize and Restore visibility follows the actual host `WindowState`. Their shared action toggles only between `Normal` and `Maximized`. Minimize updates the host to `Minimized`; Close closes the host window.

These handlers are deliberately presentation-local. They contain no runtime, persistence, Git, terminal, process, project, session, or task orchestration.

If an `AppTitleBar` is instantiated outside a host window (for example in a design/test surface), window actions safely no-op rather than dereferencing a missing host.

## Accessibility and keyboard behavior

Every non-text caption control has an explicit automation name:

- `Minimize window`,
- `Maximize window`,
- `Restore window`,
- `Close window`.

Caption controls are focusable and expose a visible semantic focus ring. Mouse hover, pressed, disabled, and close-action emphasis states are styled through semantic resources rather than relying on default WPF rendering.

The titlebar intentionally does not trap keyboard focus. Standard window-level keyboard behavior remains owned by Windows/WPF, while later P02 command/navigation work adds application commands without replacing the window chrome.

## Shell seam

`MainWindow` contains a single empty `WorkspaceHost` beneath the titlebar. It is a composition seam for `FCCD-P02-004` and later P02 tasks, not a placeholder screen.

P02-003 does not add temporary cards, fake navigation, debug labels, mock project data, or marketing content to that host.

## Deterministic verification

`tools/ui/validate-app-chrome.ps1` verifies the static contract:

- app startup resolves to `MainWindow`,
- resource composition order is tokens -> typography -> theme -> app chrome,
- no new color palette or hard-coded hex theme colors exist in chrome files,
- theme brushes are consumed dynamically,
- `WindowStyle=None` + `ResizeMode=CanResize` remain enforced,
- `WindowChrome` caption/resize/native-button settings exist,
- `AllowsTransparency=True` is rejected,
- `AppTitleBarHost` and the later-workspace `WorkspaceHost` seam exist,
- all caption buttons and automation names exist,
- maximize/restore visibility follows actual `WindowState`,
- titlebar dependency-property extension seams exist,
- code-behind remains presentation-only.

Negative fixtures deliberately remove or weaken startup, native chrome, accessibility, semantic styling, workspace seam, and maximize/restore behavior, then prove the unchanged source recovers.

On GitHub-hosted Windows, the validator also builds/runs a disposable WPF fixture against the production app project. The fixture verifies:

- the production window loads,
- `WindowChrome` is attached with the required caption and resize geometry,
- caption controls are discoverable and accessible,
- caption controls remain interactive inside the chrome region,
- maximize -> restore -> minimize transitions update the real host `WindowState`,
- an unattached titlebar safely handles a window-action click,
- extension content can be supplied through the production dependency-property seams,
- the titlebar surface changes on dark -> light theme switch,
- light -> dark recovery restores the original semantic surface.

The fixture is created under the system temporary directory and removed after execution. It does not touch owner repositories or user data.
