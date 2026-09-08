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
    throw 'P10-005 Unity log capture/parser validation must run on Windows.'
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw 'dotnet is required on PATH.'
}

$fixtureProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnityLogCaptureFixture\FCCCodeDesktop.UnityLogCaptureFixture.csproj'
if (-not (Test-Path -LiteralPath $fixtureProject)) {
    throw "Unity log capture fixture project not found: $fixtureProject"
}

Push-Location $RepositoryRoot
try {
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'SDK version check'
    if ($sdkVersion -ne '10.0.400') {
        throw "Expected .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    Write-Host 'P10-005 stage: locked fixture restore'
    & dotnet restore $fixtureProject --locked-mode --nologo
    Assert-LastExitCode 'P10-005 locked fixture restore'

    Write-Host 'P10-005 stage: Release fixture build'
    & dotnet build $fixtureProject -c Release --no-restore --nologo -warnaserror
    Assert-LastExitCode 'P10-005 Release fixture build'

    Write-Host 'P10-005 stage: deterministic incremental capture/parser acceptance'
    & dotnet run --project $fixtureProject -c Release --no-build --no-restore
    Assert-LastExitCode 'P10-005 deterministic incremental capture/parser acceptance'

    Write-Host 'P10-005 Unity log capture/parser validation: PASS.'
}
finally {
    Pop-Location
}
