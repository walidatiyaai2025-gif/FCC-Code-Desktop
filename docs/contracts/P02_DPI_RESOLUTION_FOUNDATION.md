# P02 DPI / Resolution Layout Foundation

## Status

Production P02 shell contract for `FCCD-P02-009`.

## Platform contract

- The WPF executable is explicitly **Per-Monitor V2 DPI aware** through `src/FCCCodeDesktop.App/app.manifest`.
- Layout decisions use WPF device-independent pixels (DIPs). The application does not apply a second manual scale transform on top of WPF DPI scaling.
- `UseLayoutRounding=true` and `SnapsToDevicePixels=true` remain enabled on the main window.
- The canonical minimum usable acceptance target remains **1366×768 at 100% scaling**. Later P17 acceptance still owns full screenshot/interaction verification across 1366×768, 1920×1080, 4K, and 125/150/175/200% scaling.

## Responsive workspace profiles

`WorkspaceViewportCoordinator` owns P02 presentation-only adaptation:

| Profile | WPF viewport width | Shell behavior |
|---|---:|---|
| Compact | `< 800 DIP` | collapse navigation and context panes |
| Standard | `800–1179.999… DIP` | show navigation, collapse context pane |
| Wide | `>= 1180 DIP` | show both side panes unless the user had already collapsed one |

When viewport height is below `560 DIP`, the bottom tool panel is collapsed to preserve the primary workspace. Only collapses forced by the responsive coordinator are automatically restored when space returns. A pane that the user had already collapsed is not reopened by responsive recovery.

## DPI transitions

`MainWindow` reevaluates the responsive policy on:

- initial `Loaded`,
- `SizeChanged`,
- WPF `DpiChanged`.

The current DPI scale is obtained from `VisualTreeHelper.GetDpi` and recorded by the coordinator for deterministic diagnostics/tests. Width/height thresholds remain DIP-based so monitor DPI changes do not create a second scaling system.

## Scope boundary

P02-009 is presentation infrastructure only. It does not persist pane choices, store monitor topology, modify files, invoke external processes, or implement later-phase settings/recovery behavior. Layout persistence remains P03+ work.

## Verification

`tools/ui/validate-dpi-layout.ps1` provides:

- static manifest/project/event/policy validation,
- negative fixtures for lost PerMonitorV2 awareness, manifest binding, DPI event response, and forced-pane recovery,
- executable Windows/.NET 10 fixture for wide/standard/compact behavior, DPI-scale capture, low-height collapse/recovery, user-collapse preservation, and invalid input rejection.

The validator is permanently enforced by `tools/ci/run-windows-ci.ps1` and protected by `tools/ci/validate-windows-ci.ps1`.
