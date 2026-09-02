[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RequireDotNet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Equal {
    param(
        [string]$Actual,
        [string]$Expected,
        [string]$Label
    )

    if ($Actual -ne $Expected) {
        throw "$Label expected '$Expected' but found '$Actual'."
    }
}

function Get-CentralProperty {
    param(
        [xml]$Document,
        [string]$Name,
        [string]$Condition = ''
    )

    $matches = @()
    foreach ($group in @($Document.Project.PropertyGroup)) {
        $conditionAttribute = $group.Attributes['Condition']
        $groupCondition = if ($null -eq $conditionAttribute) { '' } else { [string]$conditionAttribute.Value }

        if ($Condition -and $groupCondition -ne $Condition) {
            continue
        }
        if (-not $Condition -and $groupCondition) {
            continue
        }

        foreach ($child in @($group.ChildNodes)) {
            if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.Name -eq $Name) {
                $matches += [string]$child.InnerText
            }
        }
    }

    if ($matches.Count -ne 1) {
        throw "Expected exactly one central '$Name' property for condition '$Condition'; found $($matches.Count)."
    }

    return $matches[0]
}

function Invoke-DotNet {
    param(
        [string[]]$Arguments,
        [string]$WorkingDirectory
    )

    Push-Location $WorkingDirectory
    try {
        $output = (& dotnet @Arguments 2>&1 | Out-String)
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
        Command = "dotnet $($Arguments -join ' ')"
    }
}

function Assert-Succeeded {
    param($Result)

    if ($Result.ExitCode -ne 0) {
        throw "Command failed: $($Result.Command)`n$($Result.Output)"
    }
}

function Assert-FailedWith {
    param(
        $Result,
        [string]$Diagnostic
    )

    if ($Result.ExitCode -eq 0) {
        throw "Expected command to fail with $Diagnostic but it succeeded: $($Result.Command)"
    }

    if ($Result.Output -notmatch [regex]::Escape($Diagnostic)) {
        throw "Command failed as expected but did not report ${Diagnostic}: $($Result.Command)`n$($Result.Output)"
    }
}

$propsPath = Join-Path $RepositoryRoot 'Directory.Build.props'
$editorConfigPath = Join-Path $RepositoryRoot '.editorconfig'
$solutionPath = Join-Path $RepositoryRoot 'FCCCodeDesktop.sln'

foreach ($requiredPath in @($propsPath, $editorConfigPath, $solutionPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required quality-policy path is missing: $requiredPath"
    }
}

[xml]$props = Get-Content -LiteralPath $propsPath -Raw
Assert-Equal (Get-CentralProperty $props 'Nullable') 'enable' 'Nullable'
Assert-Equal (Get-CentralProperty $props 'ImplicitUsings') 'enable' 'ImplicitUsings'
Assert-Equal (Get-CentralProperty $props 'LangVersion') '14.0' 'LangVersion'
Assert-Equal (Get-CentralProperty $props 'EnableNETAnalyzers') 'true' 'EnableNETAnalyzers'
Assert-Equal (Get-CentralProperty $props 'AnalysisLevel') '10.0-recommended' 'AnalysisLevel'
Assert-Equal (Get-CentralProperty $props 'EnforceCodeStyleInBuild') 'true' 'EnforceCodeStyleInBuild'
Assert-Equal (Get-CentralProperty $props 'Deterministic') 'true' 'Deterministic'

$releaseCondition = "'`$(Configuration)' == 'Release'"
Assert-Equal (Get-CentralProperty $props 'TreatWarningsAsErrors' $releaseCondition) 'true' 'Release TreatWarningsAsErrors'
Assert-Equal (Get-CentralProperty $props 'CodeAnalysisTreatWarningsAsErrors' $releaseCondition) 'true' 'Release CodeAnalysisTreatWarningsAsErrors'

$editorConfig = Get-Content -LiteralPath $editorConfigPath -Raw
$requiredEditorConfigEntries = @(
    'root = true',
    'csharp_style_namespace_declarations = file_scoped:warning',
    'csharp_prefer_braces = true:warning',
    'dotnet_diagnostic.IDE0055.severity = warning',
    'dotnet_diagnostic.CA1822.severity = warning',
    'dotnet_naming_rule.interfaces_must_start_with_i.severity = warning'
)

foreach ($entry in $requiredEditorConfigEntries) {
    if (-not $editorConfig.Contains($entry)) {
        throw "Required .editorconfig entry is missing: $entry"
    }
}

$forbiddenProjectProperties = @(
    'Nullable',
    'ImplicitUsings',
    'LangVersion',
    'EnableNETAnalyzers',
    'AnalysisLevel',
    'EnforceCodeStyleInBuild',
    'TreatWarningsAsErrors',
    'CodeAnalysisTreatWarningsAsErrors',
    'NoWarn',
    'WarningsNotAsErrors'
)

$projectFiles = @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'src') -Recurse -Filter '*.csproj' -File)
if ($projectFiles.Count -eq 0) {
    throw 'No production project files were found under src/.'
}

foreach ($projectFile in $projectFiles) {
    [xml]$project = Get-Content -LiteralPath $projectFile.FullName -Raw
    foreach ($property in @($project.SelectNodes('//PropertyGroup/*'))) {
        if ($forbiddenProjectProperties -contains $property.Name) {
            throw "Project-local override '$($property.Name)' is not allowed in $($projectFile.FullName)."
        }
    }

    foreach ($packageReference in @($project.SelectNodes('//PackageReference'))) {
        $include = [string]$packageReference.Include
        if ($include -match '(?i)(CodeAnalysis|Analyzer|StyleCop)') {
            throw "Analyzer package '$include' is not allowed for P01-002; use the .NET 10 SDK analyzers."
        }
    }
}

Write-Host "Static quality-policy validation: PASS ($($projectFiles.Count) production projects)."

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    if ($RequireDotNet) {
        throw 'dotnet was required but is not available on PATH.'
    }

    Write-Host 'dotnet is unavailable; executable validation was skipped.'
    exit 0
}

$restore = Invoke-DotNet @('restore', $solutionPath, '--nologo') $RepositoryRoot
Assert-Succeeded $restore

$format = Invoke-DotNet @('format', $solutionPath, '--verify-no-changes', '--no-restore', '--verbosity', 'minimal') $RepositoryRoot
Assert-Succeeded $format

$releaseBuild = Invoke-DotNet @('build', $solutionPath, '-c', 'Release', '--no-restore', '--nologo') $RepositoryRoot
Assert-Succeeded $releaseBuild

$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("fccd-quality-policy-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null

try {
    Copy-Item -LiteralPath $propsPath -Destination (Join-Path $fixtureRoot 'Directory.Build.props')
    Copy-Item -LiteralPath $editorConfigPath -Destination (Join-Path $fixtureRoot '.editorconfig')

    $fixtureProject = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>QualityFixture</RootNamespace>
    <AssemblyName>QualityFixture</AssemblyName>
  </PropertyGroup>
</Project>
'@
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'QualityFixture.csproj') -Value $fixtureProject -Encoding utf8NoBOM

    $goodSource = @'
namespace QualityFixture;

public sealed class QualityFixture
{
    public static int Value => 42;
}
'@
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'QualityFixture.cs') -Value $goodSource -Encoding utf8NoBOM

    $fixtureBaseline = Invoke-DotNet @('build', '.\QualityFixture.csproj', '-c', 'Release', '--nologo') $fixtureRoot
    Assert-Succeeded $fixtureBaseline

    $nullableSource = @'
namespace QualityFixture;

public sealed class QualityFixture
{
    public string Name { get; set; }
}
'@
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'QualityFixture.cs') -Value $nullableSource -Encoding utf8NoBOM
    $nullableFailure = Invoke-DotNet @('build', '.\QualityFixture.csproj', '-c', 'Release', '--no-restore', '--nologo') $fixtureRoot
    Assert-FailedWith $nullableFailure 'CS8618'

    $analyzerSource = @'
namespace QualityFixture;

public sealed class QualityFixture
{
    public int Value() => 42;
}
'@
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'QualityFixture.cs') -Value $analyzerSource -Encoding utf8NoBOM
    $analyzerFailure = Invoke-DotNet @('build', '.\QualityFixture.csproj', '-c', 'Release', '--no-restore', '--nologo') $fixtureRoot
    Assert-FailedWith $analyzerFailure 'CA1822'

    $styleSource = @'
namespace QualityFixture;
public sealed class QualityFixture{public static int Value=>42;}
'@
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'QualityFixture.cs') -Value $styleSource -Encoding utf8NoBOM
    $styleFailure = Invoke-DotNet @('build', '.\QualityFixture.csproj', '-c', 'Release', '--no-restore', '--nologo') $fixtureRoot
    Assert-FailedWith $styleFailure 'IDE0055'

    Set-Content -LiteralPath (Join-Path $fixtureRoot 'QualityFixture.cs') -Value $goodSource -Encoding utf8NoBOM
    $recoveryBuild = Invoke-DotNet @('build', '.\QualityFixture.csproj', '-c', 'Release', '--no-restore', '--nologo') $fixtureRoot
    Assert-Succeeded $recoveryBuild
}
finally {
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'Executable quality-policy validation: PASS.'
Write-Host 'Negative fixtures verified nullable, analyzer, and style enforcement; recovery build PASS.'
