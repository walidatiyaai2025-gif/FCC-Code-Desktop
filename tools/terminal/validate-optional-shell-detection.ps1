[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-EmptyFile {
    param([Parameter(Mandatory)][string]$Path)

    $directory = [IO.Path]::GetDirectoryName($Path)
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    [IO.File]::WriteAllBytes($Path, [byte[]]::new(0))
}

function New-DetectionEnvironment {
    param(
        [Parameter(Mandatory)][Type]$EnvironmentType,
        [AllowNull()][string]$ProgramFiles,
        [AllowNull()][string]$ProgramFilesX86,
        [AllowNull()][string]$LocalApplicationData,
        [AllowNull()][string]$WindowsDirectory,
        [AllowNull()][string]$PathValue
    )

    $constructor = $EnvironmentType.GetConstructor(
        [Type[]]@([string], [string], [string], [string], [string]))
    if ($null -eq $constructor) {
        throw 'OptionalShellDetectionEnvironment constructor was not found.'
    }

    $arguments = [object[]]::new(5)
    $arguments[0] = $ProgramFiles
    $arguments[1] = $ProgramFilesX86
    $arguments[2] = $LocalApplicationData
    $arguments[3] = $WindowsDirectory
    $arguments[4] = $PathValue
    return $constructor.Invoke($arguments)
}

function Invoke-Detection {
    param(
        [Parameter(Mandatory)][Type]$DetectorType,
        [Parameter(Mandatory)]$Environment,
        [Threading.CancellationToken]$CancellationToken = [Threading.CancellationToken]::None
    )

    $constructor = $DetectorType.GetConstructor([Type[]]@($Environment.GetType()))
    if ($null -eq $constructor) {
        throw 'WindowsOptionalShellDetector constructor was not found.'
    }

    $detector = $constructor.Invoke([object[]]@($Environment))
    $method = $DetectorType.GetMethod('DetectAsync')
    if ($null -eq $method) {
        throw 'WindowsOptionalShellDetector.DetectAsync was not found.'
    }

    $arguments = [object[]]::new(1)
    $arguments[0] = $CancellationToken
    $valueTask = $method.Invoke($detector, $arguments)
    return $valueTask.AsTask().GetAwaiter().GetResult()
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$solution = Join-Path $repositoryRoot 'FCCCodeDesktop.sln'
$implementationPath = Join-Path $repositoryRoot 'src\FCCCodeDesktop.Terminal\WindowsOptionalShellDetector.cs'
$terminalProject = Join-Path $repositoryRoot 'src\FCCCodeDesktop.Terminal\FCCCodeDesktop.Terminal.csproj'
$unitProject = Join-Path $repositoryRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'

foreach ($path in @($solution, $implementationPath, $terminalProject, $unitProject)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required P08-006 path is missing: $path"
    }
}

$implementation = Get-Content -LiteralPath $implementationPath -Raw
$requiredTokens = @(
    'ProgramFiles',
    'LocalApplicationData',
    'System32',
    'wsl.exe',
    'cmd',
    'git.exe',
    'bash.exe',
    'StringComparer.OrdinalIgnoreCase',
    'File.Exists',
    'CancellationToken'
)
foreach ($token in $requiredTokens) {
    if (-not $implementation.Contains($token, [StringComparison]::Ordinal)) {
        throw "P08-006 implementation is missing required discovery token '$token'."
    }
}

$forbiddenTokens = @(
    'ProcessStartInfo',
    'Process.Start',
    'Microsoft.Win32.Registry',
    'Environment.SetEnvironmentVariable',
    'File.WriteAll',
    'Directory.CreateDirectory',
    'UseShellExecute'
)
foreach ($token in $forbiddenTokens) {
    if ($implementation.Contains($token, [StringComparison]::Ordinal)) {
        throw "P08-006 implementation contains forbidden side-effect token '$token'."
    }
}

Write-Host 'P08-006 static read-only discovery contract: PASS.'

& dotnet restore $solution --locked-mode --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Locked solution restore failed with exit code $LASTEXITCODE."
}

& dotnet build $terminalProject --configuration $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Terminal project build failed with exit code $LASTEXITCODE."
}

& dotnet test $unitProject `
    --configuration $Configuration `
    --no-restore `
    --nologo `
    --filter 'FullyQualifiedName~OptionalShellDetectionTests' `
    --logger 'console;verbosity=minimal'
if ($LASTEXITCODE -ne 0) {
    throw "P08-006 Application contract unit tests failed with exit code $LASTEXITCODE."
}

$terminalOutput = Join-Path $repositoryRoot "src\FCCCodeDesktop.Terminal\bin\$Configuration\net10.0-windows"
$terminalAssemblyPath = Join-Path $terminalOutput 'FCCCodeDesktop.Terminal.dll'
$applicationAssemblyPath = Join-Path $terminalOutput 'FCCCodeDesktop.Application.dll'
foreach ($path in @($terminalAssemblyPath, $applicationAssemblyPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Expected built assembly is missing: $path"
    }
}

$applicationAssembly = [Reflection.Assembly]::LoadFrom($applicationAssemblyPath)
$terminalAssembly = [Reflection.Assembly]::LoadFrom($terminalAssemblyPath)
$environmentType = $terminalAssembly.GetType(
    'FCCCodeDesktop.Terminal.OptionalShellDetectionEnvironment',
    $true)
$detectorType = $terminalAssembly.GetType(
    'FCCCodeDesktop.Terminal.WindowsOptionalShellDetector',
    $true)

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fcc-p08-006 shells عربي ' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
try {
    $programFiles = Join-Path $fixtureRoot 'Program Files'
    $gitRoot = Join-Path $programFiles 'Git'
    $gitCmd = Join-Path $gitRoot 'cmd\git.exe'
    $bashPath = Join-Path $gitRoot 'bin\bash.exe'
    New-EmptyFile $gitCmd
    New-EmptyFile $bashPath

    $windowsDirectory = Join-Path $fixtureRoot 'Windows'
    $wslPath = Join-Path $windowsDirectory 'System32\wsl.exe'
    New-EmptyFile $wslPath

    $ownerMarker = Join-Path $fixtureRoot 'owner-marker.txt'
    [IO.File]::WriteAllText($ownerMarker, 'owner-data', [Text.UTF8Encoding]::new($false))
    $beforeFiles = @(Get-ChildItem -LiteralPath $fixtureRoot -File -Recurse).Count

    $standardEnvironment = New-DetectionEnvironment `
        -EnvironmentType $environmentType `
        -ProgramFiles $programFiles `
        -ProgramFilesX86 $programFiles `
        -LocalApplicationData $null `
        -WindowsDirectory $windowsDirectory `
        -PathValue (Join-Path $gitRoot 'cmd')
    $standardResult = Invoke-Detection -DetectorType $detectorType -Environment $standardEnvironment

    if ($standardResult.Installations.Count -ne 2) {
        throw "Expected exactly Git Bash + WSL with duplicate suppression; got $($standardResult.Installations.Count)."
    }

    $gitBash = $standardResult.Installations[0]
    $wsl = $standardResult.Installations[1]
    if ($gitBash.Kind.ToString() -ne 'GitBash' -or
        $gitBash.Source.ToString() -ne 'StandardInstall' -or
        -not [string]::Equals($gitBash.ExecutablePath, [IO.Path]::GetFullPath($bashPath), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Standard Git Bash detection did not produce the expected typed installation.'
    }
    if ($wsl.Kind.ToString() -ne 'Wsl' -or
        $wsl.Source.ToString() -ne 'WindowsSystem' -or
        -not [string]::Equals($wsl.ExecutablePath, [IO.Path]::GetFullPath($wslPath), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'WSL executable detection did not produce the expected typed installation.'
    }

    $portableRoot = Join-Path $fixtureRoot 'PortableGit'
    $portableBash = Join-Path $portableRoot 'bin\bash.exe'
    New-EmptyFile (Join-Path $portableRoot 'cmd\git.exe')
    New-EmptyFile $portableBash
    $unrelatedBin = Join-Path $fixtureRoot 'Unrelated\bin'
    New-EmptyFile (Join-Path $unrelatedBin 'bash.exe')
    $portablePath = $unrelatedBin + [IO.Path]::PathSeparator + (Join-Path $portableRoot 'cmd')

    $portableEnvironment = New-DetectionEnvironment `
        -EnvironmentType $environmentType `
        -ProgramFiles $null `
        -ProgramFilesX86 $null `
        -LocalApplicationData $null `
        -WindowsDirectory $null `
        -PathValue $portablePath
    $portableResult = Invoke-Detection -DetectorType $detectorType -Environment $portableEnvironment

    if ($portableResult.Installations.Count -ne 1 -or
        $portableResult.Installations[0].Kind.ToString() -ne 'GitBash' -or
        $portableResult.Installations[0].Source.ToString() -ne 'PathDerivedGitInstall' -or
        -not [string]::Equals(
            $portableResult.Installations[0].ExecutablePath,
            [IO.Path]::GetFullPath($portableBash),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'PATH-derived portable Git Bash detection failed or accepted an unrelated generic bash candidate.'
    }

    $emptyEnvironment = New-DetectionEnvironment `
        -EnvironmentType $environmentType `
        -ProgramFiles (Join-Path $fixtureRoot 'MissingProgramFiles') `
        -ProgramFilesX86 $null `
        -LocalApplicationData $null `
        -WindowsDirectory (Join-Path $fixtureRoot 'MissingWindows') `
        -PathValue $unrelatedBin
    $emptyResult = Invoke-Detection -DetectorType $detectorType -Environment $emptyEnvironment
    if ($emptyResult.Installations.Count -ne 0) {
        throw 'Missing candidates or a generic bash path produced a false-positive optional-shell result.'
    }

    $cancelled = [Threading.CancellationTokenSource]::new()
    try {
        $cancelled.Cancel()
        $cancelObserved = $false
        try {
            Invoke-Detection `
                -DetectorType $detectorType `
                -Environment $emptyEnvironment `
                -CancellationToken $cancelled.Token | Out-Null
        }
        catch [Reflection.TargetInvocationException] {
            if ($_.Exception.InnerException -is [OperationCanceledException]) {
                $cancelObserved = $true
            }
            else {
                throw
            }
        }
        if (-not $cancelObserved) {
            throw 'Pre-cancelled optional-shell detection did not surface OperationCanceledException.'
        }
    }
    finally {
        $cancelled.Dispose()
    }

    $afterFiles = @(Get-ChildItem -LiteralPath $fixtureRoot -File -Recurse).Count
    if ($beforeFiles + 3 -ne $afterFiles) {
        throw 'Optional-shell detection unexpectedly mutated the fixture filesystem.'
    }
    if ([IO.File]::ReadAllText($ownerMarker, [Text.Encoding]::UTF8) -ne 'owner-data') {
        throw 'Optional-shell detection modified owner fixture data.'
    }

    Write-Host 'P08-006 hosted-Windows standard/portable/negative/cancellation fixture: PASS.'
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Write-Host 'P08-006 optional shell detection validation: PASS.'
