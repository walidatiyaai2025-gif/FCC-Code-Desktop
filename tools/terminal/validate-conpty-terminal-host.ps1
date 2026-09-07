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

function Read-ConPtyUntilMarker {
    param(
        [Parameter(Mandatory)][IO.StreamReader]$Reader,
        [Parameter(Mandatory)][Text.StringBuilder]$Buffer,
        [Parameter(Mandatory)][string]$Marker,
        [TimeSpan]$Timeout = ([TimeSpan]::FromSeconds(10))
    )

    $deadline = [DateTimeOffset]::UtcNow.Add($Timeout)
    $chars = [char[]]::new(1024)

    while (-not $Buffer.ToString().Contains($Marker, [StringComparison]::Ordinal)) {
        $remaining = $deadline - [DateTimeOffset]::UtcNow
        if ($remaining -le [TimeSpan]::Zero) {
            throw "Timed out waiting for ConPTY marker '$Marker'. Output so far: $($Buffer.ToString())"
        }

        try {
            $readTask = $Reader.ReadAsync($chars, 0, $chars.Length)
            $count = $readTask.WaitAsync($remaining).GetAwaiter().GetResult()
        }
        catch [TimeoutException] {
            throw "Timed out waiting for ConPTY marker '$Marker'. Output so far: $($Buffer.ToString())"
        }

        if ($count -eq 0) {
            throw "ConPTY output reached EOF before marker '$Marker'. Output so far: $($Buffer.ToString())"
        }

        $null = $Buffer.Append($chars, 0, $count)
    }
}

function Write-ConPtyInput {
    param(
        [Parameter(Mandatory)][IO.Stream]$InputStream,
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)]$Session,
        [Parameter(Mandatory)][string]$Stage
    )

    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    try {
        $InputStream.Write($bytes, 0, $bytes.Length)
        $InputStream.Flush()
    }
    catch {
        $writeState = if ($Session.Completion.IsCompleted) {
            "completed(exit=$($Session.Completion.GetAwaiter().GetResult()))"
        }
        else {
            'not-completed'
        }
        throw "ConPTY input write failed at $Stage while root completion was $writeState. $($_.Exception.Message)"
    }
}

function Wait-FileContent {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Expected,
        [TimeSpan]$Timeout = ([TimeSpan]::FromSeconds(10))
    )

    $deadline = [DateTimeOffset]::UtcNow.Add($Timeout)
    do {
        if (Test-Path -LiteralPath $Path) {
            $actual = (Get-Content -LiteralPath $Path -Raw).Trim()
            if ($actual -eq $Expected) {
                return
            }
        }
        Start-Sleep -Milliseconds 50
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    $observed = if (Test-Path -LiteralPath $Path) { (Get-Content -LiteralPath $Path -Raw) } else { '<missing>' }
    throw "Timed out waiting for '$Path' to contain '$Expected'. Observed: $observed"
}

function Wait-FileInteger {
    param(
        [Parameter(Mandatory)][string]$Path,
        [TimeSpan]$Timeout = ([TimeSpan]::FromSeconds(10))
    )

    $deadline = [DateTimeOffset]::UtcNow.Add($Timeout)
    do {
        if (Test-Path -LiteralPath $Path) {
            $text = (Get-Content -LiteralPath $Path -Raw).Trim()
            $value = 0
            if ([int]::TryParse($text, [ref]$value) -and $value -gt 0) {
                return $value
            }
        }
        Start-Sleep -Milliseconds 50
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for positive process ID in '$Path'."
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

    throw "Owned process $ProcessId remained alive after ConPTY session disposal."
}

function Invoke-ConPtyStart {
    param(
        [Parameter(Mandatory)][Type]$HostType,
        [Parameter(Mandatory)]$Host,
        [Parameter(Mandatory)]$Request,
        [Parameter(Mandatory)][Threading.CancellationToken]$CancellationToken
    )

    $arguments = [object[]]::new(2)
    $arguments[0] = Get-ReflectionBaseObject $Request
    $arguments[1] = Get-ReflectionBaseObject $CancellationToken
    $task = $HostType.GetMethod('StartAsync').Invoke($Host, $arguments)
    return $task.GetAwaiter().GetResult()
}

function Assert-ConPtyStartFailure {
    param(
        [Parameter(Mandatory)][Type]$HostType,
        [Parameter(Mandatory)]$Host,
        [Parameter(Mandatory)]$Request,
        [Parameter(Mandatory)][Threading.CancellationToken]$CancellationToken,
        [Parameter(Mandatory)][Type]$ExpectedException,
        [Parameter(Mandatory)][string]$Stage
    )

    $observed = $null
    try {
        $unexpectedSession = Invoke-ConPtyStart -HostType $HostType -Host $Host -Request $Request -CancellationToken $CancellationToken
        if ($unexpectedSession) {
            $null = $unexpectedSession.DisposeAsync().AsTask().GetAwaiter().GetResult()
        }
    }
    catch {
        $observed = $_.Exception
        while ($observed -is [Reflection.TargetInvocationException] -and $observed.InnerException) {
            $observed = $observed.InnerException
        }
        if ($observed -is [AggregateException] -and $observed.InnerExceptions.Count -eq 1) {
            $observed = $observed.InnerExceptions[0]
        }
    }

    if ($null -eq $observed) {
        throw "ConPTY $Stage unexpectedly succeeded."
    }
    if (-not $ExpectedException.IsAssignableFrom($observed.GetType())) {
        throw "ConPTY $Stage raised '$($observed.GetType().FullName)' instead of '$($ExpectedException.FullName)': $($observed.Message)"
    }
}

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
    'ProcThreadAttributeJobList',
    'UpdateProcThreadAttribute(JOB_LIST)',
    'JobObjectLimitKillOnJobClose',
    'ExtendedStartupInfoPresent',
    'StartfUseStdHandles',
    'StandardInput = IntPtr.Zero',
    'StandardOutput = IntPtr.Zero',
    'StandardError = IntPtr.Zero'
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
    'powershell.exe',
    'CreateSuspended',
    'ResumeThread',
    'AssignProcessToJobObject'
)
foreach ($token in $forbiddenTokens) {
    if ($implementation.Contains($token, [StringComparison]::OrdinalIgnoreCase)) {
        throw "P08-004 implementation contains forbidden token '$token'."
    }
}

$jobAttributeIndex = $implementation.IndexOf('UpdateProcThreadAttribute(JOB_LIST)', [StringComparison]::Ordinal)
$createProcessIndex = $implementation.IndexOf('"CreateProcessW"', [StringComparison]::Ordinal)
if ($jobAttributeIndex -lt 0 -or $createProcessIndex -lt 0 -or $jobAttributeIndex -gt $createProcessIndex) {
    throw 'P08-004 must bind the kill-on-close job in STARTUPINFOEX before CreateProcessW.'
}

Write-Host 'P08-004 static ConPTY + atomic job-list + isolated std-handle contract: PASS.'

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
$enumerableStringType = [Collections.Generic.IEnumerable[string]]
$requestConstructor = $requestType.GetConstructor(
    [Type[]]@([string], $enumerableStringType, [string], $sizeType))
if ($null -eq $requestConstructor) {
    throw 'The expected ConPtyLaunchRequest constructor was not found.'
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fcc-p08-004 conpty عربي ' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
[IO.File]::WriteAllText((Join-Path $fixtureRoot 'marker.txt'), 'owner-data', [Text.UTF8Encoding]::new($false))

$comSpec = [Environment]::GetEnvironmentVariable('ComSpec')
if ([string]::IsNullOrWhiteSpace($comSpec) -or -not (Test-Path -LiteralPath $comSpec)) {
    throw 'ComSpec was not available for the hosted-Windows ConPTY fixture.'
}

$initialSize = [Activator]::CreateInstance($sizeType, @([int]80, [int]25))
$emptyLaunchArguments = [string[]]@()
function New-ConPtyRequest {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)]$Size
    )

    $arguments = [object[]]::new(4)
    $arguments[0] = Get-ReflectionBaseObject $Executable
    $arguments[1] = $emptyLaunchArguments
    $arguments[2] = Get-ReflectionBaseObject $WorkingDirectory
    $arguments[3] = Get-ReflectionBaseObject $Size
    return $requestConstructor.Invoke($arguments)
}

$terminalHost = [Activator]::CreateInstance($hostType)

# Negative/cancellation paths are task-local launch guarantees and must fail before
# any usable session escapes to the caller.
$missingExecutable = Join-Path $fixtureRoot 'missing-shell.exe'
$missingExecutableRequest = New-ConPtyRequest -Executable $missingExecutable -WorkingDirectory $fixtureRoot -Size $initialSize
Assert-ConPtyStartFailure -HostType $hostType -Host $terminalHost -Request $missingExecutableRequest -CancellationToken ([Threading.CancellationToken]::None) -ExpectedException ([IO.FileNotFoundException]) -Stage 'missing-executable path'

$missingDirectory = Join-Path $fixtureRoot 'missing-directory'
$missingDirectoryRequest = New-ConPtyRequest -Executable $comSpec -WorkingDirectory $missingDirectory -Size $initialSize
Assert-ConPtyStartFailure -HostType $hostType -Host $terminalHost -Request $missingDirectoryRequest -CancellationToken ([Threading.CancellationToken]::None) -ExpectedException ([IO.DirectoryNotFoundException]) -Stage 'missing-working-directory path'

$cancelledRequest = New-ConPtyRequest -Executable $comSpec -WorkingDirectory $fixtureRoot -Size $initialSize
$cancelledSource = [Threading.CancellationTokenSource]::new()
$cancelledSource.Cancel()
try {
    Assert-ConPtyStartFailure -HostType $hostType -Host $terminalHost -Request $cancelledRequest -CancellationToken $cancelledSource.Token -ExpectedException ([OperationCanceledException]) -Stage 'pre-cancelled launch'
}
finally {
    $cancelledSource.Dispose()
}
Write-Host 'P08-004 launch negative/cancellation paths: PASS.'

# Exact P08-005 CMD profile contract: cmd.exe with no arguments.
$session = $null
$reader = $null
try {
    $request = New-ConPtyRequest -Executable $comSpec -WorkingDirectory $fixtureRoot -Size $initialSize
    $session = Invoke-ConPtyStart -HostType $hostType -Host $terminalHost -Request $request -CancellationToken ([Threading.CancellationToken]::None)

    if ($session.ProcessId -le 0) {
        throw 'ConPTY session returned an invalid process ID.'
    }
    if ($session.Completion.IsCompleted) {
        throw "ConPTY CMD profile exited before interaction with code $($session.Completion.GetAwaiter().GetResult())."
    }

    $reader = [IO.StreamReader]::new($session.Output, [Text.Encoding]::UTF8, $true, 4096, $true)
    $captured = [Text.StringBuilder]::new()

    $preResizePath = Join-Path $fixtureRoot 'pre-resize.txt'
    Write-ConPtyInput -InputStream $session.Input -Text ">pre-resize.txt echo P08_004_INPUT_EXECUTED`r" -Session $session -Stage 'pre-resize execution proof'
    Wait-FileContent -Path $preResizePath -Expected 'P08_004_INPUT_EXECUTED'
    if ($session.Completion.IsCompleted) {
        throw "ConPTY CMD profile completed after pre-resize command execution with exit $($session.Completion.GetAwaiter().GetResult())."
    }

    # Build the output marker in environment pieces so the complete marker never
    # appears in injected terminal input. Observing it therefore proves child output.
    Write-ConPtyInput -InputStream $session.Input -Text "set P08A=P08_004_`r" -Session $session -Stage 'pre-resize output prefix'
    Write-ConPtyInput -InputStream $session.Input -Text "set P08B=INPUT_OUTPUT_OK`r" -Session $session -Stage 'pre-resize output suffix'
    Write-ConPtyInput -InputStream $session.Input -Text "echo %P08A%%P08B%`r" -Session $session -Stage 'pre-resize output round trip'
    Read-ConPtyUntilMarker -Reader $reader -Buffer $captured -Marker 'P08_004_INPUT_OUTPUT_OK'

    $resized = [Activator]::CreateInstance($sizeType, @([int]100, [int]40))
    $null = $session.ResizeAsync($resized, [Threading.CancellationToken]::None).AsTask().GetAwaiter().GetResult()
    if ($session.Size.Columns -ne 100 -or $session.Size.Rows -ne 40) {
        throw 'ConPTY resize did not update the observable terminal size.'
    }

    $postResizePath = Join-Path $fixtureRoot 'post-resize.txt'
    Write-ConPtyInput -InputStream $session.Input -Text ">post-resize.txt echo P08_004_RESIZE_EXECUTED`r" -Session $session -Stage 'post-resize execution proof'
    Wait-FileContent -Path $postResizePath -Expected 'P08_004_RESIZE_EXECUTED'
    if ($session.Completion.IsCompleted) {
        throw "ConPTY CMD profile completed after resize with exit $($session.Completion.GetAwaiter().GetResult())."
    }

    Write-ConPtyInput -InputStream $session.Input -Text "set P08B=RESIZE_OUTPUT_OK`r" -Session $session -Stage 'post-resize output suffix'
    Write-ConPtyInput -InputStream $session.Input -Text "echo %P08A%%P08B%`r" -Session $session -Stage 'post-resize output round trip'
    Read-ConPtyUntilMarker -Reader $reader -Buffer $captured -Marker 'P08_004_RESIZE_OUTPUT_OK'

    Write-ConPtyInput -InputStream $session.Input -Text "exit /b 0`r" -Session $session -Stage 'clean exit'
    $exitCode = $session.Completion.WaitAsync([TimeSpan]::FromSeconds(20)).GetAwaiter().GetResult()
    $tail = $reader.ReadToEndAsync().WaitAsync([TimeSpan]::FromSeconds(20)).GetAwaiter().GetResult()
    $null = $captured.Append($tail)

    if ($exitCode -ne 0) {
        throw "ConPTY CMD profile exited with code $exitCode. Output: $($captured.ToString())"
    }
    foreach ($marker in @('P08_004_INPUT_OUTPUT_OK', 'P08_004_RESIZE_OUTPUT_OK')) {
        if (-not $captured.ToString().Contains($marker, [StringComparison]::Ordinal)) {
            throw "ConPTY fixture did not observe proven child-output marker '$marker'."
        }
    }
    if ((Get-Content -LiteralPath (Join-Path $fixtureRoot 'marker.txt') -Raw) -ne 'owner-data') {
        throw 'ConPTY fixture modified owner data unexpectedly.'
    }

    Write-Host 'P08-004 exact CMD-profile launch/input/output/resize/clean-exit fixture: PASS.'
}
finally {
    if ($reader) {
        $reader.Dispose()
    }
    if ($session) {
        $null = $session.DisposeAsync().AsTask().GetAwaiter().GetResult()
    }
}

# Disposal must terminate the entire owned tree while preserving an unrelated process.
$cleanupSession = $null
$sentinel = $null
try {
    $sentinelExecutable = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $sentinelExecutable)) {
        throw "Cleanup sentinel executable is missing: $sentinelExecutable"
    }
    $sentinel = Start-Process -FilePath $sentinelExecutable -ArgumentList @('-NoLogo', '-NoProfile', '-Command', 'Start-Sleep -Seconds 60') -WindowStyle Hidden -PassThru

    $childPidPath = Join-Path $fixtureRoot 'child.pid'
    Remove-Item -LiteralPath $childPidPath -Force -ErrorAction SilentlyContinue

    $cleanupRequest = New-ConPtyRequest -Executable $comSpec -WorkingDirectory $fixtureRoot -Size $initialSize
    $cleanupSession = Invoke-ConPtyStart -HostType $hostType -Host $terminalHost -Request $cleanupRequest -CancellationToken ([Threading.CancellationToken]::None)
    $rootPid = [int]$cleanupSession.ProcessId
    if ($cleanupSession.Completion.IsCompleted) {
        throw "Cleanup CMD profile exited before descendant launch with code $($cleanupSession.Completion.GetAwaiter().GetResult())."
    }

    $childCommand = 'powershell.exe -NoLogo -NoProfile -Command "$PID | Set-Content -NoNewline -Encoding Ascii -LiteralPath ''child.pid''; Start-Sleep -Seconds 60"' + "`r"
    Write-ConPtyInput -InputStream $cleanupSession.Input -Text $childCommand -Session $cleanupSession -Stage 'owned descendant launch'
    $childPid = Wait-FileInteger -Path $childPidPath

    if (-not (Test-ProcessAlive -ProcessId $rootPid)) {
        throw "Owned ConPTY root $rootPid was not alive before disposal."
    }
    if (-not (Test-ProcessAlive -ProcessId $childPid)) {
        throw "Owned ConPTY descendant $childPid was not alive before disposal."
    }
    if ($sentinel.HasExited) {
        throw 'Unrelated sentinel exited before ConPTY disposal and cannot prove ownership isolation.'
    }

    $null = $cleanupSession.DisposeAsync().AsTask().GetAwaiter().GetResult()
    $cleanupSession = $null

    Wait-ProcessGone -ProcessId $rootPid
    Wait-ProcessGone -ProcessId $childPid
    $sentinel.Refresh()
    if ($sentinel.HasExited) {
        throw 'ConPTY disposal terminated the unrelated sentinel process.'
    }

    Write-Host 'P08-004 dispose/owned-descendant cleanup/unrelated-process isolation fixture: PASS.'
}
finally {
    if ($cleanupSession) {
        $null = $cleanupSession.DisposeAsync().AsTask().GetAwaiter().GetResult()
    }
    if ($sentinel) {
        try {
            if (-not $sentinel.HasExited) {
                $sentinel.Kill($true)
                $sentinel.WaitForExit(5000)
            }
        }
        finally {
            $sentinel.Dispose()
        }
    }
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

Write-Host 'P08-004 ConPTY terminal host validation: PASS.'
