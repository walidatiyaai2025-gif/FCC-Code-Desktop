[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ReflectionBaseObject {
    param([Parameter(Mandatory)]$Value)

    if ($Value -is [Management.Automation.PSObject]) {
        return $Value.BaseObject
    }

    return $Value
}

function New-ConPtyRequest {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)]$Size
    )

    $constructorArguments = [object[]]::new(4)
    $constructorArguments[0] = Get-ReflectionBaseObject $Executable
    $constructorArguments[1] = $Arguments
    $constructorArguments[2] = Get-ReflectionBaseObject $WorkingDirectory
    $constructorArguments[3] = Get-ReflectionBaseObject $Size
    return $script:RequestConstructor.Invoke($constructorArguments)
}

function Start-ConPtySession {
    param([Parameter(Mandatory)]$Request)

    $arguments = [object[]]::new(2)
    $arguments[0] = Get-ReflectionBaseObject $Request
    $arguments[1] = Get-ReflectionBaseObject ([Threading.CancellationToken]::None)
    $task = $script:HostType.GetMethod('StartAsync').Invoke($script:TerminalHost, $arguments)
    return $task.GetAwaiter().GetResult()
}

function Test-ProcessAlive {
    param([Parameter(Mandatory)][int]$ProcessId)

    try {
        $process = [Diagnostics.Process]::GetProcessById($ProcessId)
        try {
            return -not $process.HasExited
        }
        finally {
            $process.Dispose()
        }
    }
    catch [ArgumentException] {
        return $false
    }
}

function Wait-ProcessGone {
    param(
        [Parameter(Mandatory)][int]$ProcessId,
        [TimeSpan]$Timeout = ([TimeSpan]::FromSeconds(10))
    )

    $deadline = [DateTimeOffset]::UtcNow.Add($Timeout)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (-not (Test-ProcessAlive -ProcessId $ProcessId)) {
            return
        }

        Start-Sleep -Milliseconds 50
    }

    throw "Owned process $ProcessId remained alive after terminal disposal."
}

function Wait-File {
    param(
        [Parameter(Mandatory)][string]$Path,
        [TimeSpan]$Timeout = ([TimeSpan]::FromSeconds(10))
    )

    $deadline = [DateTimeOffset]::UtcNow.Add($Timeout)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            return
        }

        Start-Sleep -Milliseconds 50
    }

    throw "Timed out waiting for terminal safety fixture output '$Path'."
}

function Assert-ExceptionType {
    param(
        [Parameter(Mandatory)][scriptblock]$Action,
        [Parameter(Mandatory)][Type]$ExpectedException,
        [Parameter(Mandatory)][string]$Stage
    )

    $observed = $null
    try {
        & $Action
    }
    catch {
        $observed = $_.Exception
        while ($observed.InnerException) {
            $observed = $observed.InnerException
        }
    }

    if ($null -eq $observed) {
        throw "Terminal safety stage '$Stage' unexpectedly succeeded."
    }

    if (-not $ExpectedException.IsAssignableFrom($observed.GetType())) {
        throw "Terminal safety stage '$Stage' raised '$($observed.GetType().FullName)' instead of '$($ExpectedException.FullName)': $($observed.Message)"
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$baselineScript = Join-Path $repositoryRoot 'tools\terminal\validate-conpty-terminal-host.ps1'
$unitProject = Join-Path $repositoryRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'

if (-not [OperatingSystem]::IsWindowsVersionAtLeast(10, 0, 17763)) {
    throw 'P08-008 process/terminal safety validation requires Windows 10 version 1809 (build 17763) or newer.'
}

foreach ($path in @($baselineScript, $unitProject)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required P08-008 path is missing: $path"
    }
}

# Reuse the already-integrated P08-004 hosted-Windows acceptance as the mandatory
# process-tree ownership baseline. It proves input/output/resize, exact CMD launch,
# kill-on-job-close descendant cleanup and unrelated-process isolation.
& $baselineScript -Configuration $Configuration

& dotnet test $unitProject -c $Configuration --no-restore --nologo --filter 'FullyQualifiedName~ProcessSupervisorTests|FullyQualifiedName~ProcessCancellationEscalatorTests|FullyQualifiedName~BoundedProcessOutputPipelineTests|FullyQualifiedName~ConPtyTerminalContractTests' --logger 'console;verbosity=minimal'
if ($LASTEXITCODE -ne 0) {
    throw "P08-008 process/terminal safety unit suite failed with exit code $LASTEXITCODE."
}

$terminalOutput = Join-Path $repositoryRoot "src\FCCCodeDesktop.Terminal\bin\$Configuration\net10.0-windows"
$terminalAssemblyPath = Join-Path $terminalOutput 'FCCCodeDesktop.Terminal.dll'
$applicationAssemblyPath = Join-Path $terminalOutput 'FCCCodeDesktop.Application.dll'
foreach ($path in @($terminalAssemblyPath, $applicationAssemblyPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Expected P08-008 built assembly is missing: $path"
    }
}

$applicationAssembly = [Reflection.Assembly]::LoadFrom($applicationAssemblyPath)
$terminalAssembly = [Reflection.Assembly]::LoadFrom($terminalAssemblyPath)
$sizeType = $applicationAssembly.GetType('FCCCodeDesktop.Application.Terminal.TerminalSize', $true)
$requestType = $applicationAssembly.GetType('FCCCodeDesktop.Application.Terminal.ConPtyLaunchRequest', $true)
$script:HostType = $terminalAssembly.GetType('FCCCodeDesktop.Terminal.WindowsConPtyTerminalHost', $true)
$enumerableStringType = [Collections.Generic.IEnumerable[string]]
$script:RequestConstructor = $requestType.GetConstructor(
    [Type[]]@([string], $enumerableStringType, [string], $sizeType))
if ($null -eq $script:RequestConstructor) {
    throw 'The expected ConPtyLaunchRequest constructor was not found for P08-008.'
}

$script:TerminalHost = [Activator]::CreateInstance($script:HostType)
$initialSize = [Activator]::CreateInstance($sizeType, @([int]80, [int]25))
$resized = [Activator]::CreateInstance($sizeType, @([int]100, [int]40))
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fcc-p08-008 terminal safety عربي ' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null

$powerShellExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
if (-not (Test-Path -LiteralPath $powerShellExe)) {
    throw "Windows PowerShell fixture executable is missing: $powerShellExe"
}

try {
    # 1. Argument quoting must be lossless. The host calls CreateProcessW directly;
    # special characters, quotes, whitespace, empty values and trailing backslashes
    # must arrive as inert argv values rather than being reinterpreted or merged.
    $argumentScriptPath = Join-Path $fixtureRoot 'argv fixture.ps1'
    $argumentOutputPath = Join-Path $fixtureRoot 'argv result.json'
    $argumentScript = @'
if ($args.Count -lt 1) {
    throw 'Missing output path.'
}

$outputPath = [string]$args[0]
$payload = if ($args.Count -gt 1) { @($args[1..($args.Count - 1)]) } else { @() }
$json = ConvertTo-Json -InputObject $payload -Compress
[IO.File]::WriteAllText($outputPath, $json, [Text.UTF8Encoding]::new($false))
'@
    [IO.File]::WriteAllText($argumentScriptPath, $argumentScript, [Text.UTF8Encoding]::new($false))

    $expectedArguments = [string[]]@(
        'plain',
        'two words',
        'quote"inside',
        'C:\folder with spaces\tail\',
        '&|<>^',
        'semi;colon',
        ''
    )
    $launchArguments = [string[]](@(
        '-NoLogo',
        '-NoProfile',
        '-NonInteractive',
        '-File',
        $argumentScriptPath,
        $argumentOutputPath
    ) + $expectedArguments)

    $argumentSession = $null
    try {
        $request = New-ConPtyRequest -Executable $powerShellExe -Arguments $launchArguments -WorkingDirectory $fixtureRoot -Size $initialSize
        $argumentSession = Start-ConPtySession -Request $request
        $exitCode = $argumentSession.Completion.WaitAsync([TimeSpan]::FromSeconds(20)).GetAwaiter().GetResult()
        if ($exitCode -ne 0) {
            throw "Argument round-trip fixture exited with code $exitCode."
        }

        Wait-File -Path $argumentOutputPath
        $observedArguments = @((Get-Content -LiteralPath $argumentOutputPath -Raw | ConvertFrom-Json))
        if ($observedArguments.Count -ne $expectedArguments.Count) {
            throw "Argument round-trip count mismatch. Expected $($expectedArguments.Count), observed $($observedArguments.Count)."
        }

        for ($index = 0; $index -lt $expectedArguments.Count; $index++) {
            if ([string]$observedArguments[$index] -cne $expectedArguments[$index]) {
                throw "Argument round-trip mismatch at index $index."
            }
        }
    }
    finally {
        if ($argumentSession) {
            $null = $argumentSession.DisposeAsync().AsTask().GetAwaiter().GetResult()
        }
    }
    Write-Host 'P08-008 lossless argument-boundary safety: PASS.'

    # 2. Disposal must be concurrency-safe and idempotent. Closing the private job
    # must terminate the owned root, and closed session I/O/resize must fail closed.
    $disposeSession = $null
    try {
        $request = New-ConPtyRequest -Executable $powerShellExe -Arguments ([string[]]@('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', 'Start-Sleep -Seconds 60')) -WorkingDirectory $fixtureRoot -Size $initialSize
        $disposeSession = Start-ConPtySession -Request $request
        $ownedPid = [int]$disposeSession.ProcessId
        $inputStream = $disposeSession.Input

        if (-not (Test-ProcessAlive -ProcessId $ownedPid)) {
            throw "Owned terminal process $ownedPid was not alive before disposal."
        }

        $disposeOne = $disposeSession.DisposeAsync().AsTask()
        $disposeTwo = $disposeSession.DisposeAsync().AsTask()
        $disposeAll = [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($disposeOne, $disposeTwo))
        $disposeAll.WaitAsync([TimeSpan]::FromSeconds(20)).GetAwaiter().GetResult()
        Wait-ProcessGone -ProcessId $ownedPid

        Assert-ExceptionType -ExpectedException ([ObjectDisposedException]) -Stage 'resize after dispose' -Action {
            $null = $disposeSession.ResizeAsync($resized, [Threading.CancellationToken]::None).AsTask().GetAwaiter().GetResult()
        }
        Assert-ExceptionType -ExpectedException ([ObjectDisposedException]) -Stage 'input after dispose' -Action {
            $inputStream.WriteByte(0)
        }

        # A third disposal is explicitly allowed and must remain a no-op.
        $null = $disposeSession.DisposeAsync().AsTask().GetAwaiter().GetResult()
        $disposeSession = $null
    }
    finally {
        if ($disposeSession) {
            $null = $disposeSession.DisposeAsync().AsTask().GetAwaiter().GetResult()
        }
    }
    Write-Host 'P08-008 concurrent/idempotent dispose and closed-state fail-closed behavior: PASS.'

    # 3. Cancellation of resize must not mutate observable terminal state.
    $cancelSession = $null
    $cancelSource = $null
    try {
        $request = New-ConPtyRequest -Executable $powerShellExe -Arguments ([string[]]@('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', 'Start-Sleep -Seconds 60')) -WorkingDirectory $fixtureRoot -Size $initialSize
        $cancelSession = Start-ConPtySession -Request $request
        $beforeColumns = [int]$cancelSession.Size.Columns
        $beforeRows = [int]$cancelSession.Size.Rows
        $cancelSource = [Threading.CancellationTokenSource]::new()
        $cancelSource.Cancel()

        Assert-ExceptionType -ExpectedException ([OperationCanceledException]) -Stage 'cancelled resize' -Action {
            $null = $cancelSession.ResizeAsync($resized, $cancelSource.Token).AsTask().GetAwaiter().GetResult()
        }
        if ($cancelSession.Size.Columns -ne $beforeColumns -or $cancelSession.Size.Rows -ne $beforeRows) {
            throw 'Cancelled resize mutated the observable terminal size.'
        }
    }
    finally {
        if ($cancelSource) {
            $cancelSource.Dispose()
        }
        if ($cancelSession) {
            $null = $cancelSession.DisposeAsync().AsTask().GetAwaiter().GetResult()
        }
    }
    Write-Host 'P08-008 cancelled-resize state preservation: PASS.'

    # 4. Completion is terminal: after a child exits, resize must fail rather than
    # touching a completed/released pseudoconsole handle.
    $completedSession = $null
    try {
        $request = New-ConPtyRequest -Executable $powerShellExe -Arguments ([string[]]@('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', 'exit 7')) -WorkingDirectory $fixtureRoot -Size $initialSize
        $completedSession = Start-ConPtySession -Request $request
        $exitCode = $completedSession.Completion.WaitAsync([TimeSpan]::FromSeconds(20)).GetAwaiter().GetResult()
        if ($exitCode -ne 7) {
            throw "Completed-session fixture returned exit code $exitCode instead of 7."
        }

        Assert-ExceptionType -ExpectedException ([InvalidOperationException]) -Stage 'resize after completion' -Action {
            $null = $completedSession.ResizeAsync($resized, [Threading.CancellationToken]::None).AsTask().GetAwaiter().GetResult()
        }
    }
    finally {
        if ($completedSession) {
            $null = $completedSession.DisposeAsync().AsTask().GetAwaiter().GetResult()
        }
    }
    Write-Host 'P08-008 completed-session resize rejection: PASS.'
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Write-Host 'P08-008 process/terminal safety validation: PASS.'
