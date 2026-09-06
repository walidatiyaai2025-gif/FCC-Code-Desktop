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

function Assert-SafeFileServiceContract {
    param(
        [string]$ContractText,
        [string]$ServiceText,
        [string]$TestText,
        [string]$DocText
    )

    foreach ($literal in @(
        'public interface IProjectFileService',
        'Task<ProjectTextFileSnapshot> ReadTextAsync',
        'Task<ProjectFileWriteResult> WriteTextAsync',
        'public enum ProjectTextEncoding',
        'public enum ProjectNewLineStyle',
        'public sealed record ProjectFileVersion',
        'public sealed record ProjectTextFileSnapshot',
        'public sealed record ProjectTextFileWriteRequest',
        'public sealed class ProjectFileConflictException'
    )) {
        Assert-ContainsLiteral $ContractText $literal 'IProjectFileService.cs'
    }

    foreach ($literal in @(
        'public sealed class FileSystemProjectFileService',
        'DefaultMaximumFileBytes = 8 * 1024 * 1024',
        'MaximumSupportedFileBytes = 128 * 1024 * 1024',
        'ValidatePaths(projectRootPath, filePath, requireExistingFile: true)',
        'ValidatePaths(request.ProjectRootPath, request.FilePath, requireExistingFile: false)',
        'EnsurePathInsideProject(normalizedRootPath, normalizedFilePath)',
        'EnsureNoReparseTraversal(normalizedRootPath, directoryPath)',
        'FileAttributes.ReparsePoint',
        'Refusing to overwrite an existing project file without the version observed by the caller.',
        'await EnsureExpectedVersionAsync(',
        'FileMode.CreateNew',
        'FileOptions.Asynchronous | FileOptions.WriteThrough',
        'File.Move(temporaryPath, paths.FullPath, overwrite: targetExisted)',
        '.fccd-',
        'SHA256.HashData(bytes)',
        'StrictUtf8.GetString(bytes)',
        'DecoderFallbackException',
        'ProjectTextEncoding.Utf16BigEndian',
        'DetectNewLineStyle(decoded.Text)',
        'cancellationToken.ThrowIfCancellationRequested()'
    )) {
        Assert-ContainsLiteral $ServiceText $literal 'FileSystemProjectFileService.cs'
    }

    foreach ($forbidden in @(
        'SearchOption.AllDirectories',
        'Process.Start',
        'ProcessStartInfo',
        'cmd.exe',
        'powershell.exe',
        'File.WriteAllText(',
        'File.WriteAllBytes(',
        'Directory.Delete(',
        'Directory.CreateDirectory('
    )) {
        if ($ServiceText.Contains($forbidden, [StringComparison]::Ordinal)) {
            throw "Safe file service contains forbidden recursive, shell, or unscoped mutation text: $forbidden"
        }
    }

    foreach ($literal in @(
        'ReadsUtf8BomMetadataAndMixedNewLinesWithoutChangingSource',
        'AtomicSavePreservesRequestedEncodingAndRequiresObservedVersion',
        'RejectsStaleVersionWithoutOverwritingExternalWork',
        'RejectsOutsideRootDirectoryTargetsInvalidEncodingAndOversizedFiles',
        'RelativePathsCancellationAndConfigurationFailExplicitly',
        'مشروع safe files',
        'external-owner-work',
        'ProjectFileConflictException'
    )) {
        Assert-ContainsLiteral $TestText $literal 'ProjectFileServiceTests.cs'
    }

    foreach ($literal in @(
        'The active project root is the trust anchor.',
        'Nested reparse-point directories are not traversed',
        'limits materialized file content to `8 MiB` by default',
        'Invalid UTF-8 or non-BOM legacy encodings are rejected instead of guessed.',
        'Existing files are never overwritten without the caller supplying that observed version.',
        'optimistic conflict detection',
        'unique `.fccd-*.tmp` file in the target directory',
        'P06-004 introduces no FCC/provider/manual/owner `REAL_TARGET` requirement'
    )) {
        Assert-ContainsLiteral $DocText $literal 'SAFE_FILE_SERVICE.md'
    }

    foreach ($text in @($ContractText, $ServiceText, $TestText, $DocText)) {
        foreach ($marker in @('TODO', 'FIXME', 'Coming soon')) {
            if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P06-004 contains forbidden unfinished-work marker '$marker'."
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

    throw "Negative P06-004 safe-file fixture was not rejected: $Label"
}

$paths = @{
    Contract = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Application\Projects\IProjectFileService.cs'
    Service = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Files\FileSystemProjectFileService.cs'
    Tests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\ProjectFileServiceTests.cs'
    Docs = Join-Path $RepositoryRoot 'docs\projects\SAFE_FILE_SERVICE.md'
}

foreach ($path in $paths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P06-004 path is missing: $path"
    }
}

$contractText = Get-Content -LiteralPath $paths.Contract -Raw
$serviceText = Get-Content -LiteralPath $paths.Service -Raw
$testText = Get-Content -LiteralPath $paths.Tests -Raw
$docText = Get-Content -LiteralPath $paths.Docs -Raw

Assert-SafeFileServiceContract $contractText $serviceText $testText $docText
Write-Host 'Static P06-004 safe file service validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-SafeFileServiceContract $contractText ($serviceText.Replace('EnsurePathInsideProject(normalizedRootPath, normalizedFilePath)', 'RemovedProjectBoundaryCheck(normalizedRootPath, normalizedFilePath)')) $testText $docText
    } 'project-root containment guard removed'
    Assert-ContractRejects {
        Assert-SafeFileServiceContract $contractText ($serviceText.Replace('EnsureNoReparseTraversal(normalizedRootPath, directoryPath)', 'RemovedReparseTraversalGuard(normalizedRootPath, directoryPath)')) $testText $docText
    } 'nested reparse traversal guard removed'
    Assert-ContractRejects {
        Assert-SafeFileServiceContract $contractText ($serviceText.Replace('Refusing to overwrite an existing project file without the version observed by the caller.', 'Overwrite allowed without caller version.')) $testText $docText
    } 'existing-file observed-version requirement removed'
    Assert-ContractRejects {
        Assert-SafeFileServiceContract $contractText ($serviceText.Replace('FileOptions.Asynchronous | FileOptions.WriteThrough', 'FileOptions.Asynchronous')) $testText $docText
    } 'write-through temporary commit removed'
    Assert-ContractRejects {
        Assert-SafeFileServiceContract $contractText ($serviceText.Replace('File.Move(temporaryPath, paths.FullPath, overwrite: targetExisted)', 'File.Copy(temporaryPath, paths.FullPath, overwrite: targetExisted)')) $testText $docText
    } 'same-directory move commit removed'
    Assert-ContractRejects {
        Assert-SafeFileServiceContract $contractText ($serviceText.Replace('SHA256.HashData(bytes)', 'Array.Empty<byte>()')) $testText $docText
    } 'content hash version token removed'
    Assert-ContractRejects {
        Assert-SafeFileServiceContract $contractText ($serviceText.Replace('DecoderFallbackException', 'Exception')) $testText $docText
    } 'strict decoder failure contract removed'
    Assert-ContractRejects {
        Assert-SafeFileServiceContract $contractText ($serviceText.Replace('DefaultMaximumFileBytes = 8 * 1024 * 1024', 'DefaultMaximumFileBytes = int.MaxValue')) $testText $docText
    } 'default file materialization bound removed'
    Write-Host 'P06-004 negative fixtures: PASS.'
}

if ($RequireRuntime) {
    if (-not $IsWindows) {
        throw 'Executable P06-004 safe file service validation requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for executable P06-004 safe file service validation.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "P06-004 validation requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $testProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj'
    & dotnet test $testProject -c Release --no-restore --no-build --nologo --filter 'FullyQualifiedName~ProjectFileServiceTests'
    if ($LASTEXITCODE -ne 0) {
        throw 'P06-004 safe file service integration tests failed.'
    }

    Write-Host 'Executable P06-004 safe file service validation: PASS.'
}
