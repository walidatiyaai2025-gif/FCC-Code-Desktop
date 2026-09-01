[CmdletBinding()]
param(
    [string]$FccClaude,
    [switch]$AllowLivePrompt,
    [string]$CliArgsJson,
    [string]$StreamArgsJson,
    [string]$ResumeArgsJson,
    [int]$TimeoutMs = 45000,
    [int]$CancelAfterMs = 2000,
    [switch]$ExerciseDuplicateResume
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir '..\..')).Path
$TargetDir = Join-Path $RepoRoot 'evidence\phases\P00\target'
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
Push-Location $RepoRoot

try {
    $repoSha = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoSha)) {
        throw 'Unable to resolve repository HEAD.'
    }

    $steps = [System.Collections.Generic.List[object]]::new()

    function Add-StepResult {
        param(
            [string]$Name,
            [string]$Status,
            [int]$ExitCode,
            [string]$EvidencePath,
            [string]$Note
        )
        $steps.Add([ordered]@{
            name = $Name
            status = $Status
            exitCode = $ExitCode
            evidencePath = $EvidencePath
            note = $Note
        })
    }

    function Invoke-NodeStep {
        param(
            [string]$Name,
            [string]$ScriptPath,
            [string[]]$Arguments,
            [string]$EvidencePath
        )
        & node $ScriptPath @Arguments
        $code = $LASTEXITCODE
        $status = if ($code -eq 0) { 'PASS' } elseif ($code -eq 2) { 'BLOCKED' } else { 'FAIL' }
        Add-StepResult -Name $Name -Status $status -ExitCode $code -EvidencePath $EvidencePath -Note ''
        return $code
    }

    $fccSelfTest = Join-Path $RepoRoot 'tools\contract-probes\fcc\self-test.mjs'
    [void](Invoke-NodeStep -Name 'fcc-pr1-self-test' -ScriptPath $fccSelfTest -Arguments @() -EvidencePath '')

    $fccOutput = Join-Path $TargetDir 'fcc-discovery-cli.json'
    $fccArgs = @('--mode', 'all', '--json', $fccOutput, '--timeout-ms', [string]$TimeoutMs, '--cancel-after-ms', [string]$CancelAfterMs)
    if ($FccClaude) { $fccArgs += @('--fcc-claude', $FccClaude) }
    if ($AllowLivePrompt) { $fccArgs += '--allow-live-prompt' }
    if ($CliArgsJson) { $fccArgs += @('--cli-args-json', $CliArgsJson) }
    [void](Invoke-NodeStep -Name 'fcc-discovery-cli-target' -ScriptPath (Join-Path $RepoRoot 'tools\contract-probes\fcc\probe.mjs') -Arguments $fccArgs -EvidencePath $fccOutput)

    [void](Invoke-NodeStep -Name 'fcc-stream-session-failure-self-test' -ScriptPath (Join-Path $RepoRoot 'tools\contract-probes\fcc-runtime\self-test.mjs') -Arguments @() -EvidencePath '')

    $runtimeOutput = Join-Path $TargetDir 'fcc-stream-session-failure.json'
    $runtimeArgs = @('--mode', 'all', '--json', $runtimeOutput, '--timeout-ms', [string]$TimeoutMs, '--cancel-after-ms', [string]$CancelAfterMs)
    if ($FccClaude) { $runtimeArgs += @('--fcc-claude', $FccClaude) }
    if ($AllowLivePrompt) { $runtimeArgs += '--allow-live-prompt' }
    if ($CliArgsJson) { $runtimeArgs += @('--cli-args-json', $CliArgsJson) }
    if ($StreamArgsJson) { $runtimeArgs += @('--stream-args-json', $StreamArgsJson) }
    if ($ResumeArgsJson) { $runtimeArgs += @('--resume-args-json', $ResumeArgsJson) }
    if ($ExerciseDuplicateResume) { $runtimeArgs += '--exercise-duplicate-resume' }
    [void](Invoke-NodeStep -Name 'fcc-stream-session-failure-target' -ScriptPath (Join-Path $RepoRoot 'tools\contract-probes\fcc-runtime\probe.mjs') -Arguments $runtimeArgs -EvidencePath $runtimeOutput)

    # Unity and Blender are separate P00 worker scopes. Their workers must integrate
    # their target invocations into this canonical runner rather than Worker 2 guessing
    # executable paths, arguments, or evidence schemas for them.
    # These remain false until the separately authorized FCCD-P00-008 and FCCD-P00-009
    # workers explicitly add their invocations to this runner. Directory existence alone
    # is not treated as proof of unified-runner integration.
    $unityIntegrated = $false
    $blenderIntegrated = $false
    Add-StepResult -Name 'unity-target-probe-integration' -Status 'BLOCKED' -ExitCode 2 -EvidencePath '' -Note 'Separate FCCD-P00-008 worker must integrate its target invocation into this runner.'
    Add-StepResult -Name 'blender-target-probe-integration' -Status 'BLOCKED' -ExitCode 2 -EvidencePath '' -Note 'Separate FCCD-P00-009 worker must integrate its target invocation into this runner.'

    $manifest = [ordered]@{
        schemaVersion = 1
        probe = 'P00_TARGET_MACHINE_VALIDATION'
        capturedAtUtc = [DateTime]::UtcNow.ToString('o')
        repoSha = $repoSha
        host = [ordered]@{
            osVersion = [Environment]::OSVersion.VersionString
            is64BitOperatingSystem = [Environment]::Is64BitOperatingSystem
            powerShellVersion = $PSVersionTable.PSVersion.ToString()
        }
        livePromptAuthorized = [bool]$AllowLivePrompt
        suiteIntegration = [ordered]@{
            fccDiscoveryCli = $true
            fccStreamingSessionFailure = $true
            unity = $unityIntegrated
            blender = $blenderIntegrated
        }
        steps = $steps
    }

    $allPass = ($steps.Count -gt 0) -and (($steps | Where-Object { $_.status -ne 'PASS' }).Count -eq 0)
    $suiteComplete = $unityIntegrated -and $blenderIntegrated
    $manifest.overallStatus = if ($allPass -and $suiteComplete) { 'PASS' } elseif (($steps | Where-Object { $_.status -eq 'FAIL' }).Count -gt 0) { 'FAIL' } else { 'BLOCKED' }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $jsonPath = Join-Path $TargetDir 'P00_TARGET_EVIDENCE.json'
    $jsonText = $manifest | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($jsonPath, $jsonText + [Environment]::NewLine, $utf8NoBom)

    $mdPath = Join-Path $TargetDir 'P00_TARGET_EVIDENCE.md'
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('# P00 Target Machine Evidence')
    $lines.Add('')
    $lines.Add("- Repo SHA: ``$repoSha``")
    $lines.Add("- Captured UTC: ``$($manifest.capturedAtUtc)``")
    $lines.Add("- Overall status: **$($manifest.overallStatus)**")
    $lines.Add("- Live provider-backed prompt authorized: ``$([bool]$AllowLivePrompt)``")
    $lines.Add('')
    $lines.Add('## Steps')
    $lines.Add('')
    foreach ($step in $steps) {
        $line = "- $($step.name): **$($step.status)** (exit $($step.exitCode))"
        if ($step.evidencePath) { $line += " — $($step.evidencePath)" }
        if ($step.note) { $line += " — $($step.note)" }
        $lines.Add($line)
    }
    $lines.Add('')
    $lines.Add('Raw tool-specific JSON referenced above is produced by repository probes that redact credential-shaped values before persistence.')
    [System.IO.File]::WriteAllLines($mdPath, $lines, $utf8NoBom)

    Write-Host "P00 target validation status: $($manifest.overallStatus)"
    Write-Host "Manifest: $jsonPath"
    Write-Host "Summary:  $mdPath"

    if ($manifest.overallStatus -eq 'PASS') { exit 0 }
    if ($manifest.overallStatus -eq 'FAIL') { exit 1 }
    exit 2
}
finally {
    Pop-Location
}
