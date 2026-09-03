[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RequireDotNet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ContainsLiteral {
    param(
        [string]$Text,
        [string]$Literal,
        [string]$Label
    )

    if (-not $Text.Contains($Literal)) {
        throw "$Label is missing required text: $Literal"
    }
}

function Invoke-BuildMetadataFixture {
    param(
        [string]$ProjectPath,
        [string]$Label,
        [bool]$ShouldSucceed,
        [hashtable]$Properties
    )

    $arguments = @(
        'msbuild',
        $ProjectPath,
        '-t:ValidateFccBuildMetadata',
        '-nologo'
    )

    foreach ($entry in $Properties.GetEnumerator()) {
        $arguments += "-p:$($entry.Key)=$($entry.Value)"
    }

    $output = (& dotnet @arguments 2>&1 | Out-String)
    $succeeded = $LASTEXITCODE -eq 0

    if ($succeeded -ne $ShouldSucceed) {
        throw "Build-metadata fixture '$Label' returned success=$succeeded unexpectedly.`n$output"
    }
}

$propsPath = Join-Path $RepositoryRoot 'Directory.Build.props'
$targetsPath = Join-Path $RepositoryRoot 'Directory.Build.targets'
$coreSourcePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Core\Build\BuildMetadata.cs'
$unitTestPath = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\BuildMetadataTests.cs'
$unitProjectPath = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'

foreach ($requiredPath in @($propsPath, $targetsPath, $coreSourcePath, $unitTestPath, $unitProjectPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required build-metadata path is missing: $requiredPath"
    }
}

$propsText = Get-Content -LiteralPath $propsPath -Raw
$targetsText = Get-Content -LiteralPath $targetsPath -Raw
$coreSourceText = Get-Content -LiteralPath $coreSourcePath -Raw
$unitProjectText = Get-Content -LiteralPath $unitProjectPath -Raw

foreach ($requiredPropsText in @(
    '<VersionPrefix>1.0.0</VersionPrefix>',
    '<Version>$(VersionPrefix)</Version>',
    '<FccIsPublicRelease Condition=',
    '<FccProductVersion Condition=',
    '<FccGitCommit Condition=',
    '$(GITHUB_SHA)',
    '<InformationalVersion>$(FccProductVersion)+$(FccGitCommit)</InformationalVersion>',
    '<Product>FCC Code Desktop</Product>',
    '<RepositoryUrl>https://github.com/walidatiyaai2025-gif/FCC-Code-Desktop</RepositoryUrl>',
    '<_Parameter1>FccProductVersion</_Parameter1>',
    '<_Parameter1>FccGitCommit</_Parameter1>',
    '<_Parameter1>FccBuildChannel</_Parameter1>',
    '<_Parameter1>FccBuildConfiguration</_Parameter1>',
    '<_Parameter1>FccRepositoryUrl</_Parameter1>'
)) {
    Assert-ContainsLiteral $propsText $requiredPropsText 'Directory.Build.props'
}

foreach ($requiredTargetText in @(
    'Target Name="ValidateFccBuildMetadata"',
    'Public release builds require exact Git source provenance.',
    'Internal builds must be visibly marked with the -dev prerelease suffix.'
)) {
    Assert-ContainsLiteral $targetsText $requiredTargetText 'Directory.Build.targets'
}

foreach ($requiredSourceText in @(
    'public sealed record BuildMetadata',
    'public interface IBuildMetadataService',
    'public sealed class AssemblyBuildMetadataService',
    'public bool HasSourceProvenance'
)) {
    Assert-ContainsLiteral $coreSourceText $requiredSourceText 'Build metadata service'
}

Assert-ContainsLiteral $unitProjectText '..\..\src\FCCCodeDesktop.Core\FCCCodeDesktop.Core.csproj' 'Unit test project'

Write-Host 'Static build-metadata policy validation: PASS.'

if ($RequireDotNet) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw 'dotnet was required but is not available on PATH.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Expected .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("fccd-build-metadata-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null

    try {
        $escapedPropsPath = [Security.SecurityElement]::Escape($propsPath)
        $escapedTargetsPath = [Security.SecurityElement]::Escape($targetsPath)
        $fixtureProjectPath = Join-Path $fixtureRoot 'BuildMetadataFixture.csproj'
        $fixtureProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$escapedPropsPath" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <Import Project="$escapedTargetsPath" />
</Project>
"@
        Set-Content -LiteralPath $fixtureProjectPath -Value $fixtureProject -Encoding utf8NoBOM

        $validCommit = '0123456789abcdef0123456789abcdef01234567'

        Invoke-BuildMetadataFixture $fixtureProjectPath 'development unknown provenance' $true @{
            FccIsPublicRelease = 'false'
            FccBuildChannel = 'Development'
            FccGitCommit = 'unknown'
        }
        Invoke-BuildMetadataFixture $fixtureProjectPath 'malformed commit rejection' $false @{
            FccIsPublicRelease = 'false'
            FccBuildChannel = 'Development'
            FccGitCommit = 'not-a-sha'
        }
        Invoke-BuildMetadataFixture $fixtureProjectPath 'production missing provenance rejection' $false @{
            FccIsPublicRelease = 'true'
            FccBuildChannel = 'Production'
            FccGitCommit = 'unknown'
        }
        Invoke-BuildMetadataFixture $fixtureProjectPath 'production channel mismatch rejection' $false @{
            FccIsPublicRelease = 'true'
            FccBuildChannel = 'Development'
            FccGitCommit = $validCommit
        }
        Invoke-BuildMetadataFixture $fixtureProjectPath 'production exact provenance' $true @{
            FccIsPublicRelease = 'true'
            FccBuildChannel = 'Production'
            FccGitCommit = $validCommit
        }

        Write-Host 'Executable build-metadata policy validation: PASS.'
        Write-Host 'Fixtures verified development fallback, malformed provenance rejection, release provenance/channel enforcement, and valid production metadata.'
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
