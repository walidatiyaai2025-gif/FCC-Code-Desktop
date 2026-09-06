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
    try { [void][xml]$Text }
    catch { throw "$Label is not valid XML/XAML: $($_.Exception.Message)" }
}

function Assert-WorkspaceSearchContract {
    param(
        [string]$ContractText,
        [string]$ServiceText,
        [string]$StateText,
        [string]$SearchXamlText,
        [string]$SearchCodeText,
        [string]$WorkspaceCodeText,
        [string]$TestText,
        [string]$DocText
    )

    Assert-ValidXaml $SearchXamlText 'ProjectSearchSurface.xaml'

    foreach ($literal in @(
        'public interface IProjectSearchService',
        'Task<ProjectSearchResultSet> SearchAsync',
        'ProjectSearchMode',
        'Content,',
        'FileName,',
        'RegularExpression,',
        'MaximumResults = WorkspaceScalePolicy.DefaultMaximumSearchResults',
        'MaximumFiles = WorkspaceScalePolicy.DefaultMaximumFilesPerOperation',
        'MaximumFileBytes = WorkspaceScalePolicy.DefaultMaximumSearchFileBytes',
        'MaximumTraversalDepth = WorkspaceScalePolicy.DefaultMaximumTraversalDepth',
        'MaximumMatchesPerFile = WorkspaceScalePolicy.DefaultMaximumSearchMatchesPerFile',
        'ProjectSearchLimitReason',
        'ProjectSearchQueryException'
    )) { Assert-ContainsLiteral $ContractText $literal 'IProjectSearchService.cs' }

    foreach ($literal in @(
        'public sealed class FileSystemProjectSearchService',
        'MaximumSupportedResults = WorkspaceScalePolicy.MaximumSupportedSearchResults',
        'MaximumSupportedFiles = WorkspaceScalePolicy.MaximumSupportedFilesPerOperation',
        'MaximumSupportedFileBytes = WorkspaceScalePolicy.MaximumSupportedSearchFileBytes',
        'RegularExpressionTimeout = TimeSpan.FromMilliseconds(250)',
        'Task.Run(() => SearchCore(request, cancellationToken), cancellationToken)',
        'Directory.EnumerateFileSystemEntries(directoryPath)',
        'FileAttributes.ReparsePoint',
        '_policy.ShouldExcludeDirectory(directoryName)',
        'ProjectSearchLimitReason.MatchesPerFile',
        'ProjectSearchLimitReason.TraversalDepth',
        'ProjectSearchLimitReason.DirectoryEntries',
        'cancellationToken.ThrowIfCancellationRequested()',
        'RegexMatchTimeoutException',
        'throwOnInvalidBytes: true',
        'FileShare.ReadWrite | FileShare.Delete',
        'LooksBinary(fullPath)',
        'IsPathInsideProject(normalizedRootPath, fullPath)'
    )) { Assert-ContainsLiteral $ServiceText $literal 'FileSystemProjectSearchService.cs' }

    foreach ($forbidden in @(
        'SearchOption.AllDirectories',
        'File.ReadAllText',
        'File.ReadAllLines',
        'File.WriteAll',
        'File.Delete',
        'Directory.Delete',
        'Process.Start',
        'ProcessStartInfo'
    )) {
        if ($ServiceText.Contains($forbidden, [StringComparison]::Ordinal)) {
            throw "Workspace search contains forbidden recursive, unbounded, destructive, or process text: $forbidden"
        }
    }

    foreach ($literal in @(
        'public sealed class ProjectSearchState',
        'IProjectSearchService _searchService',
        'ReadOnlyObservableCollection<ProjectSearchMatch> Matches',
        'public bool CanSearch',
        'public bool CanCancel',
        'public async Task SearchAsync',
        'CancellationTokenSource.CreateLinkedTokenSource',
        'public void CancelSearch()',
        'SetProject(PersistedProject? project)',
        'Search cancelled. Existing source files were not modified.'
    )) { Assert-ContainsLiteral $StateText $literal 'ProjectSearchState.cs' }

    foreach ($literal in @(
        'AutomationProperties.Name="Workspace search"',
        'AutomationProperties.Name="Workspace search query"',
        'AutomationProperties.Name="Workspace search mode"',
        'Content="Search"',
        'Content="Cancel"',
        'ItemsSource="{Binding Matches}"',
        'VirtualizingPanel.IsVirtualizing="True"',
        'VirtualizingPanel.VirtualizationMode="Recycling"',
        'Generated folders and unsafe reparse-point targets are skipped by default.'
    )) { Assert-ContainsLiteral $SearchXamlText $literal 'ProjectSearchSurface.xaml' }

    foreach ($literal in @(
        'OnSearchQueryKeyDown',
        'Key.Escape',
        'Key.Enter',
        'state.SearchAsync(CancellationToken.None)',
        'State?.CancelSearch()'
    )) { Assert-ContainsLiteral $SearchCodeText $literal 'ProjectSearchSurface.xaml.cs' }

    foreach ($literal in @(
        'SearchState = new ProjectSearchState(new FileSystemProjectSearchService());',
        'AttachSearchSurface();',
        'SearchState.SetProject(newState.ActiveProject);',
        'SearchState.SetProject(State?.ActiveProject);',
        'new ProjectSearchSurface',
        'Grid.SetRowSpan(searchSurface, 2);',
        'Grid.SetColumn(searchSurface, 1);'
    )) { Assert-ContainsLiteral $WorkspaceCodeText $literal 'ProjectWorkspaceSurface.xaml.cs' }

    foreach ($literal in @(
        'ContentSearchFindsUnicodeAndSpaceContainingPathsWithoutModifyingFiles',
        'FileNameSearchDoesNotReadFileContentAndHonorsCaseSetting',
        'RegularExpressionSearchReturnsLineAndColumnAndRejectsInvalidPattern',
        'SearchSkipsGeneratedDirectoriesBinaryAndOversizedFiles',
        'SearchSupportsBomEncodedTextAndNeverTraversesIgnoredGitMetadata',
        'ResultAndFileCapsAreBoundedAndReported',
        'CancellationMissingRootAndInvalidBoundsFailExplicitly',
        'TraversalDepthAndPerFileCapsProduceTypedPartialResults',
        'WideDirectoryMaterializationIsBoundedOrderedAndStable',
        'مشروع search with spaces'
    )) { Assert-ContainsLiteral $TestText $literal 'ProjectSearchServiceTests.cs' }

    foreach ($literal in @(
        'filename, literal-content, and line-based regular-expression modes',
        'runs on a background worker',
        'does not follow reparse-point entries',
        '`500` matches',
        '`20,000` examined files',
        '`4 MiB per content-searched file`',
        'no owner-only evidence'
    )) { Assert-ContainsLiteral $DocText $literal 'WORKSPACE_SEARCH.md' }

    foreach ($text in @($ContractText, $ServiceText, $StateText, $SearchXamlText, $SearchCodeText, $WorkspaceCodeText, $TestText, $DocText)) {
        foreach ($marker in @('TODO', 'FIXME', 'Coming soon')) {
            if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P06-007 contains forbidden unfinished-work marker '$marker'."
            }
        }
    }
}

function Assert-ContractRejects {
    param([scriptblock]$Action, [string]$Label)
    try { & $Action }
    catch {
        Write-Host "Negative fixture rejected as expected: $Label"
        return
    }
    throw "Negative P06-007 workspace-search fixture was not rejected: $Label"
}

$paths = @{
    Contract = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\IProjectSearchService.cs'
    Service = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\FileSystemProjectSearchService.cs'
    State = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectSearchState.cs'
    SearchXaml = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectSearchSurface.xaml'
    SearchCode = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectSearchSurface.xaml.cs'
    WorkspaceCode = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectWorkspaceSurface.xaml.cs'
    Tests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectSearchServiceTests.cs'
    Docs = Join-Path $RepositoryRoot 'docs\projects\WORKSPACE_SEARCH.md'
}

foreach ($path in $paths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P06-007 path is missing: $path"
    }
}

$contractText = Get-Content -LiteralPath $paths.Contract -Raw
$serviceText = Get-Content -LiteralPath $paths.Service -Raw
$stateText = Get-Content -LiteralPath $paths.State -Raw
$searchXamlText = Get-Content -LiteralPath $paths.SearchXaml -Raw
$searchCodeText = Get-Content -LiteralPath $paths.SearchCode -Raw
$workspaceCodeText = Get-Content -LiteralPath $paths.WorkspaceCode -Raw
$testText = Get-Content -LiteralPath $paths.Tests -Raw
$docText = Get-Content -LiteralPath $paths.Docs -Raw

Assert-WorkspaceSearchContract $contractText $serviceText $stateText $searchXamlText $searchCodeText $workspaceCodeText $testText $docText
Write-Host 'Static P06-007 workspace search validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-WorkspaceSearchContract $contractText ($serviceText.Replace('Task.Run(() => SearchCore(request, cancellationToken), cancellationToken)', 'Task.FromResult(SearchCore(request, cancellationToken))')) $stateText $searchXamlText $searchCodeText $workspaceCodeText $testText $docText
    } 'background worker removed'
    Assert-ContractRejects {
        Assert-WorkspaceSearchContract $contractText ($serviceText.Replace('FileAttributes.ReparsePoint', 'FileAttributes.Normal')) $stateText $searchXamlText $searchCodeText $workspaceCodeText $testText $docText
    } 'reparse-point guard removed'
    Assert-ContractRejects {
        Assert-WorkspaceSearchContract $contractText ($serviceText.Replace('cancellationToken.ThrowIfCancellationRequested()', 'RemovedCancellationCheck()')) $stateText $searchXamlText $searchCodeText $workspaceCodeText $testText $docText
    } 'cancellation checks removed'
    Assert-ContractRejects {
        Assert-WorkspaceSearchContract ($contractText.Replace('MaximumResults = WorkspaceScalePolicy.DefaultMaximumSearchResults', 'MaximumResults = int.MaxValue')) $serviceText $stateText $searchXamlText $searchCodeText $workspaceCodeText $testText $docText
    } 'default result bound removed'
    Assert-ContractRejects {
        Assert-WorkspaceSearchContract $contractText $serviceText $stateText ($searchXamlText.Replace('VirtualizingPanel.IsVirtualizing="True"', 'VirtualizingPanel.IsVirtualizing="False"')) $searchCodeText $workspaceCodeText $testText $docText
    } 'result virtualization removed'
    Assert-ContractRejects {
        Assert-WorkspaceSearchContract $contractText $serviceText ($stateText.Replace('public void CancelSearch()', 'public void RemovedCancelSearch()')) $searchXamlText $searchCodeText $workspaceCodeText $testText $docText
    } 'cancel state API removed'
    Write-Host 'P06-007 negative fixtures: PASS.'
}

if ($RequireRuntime) {
    if (-not $IsWindows) { throw 'Executable P06-007 workspace search validation requires Windows.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet is required for executable P06-007 workspace search validation.' }
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "P06-007 validation requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $testProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'
    & dotnet test $testProject -c Release --no-restore --no-build --nologo --filter 'FullyQualifiedName~ProjectSearchServiceTests'
    if ($LASTEXITCODE -ne 0) { throw 'P06-007 workspace search integration tests failed.' }
    Write-Host 'Executable P06-007 workspace search validation: PASS.'
}
