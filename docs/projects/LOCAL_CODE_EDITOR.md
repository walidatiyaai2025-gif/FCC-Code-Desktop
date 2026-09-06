# Local Code Editor

`FCCD-P06-005 — Locally bundled code editor` provides the production-native editing control that later P06 workspace lifecycle tasks will compose with the safe file service.

## Runtime and distribution boundary

The editor is implemented entirely with WPF types that ship with the desktop application and .NET runtime. It does not embed a browser, WebView, JavaScript editor, CDN asset, HTTP dependency, external executable, or runtime package download. No network access is required to construct or use the editor control.

`MainWindow` owns a production `LocalCodeEditor` resource so the control is part of the real application composition rather than a test-only mock. P06-006 will own attaching that editor to file tabs and the safe file-service lifecycle.

## Editor behavior

`CodeEditorControl` provides:

- a multiline, no-wrap, monospaced editing surface;
- horizontal and vertical scrolling for code-shaped content;
- native undo support and Tab input;
- a non-interactive line-number gutter that follows the visible editor line range;
- one-based caret line/column status derived deterministically from the current text and caret index;
- explicit editable/read-only mode;
- document and language labels for later workspace composition;
- semantic theme resources instead of hard-coded application colors;
- automation names on the editor, gutter, metadata, and status surfaces;
- Unicode text support through the WPF/.NET string model.

`CodeEditorTextMetrics` treats CRLF, LF, and lone CR as logical line breaks so line counts and caret metrics are deterministic independently of the current Windows newline convention.

## Safety and ownership boundaries

P06-005 deliberately does **not** read or write filesystem content. It does not decide encodings, persist a buffer, normalize source-file newlines, create tabs, detect external file changes, save, reload, implement dirty-state UX, search the workspace, or apply large-file policy. Those responsibilities remain with the existing/following tasks:

- `FCCD-P06-004` owns safe bounded filesystem text I/O and conflict detection;
- `FCCD-P06-006` owns tabs, file loading/saving, reload, and dirty-state behavior;
- `FCCD-P06-007` owns workspace content/file/regex search;
- `FCCD-P06-008` owns broader large-file/tree safeguards.

This boundary prevents the editor control from bypassing the safe file service or silently inventing persistence semantics.

## Validation boundary

Permanent validation is `tools/ui/validate-local-code-editor.ps1`. It provides static contract checks, destructive negative fixtures, and an executable Windows/WPF fixture that constructs the production resource, edits multilingual text, verifies line numbers/caret metrics/read-only state, and exercises scrolling-oriented multiline content.

The evidence class for P06-005 is cloud/self-test. The task introduces no new owner-only, provider, FCC, Unity, Blender, clean-machine, or manual requirement and does not alter `FINAL_OWNER_ACCEPTANCE_QUEUE`.
