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
    throw 'P10-008 Unity PlayMode test integration validation must run on Windows.'
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw 'dotnet is required on PATH.'
}

$fixtureProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnityPlayModeTestFixture\FCCCodeDesktop.UnityPlayModeTestFixture.csproj'
if (-not (Test-Path -LiteralPath $fixtureProject)) {
    throw "Unity PlayMode test fixture project not found: $fixtureProject"
}

Push-Location $RepositoryRoot
try {
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'SDK version check'
    if ($sdkVersion -ne '10.0.400') {
        throw "Expected .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    Write-Host 'P10-008 stage: locked fixture restore'
    & dotnet restore $fixtureProject --locked-mode --nologo
    Assert-LastExitCode 'P10-008 locked fixture restore'

    Write-Host 'P10-008 stage: Release fixture build'
    & dotnet build $fixtureProject -c Release --no-restore --nologo -warnaserror
    Assert-LastExitCode 'P10-008 Release fixture build'

    Write-Host 'P10-008 stage: deterministic PlayMode result acceptance'
    & dotnet run --project $fixtureProject -c Release --no-build --no-restore
    Assert-LastExitCode 'P10-008 deterministic PlayMode result acceptance'

    Write-Host 'P10-008 Unity PlayMode test integration validation: PASS.'
}
finally {
    Pop-Location
}
