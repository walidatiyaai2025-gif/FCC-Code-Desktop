# P08-004 — ConPTY terminal host

## Scope

`FCCD-P08-004` establishes the Windows ConPTY host used by shell-profile and interactive-terminal work. It is isolated from the P08-003 bounded non-interactive output pipeline: P08-004 owns pseudo-console creation, interactive byte streams, resize, child launch, and deterministic session cleanup only.

## Contract

The Application layer exposes Windows-agnostic terminal contracts:

- `TerminalSize` validates dimensions against the native `COORD` range.
- `ConPtyLaunchRequest` requires fully-qualified executable and working-directory paths and snapshots the argument list; an omitted argument sequence becomes an immutable empty profile.
- `IConPtyTerminalHost` starts one interactive pseudo-console session.
- `IConPtyTerminalSession` exposes process identity, input/output streams, current size, terminal completion, resize, and async disposal.

Shell selection and profile discovery belong to `FCCD-P08-005`; this host accepts only an already-resolved executable path and arguments. Hosted acceptance deliberately exercises the integrated CMD profile contract exactly: `cmd.exe` with no profile arguments.

## Windows implementation

`WindowsConPtyTerminalHost` requires Windows 10 1809 / build 17763 or newer and uses the supported ConPTY/process APIs:

- `CreatePseudoConsole` / `ResizePseudoConsole` / `ClosePseudoConsole`;
- `STARTUPINFOEX` with `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`;
- `PROC_THREAD_ATTRIBUTE_JOB_LIST` to assign the child to a private Job Object atomically during `CreateProcessW`, before its initial thread can execute user code;
- `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` so disposal terminates the complete owned process tree;
- `STARTF_USESTDHANDLES` with null standard handles so redirected CI/parent handles cannot become an unintended EOF/input source for the ConPTY client;
- `CreateProcessW` with `bInheritHandles=false`, a mutable command line beginning with the fully-qualified quoted executable, and no shell interpolation;
- anonymous host↔pseudo-console pipes wrapped as managed streams;
- Unicode process creation and deterministic Windows command-line quoting.

The host does not use `CREATE_SUSPENDED`, `ResumeThread`, post-create `AssignProcessToJobObject`, `ProcessStartInfo`, `CREATE_NEW_CONSOLE`, or remote/network operations.

## Lifecycle and safety

The kill-on-close Job Object is supplied in the process attribute list, so ownership is established by process creation rather than by a suspend/assign/resume race. PTY-side setup handles are released after successful child creation while the host-facing input/output stream handles remain owned by the session.

Natural root completion is observed through the retained process handle. On Windows builds that support `ReleasePseudoConsole`, the host releases the pseudoconsole after root settlement so output can drain to EOF without prematurely closing the interactive session; older supported builds use the close path. Resize is rejected after completion or disposal.

`DisposeAsync` closes input, closes the Job Object to terminate any still-owned root/descendants, waits for root settlement, then releases output, ConPTY, process, and native handles. Disposal is idempotent. The host never modifies working-directory contents itself and never targets unrelated processes by name or PID.

## Cloud verification

The permanent hosted-Windows gate `P08-004 ConPTY Terminal Host` performs:

1. static contract/safety checks, including atomic Job assignment and isolated standard-handle setup;
2. locked solution restore;
3. Release build of `FCCCodeDesktop.Terminal`;
4. focused Application contract unit tests;
5. missing-executable, missing-working-directory, and pre-cancelled launch checks;
6. real ConPTY launch against the resolved Windows `ComSpec` using the exact empty-argument CMD profile in a space/Arabic-containing working directory;
7. filesystem-backed proof that injected terminal input actually executes, plus independently constructed output markers that cannot pass from input echo alone;
8. live resize from 80×25 to 100×40 followed by another successful command/output round trip;
9. clean exit, output EOF, and preservation of pre-existing owner data;
10. a second live session whose owned PowerShell descendant is terminated by `DisposeAsync` while an unrelated sentinel process remains alive.

The standard Windows CI, Workspace Search, and Large Workspace Safeguards workflows are required non-regression gates on the exact candidate before integration and again on the resulting canonical main where their path triggers apply.

## Acceptance boundary

P08-004 is cloud-testable and has no owner-only acceptance substitute. A failed hosted ConPTY fixture is a code/test defect and blocks integration. Task closure requires the implementation PR to be normally merged only after its exact-head focused ConPTY gate and permanent non-regression gates are green, followed by exact-main verification and canonical task reconciliation.

## Non-claims

This task does not implement bounded non-interactive log retention (P08-003), shell-profile selection (P08-005), Git Bash/WSL discovery (P08-006), interactive terminal UI/copy-paste presentation (P08-007), or final P08 process/terminal safety convergence (P08-008). It adds no owner-only evidence, does not close P08, does not authorize P09/P14, and does not change either existing owner-last release blocker.
