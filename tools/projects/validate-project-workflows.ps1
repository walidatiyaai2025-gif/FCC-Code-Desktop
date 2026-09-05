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

function Assert-ProjectWorkflowContract {
    param(
        [string]$CatalogText,
        [string]$StoreText,
        [string]$ProbeText,
        [string]$StateText,
        [string]$SurfaceXamlText,
        [string]$SurfaceCodeText,
        [string]$MainWindowText,
        [string]$TestText
    )

    Assert-ValidXaml $SurfaceXamlText 'ProjectWorkspaceSurface.xaml'

    foreach ($literal in @(
        'public sealed class ProjectCatalogService',
        'NormalizeRootPath(rootPath)',
        'DirectoryExists(normalizedRootPath)',
        'FindProjectByRootPathAsync(normalizedRootPath',
        'existing with',
        'ListRecentProjectsAsync'
    )) {
        Assert-ContainsLiteral $CatalogText $literal 'ProjectCatalogService.cs'
    }

    foreach ($literal in @(
        'public sealed class SqliteProjectCatalogStore : IProjectCatalogStore',
        'WHERE RootPath = $rootPath COLLATE NOCASE',
        'ORDER BY UpdatedUtc DESC, DisplayName COLLATE NOCASE ASC, Id ASC',
        'LIMIT $maximumCount',
        'ON CONFLICT(Id) DO UPDATE SET'
    )) {
        Assert-ContainsLiteral $StoreText $literal 'SqliteProjectCatalogStore.cs'
    }

    foreach ($literal in @(
        'Path.GetFullPath(rootPath)',
        'Directory.Exists(normalizedRootPath)',
        'new DirectoryInfo(normalizedRootPath)'
    )) {
        Assert-ContainsLiteral $ProbeText $literal 'SystemProjectDirectoryProbe.cs'
    }

    foreach ($literal in @(
        'ReadOnlyObservableCollection<PersistedProject> RecentProjects',
        'await _catalog.OpenProjectAsync(rootPath',
        'await _sessions.ActivateProjectAsync(openedProject.Id',
        'SetErrorMessage(exception.Message)',
        'A project workspace operation is already running.'
    )) {
        Assert-ContainsLiteral $StateText $literal 'ProjectWorkspaceState.cs'
    }

    foreach ($literal in @(
        'AutomationProperties.Name="Projects workspace"',
        'Content="Open project…"',
        'ItemsSource="{Binding RecentProjects}"',
        'VirtualizingPanel.IsVirtualizing="True"',
        'VirtualizingPanel.VirtualizationMode="Recycling"',
        'Text="Opening a project never copies, moves, or modifies the selected source folder."'
    )) {
        Assert-ContainsLiteral $SurfaceXamlText $literal 'ProjectWorkspaceSurface.xaml'
    }

    foreach ($literal in @(
        'new OpenFolderDialog',
        'Multiselect = false',
        'state.OpenProjectAsync(dialog.FolderName',
        'state.OpenRecentProjectAsync(project',
        'ProjectWorkspaceState already records the actionable message for inline presentation.'
    )) {
        Assert-ContainsLiteral $SurfaceCodeText $literal 'ProjectWorkspaceSurface.xaml.cs'
    }

    foreach ($literal in @(
        'new ProjectWorkspaceSurface()',
        'navigationState.ProjectsContent = _projectWorkspaceSurface;',
        'new SqliteProjectCatalogStore(options)',
        'new SystemProjectDirectoryProbe()',
        'await projectState.InitializeAsync(CancellationToken.None)'
    )) {
        Assert-ContainsLiteral $MainWindowText $literal 'MainWindow.xaml.cs'
    }

    foreach ($literal in @(
        'OpenProjectPersistsAndReopenReusesIdentityAndRefreshesRecency',
        'OpenProjectSupportsGitAndNonGitFoldersWithoutTouchingSourceContent',
        'MissingFolderIsRejectedAndNotAddedToRecentProjects',
        'RecentProjectLimitIsValidatedAndAppliedDeterministically',
        'do-not-change',
        'مشروع أول with spaces'
    )) {
        Assert-ContainsLiteral $TestText $literal 'ProjectCatalogServiceTests.cs'
    }

    foreach ($text in @($CatalogText, $StoreText, $ProbeText, $StateText, $SurfaceXamlText, $SurfaceCodeText, $MainWindowText, $TestText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P06-001 contains forbidden placeholder text '$placeholder'."
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

    throw "Negative P06-001 project-workflow fixture was not rejected: $Label"
}

$paths = @{
    Catalog = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\ProjectCatalogService.cs'
    Store = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Persistence\SqliteProjectCatalogStore.cs'
    Probe = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\SystemProjectDirectoryProbe.cs'
    State = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectWorkspaceState.cs'
    SurfaceXaml = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectWorkspaceSurface.xaml'
    SurfaceCode = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectWorkspaceSurface.xaml.cs'
    MainWindow = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml.cs'
    Tests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectCatalogServiceTests.cs'
}

foreach ($path in $paths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P06-001 path is missing: $path"
    }
}

$catalogText = Get-Content -LiteralPath $paths.Catalog -Raw
$storeText = Get-Content -LiteralPath $paths.Store -Raw
$probeText = Get-Content -LiteralPath $paths.Probe -Raw
$stateText = Get-Content -LiteralPath $paths.State -Raw
$surfaceXamlText = Get-Content -LiteralPath $paths.SurfaceXaml -Raw
$surfaceCodeText = Get-Content -LiteralPath $paths.SurfaceCode -Raw
$mainWindowText = Get-Content -LiteralPath $paths.MainWindow -Raw
$testText = Get-Content -LiteralPath $paths.Tests -Raw

Assert-ProjectWorkflowContract $catalogText $storeText $probeText $stateText $surfaceXamlText $surfaceCodeText $mainWindowText $testText
Write-Host 'Static P06-001 project workflow validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-ProjectWorkflowContract ($catalogText.Replace('DirectoryExists(normalizedRootPath)', 'DirectoryCheckRemoved(normalizedRootPath)')) $storeText $probeText $stateText $surfaceXamlText $surfaceCodeText $mainWindowText $testText
    } 'missing directory existence guard'
    Assert-ContractRejects {
        Assert-ProjectWorkflowContract $catalogText ($storeText.Replace('WHERE RootPath = $rootPath COLLATE NOCASE', 'WHERE RootPath = $rootPath')) $probeText $stateText $surfaceXamlText $surfaceCodeText $mainWindowText $testText
    } 'case-insensitive project identity removed'
    Assert-ContractRejects {
        Assert-ProjectWorkflowContract $catalogText $storeText $probeText ($stateText.Replace('await _sessions.ActivateProjectAsync(openedProject.Id', 'await RemovedSessionActivationAsync(openedProject.Id')) $surfaceXamlText $surfaceCodeText $mainWindowText $testText
    } 'session workspace activation removed'
    Assert-ContractRejects {
        Assert-ProjectWorkflowContract $catalogText $storeText $probeText $stateText ($surfaceXamlText.Replace('VirtualizingPanel.IsVirtualizing="True"', 'VirtualizingPanel.IsVirtualizing="False"')) $surfaceCodeText $mainWindowText $testText
    } 'recent project virtualization removed'
    Assert-ContractRejects {
        Assert-ProjectWorkflowContract $catalogText $storeText $probeText $stateText $surfaceXamlText ($surfaceCodeText.Replace('new OpenFolderDialog', 'new RemovedFolderDialog')) $mainWindowText $testText
    } 'folder picker removed'
    Write-Host 'P06-001 negative fixtures: PASS.'
}

if ($RequireRuntime) {
    if (-not $IsWindows) {
        throw 'Executable P06-001 project workflow validation requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for executable P06-001 project workflow validation.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "P06-001 validation requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $testProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'
    & dotnet test $testProject -c Release --no-restore --no-build --nologo --filter 'FullyQualifiedName~ProjectCatalogServiceTests'
    if ($LASTEXITCODE -ne 0) {
        throw 'P06-001 project workflow integration tests failed.'
    }

    Write-Host 'Executable P06-001 project workflow validation: PASS.'
}
