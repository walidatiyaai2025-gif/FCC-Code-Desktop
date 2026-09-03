# FCC Code Desktop — Design Tokens and Typography

**Task:** `FCCD-P02-001`  
**Phase:** P02 — Premium design system and shell  
**Status:** implementation contract

## Purpose

This document defines the reusable, theme-neutral visual primitives and text hierarchy that every P02 and later WPF surface must consume instead of inventing per-view spacing, radii, control density, icon sizes, or typography.

The implementation lives under `src/FCCCodeDesktop.App/DesignSystem/` and is loaded globally by `App.xaml`.

## Ownership boundary

`FCCD-P02-001` owns:

- spacing scale,
- common inset values,
- corner-radius hierarchy,
- stroke/focus geometry,
- shared control-height density,
- icon-size primitives,
- interface and code font-family choices,
- typography sizes and line heights,
- named `TextBlock` typography roles.

It intentionally does **not** define colors, brushes, theme palettes, control templates, titlebar chrome, layout panels, feature views, or shell behavior. Dark/light semantic colors and brushes belong to `FCCD-P02-002` and must be composable without changing this contract.

## Design-token resource contract

Theme-neutral geometry is declared in `DesignTokens.xaml`.

### Spacing

The base scale is intentionally compact for an IDE/workbench:

| Resource | DIPs |
|---|---:|
| `FccSpace0` | 0 |
| `FccSpace2` | 2 |
| `FccSpace4` | 4 |
| `FccSpace6` | 6 |
| `FccSpace8` | 8 |
| `FccSpace12` | 12 |
| `FccSpace16` | 16 |
| `FccSpace20` | 20 |
| `FccSpace24` | 24 |
| `FccSpace32` | 32 |
| `FccSpace40` | 40 |
| `FccSpace48` | 48 |

Matching all-edge `Thickness` resources are provided for common inset values from 2 through 24 DIPs (`FccInset2` … `FccInset24`). Views should prefer these resources over arbitrary `Margin`/`Padding` literals.

### Radius

| Resource | DIPs | Intended use |
|---|---:|---|
| `FccRadiusNone` | 0 | edge-to-edge workbench surfaces |
| `FccRadiusSmall` | 4 | compact controls/chips |
| `FccRadiusMedium` | 6 | standard controls/cards |
| `FccRadiusLarge` | 10 | contained panels/popovers |
| `FccRadiusXLarge` | 14 | exceptional elevated surfaces |

### Density and interaction geometry

- `FccStrokeThin` = 1 DIP.
- `FccFocusRingThickness` = 2 DIPs on all sides; P02-002 supplies the actual focus brush.
- Control heights: compact 28, standard 32, comfortable 36 DIPs.
- Icon sizes: small 14, medium 16, large 20 DIPs.

These values are layout primitives, not permission to hard-code component-specific spacing everywhere. Reusable controls should expose semantic component resources when repeated patterns emerge.

## Typography contract

Typography is declared in `Typography.xaml`.

### Font families

- Interface/UI: `Segoe UI`.
- Code/monospace: `Consolas`.

Both are Windows-native choices for the supported Windows 10/11 baseline, so P02-001 introduces no bundled-font asset, license, network, or package dependency.

### Roles

| Role/style | Size | Line height | Weight | Font |
|---|---:|---:|---|---|
| `FccTextDisplay` | 22 | 30 | SemiBold | Interface |
| `FccTextSection` | 15 | 22 | SemiBold | Interface |
| `FccTextBody` | 13 | 19 | Normal | Interface |
| `FccTextMetadata` | 12 | 18 | Normal | Interface |
| `FccTextStatus` | 11 | 16 | SemiBold | Interface |
| `FccTextCode` | 13 | 19 | Normal | Monospace |

`FccTextBase` contains the shared interface family, body size/line-height defaults, normal weight, and WPF display text formatting. All named roles derive from it.

The display role is deliberately capped at 22 DIPs: FCC Code Desktop is a dense professional workbench, not a marketing page. Feature views must select from these roles instead of introducing arbitrary heading scales.

## Usage

Use keyed styles and tokens from any WPF view after `App.xaml` composition:

```xml
<TextBlock Style="{StaticResource FccTextSection}"
           Text="Sessions" />

<Border Padding="{StaticResource FccInset12}"
        CornerRadius="{StaticResource FccRadiusMedium}">
    <TextBlock Style="{StaticResource FccTextBody}"
               Text="No session selected." />
</Border>
```

Theme-dependent foreground/background/border/focus/selection values must reference semantic resources introduced by P02-002 rather than being added to these files.

## Deterministic validation

`tools/ui/validate-design-system.ps1` enforces:

- both dictionaries exist and parse as XAML/XML,
- unique resource keys,
- exact mandatory spacing/radius/density/typography values,
- required typography roles and token-based setters,
- dictionary composition order in `App.xaml`,
- no color/brush values leaking into P02-001,
- no bundled/external font dependency,
- compact workbench display scale,
- line-height readability invariants,
- deterministic negative fixtures for missing tokens, theme leakage, merge-order regression, hard-coded typography, and duplicate keys,
- clean recovery after each negative fixture.

The canonical Windows CI runner executes this validator with its negative/recovery fixtures after the normal Release build and test baseline. The Release build itself provides the WPF XAML compiler check for these dictionaries.
