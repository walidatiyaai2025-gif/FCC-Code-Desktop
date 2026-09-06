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

function Assert-NoForbiddenMarker {
    param([string[]]$Texts)
    foreach ($text in $Texts) {
        foreach ($marker in @('TODO', 'FIXME', 'Coming soon')) {
            if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P06-008 contains forbidden unfinished-work marker '$marker'."
            }
        }
    }
}

function Assert-LargeWorkspaceContract {
    param(
        [string]$PolicyText,
        [string]$ExplorerServiceText,
        [string]$FileContractText,
        [string]$FileServiceText,
        [string]$SearchContractText,
        [string]$SearchServiceText,
        [string]$PolicyTestsText,
        [string]$ExplorerTestsText,
        [string]$FileTestsText,
        [string]$SearchTestsText,
        [string]$DocsText
    )

    foreach ($literal in @(
        'public sealed class WorkspaceScalePolicy',
        'DefaultMaximumDirectoryEntries = 2_048',
        'DefaultMaximumTraversalDepth = 64',
        'DefaultMaximumFilesPerOperation = 20_000',
        'DefaultMaximumSearchResults = 500',
        'DefaultMaximumSearchMatchesPerFile = 100',
        'DefaultMaximumTextFileBytes = 8L * 1024 * 1024',
        'DefaultMaximumSearchFileBytes = 4L * 1024 * 1024',
        'DefaultMaximumPreviewCharacters = 240',
        'DefaultBinaryProbeBytes = 4_096',
        'MaximumSupportedTraversalDepth = 256',
        'MaximumSupportedFilesPerOperation = 100_000',
        'MaximumSupportedSearchResults = 5_000',
        'MaximumSupportedSearchMatchesPerFile = 5_000',
        'MaximumSupportedTextFileBytes = 128L * 1024 * 1024',
        'MaximumSupportedSearchFileBytes = 64L * 1024 * 1024',
        'public static WorkspaceScalePolicy Default { get; } = new();',
        'public bool ShouldExcludeDirectory(string directoryName)'
    )) { Assert-ContainsLiteral $PolicyText $literal 'WorkspaceScalePolicy.cs' }

    foreach ($literal in @(
        'DefaultMaximumEntriesPerDirectory = WorkspaceScalePolicy.DefaultMaximumDirectoryEntries',
        'MaximumSupportedEntriesPerDirectory = WorkspaceScalePolicy.MaximumSupportedDirectoryEntries',
        'WorkspaceScalePolicy _policy',
        'Directory.EnumerateFileSystemEntries(normalizedDirectoryPath)',
        '.Take(_maximumEntriesPerDirectory + 1)',
        '_policy.ShouldExcludeDirectory(name)',
        'directoryDepth >= _policy.MaximumTraversalDepth',
        'ProjectFileTraversalRestriction.ExcludedDirectory',
        'ProjectFileTraversalRestriction.MaximumDepth',
        'cancellationToken.ThrowIfCancellationRequested()',
        'FileAttributes.ReparsePoint',
        'EnsurePathInsideProject(normalizedRootPath, normalizedDirectoryPath)'
    )) { Assert-ContainsLiteral $ExplorerServiceText $literal 'FileSystemProjectFileExplorerService.cs' }

    foreach ($literal in @(
        'Task<ProjectFileInspection> InspectAsync',
        'ProjectFileContentKind',
        'Text,',
        'Binary,',
        'TooLarge,',
        'public bool CanOpenAsNormalText => ContentKind == ProjectFileContentKind.Text;'
    )) { Assert-ContainsLiteral $FileContractText $literal 'IProjectFileService.cs' }

    foreach ($literal in @(
        'DefaultMaximumFileBytes = (int)WorkspaceScalePolicy.DefaultMaximumTextFileBytes',
        'MaximumSupportedFileBytes = (int)WorkspaceScalePolicy.MaximumSupportedTextFileBytes',
        'WorkspaceScalePolicy _policy',
        'public async Task<ProjectFileInspection> InspectAsync',
        'ReadBinaryProbeAsync',
        'ProjectFileContentKind.Binary',
        'ProjectFileContentKind.TooLarge',
        'ReadPreviewAsync',
        '_policy.MaximumPreviewCharacters',
        '_policy.BinaryProbeBytes',
        'ValidatePaths(projectRootPath, filePath, requireExistingFile: true)',
        'cancellationToken.ThrowIfCancellationRequested()'
    )) { Assert-ContainsLiteral $FileServiceText $literal 'FileSystemProjectFileService.cs' }

    foreach ($literal in @(
        'MaximumTraversalDepth',
        'MaximumMatchesPerFile',
        'MaximumPreviewCharacters',
        'BinaryProbeBytes',
        'ProjectSearchResultSet'
    )) { Assert-ContainsLiteral $SearchContractText $literal 'IProjectSearchService.cs' }

    foreach ($literal in @(
        'MaximumSupportedResults = WorkspaceScalePolicy.MaximumSupportedSearchResults',
        'MaximumSupportedFiles = WorkspaceScalePolicy.MaximumSupportedFilesPerOperation',
        'MaximumSupportedFileBytes = WorkspaceScalePolicy.MaximumSupportedSearchFileBytes',
        'WorkspaceScalePolicy _scalePolicy',
        '_scalePolicy.ShouldExcludeDirectory(directoryName)',
        'directoryDepth >= limits.MaximumTraversalDepth',
        'limits.MaximumMatchesPerFile',
        'limits.MaximumPreviewCharacters',
        '_scalePolicy.BinaryProbeBytes',
        'RegexMatchTimeoutException',
        'FileAttributes.ReparsePoint',
        'cancellationToken.ThrowIfCancellationRequested()'
    )) { Assert-ContainsLiteral $SearchServiceText $literal 'FileSystemProjectSearchService.cs' }

    foreach ($forbidden in @(
        'SearchOption.AllDirectories',
        'Directory.GetFiles(',
        'Directory.GetDirectories(',
        'File.ReadAllText',
        'File.ReadAllLines',
        'File.WriteAll',
        'File.Delete',
        'Directory.Delete',
        'Process.Start',
        'ProcessStartInfo'
    )) {
        foreach ($entry in @(
            @{ Text = $ExplorerServiceText; Label = 'explorer service' },
            @{ Text = $SearchServiceText; Label = 'search service' }
        )) {
            if ($entry.Text.Contains($forbidden, [StringComparison]::Ordinal)) {
                throw "P06-008 $($entry.Label) contains forbidden unbounded, destructive, or process text: $forbidden"
            }
        }
    }

    foreach ($literal in @(
        'DefaultsAreFiniteAndContainCanonicalGeneratedDirectories',
        'CustomLimitsAndExclusionsAreImmutableAndCaseInsensitive',
        'RejectsEveryInvalidBoundAndUnsafeExclusion'
    )) { Assert-ContainsLiteral $PolicyTestsText $literal 'WorkspaceScalePolicyTests.cs' }

    foreach ($literal in @(
        'DirectoryEntryCapReportsLimitAndBoundsMaterialization',
        'GeneratedAndDepthLimitedDirectoriesAreVisibleButCannotBeTraversed',
        'SupportsNonAsciiAndSpaceContainingPathsWithoutModifyingSource',
        'CancellationAndInvalidConfigurationFailExplicitly'
    )) { Assert-ContainsLiteral $ExplorerTestsText $literal 'ProjectFileExplorerServiceTests.cs' }

    foreach ($literal in @(
        'InspectionClassifiesEmptyBinaryBoundaryAndLargeFilesWithoutMutation',
        'InspectionSupportsBomUnicodeArabicAndRecoversAfterCancellationAndFailure',
        'RejectsOutsideRootDirectoryTargetsInvalidEncodingAndOversizedFiles'
    )) { Assert-ContainsLiteral $FileTestsText $literal 'ProjectFileServiceTests.cs' }

    foreach ($literal in @(
        'SearchUsesCentralPolicyForDepthPerFilePreviewAndTypedMetadata',
        'SearchRejectsRequestLimitsAboveInjectedPolicy',
        'SearchSkipsGeneratedDirectoriesBinaryAndOversizedFiles',
        'ResultAndFileCapsAreBoundedAndReported'
    )) { Assert-ContainsLiteral $SearchTestsText $literal 'ProjectSearchServiceTests.cs' }

    foreach ($literal in @(
        '# Large Workspace Safeguards',
        '`WorkspaceScalePolicy`',
        '2,048 materialized entries',
        '64 directory levels',
        '20,000 files examined',
        '500 search results total and 100 matches from one file',
        '8 MiB for normal text-file materialization',
        '4 MiB for a content-search candidate',
        'Binary and oversized inputs are classified without full-file allocation',
        'does not implement or replace the locally bundled editor, editor tabs, save/reload/dirty conflict UX'
    )) { Assert-ContainsLiteral $DocsText $literal 'LARGE_WORKSPACE_SAFEGUARDS.md' }

    Assert-NoForbiddenMarker @(
        $PolicyText,
        $ExplorerServiceText,
        $FileContractText,
        $FileServiceText,
        $SearchContractText,
        $SearchServiceText,
        $PolicyTestsText,
        $ExplorerTestsText,
        $FileTestsText,
        $SearchTestsText,
        $DocsText)
}

function Assert-ContractRejects {
    param([scriptblock]$Action, [string]$Label)
    try { & $Action }
    catch {
        Write-Host "Negative fixture rejected as expected: $Label"
        return
    }
    throw "Negative P06-008 large-workspace fixture was not rejected: $Label"
}

$paths = @{
    Policy = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\WorkspaceScalePolicy.cs'
    ExplorerService = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\FileSystemProjectFileExplorerService.cs'
    FileContract = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\IProjectFileService.cs'
    FileService = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\FileSystemProjectFileService.cs'
    SearchContract = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\IProjectSearchService.cs'
    SearchService = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\FileSystemProjectSearchService.cs'
    PolicyTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\WorkspaceScalePolicyTests.cs'
    ExplorerTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectFileExplorerServiceTests.cs'
    FileTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectFileServiceTests.cs'
    SearchTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectSearchServiceTests.cs'
    Docs = Join-Path $RepositoryRoot 'docs\projects\LARGE_WORKSPACE_SAFEGUARDS.md'
}

foreach ($path in $paths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P06-008 path is missing: $path"
    }
}

$policyText = Get-Content -LiteralPath $paths.Policy -Raw
$explorerServiceText = Get-Content -LiteralPath $paths.ExplorerService -Raw
$fileContractText = Get-Content -LiteralPath $paths.FileContract -Raw
$fileServiceText = Get-Content -LiteralPath $paths.FileService -Raw
$searchContractText = Get-Content -LiteralPath $paths.SearchContract -Raw
$searchServiceText = Get-Content -LiteralPath $paths.SearchService -Raw
$policyTestsText = Get-Content -LiteralPath $paths.PolicyTests -Raw
$explorerTestsText = Get-Content -LiteralPath $paths.ExplorerTests -Raw
$fileTestsText = Get-Content -LiteralPath $paths.FileTests -Raw
$searchTestsText = Get-Content -LiteralPath $paths.SearchTests -Raw
$docsText = Get-Content -LiteralPath $paths.Docs -Raw

Assert-LargeWorkspaceContract $policyText $explorerServiceText $fileContractText $fileServiceText $searchContractText $searchServiceText $policyTestsText $explorerTestsText $fileTestsText $searchTestsText $docsText
Write-Host 'Static P06-008 large workspace safeguards validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-LargeWorkspaceContract ($policyText.Replace('DefaultMaximumTraversalDepth = 64', 'DefaultMaximumTraversalDepth = int.MaxValue')) $explorerServiceText $fileContractText $fileServiceText $searchContractText $searchServiceText $policyTestsText $explorerTestsText $fileTestsText $searchTestsText $docsText
    } 'finite traversal-depth policy removed'
    Assert-ContractRejects {
        Assert-LargeWorkspaceContract $policyText ($explorerServiceText.Replace('.Take(_maximumEntriesPerDirectory + 1)', '.ToArray()')) $fileContractText $fileServiceText $searchContractText $searchServiceText $policyTestsText $explorerTestsText $fileTestsText $searchTestsText $docsText
    } 'bounded explorer materialization removed'
    Assert-ContractRejects {
        Assert-LargeWorkspaceContract $policyText $explorerServiceText $fileContractText ($fileServiceText.Replace('ProjectFileContentKind.TooLarge', 'ProjectFileContentKind.Text')) $searchContractText $searchServiceText $policyTestsText $explorerTestsText $fileTestsText $searchTestsText $docsText
    } 'large-file classification removed'
    Assert-ContractRejects {
        Assert-LargeWorkspaceContract $policyText $explorerServiceText $fileContractText $fileServiceText $searchContractText ($searchServiceText.Replace('limits.MaximumMatchesPerFile', 'int.MaxValue')) $policyTestsText $explorerTestsText $fileTestsText $searchTestsText $docsText
    } 'per-file search bound removed'
    Assert-ContractRejects {
        Assert-LargeWorkspaceContract $policyText $explorerServiceText $fileContractText $fileServiceText $searchContractText ($searchServiceText.Replace('_scalePolicy.ShouldExcludeDirectory(directoryName)', 'false')) $policyTestsText $explorerTestsText $fileTestsText $searchTestsText $docsText
    } 'generated-directory policy removed from search'
    Write-Host 'P06-008 negative fixtures: PASS.'
}

if ($RequireRuntime) {
    if (-not $IsWindows) { throw 'Executable P06-008 validation requires Windows.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet is required for executable P06-008 validation.' }
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "P06-008 validation requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $unitProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'
    $integrationProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'

    & dotnet test $unitProject -c Release --no-restore --no-build --nologo --filter 'FullyQualifiedName~WorkspaceScalePolicyTests'
    if ($LASTEXITCODE -ne 0) { throw 'P06-008 workspace scale policy unit tests failed.' }

    foreach ($fixture in @('ProjectFileExplorerServiceTests', 'ProjectFileServiceTests', 'ProjectSearchServiceTests')) {
        & dotnet test $integrationProject -c Release --no-restore --no-build --nologo --filter "FullyQualifiedName~$fixture"
        if ($LASTEXITCODE -ne 0) { throw "P06-008 integration fixture failed: $fixture" }
    }

    Write-Host 'Executable P06-008 large workspace safeguards validation: PASS.'
}
