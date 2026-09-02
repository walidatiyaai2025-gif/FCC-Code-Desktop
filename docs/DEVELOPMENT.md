# FCC Code Desktop — Developer Bootstrap

## Prerequisites

- Windows 10/11 x64 for the canonical product build environment.
- .NET SDK `10.0.400` exactly. `global.json` disables SDK roll-forward and prerelease selection.
- PowerShell 7 (`pwsh`) for deterministic validation and test-runner scripts.
- No provider, Unity, or Blender installation is required to restore, build, or run the P01 unit/integration infrastructure.

Run `dotnet --version` from the repository root before restore. It must report `10.0.400`; use an explicit dependency/toolchain update PR to change that pin.

## Restore and build

From the repository root:

```powershell
dotnet --version
dotnet restore .\FCCCodeDesktop.sln --locked-mode
dotnet format .\FCCCodeDesktop.sln --verify-no-changes --no-restore
dotnet build .\FCCCodeDesktop.sln -c Release --no-restore
```

`FCCD-P01-001` adds no third-party package dependencies. `FCCD-P01-002` uses the analyzers shipped with the .NET 10 SDK and adds no analyzer package dependency. `FCCD-P01-003` establishes the SDK/package/lock policy without adding a product dependency. `FCCD-P01-004` adds only test-infrastructure dependencies using that central policy. Later P01 tasks own permanent CI and build/version metadata.

## Dependency and lock policy

Dependency policy is defined by `global.json`, `Directory.Packages.props`, `Directory.Build.props`, the committed per-project `packages.lock.json` files, and `docs/DEPENDENCY_POLICY.md`.

Normal restores are locked. Package versions belong only in `Directory.Packages.props`; project `PackageReference` items must not carry `Version` or `VersionOverride`. Version ranges, floating versions, and implicit SDK movement are prohibited.

When intentionally changing a package version or dependency graph, edit the central manifest first and regenerate lock files explicitly:

```powershell
dotnet restore .\FCCCodeDesktop.sln -p:RestoreLockedMode=false --force-evaluate --nologo
```

Review every resulting `packages.lock.json` diff and commit the manifest and lock changes together. A normal locked restore must then pass:

```powershell
dotnet restore .\FCCCodeDesktop.sln --locked-mode --nologo
```

Run the deterministic dependency-policy validator with:

```powershell
pwsh -NoProfile -File .\tools\dependencies\validate-dependency-policy.ps1 -RequireDotNet
```

The validator checks the exact SDK pin, central-version rules, committed lock coverage, real-solution locked restore/build behavior, and disposable local-feed negative/recovery fixtures. It does not require FCC, Unity, Blender, an owner environment, or any external package provider beyond the real solution restore path.

## Quality policy

Repository-wide compiler and analyzer defaults live in `Directory.Build.props`; source formatting, naming, and selected build-enforced style rules live in `.editorconfig`.

The P01 quality baseline enforces:

- nullable reference types for every production project;
- C# 14 language version under the .NET 10 toolchain;
- built-in .NET analyzers with the `.NET 10 recommended` rule set;
- build-time code-style analysis;
- deterministic compilation;
- Release compiler and code-analysis warnings as errors;
- no project-local overrides of the central quality properties in the current production tree;
- no third-party analyzer package introduced by P01-002.

Run the deterministic policy validator with:

```powershell
pwsh -NoProfile -File .\tools\quality\validate-quality-policy.ps1 -RequireDotNet
```

The validator performs static policy checks, restores and formats the real solution, builds it in Release, then uses only disposable temporary projects to prove that nullable (`CS8618`), analyzer (`CA1822`), and formatting/style (`IDE0055`) violations fail the Release build. It restores the clean fixture and verifies a recovery build before deleting the temporary directory.

## Unit and integration tests

P01 test infrastructure is documented in `docs/TESTING.md` and split into explicit lanes:

```powershell
pwsh -NoProfile -File .\tools\testing\run-tests.ps1 -Suite unit
pwsh -NoProfile -File .\tools\testing\run-tests.ps1 -Suite integration
pwsh -NoProfile -File .\tools\testing\run-tests.ps1 -Suite all
```

Run the complete infrastructure validator with:

```powershell
pwsh -NoProfile -File .\tools\testing\validate-test-infrastructure.ps1 -RequireDotNet
```

The unit lane verifies shared disposable-workspace behavior. The integration lane exercises real Windows filesystem/process behavior in OS-temporary workspaces, including non-zero process results, cancellation/child-tree termination, and recovery after cancellation. It never writes fixtures into owner-controlled directories.

## Foundation boundaries

The production project split follows `docs/ARCHITECTURE.md`:

- `FCCCodeDesktop.Core` — pure domain foundation.
- `FCCCodeDesktop.Runtime` — project-owned agent runtime boundary.
- `FCCCodeDesktop.Tools` — project-owned external-tool gateway boundary.
- `FCCCodeDesktop.Application` — use cases/orchestration over Core/Runtime/Tools.
- `FCCCodeDesktop.Infrastructure` — Windows/process/OS implementation primitives.
- `FCCCodeDesktop.Persistence` — persistence implementation boundary; no SQLite implementation is added in P01-001.
- `FCCCodeDesktop.Fcc` — FCC/Claude adapter boundary; no provider implementation is added in P01-001.
- `FCCCodeDesktop.Files` — file-service boundary.
- `FCCCodeDesktop.Git` — Git adapter boundary.
- `FCCCodeDesktop.Terminal` — terminal/process-integration boundary.
- `FCCCodeDesktop.Tools.Unity` — Unity adapter boundary; no Unity implementation is added in P01-001.
- `FCCCodeDesktop.Tools.Blender` — Blender adapter boundary; no Blender implementation is added in P01-001.
- `FCCCodeDesktop.Security` — security-policy implementation boundary.
- `FCCCodeDesktop.Diagnostics` — diagnostics/logging implementation boundary.
- `FCCCodeDesktop.Updater` — updater/lifecycle boundary.
- `FCCCodeDesktop.App` — WPF composition root. P02 owns the premium application shell and visual implementation.

Test-only support code lives under `tests/` and must not become a dependency of production projects. Project references point inward toward contracts/domain or from the WPF composition root toward concrete modules. The production graph remains acyclic.
