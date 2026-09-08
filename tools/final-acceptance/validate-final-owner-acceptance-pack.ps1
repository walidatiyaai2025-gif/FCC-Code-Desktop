[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RunNegativeFixtures
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-LastExitCode {
    param([string]$Stage)
    if ($LASTEXITCODE -ne 0) {
        throw "$Stage failed with exit code $LASTEXITCODE."
    }
}

function Get-EmbeddedQueue {
    param([string]$Text)
    $pattern = '(?s)<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->\s*```json\s*(.*?)\s*```\s*<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) {
        throw 'Canonical owner acceptance queue JSON block is missing or malformed.'
    }
    return ($match.Groups[1].Value | ConvertFrom-Json -Depth 30)
}

function Assert-NoSecretLikeMaterial {
    param([string]$Text, [string]$Label)
    foreach ($pattern in @(
        '(?i)Authorization\s*:\s*Bearer\s+[^\s"'';]+',
        '(?i)\b(?:sk|rk|pk|ghp|gho|ghu|ghs|github_pat|glpat)[-_][A-Za-z0-9_-]{12,}\b',
        '(?i)\b(api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|password|passwd)\b\s*[:=]\s*["'']?[A-Za-z0-9+/=_\-.]{8,}',
        '(?is)-----BEGIN [^-\r\n]*PRIVATE KEY-----'
    )) {
        if ($Text -match $pattern) {
            throw "$Label contains secret-shaped material."
        }
    }
}

if (-not $IsWindows) {
    throw 'Final owner acceptance pack cloud validation must run on Windows.'
}
foreach ($command in @('git','pwsh','dotnet')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Final owner acceptance pack validation requires '$command'."
    }
}

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$runnerPath = Join-Path $resolvedRoot 'tools\final-acceptance\run-final-owner-acceptance.ps1'
$policyValidatorPath = Join-Path $resolvedRoot 'tools\final-acceptance\validate-owner-last-policy.ps1'
$guidePath = Join-Path $resolvedRoot 'docs\FINAL_OWNER_ACCEPTANCE_GUIDE.md'
$queuePath = Join-Path $resolvedRoot 'docs\FINAL_OWNER_ACCEPTANCE_QUEUE.md'

foreach ($path in @($runnerPath, $policyValidatorPath, $guidePath, $queuePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Final owner acceptance pack file is missing: $path"
    }
}

$runnerText = Get-Content -LiteralPath $runnerPath -Raw
$guideText = Get-Content -LiteralPath $guidePath -Raw
$queueText = Get-Content -LiteralPath $queuePath -Raw
Assert-NoSecretLikeMaterial $guideText 'Final owner acceptance guide'
Assert-NoSecretLikeMaterial $queueText 'Canonical owner acceptance queue'

foreach ($literal in @(
    'CandidateSha',
    'Assert-CleanCandidateInputs',
    'Get-DeclaredPrerequisiteAssessment',
    'Protect-Text',
    'Assert-NoSecretMaterial',
    'Resume',
    'SelfTestOnly',
    'ownerExecutionClaimed',
    'overallStatus',
    "'PASS'",
    "'FAIL'",
    "'MISSING'",
    'FINAL_OWNER_EXECUTION_COMPLETE_RECONCILIATION_REQUIRED',
    'queue state remains QUEUED',
    'Unity',
    'Blender',
    'installer',
    'clean-machine'
)) {
    if (-not $runnerText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Final owner master runner is missing required invariant '$literal'."
    }
}

foreach ($literal in @(
    'single owner-facing acceptance workflow',
    'run-final-owner-acceptance.ps1',
    'CandidateSha',
    'FCC',
    'Unity',
    'Blender',
    'installer',
    'clean-machine',
    'FINAL_OWNER_ACCEPTANCE_SUMMARY.json',
    'SelfTestOnly'
)) {
    if (-not $guideText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Final owner acceptance guide is missing required coverage '$literal'."
    }
}

$queue = Get-EmbeddedQueue $queueText
if ($queue.schemaVersion -ne 1) {
    throw "Unsupported owner queue schemaVersion '$($queue.schemaVersion)'."
}
$queueItems = @($queue.items)
if ($queueItems.Count -lt 1) {
    throw 'Final owner acceptance queue unexpectedly contains no items.'
}

$policyArgs = @('-NoProfile', '-File', $policyValidatorPath, '-RepositoryRoot', $resolvedRoot)
if ($RunNegativeFixtures) {
    $policyArgs += '-RunNegativeFixtures'
}
& pwsh @policyArgs
Assert-LastExitCode 'Owner-last policy validation'

Push-Location $resolvedRoot
try {
    $head = (& git rev-parse HEAD 2>&1 | Out-String).Trim().ToLowerInvariant()
    Assert-LastExitCode 'Exact self-test HEAD resolution'
    if ($head -notmatch '^[0-9a-f]{40}$') {
        throw "Invalid exact self-test HEAD '$head'."
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'Self-test SDK version check'
    if ($sdkVersion -ne '10.0.400') {
        throw "Final owner pack self-test requires .NET SDK 10.0.400, got '$sdkVersion'."
    }

    $summaryRelative = 'evidence/final-owner/SELF_TEST_ONLY_SUMMARY.json'
    $summaryFull = Join-Path $resolvedRoot $summaryRelative.Replace('/', '\')
    if (Test-Path -LiteralPath $summaryFull) {
        Remove-Item -LiteralPath $summaryFull -Force
    }

    try {
        & pwsh -NoProfile -File $runnerPath `
            -CandidateSha $head `
            -RepositoryRoot $resolvedRoot `
            -SummaryPath $summaryRelative `
            -SelfTestOnly
        Assert-LastExitCode 'Final owner master runner SELF_TEST_ONLY'

        if (-not (Test-Path -LiteralPath $summaryFull -PathType Leaf)) {
            throw 'Final owner SELF_TEST_ONLY did not emit its machine-readable summary.'
        }
        $summaryRaw = Get-Content -LiteralPath $summaryFull -Raw
        Assert-NoSecretLikeMaterial $summaryRaw 'Final owner SELF_TEST_ONLY summary'
        $summary = $summaryRaw | ConvertFrom-Json -Depth 30

        if ($summary.schemaVersion -ne 1) { throw 'Final owner summary schemaVersion drifted.' }
        if ($summary.mode -ne 'SELF_TEST_ONLY') { throw 'Final owner self-test summary mode is not SELF_TEST_ONLY.' }
        if ($summary.selfTestStatus -ne 'PASS') { throw 'Final owner self-test summary did not report selfTestStatus=PASS.' }
        if ([bool]$summary.ownerExecutionClaimed) { throw 'Final owner self-test falsely claimed owner execution.' }

        $summaryItems = @($summary.items)
        if ($summaryItems.Count -ne $queueItems.Count) {
            throw 'Final owner summary does not contain every canonical queue item exactly once.'
        }
        foreach ($queueItem in $queueItems) {
            $matches = @($summaryItems | Where-Object id -eq $queueItem.id)
            if ($matches.Count -ne 1) {
                throw "Final owner summary does not contain exactly one '$($queueItem.id)' record."
            }
            $expectedStatus = if ($queueItem.state -eq 'PASS_INTEGRATED') { 'PASS' } else { 'MISSING' }
            if ($matches[0].status -ne $expectedStatus) {
                throw "Final owner SELF_TEST_ONLY status mismatch for '$($queueItem.id)': expected '$expectedStatus', got '$($matches[0].status)'."
            }
        }
        if (@($summaryItems | Where-Object status -eq 'FAIL').Count -gt 0) {
            throw 'Final owner SELF_TEST_ONLY produced a FAIL item.'
        }
    }
    finally {
        if (Test-Path -LiteralPath $summaryFull) {
            Remove-Item -LiteralPath $summaryFull -Force
        }
        $summaryDirectory = Split-Path -Parent $summaryFull
        if ((Test-Path -LiteralPath $summaryDirectory -PathType Container) -and
            @(Get-ChildItem -LiteralPath $summaryDirectory -Force).Count -eq 0) {
            Remove-Item -LiteralPath $summaryDirectory -Force
        }
    }

    $dirty = @(& git status --porcelain --untracked-files=all)
    Assert-LastExitCode 'Post-self-test worktree check'
    if (@($dirty | Where-Object { $_ }).Count -gt 0) {
        throw "Final owner pack cloud self-test left repository changes: $($dirty -join '; ')"
    }

    Write-Host 'FINAL_OWNER_ACCEPTANCE_PACK_CLOUD_VALIDATION_PASS'
}
finally {
    Pop-Location
}
