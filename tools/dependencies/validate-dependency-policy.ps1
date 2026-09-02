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

function Get-AttributeValue {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$Name
    )

    $attribute = $Node.Attributes[$Name]
    if ($null -eq $attribute) {
        return ''
    }

    return [string]$attribute.Value
}

function Get-ChildValue {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$Name
    )

    foreach ($child in @($Node.ChildNodes)) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.Name -eq $Name) {
            return [string]$child.InnerText
        }
    }

    return ''
}

function Get-SinglePropertyValue {
    param(
        [xml]$Document,
        [string]$Name
    )

    $nodes = @($Document.SelectNodes("/Project/PropertyGroup/$Name"))
    if ($nodes.Count -ne 1) {
        throw "Expected exactly one '$Name' property; found $($nodes.Count)."
    }

    return [string]$nodes[0].InnerText
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

function Assert-Failed {
    param($Result)

    if ($Result.ExitCode -eq 0) {
        throw "Expected command to fail but it succeeded: $($Result.Command)"
    }
}

function Assert-FailedWith {
    param(
        $Result,
        [string]$Diagnostic
    )

    Assert-Failed $Result
    if ($Result.Output -notmatch [regex]::Escape($Diagnostic)) {
        throw "Command failed but did not report ${Diagnostic}: $($Result.Command)`n$($Result.Output)"
    }
}

function Get-LockResolvedVersion {
    param(
        [string]$LockPath,
        [string]$PackageId
    )

    $lock = Get-Content -LiteralPath $LockPath -Raw | ConvertFrom-Json -Depth 20
    foreach ($framework in @($lock.dependencies.PSObject.Properties)) {
        $packageProperty = $framework.Value.PSObject.Properties[$PackageId]
        if ($null -ne $packageProperty) {
            return [string]$packageProperty.Value.resolved
        }
    }

    throw "Package '$PackageId' was not found in lock file '$LockPath'."
}

$globalJsonPath = Join-Path $RepositoryRoot 'global.json'
$packagesPropsPath = Join-Path $RepositoryRoot 'Directory.Packages.props'
$buildPropsPath = Join-Path $RepositoryRoot 'Directory.Build.props'
$solutionPath = Join-Path $RepositoryRoot 'FCCCodeDesktop.sln'

foreach ($requiredPath in @($globalJsonPath, $packagesPropsPath, $buildPropsPath, $solutionPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required dependency-policy path is missing: $requiredPath"
    }
}

$globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json -Depth 10
Assert-Equal ([string]$globalJson.sdk.version) '10.0.400' 'Pinned SDK version'
Assert-Equal ([string]$globalJson.sdk.rollForward) 'disable' 'SDK roll-forward policy'
if ([bool]$globalJson.sdk.allowPrerelease) {
    throw 'SDK prerelease selection must remain disabled.'
}

[xml]$packagesProps = Get-Content -LiteralPath $packagesPropsPath -Raw
Assert-Equal (Get-SinglePropertyValue $packagesProps 'ManagePackageVersionsCentrally') 'true' 'ManagePackageVersionsCentrally'
Assert-Equal (Get-SinglePropertyValue $packagesProps 'CentralPackageVersionOverrideEnabled') 'false' 'CentralPackageVersionOverrideEnabled'

[xml]$buildProps = Get-Content -LiteralPath $buildPropsPath -Raw
Assert-Equal (Get-SinglePropertyValue $buildProps 'RestorePackagesWithLockFile') 'true' 'RestorePackagesWithLockFile'
$lockedNodes = @($buildProps.SelectNodes('/Project/PropertyGroup/RestoreLockedMode'))
if ($lockedNodes.Count -ne 1) {
    throw "Expected exactly one RestoreLockedMode property; found $($lockedNodes.Count)."
}
Assert-Equal ([string]$lockedNodes[0].InnerText) 'true' 'RestoreLockedMode'
$lockedCondition = Get-AttributeValue $lockedNodes[0] 'Condition'
Assert-Equal $lockedCondition "'`$(RestoreLockedMode)' == ''" 'RestoreLockedMode condition'

$centralVersions = @{}
foreach ($packageVersion in @($packagesProps.SelectNodes('//PackageVersion'))) {
    $packageId = Get-AttributeValue $packageVersion 'Include'
    $version = Get-AttributeValue $packageVersion 'Version'
    if (-not $version) {
        $version = Get-ChildValue $packageVersion 'Version'
    }

    if (-not $packageId) {
        throw 'Every central PackageVersion must use an explicit Include package ID.'
    }
    if (-not $version) {
        throw "Central PackageVersion '$packageId' is missing an explicit version."
    }
    if ($version -match '[\*\[\]\(\),]' -or $version.Contains('$(')) {
        throw "Central PackageVersion '$packageId' must use an exact non-floating version; found '$version'."
    }
    if ($centralVersions.ContainsKey($packageId)) {
        throw "Duplicate central PackageVersion entry for '$packageId'."
    }

    $centralVersions[$packageId] = $version
}

$projectFiles = @(Get-ChildItem -LiteralPath $RepositoryRoot -Recurse -Filter '*.csproj' -File | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
})
if ($projectFiles.Count -eq 0) {
    throw 'No project files were found for dependency-policy validation.'
}

$packageReferenceCount = 0
foreach ($projectFile in $projectFiles) {
    [xml]$project = Get-Content -LiteralPath $projectFile.FullName -Raw
    foreach ($packageReference in @($project.SelectNodes('//PackageReference'))) {
        $packageReferenceCount++
        $packageId = Get-AttributeValue $packageReference 'Include'
        if (-not $packageId) {
            $packageId = Get-AttributeValue $packageReference 'Update'
        }
        if (-not $packageId) {
            throw "PackageReference in '$($projectFile.FullName)' must declare Include or Update."
        }

        $localVersion = Get-AttributeValue $packageReference 'Version'
        if (-not $localVersion) {
            $localVersion = Get-ChildValue $packageReference 'Version'
        }
        $versionOverride = Get-AttributeValue $packageReference 'VersionOverride'
        if (-not $versionOverride) {
            $versionOverride = Get-ChildValue $packageReference 'VersionOverride'
        }

        if ($localVersion) {
            throw "PackageReference '$packageId' in '$($projectFile.FullName)' declares project-local Version '$localVersion'; versions belong in Directory.Packages.props."
        }
        if ($versionOverride) {
            throw "PackageReference '$packageId' in '$($projectFile.FullName)' uses forbidden VersionOverride '$versionOverride'."
        }
        if (-not $centralVersions.ContainsKey($packageId)) {
            throw "PackageReference '$packageId' in '$($projectFile.FullName)' has no central PackageVersion entry."
        }
    }

    $lockPath = Join-Path $projectFile.DirectoryName 'packages.lock.json'
    if (-not (Test-Path -LiteralPath $lockPath)) {
        throw "Committed packages.lock.json is missing for '$($projectFile.FullName)'."
    }

    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json -Depth 20
    if ([int]$lock.version -ne 1) {
        throw "Unsupported lock-file format version '$($lock.version)' in '$lockPath'."
    }
    if ($null -eq $lock.dependencies) {
        throw "Lock file '$lockPath' has no dependencies object."
    }
}

Write-Host "Static dependency-policy validation: PASS ($($projectFiles.Count) projects, $packageReferenceCount PackageReference entries)."

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    if ($RequireDotNet) {
        throw 'dotnet was required but is not available on PATH.'
    }

    Write-Host 'dotnet is unavailable; executable dependency validation was skipped.'
    exit 0
}

$versionResult = Invoke-DotNet @('--version') $RepositoryRoot
Assert-Succeeded $versionResult
Assert-Equal $versionResult.Output.Trim() '10.0.400' 'Resolved SDK version'

$lockedRestore = Invoke-DotNet @('restore', $solutionPath, '--locked-mode', '--nologo') $RepositoryRoot
Assert-Succeeded $lockedRestore

$releaseBuild = Invoke-DotNet @('build', $solutionPath, '-c', 'Release', '--no-restore', '--nologo') $RepositoryRoot
Assert-Succeeded $releaseBuild

$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("fccd-dependency-policy-" + [Guid]::NewGuid().ToString('N'))
$packageSourceDir = Join-Path $fixtureRoot 'package source'
$feedDir = Join-Path $fixtureRoot 'local feed'
$consumerDir = Join-Path $fixtureRoot 'consumer مساحة'
New-Item -ItemType Directory -Path $packageSourceDir, $feedDir, $consumerDir -Force | Out-Null

try {
    Copy-Item -LiteralPath $globalJsonPath -Destination (Join-Path $fixtureRoot 'global.json')

    $packageProject = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PackageId>Fccd.DependencyPolicy.Fixture</PackageId>
    <Authors>FCC Code Desktop</Authors>
    <Description>Disposable dependency-lock validation package.</Description>
    <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
  </PropertyGroup>
</Project>
'@
    Set-Content -LiteralPath (Join-Path $packageSourceDir 'Fixture.Dependency.csproj') -Value $packageProject -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $packageSourceDir 'FixtureDependency.cs') -Value "namespace Fccd.DependencyPolicy.Fixture;`n`npublic static class FixtureDependency { public const int Value = 1; }`n" -Encoding utf8NoBOM

    $pack100 = Invoke-DotNet @('pack', '.\Fixture.Dependency.csproj', '-c', 'Release', '-o', $feedDir, '--nologo', '-p:PackageVersion=1.0.0') $packageSourceDir
    Assert-Succeeded $pack100
    $pack101 = Invoke-DotNet @('pack', '.\Fixture.Dependency.csproj', '-c', 'Release', '-o', $feedDir, '--nologo', '-p:PackageVersion=1.0.1') $packageSourceDir
    Assert-Succeeded $pack101

    Copy-Item -LiteralPath $buildPropsPath -Destination (Join-Path $consumerDir 'Directory.Build.props')

    $consumerPackagesProps = @'
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Fccd.DependencyPolicy.Fixture" Version="1.0.0" />
  </ItemGroup>
</Project>
'@
    $consumerPackagesPropsPath = Join-Path $consumerDir 'Directory.Packages.props'
    Set-Content -LiteralPath $consumerPackagesPropsPath -Value $consumerPackagesProps -Encoding utf8NoBOM

    $escapedFeedPath = [System.Security.SecurityElement]::Escape($feedDir)
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="fixture" value="$escapedFeedPath" />
  </packageSources>
</configuration>
"@
    Set-Content -LiteralPath (Join-Path $consumerDir 'NuGet.Config') -Value $nugetConfig -Encoding utf8NoBOM

    $consumerProject = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Fccd.DependencyPolicy.Fixture" />
  </ItemGroup>
</Project>
'@
    $consumerProjectPath = Join-Path $consumerDir 'DependencyConsumer.csproj'
    Set-Content -LiteralPath $consumerProjectPath -Value $consumerProject -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $consumerDir 'Consumer.cs') -Value "namespace DependencyConsumer;`n`npublic static class Consumer { public static int Value => 1; }`n" -Encoding utf8NoBOM

    $missingLockFailure = Invoke-DotNet @('restore', '.\DependencyConsumer.csproj', '--locked-mode', '--nologo') $consumerDir
    Assert-Failed $missingLockFailure

    $generateLock = Invoke-DotNet @('restore', '.\DependencyConsumer.csproj', '-p:RestoreLockedMode=false', '--force-evaluate', '--nologo') $consumerDir
    Assert-Succeeded $generateLock

    $consumerLockPath = Join-Path $consumerDir 'packages.lock.json'
    if (-not (Test-Path -LiteralPath $consumerLockPath)) {
        throw 'Unlocked fixture restore did not generate packages.lock.json.'
    }
    Assert-Equal (Get-LockResolvedVersion $consumerLockPath 'Fccd.DependencyPolicy.Fixture') '1.0.0' 'Fixture baseline locked package version'

    $baselineLockedRestore = Invoke-DotNet @('restore', '.\DependencyConsumer.csproj', '--locked-mode', '--nologo') $consumerDir
    Assert-Succeeded $baselineLockedRestore

    $mutatedPackagesProps = $consumerPackagesProps.Replace('Version="1.0.0"', 'Version="1.0.1"')
    Set-Content -LiteralPath $consumerPackagesPropsPath -Value $mutatedPackagesProps -Encoding utf8NoBOM
    $staleLockFailure = Invoke-DotNet @('restore', '.\DependencyConsumer.csproj', '--locked-mode', '--nologo') $consumerDir
    Assert-Failed $staleLockFailure

    $regenerateLock = Invoke-DotNet @('restore', '.\DependencyConsumer.csproj', '-p:RestoreLockedMode=false', '--force-evaluate', '--nologo') $consumerDir
    Assert-Succeeded $regenerateLock
    Assert-Equal (Get-LockResolvedVersion $consumerLockPath 'Fccd.DependencyPolicy.Fixture') '1.0.1' 'Fixture regenerated locked package version'

    $recoveredLockedRestore = Invoke-DotNet @('restore', '.\DependencyConsumer.csproj', '--locked-mode', '--nologo') $consumerDir
    Assert-Succeeded $recoveredLockedRestore

    $badProject = $consumerProject.Replace('<PackageReference Include="Fccd.DependencyPolicy.Fixture" />', '<PackageReference Include="Fccd.DependencyPolicy.Fixture" Version="1.0.1" />')
    Set-Content -LiteralPath $consumerProjectPath -Value $badProject -Encoding utf8NoBOM
    $localVersionFailure = Invoke-DotNet @('restore', '.\DependencyConsumer.csproj', '-p:RestoreLockedMode=false', '--force-evaluate', '--nologo') $consumerDir
    Assert-FailedWith $localVersionFailure 'NU1008'

    Set-Content -LiteralPath $consumerProjectPath -Value $consumerProject -Encoding utf8NoBOM
    $finalLockedRestore = Invoke-DotNet @('restore', '.\DependencyConsumer.csproj', '--locked-mode', '--nologo') $consumerDir
    Assert-Succeeded $finalLockedRestore

    $fixtureBuild = Invoke-DotNet @('build', '.\DependencyConsumer.csproj', '-c', 'Release', '--no-restore', '--nologo') $consumerDir
    Assert-Succeeded $fixtureBuild
}
finally {
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'Executable dependency-policy validation: PASS.'
Write-Host 'Negative fixtures verified missing/stale lock rejection and project-local version rejection; lock regeneration/recovery PASS.'
