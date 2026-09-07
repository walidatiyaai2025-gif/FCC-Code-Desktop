param(
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$unitProject = Join-Path $repoRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'
$terminalProject = Join-Path $repoRoot 'src\FCCCodeDesktop.Terminal\FCCCodeDesktop.Terminal.csproj'

Push-Location $repoRoot
try {
    & dotnet restore $unitProject --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "Locked restore failed with exit code $LASTEXITCODE."
    }

    & dotnet build $terminalProject --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Terminal Release build failed with exit code $LASTEXITCODE."
    }

    & dotnet test $unitProject `
        --configuration $Configuration `
        --no-restore `
        --filter 'FullyQualifiedName~OptionalShellDetectionTests'
    if ($LASTEXITCODE -ne 0) {
        throw "P08-006 focused unit tests failed with exit code $LASTEXITCODE."
    }

    Write-Host 'P08-006 optional shell detection validation: PASS.'
}
finally {
    Pop-Location
}
