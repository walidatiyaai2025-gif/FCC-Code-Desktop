# P08-004 — ConPTY terminal host

## Scope

`FCCD-P08-004` establishes the Windows ConPTY host used by later shell-profile and interactive-terminal work. It is intentionally isolated from the active P08-003 bounded non-interactive output pipeline: P08-004 owns pseudo-console creation, interactive byte streams, resize, child launch, and deterministic session cleanup only.

## Contract

The Application layer exposes Windows-agnostic terminal contracts:

- `TerminalSize` validates dimensions against the native `COORD` range.
- `ConPtyLaunchRequest` requires fully-qualified executable and working-directory paths and snapshots the argument list.
- `IConPtyTerminalHost` starts one interactive pseudo-console session.
- `IConPtyTerminalSession` exposes process identity, input/output streams, current size, terminal completion, resize, and async disposal.

Shell selection and profile discovery are deliberately deferred to `FCCD-P08-005`; the host accepts only an already-resolved executable path and arguments.

## Windows implementation

`WindowsConPtyTerminalHost` requires Windows 10 1809 / build 17763 or newer and uses the supported ConPTY APIs:

- `CreatePseudoConsole` / `ResizePseudoConsole` / `ClosePseudoConsole`;
- `STARTUPINFOEX` with `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`;
- suspended `CreateProcessW`, followed by job assignment before first user-code execution;
- a private kill-on-close Job Object so disposal cannot silently orphan the owned child tree;
- anonymous host↔pseudo-console pipes wrapped as managed streams;
- Unicode process creation and deterministic Windows command-line quoting.

The implementation does not use `ProcessStartInfo`, shell interpolation, `CREATE_NEW_CONSOLE`, forceful checkout/reset concepts, or any remote/network operation.

## Lifecycle and safety

The child is created suspended, assigned to the host-owned Job Object, and only then resumed. If launch fails before safe ownership is established, the partial native resources are closed and the suspended child is explicitly terminated when necessary. Session disposal closes input, closes the job to terminate any still-owned process tree, waits for root-process settlement, closes ConPTY, then releases streams and native handles.

Natural process completion closes the pseudo console so the output stream receives EOF. Resize is rejected after completion/disposal. The host never modifies the working directory contents.

## Cloud verification

The permanent hosted-Windows gate `P08-004 ConPTY Terminal Host` performs:

1. static contract/safety checks;
2. locked restore;
3. Release build of `FCCCodeDesktop.Terminal`;
4. focused Application contract unit tests;
5. real ConPTY launch against the resolved Windows `ComSpec` in a space/Arabic-containing working directory;
6. interactive input/output round trip;
7. live resize from 80×25 to 100×40;
8. clean exit and owner-file preservation.

The standard Windows CI, Workspace Search, and Large Workspace Safeguards workflows remain required non-regression gates before integration.

## Non-claims

This task does not implement bounded non-interactive log retention (P08-003), shell profiles (P08-005), Git Bash/WSL discovery (P08-006), interactive terminal UI (P08-007), or final process/terminal safety convergence (P08-008). It adds no owner-only evidence, does not close P08, does not authorize P09/P14, and does not change either existing owner-last release blocker.
