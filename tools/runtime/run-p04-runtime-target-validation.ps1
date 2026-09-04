[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$EvidencePath = 'evidence/phases/P04/runtime-contract/P04_RUNTIME_TARGET_EVIDENCE.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-LastExitCode {
    param([string]$Stage)
    if ($LASTEXITCODE -ne 0) {
        throw "$Stage failed with exit code $LASTEXITCODE."
    }
}

if (-not $IsWindows) {
    throw 'Authoritative P04 runtime target validation must run on the owner Windows target.'
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required for exact-head P04 target provenance.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet is required for the P04 target harness.'
}

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
Push-Location $resolvedRoot
try {
    $gitRoot = (& git rev-parse --show-toplevel 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'Repository identity check'
    if ([IO.Path]::GetFullPath($gitRoot).TrimEnd('\') -ne [IO.Path]::GetFullPath($resolvedRoot).TrimEnd('\')) {
        throw "P04 target runner resolved the wrong repository. Expected '$resolvedRoot', got '$gitRoot'."
    }

    $head = (& git rev-parse HEAD 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'Exact HEAD resolution'
    if ($head -notmatch '^[0-9a-f]{40}$') {
        throw "P04 target runner resolved an invalid HEAD SHA: '$head'."
    }

    $statusLines = @(& git status --porcelain --untracked-files=all)
    Assert-LastExitCode 'Exact worktree status check'
    $disallowedStatus = @(
        $statusLines | Where-Object {
            $_ -and
            -not $_.Replace('\\', '/').Contains('evidence/phases/P04/runtime-contract/', [StringComparison]::OrdinalIgnoreCase)
        }
    )
    if ($disallowedStatus.Count -gt 0) {
        throw "Authoritative P04 target validation requires exact HEAD source inputs. Disallowed worktree changes: $($disallowedStatus -join '; ')"
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'SDK version check'
    if ($sdkVersion -ne '10.0.400') {
        throw "P04 target validation requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $phaseText = Get-Content -LiteralPath (Join-Path $resolvedRoot 'CURRENT_PHASE.md') -Raw
    if (-not $phaseText.Contains('CURRENT_PHASE: P04')) {
        throw 'P04 target validation cannot run as authoritative closure evidence when P04 is not current.'
    }
    if (-not $phaseText.Contains('FCCD-P04-008')) {
        throw 'CURRENT_PHASE.md does not retain FCCD-P04-008 target-validation ownership.'
    }

    $harnessProject = Join-Path $resolvedRoot 'tools\runtime\P04RuntimeTargetHarness\P04RuntimeTargetHarness.csproj'
    if (-not (Test-Path -LiteralPath $harnessProject)) {
        throw "P04 target harness is missing: $harnessProject"
    }

    $fullEvidencePath = if ([IO.Path]::IsPathRooted($EvidencePath)) {
        [IO.Path]::GetFullPath($EvidencePath)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $resolvedRoot $EvidencePath))
    }
    $allowedEvidenceRoot = [IO.Path]::GetFullPath(
        (Join-Path $resolvedRoot 'evidence\phases\P04\runtime-contract'))
    if (-not $fullEvidencePath.StartsWith($allowedEvidenceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'P04 target evidence must stay under evidence/phases/P04/runtime-contract/.'
    }
    [void](New-Item -ItemType Directory -Path (Split-Path -Parent $fullEvidencePath) -Force)

    & dotnet run --project $harnessProject -c Release -- `
        --evidence $fullEvidencePath `
        --classification REAL_TARGET `
        --expected-sha $head
    $harnessExitCode = $LASTEXITCODE

    if (-not (Test-Path -LiteralPath $fullEvidencePath)) {
        throw 'P04 target harness returned without producing sanitized target evidence.'
    }

    $evidence = Get-Content -LiteralPath $fullEvidencePath -Raw | ConvertFrom-Json
    if ($evidence.task -ne 'FCCD-P04-008') {
        throw "Unexpected P04 target evidence task marker: $($evidence.task)"
    }
    if ($evidence.evidenceClassification -ne 'REAL_TARGET') {
        throw 'Authoritative P04 evidence must be classified REAL_TARGET.'
    }
    if ($evidence.testedRepoSha -ne $head) {
        throw "P04 target evidence SHA '$($evidence.testedRepoSha)' does not equal exact HEAD '$head'."
    }
    if ($evidence.rateLimitObservation -ne 'NOT_INDUCED') {
        throw 'P04 target validation does not manufacture provider 429/rate-limit traffic.'
    }

    $expectedScenarios = @(
        'structured_success_stream_session',
        'structured_resume',
        'structured_invalid_session_failure',
        'structured_cancellation',
        'fallback_after_structured_failure'
    )
    foreach ($scenarioName in $expectedScenarios) {
        $scenario = @($evidence.scenarios | Where-Object name -eq $scenarioName)
        if ($scenario.Count -ne 1) {
            throw "P04 target evidence is missing unique scenario '$scenarioName'."
        }
        if ($scenario[0].status -ne 'PASS') {
            throw "P04 target scenario '$scenarioName' did not PASS: $($scenario[0].observation)"
        }
    }

    if ($harnessExitCode -ne 0 -or $evidence.overallStatus -ne 'PASS') {
        throw "P04 target runtime contract suite did not pass. Harness exit=$harnessExitCode status=$($evidence.overallStatus)."
    }

    $summaryPath = [IO.Path]::ChangeExtension($fullEvidencePath, '.md')
    $summary = @"
# P04 Runtime Contract Target Evidence

- Task: `FCCD-P04-008`
- Evidence classification: `REAL_TARGET`
- Tested repository SHA: `$head`
- Overall status: `PASS`
- Runtime version: `$($evidence.runtimeVersion)`
- FCC loopback health observation: `$($evidence.loopbackHealth)`
- Rate-limit observation: `NOT_INDUCED` (no provider 429 was manufactured)
- Scenarios: structured success/stream/session, resume, invalid-session failure, cancellation, fallback-after-failure — all PASS.

This file is a sanitized summary of `$(Split-Path -Leaf $fullEvidencePath)`. It is target evidence only; a convergence worker must still verify ancestry, integrate the evidence, reconcile `FCCD-P04-008`, and run the exact-head P04 exit gate before P05 may begin.
"@
    Set-Content -LiteralPath $summaryPath -Value $summary -Encoding utf8NoBOM

    Write-Host "P04 authoritative runtime target contract suite: PASS on $head"
    Write-Host "Evidence: $fullEvidencePath"
    Write-Host "Summary:  $summaryPath"
}
finally {
    Pop-Location
}
