# FCC Code Desktop — Testing

## Purpose

P01 establishes deterministic unit and integration test lanes before product features expand. Test infrastructure must be repeatable from a fresh Windows checkout, use the repository dependency policy, and keep all disposable data outside owner-controlled paths.

## Projects

- `tests/FCCCodeDesktop.Testing` — shared test-only support library. It is not a discoverable test project.
- `tests/FCCCodeDesktop.UnitTests` — fast isolated unit lane.
- `tests/FCCCodeDesktop.IntegrationTests` — Windows filesystem/process integration lane using disposable workspaces.

All test dependencies use Central Package Management in `Directory.Packages.props`; project files must not declare package versions or version overrides. All three test projects participate in the repository lock-file policy.

## Test package baseline

The initial package versions are taken from the xUnit project template shipped with the repository-pinned .NET SDK `10.0.400`:

- `coverlet.collector` `6.0.4`
- `Microsoft.NET.Test.Sdk` `17.14.1`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `3.1.4`

Package updates must follow `docs/DEPENDENCY_POLICY.md` and regenerate committed lock files explicitly.

## Running tests

From the repository root:

```powershell
pwsh -NoProfile -File .\tools\testing\run-tests.ps1 -Suite unit
pwsh -NoProfile -File .\tools\testing\run-tests.ps1 -Suite integration
pwsh -NoProfile -File .\tools\testing\run-tests.ps1 -Suite all
```

After a locked restore and Release build, validation can skip repeated work:

```powershell
pwsh -NoProfile -File .\tools\testing\run-tests.ps1 -Suite all -Configuration Release -NoRestore -NoBuild
```

The runner accepts only `all`, `unit`, or `integration`; invalid suite values fail before any test process is launched.

## Deterministic validation

Run:

```powershell
pwsh -NoProfile -File .\tools\testing\validate-test-infrastructure.ps1 -RequireDotNet
```

The validator checks required test projects/files, central package ownership, solution membership, exact SDK resolution, locked restore, Release build, both test lanes, and invalid runner-input rejection.

## Disposable data and recovery

`TemporaryDirectory` creates unique workspaces under the operating-system temporary directory. Tests must derive paths through that workspace and must not write fixtures into user repositories, home folders, Desktop/Documents, provider directories, Unity projects, Blender projects, or other owner data.

The shared process helper redirects output, honors cancellation tokens, kills the disposable child process tree on cancellation, and leaves the temporary workspace reusable for recovery assertions. Integration coverage verifies:

- a happy process invocation under a path containing spaces and Arabic characters;
- a non-zero child-process failure without owner-data mutation;
- cancellation of a long-running child process;
- filesystem recovery after cancellation.

Temporary workspaces are deleted on disposal. Tests that intentionally fail must still use `using`/`finally` ownership so cleanup is deterministic.

## Scope boundary

P01-004 provides test infrastructure only. It does not implement the permanent GitHub Actions CI pipeline owned by P01-005, build metadata owned by P01-006, later product features, provider calls, Unity execution, Blender execution, or phase/ledger closure reconciliation.
