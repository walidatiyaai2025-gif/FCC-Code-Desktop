# P08-006 — Optional Git Bash / WSL detection

## Scope

`FCCD-P08-006` adds read-only discovery of optional local shells used by later terminal-profile and UX work. It deliberately does not start a shell, create a ConPTY session, mutate process/global environment state, enumerate or change WSL distributions, or contact any remote endpoint.

## Contract

The Application layer exposes `IOptionalShellDetector` plus immutable typed results:

- `OptionalShellKind.GitBash`
- `OptionalShellKind.Wsl`
- fully-qualified executable path
- detection source (`StandardInstall`, `PathDerivedGitInstall`, or `WindowsSystem`)

Detection claims only that a matching executable is present. It does **not** claim that the shell can launch successfully, that a WSL distribution is installed/configured, or that any profile has been selected. Those runtime semantics belong to later P08 work.

## Git Bash discovery

The Windows detector checks deterministic Git-for-Windows roots under:

- `%ProgramFiles%\\Git`
- `%ProgramFiles(x86)%\\Git`
- `%LocalAppData%\\Programs\\Git`

A Git Bash result is accepted only when the candidate root contains both a Git executable (`cmd\\git.exe` or `bin\\git.exe`) and `bin\\bash.exe` (falling back to `usr\\bin\\bash.exe`).

PATH-derived detection is conservative: each PATH entry is normalized and only roots that also satisfy the Git-for-Windows executable pair are accepted. A generic `bash.exe` from Cygwin/MSYS/another product is therefore not mislabeled as Git Bash.

Duplicate executable paths are removed case-insensitively and standard-install evidence wins over later PATH-derived duplicates.

## WSL discovery

WSL detection is limited to the presence of `%SystemRoot%\\System32\\wsl.exe`. No `wsl.exe` command is executed, so discovery cannot hang on distribution startup, produce localized parser ambiguity, or mutate WSL state.

Presence of `wsl.exe` is intentionally represented only as executable detection. Distribution availability and actual launch behavior are outside P08-006.

## Safety and bounds

- no process creation;
- no registry writes;
- no environment mutation;
- no filesystem writes;
- no network access;
- cancellation is checked before and during candidate enumeration;
- environment-derived roots are normalized defensively and malformed entries are ignored;
- returned collections are immutable snapshots with deterministic ordering.

## Cloud validation

The focused unit suite uses disposable filesystem fixtures to prove:

- standard Git Bash detection;
- WSL executable detection;
- case-insensitive de-duplication;
- PATH-derived portable Git-for-Windows detection;
- rejection of unrelated generic `bash.exe` candidates;
- empty results when candidates are absent;
- pre-cancelled execution.

A dedicated hosted-Windows workflow runs the focused test suite in Release configuration. Standard Windows CI, Workspace Search, and Large Workspace Safeguards remain mandatory non-regression gates before integration.

## Non-claims

This task does not implement P08-003 bounded output, P08-004 ConPTY hosting, P08-005 shell profiles, P08-007 terminal UI, or P08-008 final process-safety convergence. It adds no owner-only evidence, does not close P08, does not authorize P09/P14, and does not alter either existing owner-last release blocker.
