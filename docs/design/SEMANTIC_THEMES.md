# FCC Code Desktop — Dark/Light Semantic Themes

**Task:** `FCCD-P02-002`  
**Phase:** P02 — Premium design system and shell  
**Status:** implementation contract

## Purpose

FCC Code Desktop exposes one semantic appearance contract with two complete value sets: dark and light. Feature views consume semantic brushes and do not select raw palette colors directly.

The theme dictionaries live under:

```text
src/FCCCodeDesktop.App/DesignSystem/Themes/
  Theme.Dark.xaml
  Theme.Light.xaml
```

`App.xaml` loads the dark theme by default after the theme-neutral P02-001 design-token and typography dictionaries. `ThemeService` provides the runtime seam for switching between dark and light without changing component architecture.

## Ownership boundary

`FCCD-P02-002` owns:

- dark and light semantic color values,
- brush resources mapped one-to-one to semantic colors,
- canvas/surface hierarchy,
- foreground hierarchy,
- borders/dividers,
- accent/action hover/pressed states,
- focus and selection semantics,
- hover/pressed/disabled overlays,
- success/warning/error/info foreground/background semantics,
- runtime theme switching and switch-failure recovery,
- deterministic theme-contract and contrast verification.

It does **not** own titlebar/window chrome, shell layout, navigation surfaces, tool panels, command palette, state components, settings persistence, or later feature UI. Those remain in their canonical P02/P03 tasks.

## Consumption rule

Views and reusable controls use `FccBrush*` resources. Raw `FccColor*` resources are palette backing values and are primarily consumed by the brush definitions.

```xml
<Border Background="{DynamicResource FccBrushSurface}"
        BorderBrush="{DynamicResource FccBrushBorder}">
    <TextBlock Foreground="{DynamicResource FccBrushTextPrimary}"
               Style="{StaticResource FccTextBody}" />
</Border>
```

Use `DynamicResource` for theme-dependent brushes so existing controls observe runtime dictionary replacement without architectural reconstruction. Geometry and typography remain `StaticResource`-based because they are theme-neutral P02-001 resources.

## Semantic resource families

Both theme dictionaries expose the exact same keys.

### Canvas and surfaces

- `FccBrushCanvas`
- `FccBrushSurface`
- `FccBrushSurfaceRaised`
- `FccBrushSurfaceSubtle`

### Text

- `FccBrushTextPrimary`
- `FccBrushTextSecondary`
- `FccBrushTextMuted`
- `FccBrushTextDisabled`
- `FccBrushTextInverse`

### Structure

- `FccBrushBorder`
- `FccBrushDivider`

### Accent/action

- `FccBrushAccent`
- `FccBrushAccentHover`
- `FccBrushAccentPressed`
- `FccBrushAccentForeground`

### Interaction

- `FccBrushFocus`
- `FccBrushSelectionBackground`
- `FccBrushSelectionForeground`
- `FccBrushHoverOverlay`
- `FccBrushPressedOverlay`
- `FccBrushDisabledOverlay`

### Status

- `FccBrushSuccess` / `FccBrushSuccessBackground`
- `FccBrushWarning` / `FccBrushWarningBackground`
- `FccBrushError` / `FccBrushErrorBackground`
- `FccBrushInfo` / `FccBrushInfoBackground`

Color alone must never become the only status indicator. Later components combine these semantics with text, icons, labels, or structural state as required by the UI standard.

## Contrast contract

The deterministic validator requires at least:

- 4.5:1 for primary text on canvas,
- 4.5:1 for secondary text on canvas,
- 4.5:1 for accent foreground on accent,
- 4.5:1 for selection foreground/background,
- 4.5:1 for success/warning/error/info foreground/background pairs,
- 3:1 for the focus indicator against the canvas.

The validator also requires a genuinely dark dark-canvas and genuinely light light-canvas, preventing an accidental near-identical pair from satisfying key-equivalence checks.

## Runtime switching and recovery

`ThemeService` accepts the root application `ResourceDictionary` and exposes:

```csharp
AppearanceTheme? CurrentTheme
void Apply(AppearanceTheme theme)
bool TryApply(AppearanceTheme theme, out Exception? error)
```

The switch sequence is intentionally transactional:

1. Resolve the requested assembly component resource.
2. Load the complete compiled WPF dictionary with `Application.LoadComponent`.
3. Validate the candidate's `FccThemeName` identity.
4. Insert the valid candidate at the current theme position.
5. Remove prior recognized theme dictionaries.
6. If replacement cleanup fails, remove the candidate and preserve the prior theme.

Unsupported themes are rejected without mutating the current resources. Applying the already-active theme is idempotent.

Theme recognition is based on the semantic `FccThemeName` identity rather than `ResourceDictionary.Source`. This is deliberate: `App.xaml` may materialize the default dictionary from a relative XAML source, while runtime switching loads a referenced component directly. Identity-based recognition avoids URI/base-context coupling and ensures the first switch replaces rather than duplicates the default theme.

P02-002 intentionally does not persist the selected theme; canonical settings persistence belongs to P03.

## Verification

`tools/ui/validate-semantic-themes.ps1` verifies:

- both XAML dictionaries parse,
- identical semantic key sets,
- exact Color → SolidColorBrush mapping,
- opaque structural/status colors and bounded translucent interaction overlays,
- dark/light differentiation,
- required contrast ratios,
- default dark composition after P02-001 resources,
- safe compiled-component loading and rollback contract,
- deterministic negative fixtures for missing resources, incorrect brush mapping, low contrast, wrong theme identity, default-theme composition regression, and rollback removal,
- recovery after all negative fixtures,
- a disposable Windows WPF runtime fixture that loads the real compiled token/typography/theme resources and exercises dark→light, idempotent apply, invalid-theme rejection with state preservation, and light→dark recovery while preserving non-theme dictionaries.

The canonical GitHub-hosted Windows Release CI runs the semantic-theme validator with both negative fixtures and the runtime fixture after the normal solution Release build/tests and the P02-001 design-system contract.
