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

function Assert-EditorLifecycleContract {
    param(
        [string]$WorkspaceText,
        [string]$SurfaceXamlText,
        [string]$SurfaceCodeText,
        [string]$ProjectSurfaceCodeText,
        [string]$TestsText,
        [string]$IntegrationTestsText,
        [string]$DocText
    )

    Assert-ValidXaml $SurfaceXamlText 'ProjectEditorSurface.xaml'

    foreach ($literal in @(
        'public sealed class ProjectEditorWorkspace : INotifyPropertyChanged',
        'ReadOnlyObservableCollection<ProjectEditorDocument> Documents',
        'public async Task<ProjectEditorDocument> OpenAsync(',
        '.InspectAsync(normalizedRoot, normalizedPath, cancellationToken)',
        'if (!inspection.CanOpenAsNormalText)',
        'public async Task SaveAsync(',
        'document.Version)',
        'catch (ProjectFileConflictException exception)',
        'document.MarkConflict(exception.Message)',
        'public async Task ReloadAsync(',
        'if (document.IsDirty && !discardUnsavedChanges)',
        'public void Close(ProjectEditorDocument document, bool discardUnsavedChanges)',
        'ProjectEditorTextPolicy.NormalizeForSave(document.Text, document.NewLineStyle)',
        'Existing tabs save only to their original project roots.'
    )) { Assert-ContainsLiteral $WorkspaceText $literal 'ProjectEditorWorkspace.cs' }

    foreach ($literal in @(
        'x:Class="FCCCodeDesktop.App.Editor.ProjectEditorSurface"',
        'AutomationProperties.Name="Project editor workspace"',
        'Content="Save"',
        'Content="Reload"',
        'Content="Close"',
        'ItemsSource="{Binding Documents}"',
        'SelectedItem="{Binding SelectedDocument, Mode=TwoWay}"',
        '<editor:CodeEditorControl Text="{Binding Text, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"',
        'Text="{Binding DisplayLabel}"',
        'Binary and oversized files are refused before editor materialization.'
    )) { Assert-ContainsLiteral $SurfaceXamlText $literal 'ProjectEditorSurface.xaml' }

    foreach ($literal in @(
        'MessageBoxButton.YesNo',
        'discardUnsavedChanges: true',
        'await workspace.SaveSelectedAsync',
        'await workspace.ReloadSelectedAsync',
        'workspace.CloseSelected'
    )) { Assert-ContainsLiteral $SurfaceCodeText $literal 'ProjectEditorSurface.xaml.cs' }

    foreach ($literal in @(
        'EditorWorkspace = new ProjectEditorWorkspace(new FileSystemProjectFileService())',
        'TreeView.SelectedItemChangedEvent',
        'OnFileExplorerSelectionChanged',
        '.OpenAsync(project.RootPath, node.FullPath, CancellationToken.None)',
        'Workspace = EditorWorkspace',
        'Grid.SetColumn(editorSurface, 2)'
    )) { Assert-ContainsLiteral $ProjectSurfaceCodeText $literal 'ProjectWorkspaceSurface.xaml.cs' }

    foreach ($literal in @(
        'SaveAsyncUsesObservedVersionEncodingAndOriginalNewLineStyle',
        'SaveAsyncExternalConflictRetainsDirtyBuffer',
        'ReloadAsyncDirtyBufferRequiresExplicitDiscard',
        'CloseDirtyBufferRequiresExplicitDiscard',
        'OpenAsyncBinaryAndOversizedFilesFailBeforeRead',
        'SetActiveProjectDoesNotRetargetExistingTabs'
    )) { Assert-ContainsLiteral $TestsText $literal 'ProjectEditorWorkspaceTests.cs' }

    foreach ($literal in @(
        'RealFileServicePreservesUnicodePathEncodingNewLinesConflictAndReload',
        'new FileSystemProjectFileService()',
        'ProjectTextEncoding.Utf16BigEndian',
        'ProjectNewLineStyle.Lf',
        'Assert.ThrowsAsync<ProjectFileConflictException>',
        'discardUnsavedChanges: true'
    )) { Assert-ContainsLiteral $IntegrationTestsText $literal 'ProjectEditorWorkspaceIntegrationTests.cs' }

    foreach ($literal in @(
        'P06-004 safe file service',
        'P06-005 native editor',
        'optimistic version token',
        'dirty tabs are never silently discarded',
        'binary and oversized files',
        'FINAL_OWNER_ACCEPTANCE_QUEUE'
    )) { Assert-ContainsLiteral $DocText $literal 'EDITOR_LIFECYCLE.md' }

    foreach ($text in @($WorkspaceText, $SurfaceXamlText, $SurfaceCodeText, $ProjectSurfaceCodeText, $TestsText, $IntegrationTestsText, $DocText)) {
        foreach ($marker in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P06-006 contains forbidden unfinished-work marker '$marker'."
            }
        }
    }

    foreach ($forbidden in @('File.WriteAllText', 'File.WriteAllBytes', 'new FileStream(', 'Process.Start', 'HttpClient')) {
        if ($WorkspaceText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $SurfaceCodeText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $ProjectSurfaceCodeText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
            throw "P06-006 bypasses the safe file-service/editor boundary: $forbidden"
        }
    }
}

function Assert-Rejects {
    param([scriptblock]$Action, [string]$Label)
    try { & $Action }
    catch {
        Write-Host "Negative fixture rejected as expected: $Label"
        return
    }
    throw "Negative P06-006 fixture was not rejected: $Label"
}

$paths = @{
    Workspace = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\ProjectEditorWorkspace.cs'
    SurfaceXaml = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Editor\ProjectEditorSurface.xaml'
    SurfaceCode = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Editor\ProjectEditorSurface.xaml.cs'
    ProjectSurfaceCode = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Projects\ProjectWorkspaceSurface.xaml.cs'
    Tests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\ProjectEditorWorkspaceTests.cs'
    IntegrationTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectEditorWorkspaceIntegrationTests.cs'
    Doc = Join-Path $RepositoryRoot 'docs\projects\EDITOR_LIFECYCLE.md'
}

foreach ($entry in $paths.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value)) { throw "Required P06-006 path is missing: $($entry.Value)" }
}

$workspaceText = Get-Content -LiteralPath $paths.Workspace -Raw
$surfaceXamlText = Get-Content -LiteralPath $paths.SurfaceXaml -Raw
$surfaceCodeText = Get-Content -LiteralPath $paths.SurfaceCode -Raw
$projectSurfaceCodeText = Get-Content -LiteralPath $paths.ProjectSurfaceCode -Raw
$testsText = Get-Content -LiteralPath $paths.Tests -Raw
$integrationTestsText = Get-Content -LiteralPath $paths.IntegrationTests -Raw
$docText = Get-Content -LiteralPath $paths.Doc -Raw

Assert-EditorLifecycleContract $workspaceText $surfaceXamlText $surfaceCodeText $projectSurfaceCodeText $testsText $integrationTestsText $docText
Write-Host 'Static P06-006 editor lifecycle contract: PASS.'

if ($RunFixtures) {
    $withoutVersion = $workspaceText.Replace('document.Version)', 'expectedVersion: null)', [StringComparison]::Ordinal)
    Assert-Rejects { Assert-EditorLifecycleContract $withoutVersion $surfaceXamlText $surfaceCodeText $projectSurfaceCodeText $testsText $integrationTestsText $docText } 'save without optimistic version token'

    $withoutDirtyGuard = $workspaceText.Replace('if (document.IsDirty && !discardUnsavedChanges)', 'if (false)', [StringComparison]::Ordinal)
    Assert-Rejects { Assert-EditorLifecycleContract $withoutDirtyGuard $surfaceXamlText $surfaceCodeText $projectSurfaceCodeText $testsText $integrationTestsText $docText } 'dirty reload/close guard removed'

    $withoutInspection = $workspaceText.Replace('.InspectAsync(normalizedRoot, normalizedPath, cancellationToken)', '.ReadTextAsync(normalizedRoot, normalizedPath, cancellationToken)', [StringComparison]::Ordinal)
    Assert-Rejects { Assert-EditorLifecycleContract $withoutInspection $surfaceXamlText $surfaceCodeText $projectSurfaceCodeText $testsText $integrationTestsText $docText } 'large/binary preflight removed'

    $withoutRealServiceIntegration = $integrationTestsText.Replace('new FileSystemProjectFileService()', 'new FakeProjectFileService()', [StringComparison]::Ordinal)
    Assert-Rejects { Assert-EditorLifecycleContract $workspaceText $surfaceXamlText $surfaceCodeText $projectSurfaceCodeText $testsText $withoutRealServiceIntegration $docText } 'real safe-file-service integration removed'

    Write-Host 'Negative P06-006 editor lifecycle fixtures: PASS.'
}

if ($RequireRuntime) {
    if (-not $IsWindows) { throw 'Executable P06-006 validation requires Windows.' }
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) { throw 'dotnet is required for executable P06-006 validation.' }
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "P06-006 validation requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $unitProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'
    & dotnet test $unitProject -c Release --no-restore --no-build --nologo --filter 'FullyQualifiedName~ProjectEditorWorkspaceTests'
    if ($LASTEXITCODE -ne 0) { throw 'P06-006 editor lifecycle unit tests failed.' }

    $integrationProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'
    & dotnet test $integrationProject -c Release --no-restore --no-build --nologo --filter 'FullyQualifiedName~ProjectEditorWorkspaceIntegrationTests'
    if ($LASTEXITCODE -ne 0) { throw 'P06-006 real safe-file lifecycle integration test failed.' }

    Write-Host 'Executable P06-006 editor lifecycle unit + integration validation: PASS.'
}
