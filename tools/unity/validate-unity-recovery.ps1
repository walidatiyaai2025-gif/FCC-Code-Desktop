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
    throw 'P10-012 Unity cancellation/recovery validation must run on Windows.'
}

$fixtureProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnityRecoveryFixture\FCCCodeDesktop.UnityRecoveryFixture.csproj'
$contractPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Tools.Unity\UnityOperationRecovery.cs'
foreach ($requiredPath in @($fixtureProject, $contractPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required P10-012 validation input not found: $requiredPath"
    }
}

$contractText = Get-Content -LiteralPath $contractPath -Raw
foreach ($forbidden in @('Process.GetProcessesByName', 'taskkill', 'Kill(entireProcessTree', 'UseShellExecute = true')) {
    if ($contractText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
        throw "P10-012 recovery policy contains forbidden process-control primitive '$forbidden'."
    }
}
if (-not $contractText.Contains('PreserveForeignProcess', [StringComparison]::Ordinal)) {
    throw 'P10-012 recovery policy must explicitly preserve foreign/mismatched processes.'
}
if (-not $contractText.Contains('RequireFreshCorrelatedEvidence', [StringComparison]::Ordinal)) {
    throw 'P10-012 recovery policy must reject stale evidence and require fresh correlation before retry.'
}

Push-Location $RepositoryRoot
try {
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'SDK version check'
    if ($sdkVersion -ne '10.0.400') {
        throw "Expected .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    Write-Host 'P10-012 stage: locked fixture restore'
    & dotnet restore $fixtureProject --locked-mode --nologo
    Assert-LastExitCode 'P10-012 locked fixture restore'

    Write-Host 'P10-012 stage: Release fixture build'
    & dotnet build $fixtureProject -c Release --no-restore --nologo -warnaserror
    Assert-LastExitCode 'P10-012 Release fixture build'

    Write-Host 'P10-012 stage: deterministic cancellation/recovery acceptance'
    & dotnet run --project $fixtureProject -c Release --no-build --no-restore
    Assert-LastExitCode 'P10-012 deterministic cancellation/recovery acceptance'

    Write-Host 'P10-012 Unity cancellation/recovery validation: PASS.'
}
finally {
    Pop-Location
}
