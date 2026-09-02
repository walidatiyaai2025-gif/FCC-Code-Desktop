[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RequireDotNet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$solutionPath = Join-Path $RepositoryRoot 'FCCCodeDesktop.sln'
$runnerPath = Join-Path $RepositoryRoot 'tools\testing\run-tests.ps1'
$testingProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.Testing\FCCCodeDesktop.Testing.csproj'
$unitProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'
$integrationProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'

$requiredPaths = @(
    $solutionPath,
    $runnerPath,
    $testingProject,
    $unitProject,
    $integrationProject,
    (Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.Testing\TemporaryDirectory.cs'),
    (Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.Testing\TestProcess.cs'),
    (Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\TemporaryDirectoryTests.cs'),
    (Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\InfrastructureIntegrationTests.cs')
)

foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required test-infrastructure path is missing: $requiredPath"
    }
}

$expectedPackages = @(
    'coverlet.collector',
    'Microsoft.NET.Test.Sdk',
    'xunit',
    'xunit.runner.visualstudio'
)

foreach ($testProjectPath in @($unitProject, $integrationProject)) {
    [xml]$project = Get-Content -LiteralPath $testProjectPath -Raw
    $isTestProjectNodes = @($project.SelectNodes('/Project/PropertyGroup/IsTestProject'))
    if ($isTestProjectNodes.Count -ne 1 -or [string]$isTestProjectNodes[0].InnerText -ne 'true') {
        throw "Test project must set IsTestProject=true exactly once: $testProjectPath"
    }

    $packageReferences = @($project.SelectNodes('//PackageReference'))
    $packageIds = @()
    foreach ($packageReference in $packageReferences) {
        $packageId = [string]$packageReference.Include
        if (-not $packageId) {
            throw "PackageReference is missing Include in $testProjectPath"
        }
        if ($packageReference.Version -or $packageReference.VersionOverride) {
            throw "Test package '$packageId' must use central version ownership in $testProjectPath"
        }
        $packageIds += $packageId
    }

    foreach ($expectedPackage in $expectedPackages) {
        if ($packageIds -notcontains $expectedPackage) {
            throw "Test project '$testProjectPath' is missing package '$expectedPackage'."
        }
    }

    if ($packageIds.Count -ne $expectedPackages.Count) {
        throw "Unexpected PackageReference count in '$testProjectPath': $($packageIds.Count)."
    }
}

[xml]$supportProject = Get-Content -LiteralPath $testingProject -Raw
if (@($supportProject.SelectNodes('/Project/PropertyGroup/IsTestProject')).Count -ne 0) {
    throw 'Shared test support project must not be marked as a discoverable test project.'
}

Write-Host 'Static test-infrastructure validation: PASS.'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    if ($RequireDotNet) {
        throw 'dotnet was required but is not available on PATH.'
    }

    Write-Host 'dotnet is unavailable; executable test-infrastructure validation was skipped.'
    exit 0
}

$version = (& dotnet --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $version -ne '10.0.400') {
    throw "Expected .NET SDK 10.0.400 but resolved '$version'."
}

$solutionProjects = (& dotnet sln $solutionPath list 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw "Failed to list solution projects:`n$solutionProjects"
}
foreach ($projectName in @('FCCCodeDesktop.Testing.csproj', 'FCCCodeDesktop.UnitTests.csproj', 'FCCCodeDesktop.IntegrationTests.csproj')) {
    if ($solutionProjects -notmatch [regex]::Escape($projectName)) {
        throw "Solution is missing test-infrastructure project '$projectName'."
    }
}

& dotnet restore $solutionPath --locked-mode --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'Locked solution restore failed.'
}

& dotnet build $solutionPath -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'Release solution build failed.'
}

& pwsh -NoProfile -File $runnerPath -Suite unit -Configuration Release -NoRestore -NoBuild
if ($LASTEXITCODE -ne 0) {
    throw 'Unit test lane failed.'
}

& pwsh -NoProfile -File $runnerPath -Suite integration -Configuration Release -NoRestore -NoBuild
if ($LASTEXITCODE -ne 0) {
    throw 'Integration test lane failed.'
}

$invalidSuiteOutput = (& pwsh -NoProfile -File $runnerPath -Suite invalid-suite 2>&1 | Out-String)
$invalidSuiteExitCode = $LASTEXITCODE
if ($invalidSuiteExitCode -eq 0) {
    throw "Invalid test-suite selection unexpectedly succeeded:`n$invalidSuiteOutput"
}

Write-Host 'Executable test-infrastructure validation: PASS.'
Write-Host 'Unit/integration discovery, happy/negative/cancellation/recovery tests, and invalid-runner-input rejection are verified.'
