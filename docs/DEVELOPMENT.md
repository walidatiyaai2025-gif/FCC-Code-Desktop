# FCC Code Desktop — Developer Bootstrap

## Prerequisites

- Windows 10/11 x64 for the canonical product build environment.
- .NET 10 SDK.
- No provider, Unity, or Blender installation is required to restore or build the P01 solution foundation.

The exact SDK pin/roll-forward policy belongs to `FCCD-P01-003` and is intentionally not introduced by `FCCD-P01-001` or `FCCD-P01-002`.

## Restore and build

From the repository root:

```powershell
dotnet --info
dotnet restore .\FCCCodeDesktop.sln
dotnet format .\FCCCodeDesktop.sln --verify-no-changes --no-restore
dotnet build .\FCCCodeDesktop.sln -c Release --no-restore
```

`FCCD-P01-001` adds no third-party package dependencies. `FCCD-P01-002` uses the analyzers shipped with the .NET 10 SDK and adds no analyzer package dependency. Later P01 tasks own dependency locking, general unit/integration test infrastructure, permanent CI, and build/version metadata.

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

Project references point inward toward contracts/domain or from the WPF composition root toward concrete modules. The graph is acyclic.
