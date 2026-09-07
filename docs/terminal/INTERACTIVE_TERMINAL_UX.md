# Interactive terminal UX — FCCD-P08-007

## Canonical scope

`FCCD-P08-007` composes the existing typed P08 ConPTY host into the Bottom Tool Panel terminal seam. It does not replace P08-004 process ownership and does not claim P08-008 final safety convergence.

## User interaction contract

- PowerShell and Command Prompt are available from validated Windows executable paths.
- Git Bash and WSL appear only when the read-only optional-shell detector reports them.
- Start and Close expose explicit terminal lifecycle ownership.
- UTF-8 input and output flow only through `IConPtyTerminalSession`; the UI does not bypass that boundary with ad-hoc process launch.
- Ctrl+C copies selected terminal text; without a selection it sends ETX to the active session.
- Ctrl+V sends clipboard text; terminal navigation keys map to terminal escape sequences.
- Terminal resize is debounced and forwarded to ConPTY.

## Presentation and responsiveness

The terminal uses a read-only `RichTextBox` document surface with bounded rendered scrollback and bounded pending output. Output arriving faster than the UI can paint is coalesced through a single scheduled dispatcher flush rather than rebuilding the whole transcript for every chunk. ANSI SGR foreground colors are rendered, including standard, bright, indexed, and true-color sequences; unknown control sequences are consumed conservatively rather than displayed as raw escape noise.

## Shutdown safety

Window close is cancelled until `InteractiveTerminalSurface.DisposeAsync()` completes. Session close cancels queued resize work, clears pending presentation work, cancels the output pump, disposes the ConPTY session, and awaits the pump before lifecycle completion. The P08-004 kill-on-close Job Object remains the authoritative owned-process cleanup boundary.

## Validation

`tools/terminal/validate-interactive-terminal-ux.ps1` provides static, negative/recovery, hosted-Windows/WPF runtime, ANSI-rendering, high-output bounding, input, resize, composition, and async-disposal coverage. `.github/workflows/p08-007-interactive-terminal-ux.yml` is the dedicated exact-head/exact-main gate.

Canonical integration evidence is `evidence/phases/P08/P08_007_INTEGRATED_RECONCILIATION_2026-09-08.md`. No owner-only evidence is required for P08-007. P08-008 remains separate and P09 remains prohibited until P08 is formally closed.
