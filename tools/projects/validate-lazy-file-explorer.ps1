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

function Assert-LazyExplorerContract {
    param(
        [string]$ContractText,
        [string]$ServiceText,
        [string]$NodeText,
        [string]$StateText,
        [string]$SurfaceXamlText,
        [string]$SurfaceCodeText,
        [string]$TestText,
        [string]$DocText
    )

    Assert-ValidXaml $SurfaceXamlText 'ProjectWorkspaceSurface.xaml'

    foreach ($literal in @(
        'public interface IProjectFileExplorerService',
        'Task<ProjectDirectoryListing> ListChildrenAsync',
        'ProjectFileSystemEntry',
        'ProjectDirectoryListing',
        'bool IsReparsePoint',
        'ProjectFileTraversalRestriction TraversalRestriction',
        'bool LimitReached'
    )) {
        Assert-ContainsLiteral $ContractText $literal 'IProjectFileExplorerService.cs'
    }

    foreach ($literal in @(
        'public sealed class FileSystemProjectFileExplorerService',
        'DefaultMaximumEntriesPerDirectory = WorkspaceScalePolicy.DefaultMaximumDirectoryEntries',
        'MaximumSupportedEntriesPerDirectory = WorkspaceScalePolicy.MaximumSupportedDirectoryEntries',
        'Task.Run(',
        'ListChildrenCore(projectRootPath, directoryPath, cancellationToken)',
        'Directory.EnumerateFileSystemEntries(normalizedDirectoryPath)',
        '.Take(_maximumEntriesPerDirectory + 1)',
        'EnsurePathInsideProject(normalizedRootPath, normalizedDirectoryPath)',
        'Path.GetRelativePath(rootPath, candidatePath)',
        'FileAttributes.ReparsePoint',
        'Reparse-point directories are visible but are not traversed',
        'cancellationToken.ThrowIfCancellationRequested()',
        '.OrderByDescending(entry => entry.IsDirectory)'
    )) {
        Assert-ContainsLiteral $ServiceText $literal 'FileSystemProjectFileExplorerService.cs'
    }

    foreach ($forbidden in @(
        'SearchOption.AllDirectories',
        'EnumerateFiles(normalizedRootPath',
        'EnumerateDirectories(normalizedRootPath',
        'File.WriteAll',
        'File.Delete',
        'Directory.Delete',
        'Process.Start',
        'ProcessStartInfo'
    )) {
        if ($ServiceText.Contains($forbidden, [StringComparison]::Ordinal)) {
            throw "Lazy explorer contains forbidden recursive, destructive, or process text: $forbidden"
        }
    }

    foreach ($literal in @(
        'public sealed class ProjectFileTreeNode',
        'ReadOnlyObservableCollection<ProjectFileTreeNode> Children',
        '&& !IsTraversalRestricted;',
        'Expand to load…',
        'Loading directory…',
        'This directory is empty.',
        'Showing the first'
    )) {
        Assert-ContainsLiteral $NodeText $literal 'ProjectFileTreeNode.cs'
    }

    foreach ($literal in @(
        'public sealed class ProjectFileExplorerState',
        'IProjectFileExplorerService _fileExplorer',
        'ReadOnlyObservableCollection<ProjectFileTreeNode> Roots',
        'public void SetProject(PersistedProject? project)',
        'public async Task LoadChildrenAsync(',
        '.ListChildrenAsync(project.RootPath, node.FullPath, cancellationToken)',
        'RebuildRoot();'
    )) {
        Assert-ContainsLiteral $StateText $literal 'ProjectFileExplorerState.cs'
    }

    foreach ($literal in @(
        'Text="Files"',
        'Content="Refresh tree"',
        'ItemsSource="{Binding FileExplorerState.Roots, ElementName=Root}"',
        'AutomationProperties.Name="Lazy project file explorer"',
        'VirtualizingPanel.IsVirtualizing="True"',
        'VirtualizingPanel.VirtualizationMode="Recycling"',
        'EventSetter Event="Expanded" Handler="OnFileExplorerNodeExpanded"',
        'Binding IsStatusNode',
        'Text="No recent projects"',
        'The file explorer is read-only in this phase; expanding a folder enumerates only that directory and never follows reparse-point directories.'
    )) {
        Assert-ContainsLiteral $SurfaceXamlText $literal 'ProjectWorkspaceSurface.xaml'
    }

    foreach ($literal in @(
        'new ProjectFileExplorerState(new FileSystemProjectFileExplorerService())',
        'new PropertyMetadata(null, OnStateChanged)',
        'newState.PropertyChanged += surface.OnProjectStatePropertyChanged;',
        'FileExplorerState.SetProject(newState.ActiveProject);',
        'OnRefreshFileExplorerClick',
        'OnFileExplorerNodeExpanded',
        'ReferenceEquals(e.OriginalSource, item)',
        'FileExplorerState.LoadChildrenAsync(node, CancellationToken.None)'
    )) {
        Assert-ContainsLiteral $SurfaceCodeText $literal 'ProjectWorkspaceSurface.xaml.cs'
    }

    foreach ($literal in @(
        'ListsOnlyImmediateChildrenAndSortsDirectoriesBeforeFiles',
        'SupportsNonAsciiAndSpaceContainingPathsWithoutModifyingSource',
        'RejectsOutsideRootAndMissingDirectoryWithoutEnumeratingOwnerData',
        'DirectoryEntryCapReportsLimitAndBoundsMaterialization',
        'CancellationAndInvalidConfigurationFailExplicitly',
        'مشروع explorer with spaces',
        'do-not-change'
    )) {
        Assert-ContainsLiteral $TestText $literal 'ProjectFileExplorerServiceTests.cs'
    }

    foreach ($literal in @(
        'Opening a project creates one root node only.',
        'limits one directory listing to `2048` entries by default',
        'Reparse-point entries may be displayed',
        'no FCC/provider/manual target evidence'
    )) {
        Assert-ContainsLiteral $DocText $literal 'LAZY_FILE_EXPLORER.md'
    }

    foreach ($text in @($ContractText, $ServiceText, $NodeText, $StateText, $SurfaceXamlText, $SurfaceCodeText, $TestText, $DocText)) {
        foreach ($marker in @('TODO', 'FIXME', 'Coming soon')) {
            if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P06-003 contains forbidden unfinished-work marker '$marker'."
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

    throw "Negative P06-003 lazy-explorer fixture was not rejected: $Label"
}

$paths = @{
    Contract = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\IProjectFileExplorerService.cs'
    Service = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\FileSystemProjectFileExplorerService.cs'
    Node = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectFileTreeNode.cs'
    State = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectFileExplorerState.cs'
    SurfaceXaml = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectWorkspaceSurface.xaml'
    SurfaceCode = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectWorkspaceSurface.xaml.cs'
    Tests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectFileExplorerServiceTests.cs'
    Docs = Join-Path $RepositoryRoot 'docs\projects\LAZY_FILE_EXPLORER.md'
}

foreach ($path in $paths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P06-003 path is missing: $path"
    }
}

$contractText = Get-Content -LiteralPath $paths.Contract -Raw
$serviceText = Get-Content -LiteralPath $paths.Service -Raw
$nodeText = Get-Content -LiteralPath $paths.Node -Raw
$stateText = Get-Content -LiteralPath $paths.State -Raw
$surfaceXamlText = Get-Content -LiteralPath $paths.SurfaceXaml -Raw
$surfaceCodeText = Get-Content -LiteralPath $paths.SurfaceCode -Raw
$testText = Get-Content -LiteralPath $paths.Tests -Raw
$docText = Get-Content -LiteralPath $paths.Docs -Raw

Assert-LazyExplorerContract $contractText $serviceText $nodeText $stateText $surfaceXamlText $surfaceCodeText $testText $docText
Write-Host 'Static P06-003 lazy file explorer validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-LazyExplorerContract $contractText ($serviceText.Replace('.Take(_maximumEntriesPerDirectory + 1)', '.Skip(0)')) $nodeText $stateText $surfaceXamlText $surfaceCodeText $testText $docText
    } 'per-directory materialization bound removed'
    Assert-ContractRejects {
        Assert-LazyExplorerContract $contractText ($serviceText.Replace('EnsurePathInsideProject(normalizedRootPath, normalizedDirectoryPath)', 'RemovedProjectBoundaryCheck(normalizedRootPath, normalizedDirectoryPath)')) $nodeText $stateText $surfaceXamlText $surfaceCodeText $testText $docText
    } 'project-root boundary guard removed'
    Assert-ContractRejects {
        Assert-LazyExplorerContract $contractText ($serviceText.Replace('FileAttributes.ReparsePoint', 'FileAttributes.Normal')) $nodeText $stateText $surfaceXamlText $surfaceCodeText $testText $docText
    } 'reparse-point guard removed'
    Assert-ContractRejects {
        Assert-LazyExplorerContract $contractText $serviceText $nodeText ($stateText.Replace('.ListChildrenAsync(project.RootPath, node.FullPath, cancellationToken)', '.RemovedListChildrenAsync(project.RootPath, node.FullPath, cancellationToken)')) $surfaceXamlText $surfaceCodeText $testText $docText
    } 'lazy service invocation removed'
    Assert-ContractRejects {
        Assert-LazyExplorerContract $contractText $serviceText $nodeText $stateText ($surfaceXamlText.Replace('EventSetter Event="Expanded" Handler="OnFileExplorerNodeExpanded"', 'EventSetter Event="Expanded" Handler="RemovedExpansionHandler"')) $surfaceCodeText $testText $docText
    } 'expand-to-load wiring removed'
    Assert-ContractRejects {
        Assert-LazyExplorerContract $contractText $serviceText $nodeText $stateText ($surfaceXamlText.Replace('VirtualizingPanel.IsVirtualizing="True"', 'VirtualizingPanel.IsVirtualizing="False"')) $surfaceCodeText $testText $docText
    } 'tree virtualization removed'
    Write-Host 'P06-003 negative fixtures: PASS.'
}

if ($RequireRuntime) {
    if (-not $IsWindows) {
        throw 'Executable P06-003 lazy file explorer validation requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for executable P06-003 lazy file explorer validation.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "P06-003 validation requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $testProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'
    & dotnet test $testProject -c Release --no-restore --no-build --nologo --filter 'FullyQualifiedName~ProjectFileExplorerServiceTests'
    if ($LASTEXITCODE -ne 0) {
        throw 'P06-003 lazy file explorer integration tests failed.'
    }

    Write-Host 'Executable P06-003 lazy file explorer validation: PASS.'
}
