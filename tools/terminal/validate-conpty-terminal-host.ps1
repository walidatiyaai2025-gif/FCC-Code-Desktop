[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$implementationPath = Join-Path $repositoryRoot 'src\FCCCodeDesktop.Terminal\WindowsConPtyTerminalHost.cs'
$terminalProject = Join-Path $repositoryRoot 'src\FCCCodeDesktop.Terminal\FCCCodeDesktop.Terminal.csproj'
$unitProject = Join-Path $repositoryRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'

if (-not [OperatingSystem]::IsWindowsVersionAtLeast(10, 0, 17763)) {
    throw 'P08-004 validation requires Windows 10 version 1809 (build 17763) or newer.'
}

foreach ($path in @($implementationPath, $terminalProject, $unitProject)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required P08-004 path is missing: $path"
    }
}

$implementation = Get-Content -LiteralPath $implementationPath -Raw
$requiredTokens = @(
    'CreatePseudoConsole',
    'ResizePseudoConsole',
    'ProcThreadAttributePseudoConsole',
    'CreateSuspended',
    'AssignProcessToJobObject',
    'JobObjectLimitKillOnJobClose',
    'TerminateProcess',
    'ExtendedStartupInfoPresent'
)
foreach ($token in $requiredTokens) {
    if (-not $implementation.Contains($token, [StringComparison]::Ordinal)) {
        throw "P08-004 implementation is missing required safety token '$token'."
    }
}

$forbiddenTokens = @(
    'CREATE_NEW_CONSOLE',
    'UseShellExecute = true',
    'ProcessStartInfo',
    'cmd.exe',
    'powershell.exe'
)
foreach ($token in $forbiddenTokens) {
    if ($implementation.Contains($token, [StringComparison]::OrdinalIgnoreCase)) {
        throw "P08-004 implementation contains forbidden token '$token'."
    }
}

Write-Host 'P08-004 static ConPTY contract: PASS.'

& dotnet restore (Join-Path $repositoryRoot 'FCCCodeDesktop.sln') --locked-mode --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Locked restore failed with exit code $LASTEXITCODE."
}

& dotnet build $terminalProject -c $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Terminal project build failed with exit code $LASTEXITCODE."
}

& dotnet test $unitProject -c $Configuration --no-restore --nologo --filter 'FullyQualifiedName~ConPtyTerminalContractTests' --logger 'console;verbosity=minimal'
if ($LASTEXITCODE -ne 0) {
    throw "ConPTY contract unit tests failed with exit code $LASTEXITCODE."
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
$sizeType = $applicationAssembly.GetType('FCCCodeDesktop.Application.Terminal.TerminalSize', $true)
$requestType = $applicationAssembly.GetType('FCCCodeDesktop.Application.Terminal.ConPtyLaunchRequest', $true)
$hostType = $terminalAssembly.GetType('FCCCodeDesktop.Terminal.WindowsConPtyTerminalHost', $true)

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fcc-p08-004 conpty عربي ' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
[IO.File]::WriteAllText((Join-Path $fixtureRoot 'marker.txt'), 'owner-data', [Text.UTF8Encoding]::new($false))

$session = $null
$reader = $null
try {
    $comSpec = [Environment]::GetEnvironmentVariable('ComSpec')
    if ([string]::IsNullOrWhiteSpace($comSpec) -or -not (Test-Path -LiteralPath $comSpec)) {
        throw 'ComSpec was not available for the hosted-Windows ConPTY fixture.'
    }

    $initialSize = [Activator]::CreateInstance($sizeType, @([int]80, [int]25))
    $request = [Activator]::CreateInstance(
        $requestType,
        @($comSpec, [string[]]@('/d', '/q'), $fixtureRoot, $initialSize))
    $host = [Activator]::CreateInstance($hostType)
    $startTask = $hostType.GetMethod('StartAsync').Invoke(
        $host,
        @($request, [Threading.CancellationToken]::None))
    $session = $startTask.GetAwaiter().GetResult()

    if ($session.ProcessId -le 0) {
        throw 'ConPTY session returned an invalid process ID.'
    }

    $resized = [Activator]::CreateInstance($sizeType, @([int]100, [int]40))
    $session.ResizeAsync($resized, [Threading.CancellationToken]::None).AsTask().GetAwaiter().GetResult()
    if ($session.Size.Columns -ne 100 -or $session.Size.Rows -ne 40) {
        throw 'ConPTY resize did not update the observable terminal size.'
    }

    $reader = [IO.StreamReader]::new($session.Output, [Text.Encoding]::UTF8, $true, 4096, $true)
    $readTask = $reader.ReadToEndAsync()
    $commandBytes = [Text.Encoding]::UTF8.GetBytes("if exist marker.txt echo P08_004_CONPTY_OK`r`nexit /b 0`r`n")
    $session.Input.Write($commandBytes, 0, $commandBytes.Length)
    $session.Input.Flush()

    $exitCode = $session.Completion.WaitAsync([TimeSpan]::FromSeconds(20)).GetAwaiter().GetResult()
    $output = $readTask.WaitAsync([TimeSpan]::FromSeconds(20)).GetAwaiter().GetResult()

    if ($exitCode -ne 0) {
        throw "ConPTY fixture exited with code $exitCode. Output: $output"
    }

    if (-not $output.Contains('P08_004_CONPTY_OK', [StringComparison]::Ordinal)) {
        throw "ConPTY fixture did not round-trip interactive input/output. Output: $output"
    }

    if ((Get-Content -LiteralPath (Join-Path $fixtureRoot 'marker.txt') -Raw) -ne 'owner-data') {
        throw 'ConPTY fixture modified owner data unexpectedly.'
    }

    Write-Host 'P08-004 hosted-Windows ConPTY launch/input/output/resize fixture: PASS.'
}
finally {
    if ($reader) {
        $reader.Dispose()
    }
    if ($session) {
        $session.DisposeAsync().AsTask().GetAwaiter().GetResult()
    }
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Write-Host 'P08-004 ConPTY terminal host validation: PASS.'
