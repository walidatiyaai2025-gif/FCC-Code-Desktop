[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RunFixtures,
    [switch]$RequireRuntime
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ContainsLiteral {
    param([string]$Text, [string]$Literal, [string]$Label)

    if (-not $Text.Contains($Literal, [StringComparison]::Ordinal)) {
        throw "$Label is missing required text: $Literal"
    }
}

function Assert-DetectorContract {
    param(
        [string]$DetectorText,
        [string]$FixtureText,
        [string]$FixtureProjectText,
        [string]$FixtureLockText
    )

    foreach ($literal in @(
        'public interface IUnityProjectDetector',
        'public sealed class UnityProjectDetector : IUnityProjectDetector',
        'ValidUnityProject = 1',
        'ProjectRootNotFound = 2',
        'NotUnityProject = 3',
        'UnityProjectIncomplete = 4',
        'ProjectVersionMissing = 5',
        'ProjectVersionReadFailure = 6',
        'ProjectSettingsDirectoryName = "ProjectSettings"',
        'ProjectVersionFileName = "ProjectVersion.txt"',
        'm_EditorVersion:',
        'm_EditorVersionWithRevision:',
        'File.ReadAllTextAsync(',
        'cancellationToken.ThrowIfCancellationRequested()',
        'markers.HasCompleteProjectLayout',
        'ConfigureAwait(false)'
    )) {
        Assert-ContainsLiteral $DetectorText $literal 'UnityProjectDetection.cs'
    }

    foreach ($forbidden in @(
        'Process.Start',
        'ProcessStartInfo',
        'Directory.CreateDirectory',
        'Directory.Delete',
        'File.Write',
        'File.Delete',
        'File.Move',
        'File.Replace'
    )) {
        if ($DetectorText.Contains($forbidden, [StringComparison]::Ordinal)) {
            throw "P10-001 detector contains forbidden mutation/process text: $forbidden"
        }
    }

    foreach ($literal in @(
        'ValidateCompleteProjectAsync',
        'ValidateRevisionFallbackAsync',
        'ValidateIncompleteProjectAsync',
        'ValidateMissingVersionAsync',
        'ValidateNonUnityDirectoryAsync',
        'ValidateMissingRootAsync',
        'ValidateCancellationAsync',
        'ValidateInvalidInput',
        'مشروع كامل with spaces',
        'owner bytes preserved',
        'P10-001 Unity project/version detector fixture: PASS'
    )) {
        Assert-ContainsLiteral $FixtureText $literal 'P10-001 fixture Program.cs'
    }

    Assert-ContainsLiteral $FixtureProjectText '<TargetFramework>net10.0-windows</TargetFramework>' 'P10-001 fixture project'
    Assert-ContainsLiteral $FixtureProjectText 'FCCCodeDesktop.Tools.Unity\FCCCodeDesktop.Tools.Unity.csproj' 'P10-001 fixture project'
    Assert-ContainsLiteral $FixtureLockText '"fcccodedesktop.tools.unity"' 'P10-001 fixture lock'

    foreach ($text in @($DetectorText, $FixtureText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P10-001 contains forbidden placeholder text '$placeholder'."
            }
        }
    }
}

function Assert-ContractRejects {
    param([scriptblock]$Action, [string]$Label)

    try {
        & $Action
    }
    catch {
        Write-Host "Negative fixture rejected as expected: $Label"
        return
    }

    throw "Negative P10-001 fixture was not rejected: $Label"
}

$paths = @{
    Detector = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Tools.Unity\UnityProjectDetection.cs'
    Fixture = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnityProjectDetectionFixture\Program.cs'
    FixtureProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnityProjectDetectionFixture\FCCCodeDesktop.UnityProjectDetectionFixture.csproj'
    FixtureLock = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnityProjectDetectionFixture\packages.lock.json'
}

foreach ($path in $paths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P10-001 path is missing: $path"
    }
}

$detectorText = Get-Content -LiteralPath $paths.Detector -Raw
$fixtureText = Get-Content -LiteralPath $paths.Fixture -Raw
$fixtureProjectText = Get-Content -LiteralPath $paths.FixtureProject -Raw
$fixtureLockText = Get-Content -LiteralPath $paths.FixtureLock -Raw

Assert-DetectorContract $detectorText $fixtureText $fixtureProjectText $fixtureLockText
Write-Host 'Static P10-001 Unity project/version detector validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-DetectorContract ($detectorText.Replace('File.ReadAllTextAsync(', 'File.ReadAllTextRemovedAsync(')) $fixtureText $fixtureProjectText $fixtureLockText
    } 'ProjectVersion read removed'
    Assert-ContractRejects {
        Assert-DetectorContract ($detectorText.Replace('cancellationToken.ThrowIfCancellationRequested()', 'CancellationRemoved()')) $fixtureText $fixtureProjectText $fixtureLockText
    } 'cancellation guard removed'
    Assert-ContractRejects {
        Assert-DetectorContract ($detectorText.Replace('markers.HasCompleteProjectLayout', 'true')) $fixtureText $fixtureProjectText $fixtureLockText
    } 'complete-layout guard removed'
    Assert-ContractRejects {
        Assert-DetectorContract ($detectorText + "`n// Process.Start") $fixtureText $fixtureProjectText $fixtureLockText
    } 'process launch introduced'
    Assert-ContractRejects {
        Assert-DetectorContract $detectorText ($fixtureText.Replace('ValidateCancellationAsync', 'ValidateCancellationRemovedAsync')) $fixtureProjectText $fixtureLockText
    } 'cancellation fixture removed'
    Write-Host 'P10-001 static negative fixtures: PASS.'
}

if ($RequireRuntime) {
    if (-not $IsWindows) {
        throw 'Executable P10-001 Unity project detection validation requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for executable P10-001 Unity project detection validation.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "P10-001 validation requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    & dotnet restore $paths.FixtureProject --locked-mode --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'P10-001 fixture locked restore failed.'
    }

    & dotnet run --project $paths.FixtureProject -c Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw 'P10-001 Unity project/version detector executable fixture failed.'
    }

    Write-Host 'Executable P10-001 Unity project/version detector validation: PASS.'
}
