[CmdletBinding()]
param([switch]$RunFixtures, [switch]$RequireRuntime)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $root 'tests\FCCCodeDesktop.FccStartupFixture\FCCCodeDesktop.FccStartupFixture.csproj'
foreach ($path in @($project, (Join-Path $root 'src\FCCCodeDesktop.Fcc\FccServerLifecycle.cs'), (Join-Path $root 'src\FCCCodeDesktop.Fcc\FccManagedAgentRuntime.cs'))) {
    if (-not (Test-Path -LiteralPath $path)) { throw "FCC startup implementation is missing: $path" }
}
if ($RunFixtures -or $RequireRuntime) {
    if (-not $IsWindows) { throw 'FCC startup fixtures require Windows process supervision.' }
    & dotnet restore $project --locked-mode --nologo
    if ($LASTEXITCODE -ne 0) { throw 'FCC startup fixture locked restore failed.' }
    & dotnet run --project $project -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'FCC startup fixtures failed.' }
}
Write-Host 'FCC server startup validation: PASS (fixture-only; no provider acceptance claimed).'
