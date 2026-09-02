[CmdletBinding()]
param(
    [ValidateSet('all', 'unit', 'integration')]
    [string]$Suite = 'all',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRestore,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$unitProject = Join-Path $repositoryRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'
$integrationProject = Join-Path $repositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'

$projects = switch ($Suite) {
    'unit' { @($unitProject) }
    'integration' { @($integrationProject) }
    'all' { @($unitProject, $integrationProject) }
}

foreach ($project in $projects) {
    if (-not (Test-Path -LiteralPath $project)) {
        throw "Test project is missing: $project"
    }

    $arguments = @(
        'test',
        $project,
        '-c', $Configuration,
        '--nologo',
        '--logger', 'console;verbosity=minimal'
    )

    if ($NoRestore) {
        $arguments += '--no-restore'
    }
    if ($NoBuild) {
        $arguments += '--no-build'
    }

    Write-Host "Running $Suite test lane: $project"
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Test lane '$Suite' failed for '$project' with exit code $LASTEXITCODE."
    }
}

Write-Host "Test lane '$Suite': PASS."
