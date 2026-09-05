[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-LastExitCode {
    param([string]$Stage)
    if ($LASTEXITCODE -ne 0) {
        throw "$Stage failed with exit code $LASTEXITCODE."
    }
}

function Read-YesNo {
    param([string]$Prompt)

    while ($true) {
        $answer = (Read-Host "$Prompt [y/n]").Trim().ToLowerInvariant()
        if ($answer -in @('y', 'yes')) { return $true }
        if ($answer -in @('n', 'no')) { return $false }
        Write-Host 'Please answer y or n.' -ForegroundColor Yellow
    }
}

function Write-Evidence {
    param(
        [string]$Path,
        [string]$Sha,
        [string]$Status,
        [hashtable]$Checks
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $document = [ordered]@{
        schemaVersion = 1
        evidenceClassification = 'REAL_TARGET'
        sourceRequirement = 'P05_EXIT_GATE'
        testedRepoSha = $Sha
        overallStatus = $Status
        generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        sanitized = $true
        observations = [ordered]@{
            realProviderTaskCompleted = [bool]$Checks.realProviderTaskCompleted
            structuredExecutionObserved = [bool]$Checks.structuredExecutionObserved
            stopRetryObserved = [bool]$Checks.stopRetryObserved
            appClosedAndReopened = [bool]$Checks.appClosedAndReopened
            sessionResumedWithDurableState = [bool]$Checks.sessionResumedWithDurableState
        }
        note = 'No prompt text, provider response content, credentials, environment variables, or user project contents are recorded.'
    }

    $document | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding utf8
}

if (-not $IsWindows) {
    throw 'P05 phase-exit owner validation must run on the authoritative owner Windows environment.'
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required for exact-head provenance.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK is required for P05 owner validation.'
}
if (-not (Get-Command fcc-claude -ErrorAction SilentlyContinue)) {
    throw 'fcc-claude must be installed and available on PATH for genuine P05 REAL_TARGET validation.'
}

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$solutionPath = Join-Path $root 'FCCCodeDesktop.sln'
$appProject = Join-Path $root 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'
$appExecutable = Join-Path $root 'src\FCCCodeDesktop.App\bin\Release\net10.0-windows\FCCCodeDesktop.App.exe'
$evidencePath = Join-Path $root 'evidence\phases\P05\owner\P05_PHASE_EXIT_REAL_TARGET.json'

foreach ($path in @($solutionPath, $appProject)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P05 validation path is missing: $path"
    }
}

Push-Location $root
try {
    $gitRoot = (& git rev-parse --show-toplevel 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'Repository identity check'
    if ([IO.Path]::GetFullPath($gitRoot).TrimEnd('\') -ne [IO.Path]::GetFullPath($root).TrimEnd('\')) {
        throw "Wrong repository. Expected '$root', got '$gitRoot'."
    }

    $head = (& git rev-parse HEAD 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'HEAD resolution'
    if ($head -notmatch '^[0-9a-f]{40}$') {
        throw "Invalid exact HEAD SHA '$head'."
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    Assert-LastExitCode '.NET SDK version check'
    if ($sdkVersion -ne '10.0.400') {
        throw "Expected .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    Write-Host "P05 owner validation candidate: $head"
    Write-Host 'Running deterministic cloud-equivalent prerequisites before the real interaction scenario...'
    & dotnet restore $solutionPath --locked-mode --nologo
    Assert-LastExitCode 'Locked restore'
    & dotnet build $solutionPath -c Release --no-restore --nologo
    Assert-LastExitCode 'Release build'
    & pwsh -NoProfile -File .\tools\ui\validate-streaming-conversation.ps1 -RunFixtures -RequireRuntime
    Assert-LastExitCode 'Streaming conversation validator'
    & pwsh -NoProfile -File .\tools\ui\validate-tool-activity-timeline.ps1 -RunFixtures -RequireRuntime
    Assert-LastExitCode 'Tool activity timeline validator'
    & pwsh -NoProfile -File .\tools\ui\validate-session-workspace.ps1 -RunFixtures -RequireRuntime
    Assert-LastExitCode 'Session workspace validator'
    & pwsh -NoProfile -File .\tools\ui\validate-task-state-machine.ps1 -RunFixtures -RequireRuntime
    Assert-LastExitCode 'Task state machine validator'
    & pwsh -NoProfile -File .\tools\ui\validate-task-controls.ps1 -RunFixtures -RequireRuntime
    Assert-LastExitCode 'Task controls validator'
    & pwsh -NoProfile -File .\tools\ui\validate-conversation-content-rendering.ps1 -RunFixtures -RequireRuntime
    Assert-LastExitCode 'Conversation content validator'
    & pwsh -NoProfile -File .\tools\ui\validate-conversation-virtualization.ps1 -RunFixtures -RequireRuntime
    Assert-LastExitCode 'Conversation virtualization validator'

    if (-not (Test-Path -LiteralPath $appExecutable -PathType Leaf)) {
        throw "Release application executable was not produced: $appExecutable"
    }

    Write-Host ''
    Write-Host 'REAL_TARGET SCENARIO — FIRST APP RUN' -ForegroundColor Cyan
    Write-Host '1. Open or create a disposable project/session in FCC Code Desktop.'
    Write-Host '2. Submit a harmless real provider-backed task that does not modify owner files, for example: Reply only with FCCD_P05_OWNER_TARGET_OK.'
    Write-Host '3. Confirm streamed assistant output and structured execution/activity are visible.'
    Write-Host '4. Submit a second harmless task, use Stop while it is running, then Retry and confirm the retry completes.'
    Write-Host '5. Close FCC Code Desktop normally.'
    Write-Host 'The application will start now. Do not enter credentials or secrets into the disposable test prompt.'

    $firstRun = Start-Process -FilePath $appExecutable -WorkingDirectory $root -PassThru
    $firstRun.WaitForExit()
    if ($firstRun.ExitCode -ne 0) {
        throw "First FCC Code Desktop run exited with code $($firstRun.ExitCode)."
    }

    $realProviderTaskCompleted = Read-YesNo 'Did a genuine provider-backed task complete successfully in the FCC Code Desktop conversation surface?'
    $structuredExecutionObserved = Read-YesNo 'Did you observe streamed assistant output plus structured execution/activity for that task?'
    $stopRetryObserved = Read-YesNo 'Did Stop interrupt a running harmless task and did Retry subsequently complete successfully?'

    Write-Host ''
    Write-Host 'REAL_TARGET SCENARIO — SECOND APP RUN' -ForegroundColor Cyan
    Write-Host 'Reopen the application, return to the same project/session, verify the prior messages/task history are present, and resume the session without data loss. Then close the app normally.'

    $secondRun = Start-Process -FilePath $appExecutable -WorkingDirectory $root -PassThru
    $secondRun.WaitForExit()
    if ($secondRun.ExitCode -ne 0) {
        throw "Second FCC Code Desktop run exited with code $($secondRun.ExitCode)."
    }

    $appClosedAndReopened = Read-YesNo 'Did FCC Code Desktop close normally and reopen successfully on the same exact build?'
    $sessionResumedWithDurableState = Read-YesNo 'After reopen, was the same session resumable with prior conversation/task state intact?'

    $checks = @{
        realProviderTaskCompleted = $realProviderTaskCompleted
        structuredExecutionObserved = $structuredExecutionObserved
        stopRetryObserved = $stopRetryObserved
        appClosedAndReopened = $appClosedAndReopened
        sessionResumedWithDurableState = $sessionResumedWithDurableState
    }

    $allPassed = -not ($checks.Values -contains $false)
    $status = if ($allPassed) { 'PASS' } else { 'FAIL' }
    Write-Evidence -Path $evidencePath -Sha $head -Status $status -Checks $checks

    if (-not $allPassed) {
        throw "P05 phase-exit REAL_TARGET scenario did not pass. Evidence recorded as FAIL at $evidencePath"
    }

    $rawEvidence = Get-Content -LiteralPath $evidencePath -Raw
    if ($rawEvidence -match '(?i)Authorization\s*:\s*Bearer\s+\S+' -or $rawEvidence -match '(?i)\bsk-[A-Za-z0-9_-]{12,}') {
        throw 'Potential plaintext secret detected in generated P05 owner evidence.'
    }

    Write-Host "P05_PHASE_EXIT_REAL_TARGET_PASS_RECONCILIATION_REQUIRED: $evidencePath"
}
finally {
    Pop-Location
}
