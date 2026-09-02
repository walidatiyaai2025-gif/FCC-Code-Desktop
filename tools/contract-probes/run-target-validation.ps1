[CmdletBinding()]
param(
    [string]$FccClaude,
    [switch]$AllowLivePrompt,
    [string]$CliArgsJson,
    [string]$StreamArgsJson,
    [string]$ResumeArgsJson,
    [int]$TimeoutMs = 45000,
    [int]$CancelAfterMs = 2000,
    [switch]$ExerciseDuplicateResume,
    [string]$UnityEditor,
    [string]$UnityHub,
    [string]$UnityProject,
    [string]$UnityFixtureRoot,
    [int]$UnityTimeoutMs = 300000,
    [int]$UnityCancelAfterMs = 3000,
    [switch]$KeepUnityFixture,
    [string]$BlenderExecutable,
    [string]$BlenderFixtureRoot,
    [int]$BlenderTimeoutMs = 180000,
    [int]$BlenderCancelAfterMs = 2500,
    [switch]$KeepBlenderFixture
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir '..\..')).Path
Push-Location $RepoRoot

try {
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
        throw 'WRONG_EXECUTION_ENVIRONMENT: P00 target validation must run on the owner Windows target. Cloud/Linux execution must not produce target evidence.'
    }

    $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    if (-not $gitCommand) { throw 'TARGET_PREREQUISITE_MISSING: git is required for exact-head target evidence.' }
    $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
    if (-not $nodeCommand) { throw 'TARGET_PREREQUISITE_MISSING: node is required to execute the canonical P00 probes.' }

    $gitRoot = (& git rev-parse --show-toplevel).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitRoot)) { throw 'Unable to resolve repository root.' }
    $expectedRoot = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd('\')
    $actualRoot = [System.IO.Path]::GetFullPath($gitRoot).TrimEnd('\')
    if (-not [string]::Equals($expectedRoot, $actualRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "WRONG_REPOSITORY_CHECKOUT: expected $expectedRoot but git resolved $actualRoot."
    }

    $repoSha = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoSha)) { throw 'Unable to resolve repository HEAD.' }

    # Exact-head attribution applies to source/configuration inputs. The runner's own
    # target-evidence outputs are intentionally overwriteable so a previous target run
    # does not make the next deterministic rerun impossible.
    $runnerEvidenceExclude = ':(exclude)evidence/phases/P00/target/**'
    $sourceDirtyEntries = @(& git status --porcelain=v1 --untracked-files=all -- . $runnerEvidenceExclude)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to verify repository worktree state.' }
    if ($sourceDirtyEntries.Count -gt 0) {
        throw 'EXACT_HEAD_REQUIRED: target validation refuses source/configuration worktree changes outside repository-owned target-evidence outputs because they cannot be attributed to the recorded repo SHA.'
    }

    $gitVersion = (& git --version).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve git version.' }
    $nodeVersion = (& node --version).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve Node.js version.' }

    # P00-005 is already closed from integrated exact-head Windows evidence. The final
    # target manifest may reuse it only when its tested source commit remains in the
    # ancestry of the exact current HEAD. Missing/malformed/non-ancestor evidence is
    # deliberately passed to the summarizer as unsatisfied rather than silently trusted.
    $integratedFailureOutput = Join-Path $RepoRoot 'evidence\phases\P00\failure\fcc-failure-target-exact-head.json'
    $integratedFailureSourceIsAncestor = $false
    if (Test-Path $integratedFailureOutput) {
        try {
            $integratedFailureEvidence = Get-Content -Raw -Path $integratedFailureOutput | ConvertFrom-Json
            $integratedFailureSourceSha = [string]$integratedFailureEvidence.testedSourceSha
            if ($integratedFailureSourceSha -match '^[0-9a-fA-F]{40}$') {
                & git merge-base --is-ancestor $integratedFailureSourceSha $repoSha
                $integratedFailureSourceIsAncestor = ($LASTEXITCODE -eq 0)
            }
        } catch {
            $integratedFailureSourceIsAncestor = $false
        }
    }

    $TargetDir = Join-Path $RepoRoot 'evidence\phases\P00\target'
    New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
    $steps = [System.Collections.Generic.List[object]]::new()

    function Add-StepResult {
        param([string]$Name, [string]$Status, [int]$ExitCode, [string]$EvidencePath, [string]$Note)
        $steps.Add([ordered]@{ name = $Name; status = $Status; exitCode = $ExitCode; evidencePath = $EvidencePath; note = $Note })
    }

    function Invoke-NodeStep {
        param([string]$Name, [string]$ScriptPath, [string[]]$Arguments, [string]$EvidencePath)
        & node $ScriptPath @Arguments
        $code = $LASTEXITCODE
        $status = if ($code -eq 0) { 'PASS' } elseif ($code -eq 2) { 'BLOCKED' } else { 'FAIL' }
        Add-StepResult -Name $Name -Status $status -ExitCode $code -EvidencePath $EvidencePath -Note ''
        return $code
    }

    $fccSelfTest = Join-Path $RepoRoot 'tools\contract-probes\fcc\self-test.mjs'
    [void](Invoke-NodeStep -Name 'fcc-pr1-self-test' -ScriptPath $fccSelfTest -Arguments @() -EvidencePath '')

    $summarySelfTest = Join-Path $RepoRoot 'tools\contract-probes\target-evidence-summary-self-test.mjs'
    [void](Invoke-NodeStep -Name 'target-evidence-summary-self-test' -ScriptPath $summarySelfTest -Arguments @() -EvidencePath '')

    $fccOutput = Join-Path $TargetDir 'fcc-discovery-cli.json'
    $fccArgs = @('--mode', 'all', '--json', $fccOutput, '--timeout-ms', [string]$TimeoutMs, '--cancel-after-ms', [string]$CancelAfterMs)
    if ($FccClaude) { $fccArgs += @('--fcc-claude', $FccClaude) }
    if ($AllowLivePrompt) { $fccArgs += '--allow-live-prompt' }
    if ($CliArgsJson) { $fccArgs += @('--cli-args-json', $CliArgsJson) }
    [void](Invoke-NodeStep -Name 'fcc-discovery-cli-target' -ScriptPath (Join-Path $RepoRoot 'tools\contract-probes\fcc\probe.mjs') -Arguments $fccArgs -EvidencePath $fccOutput)
    $fccTargetCode = [int]$steps[$steps.Count - 1].exitCode

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
    $runtimeTargetCode = [int]$steps[$steps.Count - 1].exitCode

    # FCCD-P00-008: self-test proves repository-owned logic only; target probe returns exit 2 if Unity is missing or mandatory evidence is incomplete.
    [void](Invoke-NodeStep -Name 'unity-contract-self-test' -ScriptPath (Join-Path $RepoRoot 'tools\contract-probes\unity\self-test.mjs') -Arguments @() -EvidencePath '')

    $unityOutput = Join-Path $TargetDir 'unity-contract.json'
    $unityArgs = @('--mode', 'all', '--json', $unityOutput, '--timeout-ms', [string]$UnityTimeoutMs, '--cancel-after-ms', [string]$UnityCancelAfterMs)
    if ($UnityEditor) { $unityArgs += @('--unity', $UnityEditor) }
    if ($UnityHub) { $unityArgs += @('--hub', $UnityHub) }
    if ($UnityProject) { $unityArgs += @('--project', $UnityProject) }
    if ($UnityFixtureRoot) { $unityArgs += @('--fixture-root', $UnityFixtureRoot) }
    if ($KeepUnityFixture) { $unityArgs += '--keep-fixture' }
    [void](Invoke-NodeStep -Name 'unity-contract-target' -ScriptPath (Join-Path $RepoRoot 'tools\contract-probes\unity\probe.mjs') -Arguments $unityArgs -EvidencePath $unityOutput)
    $unityTargetCode = [int]$steps[$steps.Count - 1].exitCode
    $unityIntegrated = $true

    # FCCD-P00-009: repository logic is self-tested separately from real target evidence.
    [void](Invoke-NodeStep -Name 'blender-contract-self-test' -ScriptPath (Join-Path $RepoRoot 'tools\contract-probes\blender\self-test.mjs') -Arguments @() -EvidencePath '')
    $blenderOutput = Join-Path $TargetDir 'blender-contract.json'
    $blenderArgs = @('--mode', 'all', '--json', $blenderOutput, '--timeout-ms', [string]$BlenderTimeoutMs, '--cancel-after-ms', [string]$BlenderCancelAfterMs)
    if ($BlenderExecutable) { $blenderArgs += @('--blender', $BlenderExecutable) }
    if ($BlenderFixtureRoot) { $blenderArgs += @('--fixture-root', $BlenderFixtureRoot) }
    if ($KeepBlenderFixture) { $blenderArgs += '--keep-fixture' }
    [void](Invoke-NodeStep -Name 'blender-contract-target' -ScriptPath (Join-Path $RepoRoot 'tools\contract-probes\blender\probe.mjs') -Arguments $blenderArgs -EvidencePath $blenderOutput)
    $blenderTargetCode = [int]$steps[$steps.Count - 1].exitCode
    $blenderIntegrated = $true

    # Build a compact contract summary from sanitized tool-specific evidence. Probe exit
    # codes are inputs, but readable mandatory evidence may still fail closed when its
    # content contradicts those exits (for example missing Blender with an impossible 0).
    $contractSummaryPath = Join-Path $TargetDir 'P00_TARGET_CONTRACT_SUMMARY.json'
    $contractSummaryScript = Join-Path $RepoRoot 'tools\contract-probes\target-evidence-summary.mjs'
    $contractSummaryArgs = @(
        '--repo-root', $RepoRoot,
        '--authoritative-target',
        '--fcc-file', $fccOutput, '--fcc-exit', [string]$fccTargetCode,
        '--runtime-file', $runtimeOutput, '--runtime-exit', [string]$runtimeTargetCode,
        '--unity-file', $unityOutput, '--unity-exit', [string]$unityTargetCode,
        '--blender-file', $blenderOutput, '--blender-exit', [string]$blenderTargetCode,
        '--integrated-failure-file', $integratedFailureOutput,
        '--output', $contractSummaryPath
    )
    if ($integratedFailureSourceIsAncestor) { $contractSummaryArgs += '--integrated-failure-source-is-ancestor' }
    & node $contractSummaryScript @contractSummaryArgs
    $contractSummaryExit = $LASTEXITCODE
    if ($contractSummaryExit -ne 0) {
        throw "TARGET_EVIDENCE_SUMMARY_FAILED: summarizer exit $contractSummaryExit."
    }
    $contractSummary = Get-Content -Raw -Path $contractSummaryPath | ConvertFrom-Json

    # The summary generator itself can execute successfully while reporting fail-closed
    # contract/readiness states. Those states must affect the unified runner result.
    $contractStatuses = @($contractSummary.contracts.PSObject.Properties | ForEach-Object { [string]$_.Value.status })
    $summaryStatus = 'PASS'
    if (($contractStatuses -contains 'FAIL') -or ([string]$contractSummary.p00Readiness.p00_005.status -eq 'FAIL') -or ([string]$contractSummary.p00Readiness.pg_002.status -ne 'RESOLVED')) {
        $summaryStatus = 'FAIL'
    } elseif (($contractStatuses -contains 'BLOCKED') -or ([string]$contractSummary.p00Readiness.p00_009.status -eq 'BLOCKED')) {
        $summaryStatus = 'BLOCKED'
    }
    $summaryExitCode = if ($summaryStatus -eq 'PASS') { 0 } elseif ($summaryStatus -eq 'BLOCKED') { 2 } else { 1 }
    Add-StepResult -Name 'target-evidence-summary' -Status $summaryStatus -ExitCode $summaryExitCode -EvidencePath $contractSummaryPath -Note 'schemaVersion=2; fail-closed contract and P00 readiness metadata'

    $manifest = [ordered]@{
        schemaVersion = 2
        probe = 'P00_TARGET_MACHINE_VALIDATION'
        capturedAtUtc = [DateTime]::UtcNow.ToString('o')
        repoSha = $repoSha
        host = [ordered]@{
            osVersion = [Environment]::OSVersion.VersionString
            is64BitOperatingSystem = [Environment]::Is64BitOperatingSystem
            powerShellVersion = $PSVersionTable.PSVersion.ToString()
            gitVersion = $gitVersion
            nodeVersion = $nodeVersion
        }
        livePromptAuthorized = [bool]$AllowLivePrompt
        suiteIntegration = [ordered]@{
            fccDiscoveryCli = $true
            fccStreamingSessionFailure = $true
            unity = $unityIntegrated
            blender = $blenderIntegrated
        }
        contractSummarySchemaVersion = $contractSummary.schemaVersion
        contracts = $contractSummary.contracts
        p00Readiness = $contractSummary.p00Readiness
        steps = $steps
    }

    $allPass = ($steps.Count -gt 0) -and (($steps | Where-Object { $_.status -ne 'PASS' }).Count -eq 0)
    $suiteComplete = $unityIntegrated -and $blenderIntegrated
    $manifest.overallStatus = if ($allPass -and $suiteComplete -and $contractSummary.p00Readiness.p00TargetValidationComplete) { 'PASS' } elseif (($steps | Where-Object { $_.status -eq 'FAIL' }).Count -gt 0) { 'FAIL' } else { 'BLOCKED' }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $jsonPath = Join-Path $TargetDir 'P00_TARGET_EVIDENCE.json'
    $jsonText = $manifest | ConvertTo-Json -Depth 12
    [System.IO.File]::WriteAllText($jsonPath, $jsonText + [Environment]::NewLine, $utf8NoBom)

    $mdPath = Join-Path $TargetDir 'P00_TARGET_EVIDENCE.md'
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('# P00 Target Machine Evidence')
    $lines.Add('')
    $lines.Add("- Repo SHA: ``$repoSha``")
    $lines.Add("- Captured UTC: ``$($manifest.capturedAtUtc)``")
    $lines.Add("- Overall status: **$($manifest.overallStatus)**")
    $lines.Add("- Live provider-backed prompt authorized: ``$([bool]$AllowLivePrompt)``")
    $lines.Add("- P00-005 integrated exact-head evidence: **$($contractSummary.p00Readiness.p00_005.status)** - ``$($contractSummary.p00Readiness.p00_005.artifactPath)``")
    $lines.Add("- PG-002 policy: **$($contractSummary.p00Readiness.pg_002.status)** / ``$($contractSummary.p00Readiness.pg_002.observationState)`` - actual 429 observed: ``$($contractSummary.p00Readiness.pg_002.actual429Observed)``")
    $lines.Add("- P00-009 closure support: **$($contractSummary.p00Readiness.p00_009.status)** - ``$($contractSummary.p00Readiness.p00_009.artifactPath)``")
    $lines.Add('')
    $lines.Add('## Contract summary')
    $lines.Add('')
    foreach ($property in $contractSummary.contracts.PSObject.Properties) {
        $contract = $property.Value
        $line = "- $($property.Name): **$($contract.status)** / ``$($contract.resultState)`` - $($contract.reason) - authoritative target execution: ``$($contract.executedOnAuthoritativeTarget)`` - target behavior observed: ``$($contract.targetBehaviorObserved)``"
        if ($contract.artifactPath) { $line += " - evidence: ``$($contract.artifactPath)``" }
        $lines.Add($line)
    }
    $lines.Add('')
    $lines.Add('## Steps')
    $lines.Add('')
    foreach ($step in $steps) {
        $line = "- $($step.name): **$($step.status)** (exit $($step.exitCode))"
        if ($step.evidencePath) { $line += " - $($step.evidencePath)" }
        if ($step.note) { $line += " - $($step.note)" }
        $lines.Add($line)
    }
    $lines.Add('')
    $lines.Add('Raw tool-specific JSON referenced above is produced by repository probes that redact credential-shaped values before persistence. The compact contract summary copies controlled classifications and bounded version/path metadata only; it does not copy raw provider output.')
    [System.IO.File]::WriteAllLines($mdPath, $lines, $utf8NoBom)

    Write-Host "P00 target validation status: $($manifest.overallStatus)"
    Write-Host "Manifest: $jsonPath"
    Write-Host "Summary:  $mdPath"

    if ($manifest.overallStatus -eq 'PASS') { exit 0 }
    if ($manifest.overallStatus -eq 'FAIL') { exit 1 }
    exit 2
}
finally { Pop-Location }
