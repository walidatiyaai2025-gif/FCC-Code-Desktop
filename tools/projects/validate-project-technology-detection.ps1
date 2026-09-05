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

function Assert-ValidXaml {
    param([string]$Text, [string]$Label)

    try {
        [void][xml]$Text
    }
    catch {
        throw "$Label is not valid XML/XAML: $($_.Exception.Message)"
    }
}

function Assert-TechnologyDetectionContract {
    param(
        [string]$ContractText,
        [string]$DetectorText,
        [string]$StateText,
        [string]$SurfaceXamlText,
        [string]$SurfaceCodeText,
        [string]$MainWindowText,
        [string]$TestText,
        [string]$TestProjectText,
        [string]$LockText,
        [string]$DocText
    )

    Assert-ValidXaml $SurfaceXamlText 'ProjectWorkspaceSurface.xaml'

    foreach ($literal in @(
        'public interface IProjectTechnologyDetectionService',
        'ProjectTechnologyScanResult',
        'IReadOnlyList<ProjectTechnologyDetection> Technologies',
        'bool LimitReached',
        'CancellationToken cancellationToken = default'
    )) {
        Assert-ContainsLiteral $ContractText $literal 'ProjectTechnologyDetection.cs'
    }

    foreach ($literal in @(
        'public sealed class FileSystemProjectTechnologyDetectionService',
        'DefaultMaximumDepth = 3',
        'DefaultMaximumEntries = 4096',
        'MaximumSupportedEntries = 100_000',
        'Task.Run(() => DetectCore(rootPath, cancellationToken), cancellationToken)',
        'cancellationToken.ThrowIfCancellationRequested()',
        '.EnumerateFileSystemEntries(directoryPath)',
        '.Take(remainingCapacity + 1)',
        'FileAttributes.ReparsePoint',
        'IgnoredDirectoryNames.Contains(directoryName)',
        '"node_modules"',
        '"Library"',
        'ProjectSettings/ProjectVersion.txt',
        'package.json',
        'pyproject.toml',
        'Cargo.toml',
        'CMakeLists.txt',
        'entriesExamined >= _maximumEntries'
    )) {
        Assert-ContainsLiteral $DetectorText $literal 'FileSystemProjectTechnologyDetectionService.cs'
    }

    foreach ($forbidden in @(
        'Process.Start',
        'ProcessStartInfo',
        'File.WriteAll',
        'File.Delete',
        'Directory.Delete'
    )) {
        if ($DetectorText.Contains($forbidden, [StringComparison]::Ordinal)) {
            throw "Technology detector contains forbidden side-effect/process text: $forbidden"
        }
    }

    foreach ($literal in @(
        'IProjectTechnologyDetectionService _technologyDetection',
        'ReadOnlyObservableCollection<ProjectTechnologyDetection> DetectedTechnologies',
        'CanRescanTechnologies',
        'RefreshTechnologyDetectionAsync',
        '_technologyDetection.DetectAsync(rootPath, cancellationToken)',
        'ResetTechnologyDetection()'
    )) {
        Assert-ContainsLiteral $StateText $literal 'ProjectWorkspaceState.cs'
    }

    foreach ($literal in @(
        'ItemsSource="{Binding DetectedTechnologies}"',
        'Content="Rescan markers"',
        'IsEnabled="{Binding CanRescanTechnologies}"',
        'AutomationProperties.Name="Detected project technologies"',
        'Technology detection is read-only and marker-based; it never launches project toolchains or modifies source files.'
    )) {
        Assert-ContainsLiteral $SurfaceXamlText $literal 'ProjectWorkspaceSurface.xaml'
    }

    Assert-ContainsLiteral $SurfaceCodeText 'OnRescanTechnologiesClick' 'ProjectWorkspaceSurface.xaml.cs'
    Assert-ContainsLiteral $SurfaceCodeText 'state.RefreshTechnologyDetectionAsync(CancellationToken.None)' 'ProjectWorkspaceSurface.xaml.cs'
    Assert-ContainsLiteral $MainWindowText 'new FileSystemProjectTechnologyDetectionService()' 'MainWindow.xaml.cs'

    foreach ($literal in @(
        'DetectsMixedTechnologyMarkersDeterministicallyWithoutModifyingSource',
        'GeneratedDirectoriesAndReparseSensitiveBoundariesAreIgnored',
        'EntryCapStopsTraversalAndReportsLimitWithoutLaunchingAnything',
        'MissingRootAndCancellationFailExplicitly',
        'ConstructorRejectsUnboundedScanConfiguration',
        'do-not-change',
        'مشروع mixed with spaces'
    )) {
        Assert-ContainsLiteral $TestText $literal 'ProjectTechnologyDetectionServiceTests.cs'
    }

    Assert-ContainsLiteral $TestProjectText 'FCCCodeDesktop.Files\FCCCodeDesktop.Files.csproj' 'FCCCodeDesktop.IntegrationTests.csproj'
    Assert-ContainsLiteral $LockText '"fcccodedesktop.files"' 'integration packages.lock.json'
    Assert-ContainsLiteral $DocText 'never starts a process' 'PROJECT_TECHNOLOGY_DETECTION.md'
    Assert-ContainsLiteral $DocText 'maximum file-system entries examined: `4096`' 'PROJECT_TECHNOLOGY_DETECTION.md'

    foreach ($text in @($ContractText, $DetectorText, $StateText, $SurfaceXamlText, $SurfaceCodeText, $MainWindowText, $TestText, $DocText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P06-002 contains forbidden placeholder text '$placeholder'."
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

    throw "Negative P06-002 technology-detection fixture was not rejected: $Label"
}

$paths = @{
    Contract = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\ProjectTechnologyDetection.cs'
    Detector = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\FileSystemProjectTechnologyDetectionService.cs'
    State = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectWorkspaceState.cs'
    SurfaceXaml = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectWorkspaceSurface.xaml'
    SurfaceCode = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectWorkspaceSurface.xaml.cs'
    MainWindow = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml.cs'
    Tests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectTechnologyDetectionServiceTests.cs'
    TestProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'
    Lock = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\packages.lock.json'
    Docs = Join-Path $RepositoryRoot 'docs\projects\PROJECT_TECHNOLOGY_DETECTION.md'
}

foreach ($path in $paths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P06-002 path is missing: $path"
    }
}

$contractText = Get-Content -LiteralPath $paths.Contract -Raw
$detectorText = Get-Content -LiteralPath $paths.Detector -Raw
$stateText = Get-Content -LiteralPath $paths.State -Raw
$surfaceXamlText = Get-Content -LiteralPath $paths.SurfaceXaml -Raw
$surfaceCodeText = Get-Content -LiteralPath $paths.SurfaceCode -Raw
$mainWindowText = Get-Content -LiteralPath $paths.MainWindow -Raw
$testText = Get-Content -LiteralPath $paths.Tests -Raw
$testProjectText = Get-Content -LiteralPath $paths.TestProject -Raw
$lockText = Get-Content -LiteralPath $paths.Lock -Raw
$docText = Get-Content -LiteralPath $paths.Docs -Raw

Assert-TechnologyDetectionContract $contractText $detectorText $stateText $surfaceXamlText $surfaceCodeText $mainWindowText $testText $testProjectText $lockText $docText
Write-Host 'Static P06-002 project technology detection validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-TechnologyDetectionContract $contractText ($detectorText.Replace('DefaultMaximumEntries = 4096', 'DefaultMaximumEntriesRemoved = 4096')) $stateText $surfaceXamlText $surfaceCodeText $mainWindowText $testText $testProjectText $lockText $docText
    } 'entry bound removed'
    Assert-ContractRejects {
        Assert-TechnologyDetectionContract $contractText ($detectorText.Replace('.EnumerateFileSystemEntries(directoryPath)', '.EnumerateFileSystemEntriesRemoved(directoryPath)')) $stateText $surfaceXamlText $surfaceCodeText $mainWindowText $testText $testProjectText $lockText $docText
    } 'file-system enumeration removed'
    Assert-ContractRejects {
        Assert-TechnologyDetectionContract $contractText ($detectorText.Replace('.Take(remainingCapacity + 1)', '.Skip(0)')) $stateText $surfaceXamlText $surfaceCodeText $mainWindowText $testText $testProjectText $lockText $docText
    } 'bounded directory materialization removed'
    Assert-ContractRejects {
        Assert-TechnologyDetectionContract $contractText ($detectorText.Replace('FileAttributes.ReparsePoint', 'FileAttributes.Normal')) $stateText $surfaceXamlText $surfaceCodeText $mainWindowText $testText $testProjectText $lockText $docText
    } 'reparse-point guard removed'
    Assert-ContractRejects {
        Assert-TechnologyDetectionContract $contractText ($detectorText.Replace('"node_modules"', '"modules-removed"')) $stateText $surfaceXamlText $surfaceCodeText $mainWindowText $testText $testProjectText $lockText $docText
    } 'generated-directory exclusion removed'
    Assert-ContractRejects {
        Assert-TechnologyDetectionContract $contractText $detectorText ($stateText.Replace('_technologyDetection.DetectAsync(rootPath, cancellationToken)', 'RemovedTechnologyDetectionAsync(rootPath, cancellationToken)')) $surfaceXamlText $surfaceCodeText $mainWindowText $testText $testProjectText $lockText $docText
    } 'presentation-state detection wiring removed'
    Assert-ContractRejects {
        Assert-TechnologyDetectionContract $contractText $detectorText ($stateText.Replace('ResetTechnologyDetection()', 'ResetTechnologyDetectionRemoved()')) $surfaceXamlText $surfaceCodeText $mainWindowText $testText $testProjectText $lockText $docText
    } 'stale technology reset removed'
    Assert-ContractRejects {
        Assert-TechnologyDetectionContract $contractText $detectorText $stateText ($surfaceXamlText.Replace('Content="Rescan markers"', 'Content="Rescan removed"')) $surfaceCodeText $mainWindowText $testText $testProjectText $lockText $docText
    } 'rescan UX removed'
    Assert-ContractRejects {
        Assert-TechnologyDetectionContract $contractText $detectorText $stateText $surfaceXamlText $surfaceCodeText $mainWindowText $testText ($testProjectText.Replace('FCCCodeDesktop.Files\FCCCodeDesktop.Files.csproj', 'FCCCodeDesktop.Files\Removed.csproj')) $lockText $docText
    } 'real Files adapter test reference removed'
    Write-Host 'P06-002 negative fixtures: PASS.'
}

if ($RequireRuntime) {
    if (-not $IsWindows) {
        throw 'Executable P06-002 technology detection validation requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for executable P06-002 technology detection validation.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "P06-002 validation requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $testProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'
    & dotnet test $testProject -c Release --no-restore --no-build --nologo --filter 'FullyQualifiedName~ProjectTechnologyDetectionServiceTests'
    if ($LASTEXITCODE -ne 0) {
        throw 'P06-002 project technology detection integration tests failed.'
    }

    Write-Host 'Executable P06-002 project technology detection validation: PASS.'
}
