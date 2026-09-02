# FCC Code Desktop — Developer Bootstrap

## Prerequisites

- Windows 10/11 x64 for the canonical product build environment.
- .NET 10 SDK.
- No provider, Unity, or Blender installation is required to restore or build the P01 solution foundation.

The exact SDK pin/roll-forward policy belongs to `FCCD-P01-003` and is intentionally not introduced by `FCCD-P01-001`.

## Restore and build

From the repository root:

```powershell
dotnet --info
dotnet restore .\FCCCodeDesktop.sln
dotnet build .\FCCCodeDesktop.sln -c Release --no-restore
```

`FCCD-P01-001` adds no third-party package dependencies. Later P01 tasks own analyzer/style policy, dependency locking, test infrastructure, CI, and build/version metadata.

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
