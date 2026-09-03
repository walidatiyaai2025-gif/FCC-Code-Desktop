[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-LastExitCode {
    param([string]$Stage)

    if ($LASTEXITCODE -ne 0) {
        throw "$Stage failed with exit code $LASTEXITCODE."
    }
}

if (-not $IsWindows) {
    throw 'The canonical P01 Windows CI baseline must run on Windows.'
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw 'dotnet is required on PATH.'
}

$pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
if (-not $pwsh) {
    throw 'PowerShell 7 (pwsh) is required on PATH.'
}

$solutionPath = Join-Path $RepositoryRoot 'FCCCodeDesktop.sln'
if (-not (Test-Path -LiteralPath $solutionPath)) {
    throw "Solution not found: $solutionPath"
}

Push-Location $RepositoryRoot
try {
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'SDK version check'
    if ($sdkVersion -ne '10.0.400') {
        throw "Expected .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    Write-Host 'CI stage: locked restore'
    & dotnet restore $solutionPath --locked-mode --nologo
    Assert-LastExitCode 'Locked restore'

    Write-Host 'CI stage: format verification'
    & dotnet format $solutionPath --verify-no-changes --no-restore
    Assert-LastExitCode 'Format verification'

    Write-Host 'CI stage: Release build'
    & dotnet build $solutionPath -c Release --no-restore --nologo
    Assert-LastExitCode 'Release build'

    Write-Host 'CI stage: unit and integration tests'
    & pwsh -NoProfile -File .\tools\testing\run-tests.ps1 -Suite all -Configuration Release -NoRestore -NoBuild
    Assert-LastExitCode 'Unit/integration tests'

    Write-Host 'CI stage: build metadata policy'
    & pwsh -NoProfile -File .\tools\build\validate-build-metadata.ps1 -RequireDotNet
    Assert-LastExitCode 'Build metadata validation'

    Write-Host 'CI stage: dependency policy'
    & pwsh -NoProfile -File .\tools\dependencies\validate-dependency-policy.ps1 -RequireDotNet
    Assert-LastExitCode 'Dependency policy validation'

    Write-Host 'CI stage: quality policy'
    & pwsh -NoProfile -File .\tools\quality\validate-quality-policy.ps1 -RequireDotNet
    Assert-LastExitCode 'Quality policy validation'

    Write-Host 'CI stage: test infrastructure policy'
    & pwsh -NoProfile -File .\tools\testing\validate-test-infrastructure.ps1 -RequireDotNet
    Assert-LastExitCode 'Test infrastructure validation'

    Write-Host 'CI stage: design-system contract'
    & pwsh -NoProfile -File .\tools\ui\validate-design-system.ps1 -RunFixtures
    Assert-LastExitCode 'Design-system validation'

    Write-Host 'CI stage: semantic dark/light themes'
    & pwsh -NoProfile -File .\tools\ui\validate-semantic-themes.ps1 -RunFixtures -RequireRuntime
    Assert-LastExitCode 'Semantic theme validation'

    Write-Host 'CI stage: premium application chrome'
    & pwsh -NoProfile -File .\tools\ui\validate-app-chrome.ps1 -RunFixtures -RequireRuntime
    Assert-LastExitCode 'Application chrome validation'

    Write-Host 'Windows CI baseline: PASS.'
}
finally {
    Pop-Location
}
