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

function Assert-DoesNotContainLiteral {
    param([string]$Text, [string]$Literal, [string]$Label)
    if ($Text.Contains($Literal, [StringComparison]::Ordinal)) {
        throw "$Label contains forbidden text: $Literal"
    }
}

function Assert-LargeWorkspaceContract {
    param(
        [string]$PolicyText,
        [string]$ExplorerContractText,
        [string]$ExplorerServiceText,
        [string]$FileContractText,
        [string]$FileServiceText,
        [string]$SearchContractText,
        [string]$SearchServiceText,
        [string]$PolicyTestsText,
        [string]$ExplorerTestsText,
        [string]$FileTestsText,
        [string]$SearchTestsText,
        [string]$DocText
    )

    foreach ($literal in @(
        'public sealed class WorkspaceScalePolicy',
        'DefaultMaximumDirectoryEntries = 2_048',
        'MaximumSupportedDirectoryEntries = 20_000',
        'DefaultMaximumTraversalDepth = 64',
        'MaximumSupportedTraversalDepth = 256',
        'DefaultMaximumFilesPerOperation = 20_000',
        'MaximumSupportedFilesPerOperation = 100_000',
        'DefaultMaximumSearchResults = 500',
        'MaximumSupportedSearchResults = 5_000',
        'DefaultMaximumSearchMatchesPerFile = 100',
        'MaximumSupportedSearchMatchesPerFile = 5_000',
        'DefaultMaximumTextFileBytes = 8L * 1024 * 1024',
        'MaximumSupportedTextFileBytes = 128L * 1024 * 1024',
        'DefaultMaximumSearchFileBytes = 4L * 1024 * 1024',
        'MaximumSupportedSearchFileBytes = 64L * 1024 * 1024',
        'DefaultMaximumPreviewCharacters = 240',
        'DefaultBinaryProbeBytes = 4_096',
        'new ReadOnlySet<string>(exclusions)',
        'StringComparer.OrdinalIgnoreCase',
        'ShouldExcludeDirectory(string directoryName)',
        'MaximumExcludedDirectoryNames = 256'
    )) { Assert-ContainsLiteral $PolicyText $literal 'WorkspaceScalePolicy.cs' }

    foreach ($generatedDirectory in @('".git"', '"node_modules"', '"bin"', '"obj"', '"Library"', '"Temp"', '"Logs"')) {
        Assert-ContainsLiteral $PolicyText $generatedDirectory 'WorkspaceScalePolicy.cs exclusions'
    }

    foreach ($literal in @(
        'ProjectFileTraversalRestriction TraversalRestriction',
        'ExcludedDirectory',
        'MaximumDepth',
        'int DirectoryDepth = 0',
        'int ExcludedDirectories = 0',
        'int DepthLimitedDirectories = 0'
    )) { Assert-ContainsLiteral $ExplorerContractText $literal 'IProjectFileExplorerService.cs' }

    foreach ($literal in @(
        'FileSystemProjectFileExplorerService(WorkspaceScalePolicy policy)',
        '_maximumEntriesPerDirectory = policy.MaximumDirectoryEntries',
        '.Take(_maximumEntriesPerDirectory + 1)',
        '_policy.MaximumTraversalDepth',
        '_policy.ShouldExcludeDirectory(name)',
        'EnsureNoReparseTraversal(normalizedRootPath, normalizedDirectoryPath)',
        'ProjectFileTraversalRestriction.ExcludedDirectory',
        'ProjectFileTraversalRestriction.MaximumDepth',
        'Directory.EnumerateFileSystemEntries(normalizedDirectoryPath)'
    )) { Assert-ContainsLiteral $ExplorerServiceText $literal 'FileSystemProjectFileExplorerService.cs' }

    foreach ($literal in @(
        'Task<ProjectFileInspection> InspectAsync',
        'public enum ProjectFileContentKind',
        'Text,',
        'Binary,',
        'TooLarge,',
        'public sealed record ProjectFileInspection'
    )) { Assert-ContainsLiteral $FileContractText $literal 'IProjectFileService.cs' }

    foreach ($literal in @(
        'FileSystemProjectFileService(WorkspaceScalePolicy policy)',
        '_policy.BinaryProbeBytes',
        '_policy.MaximumPreviewCharacters',
        'ProjectFileContentKind.TooLarge',
        'ReadBinaryProbeAsync',
        'ReadPreviewAsync',
        'ArrayPool<byte>.Shared.Rent',
        'FileShare.ReadWrite | FileShare.Delete',
        'ValidatePaths(projectRootPath, filePath, requireExistingFile: true)',
        'fileInfo.Length > int.MaxValue',
        'stream.Length > _maximumFileBytes || stream.Length > int.MaxValue'
    )) { Assert-ContainsLiteral $FileServiceText $literal 'FileSystemProjectFileService.cs' }

    foreach ($literal in @(
        'MaximumResults = WorkspaceScalePolicy.DefaultMaximumSearchResults',
        'MaximumFiles = WorkspaceScalePolicy.DefaultMaximumFilesPerOperation',
        'MaximumFileBytes = WorkspaceScalePolicy.DefaultMaximumSearchFileBytes',
        'MaximumMatchesPerFile = WorkspaceScalePolicy.DefaultMaximumSearchMatchesPerFile',
        'MaximumTraversalDepth = WorkspaceScalePolicy.DefaultMaximumTraversalDepth',
        'MaximumPreviewCharacters = WorkspaceScalePolicy.DefaultMaximumPreviewCharacters',
        'BinaryProbeBytes = WorkspaceScalePolicy.DefaultBinaryProbeBytes'
    )) { Assert-ContainsLiteral $SearchContractText $literal 'IProjectSearchService.cs' }

    foreach ($literal in @(
        'FileSystemProjectSearchService(WorkspaceScalePolicy policy)',
        '_policy.ShouldExcludeDirectory(directoryName)',
        'pendingDirectory.Depth >= request.MaximumTraversalDepth',
        'EnumerateDirectoryEntries(',
        'entries.Count > _policy.MaximumDirectoryEntries',
        'request.MaximumMatchesPerFile',
        '_policy.MaximumSearchResults',
        '_policy.MaximumFilesPerOperation',
        '_policy.MaximumSearchFileBytes',
        '_policy.MaximumSearchMatchesPerFile',
        '_policy.MaximumTraversalDepth',
        '_policy.MaximumPreviewCharacters',
        '_policy.BinaryProbeBytes',
        'ProjectSearchLimitReason.MatchesPerFile',
        'ProjectSearchLimitReason.TraversalDepth',
        'ProjectSearchLimitReason.DirectoryEntries',
        'RegexMatchTimeoutException',
        'cancellationToken.ThrowIfCancellationRequested()'
    )) { Assert-ContainsLiteral $SearchServiceText $literal 'FileSystemProjectSearchService.cs' }

    foreach ($literal in @(
        'DefaultsAreFiniteAndContainCanonicalGeneratedDirectories',
        'CustomLimitsAndExclusionsAreImmutableAndCaseInsensitive',
        'RejectsEveryInvalidBoundAndUnsafeExclusion'
    )) { Assert-ContainsLiteral $PolicyTestsText $literal 'WorkspaceScalePolicyTests.cs' }

    foreach ($literal in @(
        'GeneratedAndDepthLimitedDirectoriesAreVisibleButCannotBeTraversed',
        'Assert.Equal(1, sourceListing.DepthLimitedDirectories)'
    )) { Assert-ContainsLiteral $ExplorerTestsText $literal 'ProjectFileExplorerServiceTests.cs' }

    foreach ($literal in @(
        'InspectionClassifiesEmptyBinaryBoundaryAndLargeFilesWithoutMutation',
        'InspectionSupportsBomUnicodeArabicAndRecoversAfterCancellationAndFailure'
    )) { Assert-ContainsLiteral $FileTestsText $literal 'ProjectFileServiceTests.cs' }

    foreach ($literal in @(
        'SharedPolicyBoundsTraversalPerFileMatchesAndReportsMetadataWithoutMutation',
        'SearchRejectsRequestsAboveInjectedWorkspacePolicy',
        'TraversalDepthAndPerFileCapsProduceTypedPartialResults',
        'WideDirectoryMaterializationIsBoundedOrderedAndStable',
        'Assert.Equal(2, result.MaximumMatchesPerFile)',
        'Assert.Equal(1, result.MaximumTraversalDepth)',
        'ProjectSearchLimitReason.DirectoryEntries'
    )) { Assert-ContainsLiteral $SearchTestsText $literal 'ProjectSearchServiceTests.cs' }

    foreach ($literal in @(
        '# Large Workspace Safeguards',
        '2,048 materialized entries',
        '64 directory levels',
        '20,000 files examined',
        '100 matches from one file',
        '8 MiB for normal text-file materialization',
        '4 MiB for a content-search candidate',
        'Regular-expression evaluation remains time-bounded',
        'No safeguard operation mutates searched, inspected, or enumerated project files'
    )) { Assert-ContainsLiteral $DocText $literal 'LARGE_WORKSPACE_SAFEGUARDS.md' }

    foreach ($text in @(
        $PolicyText,
        $ExplorerContractText,
        $ExplorerServiceText,
        $FileContractText,
        $FileServiceText,
        $SearchContractText,
        $SearchServiceText,
        $PolicyTestsText,
        $ExplorerTestsText,
        $FileTestsText,
        $SearchTestsText,
        $DocText
    )) {
        foreach ($marker in @('TODO', 'FIXME', 'Coming soon')) {
            if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P06-008 contains forbidden unfinished-work marker '$marker'."
            }
        }
    }

    foreach ($serviceText in @($ExplorerServiceText, $FileServiceText, $SearchServiceText)) {
        foreach ($forbidden in @(
            'SearchOption.AllDirectories',
            'File.WriteAllText',
            'File.WriteAllBytes',
            'Directory.Delete',
            'Process.Start',
            'ProcessStartInfo'
        )) {
            Assert-DoesNotContainLiteral $serviceText $forbidden 'P06-008 production safeguard service'
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
    throw "Negative P06-008 safeguard fixture was not rejected: $Label"
}

$paths = @{
    Policy = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\WorkspaceScalePolicy.cs'
    ExplorerContract = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\IProjectFileExplorerService.cs'
    ExplorerService = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\FileSystemProjectFileExplorerService.cs'
    FileContract = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\IProjectFileService.cs'
    FileService = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\FileSystemProjectFileService.cs'
    SearchContract = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\IProjectSearchService.cs'
    SearchService = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\FileSystemProjectSearchService.cs'
    PolicyTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\WorkspaceScalePolicyTests.cs'
    ExplorerTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectFileExplorerServiceTests.cs'
    FileTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectFileServiceTests.cs'
    SearchTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectSearchServiceTests.cs'
    StressTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\LargeWorkspaceSafeguardsTests.cs'
    Docs = Join-Path $RepositoryRoot 'docs\projects\LARGE_WORKSPACE_SAFEGUARDS.md'
}

foreach ($path in $paths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P06-008 path is missing: $path"
    }
}

$text = @{}
foreach ($key in $paths.Keys) {
    $text[$key] = Get-Content -LiteralPath $paths[$key] -Raw
}

Assert-LargeWorkspaceContract `
    $text.Policy `
    $text.ExplorerContract `
    $text.ExplorerService `
    $text.FileContract `
    $text.FileService `
    $text.SearchContract `
    $text.SearchService `
    $text.PolicyTests `
    $text.ExplorerTests `
    $text.FileTests `
    $text.SearchTests `
    $text.Docs

foreach ($literal in @(
    'public sealed class LargeWorkspaceSafeguardsTests',
    'SyntheticLargeTreeIsBoundedResponsiveAndStableAfterCancellation',
    'LockedFileIsSkippedAndOperationRecoversWithoutMutation',
    'ReparsePointDoesNotEscapeProjectRootWhenSupported',
    'cancellation.Cancel();',
    'Assert.True(cancellation.IsCancellationRequested);',
    'FileShare.None',
    'Directory.CreateSymbolicLink',
    'ProjectSearchLimitReason.Files',
    'Assert.Equal(sentinelWriteTime, File.GetLastWriteTimeUtc(sentinelPath))'
)) { Assert-ContainsLiteral $text.StressTests $literal 'LargeWorkspaceSafeguardsTests.cs' }
Assert-DoesNotContainLiteral $text.StressTests 'cancellation.CancelAfter(' 'LargeWorkspaceSafeguardsTests.cs timing-dependent cancellation'
Write-Host 'Static P06-008 large workspace safeguard validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-LargeWorkspaceContract `
            ($text.Policy.Replace('DefaultMaximumTraversalDepth = 64', 'DefaultMaximumTraversalDepth = int.MaxValue')) `
            $text.ExplorerContract $text.ExplorerService $text.FileContract $text.FileService $text.SearchContract $text.SearchService `
            $text.PolicyTests $text.ExplorerTests $text.FileTests $text.SearchTests $text.Docs
    } 'finite traversal depth default removed'

    Assert-ContractRejects {
        Assert-LargeWorkspaceContract `
            $text.Policy $text.ExplorerContract `
            ($text.ExplorerService.Replace('.Take(_maximumEntriesPerDirectory + 1)', '.Skip(0)')) `
            $text.FileContract $text.FileService $text.SearchContract $text.SearchService `
            $text.PolicyTests $text.ExplorerTests $text.FileTests $text.SearchTests $text.Docs
    } 'explorer materialization entry bound removed'

    Assert-ContractRejects {
        Assert-LargeWorkspaceContract `
            $text.Policy $text.ExplorerContract ($text.ExplorerService.Replace('_policy.ShouldExcludeDirectory(name)', 'false')) `
            $text.FileContract $text.FileService $text.SearchContract $text.SearchService `
            $text.PolicyTests $text.ExplorerTests $text.FileTests $text.SearchTests $text.Docs
    } 'explorer generated-directory exclusion removed'

    Assert-ContractRejects {
        Assert-LargeWorkspaceContract `
            $text.Policy $text.ExplorerContract $text.ExplorerService $text.FileContract `
            ($text.FileService.Replace('fileInfo.Length > int.MaxValue', 'false')) `
            $text.SearchContract $text.SearchService $text.PolicyTests $text.ExplorerTests $text.FileTests $text.SearchTests $text.Docs
    } 'file materialization overflow guard removed'

    Assert-ContractRejects {
        Assert-LargeWorkspaceContract `
            $text.Policy $text.ExplorerContract $text.ExplorerService $text.FileContract `
            ($text.FileService.Replace('_policy.BinaryProbeBytes', '1')) $text.SearchContract $text.SearchService `
            $text.PolicyTests $text.ExplorerTests $text.FileTests $text.SearchTests $text.Docs
    } 'file inspection binary probe policy removed'

    Assert-ContractRejects {
        Assert-LargeWorkspaceContract `
            $text.Policy $text.ExplorerContract $text.ExplorerService $text.FileContract $text.FileService $text.SearchContract `
            ($text.SearchService.Replace('request.MaximumMatchesPerFile', 'int.MaxValue')) `
            $text.PolicyTests $text.ExplorerTests $text.FileTests $text.SearchTests $text.Docs
    } 'search per-file match budget removed'

    Assert-ContractRejects {
        Assert-LargeWorkspaceContract `
            $text.Policy $text.ExplorerContract $text.ExplorerService $text.FileContract $text.FileService $text.SearchContract `
            ($text.SearchService.Replace('pendingDirectory.Depth >= request.MaximumTraversalDepth', 'false')) `
            $text.PolicyTests $text.ExplorerTests $text.FileTests $text.SearchTests $text.Docs
    } 'search traversal depth limit removed'

    Assert-ContractRejects {
        Assert-LargeWorkspaceContract `
            $text.Policy $text.ExplorerContract $text.ExplorerService $text.FileContract $text.FileService $text.SearchContract `
            ($text.SearchService.Replace('entries.Count > _policy.MaximumDirectoryEntries', 'false')) `
            $text.PolicyTests $text.ExplorerTests $text.FileTests $text.SearchTests $text.Docs
    } 'search directory materialization bound removed'

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
    & dotnet test $unitProject -c Release --no-restore --no-build --nologo --filter 'FullyQualifiedName~WorkspaceScalePolicyTests'
    if ($LASTEXITCODE -ne 0) { throw 'P06-008 workspace policy unit tests failed.' }

    $integrationProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'
    $integrationFilter = 'FullyQualifiedName~ProjectFileExplorerServiceTests|FullyQualifiedName~ProjectFileServiceTests|FullyQualifiedName~ProjectSearchServiceTests|FullyQualifiedName~LargeWorkspaceSafeguardsTests'
    & dotnet test $integrationProject -c Release --no-restore --no-build --nologo --filter $integrationFilter
    if ($LASTEXITCODE -ne 0) { throw 'P06-008 project file/explorer/search integration tests failed.' }

    Write-Host 'Executable P06-008 large workspace safeguard validation: PASS.'
}
