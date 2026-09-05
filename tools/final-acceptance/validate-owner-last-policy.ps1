[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RunNegativeFixtures
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-QueueDocumentFromText {
    param([string]$QueueText)

    $pattern = '(?s)<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->\s*```json\s*(.*?)\s*```\s*<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->'
    $match = [regex]::Match($QueueText, $pattern)
    if (-not $match.Success) {
        throw 'Canonical owner acceptance queue JSON block is missing or malformed.'
    }

    try {
        return ($match.Groups[1].Value | ConvertFrom-Json -Depth 30)
    }
    catch {
        throw "Canonical owner acceptance queue JSON is invalid: $($_.Exception.Message)"
    }
}

function Assert-RequiredProperty {
    param(
        [object]$Object,
        [string]$Name,
        [string]$ItemId
    )

    if ($Object.PSObject.Properties.Name -notcontains $Name) {
        throw "Owner queue item '$ItemId' is missing required property '$Name'."
    }
    $value = $Object.$Name
    if ($null -eq $value -or ($value -is [string] -and [string]::IsNullOrWhiteSpace($value))) {
        throw "Owner queue item '$ItemId' has an empty required property '$Name'."
    }
}

function Resolve-RepositoryPath {
    param(
        [string]$Root,
        [string]$RelativePath,
        [string]$Label
    )

    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath.Contains('..', [StringComparison]::Ordinal)) {
        throw "$Label must be repository-relative without traversal: $RelativePath"
    }
    $fullPath = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    $rootPrefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label escaped the repository root: $RelativePath"
    }
    return $fullPath
}

function Assert-OwnerLastContract {
    param(
        [string]$Root,
        [string]$PolicyText,
        [string]$QueueText,
        [string]$LedgerText,
        [string]$PhaseText,
        [string]$TargetRunnerText,
        [string]$FinalRunnerText,
        [bool]$CheckFilesystem
    )

    foreach ($literal in @(
        'scheduling amendment',
        'does **not** weaken',
        'A deferred source task is not `CLOSED`',
        'docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md',
        'P22 release closure and `VERIFIED_FINAL_COMPLETE=true` are prohibited',
        'No known code defect, failed CI, missing automated test, security defect, data-integrity defect, repairable repository problem, or missing implementation is being relabeled as owner-only.'
    )) {
        if (-not $PolicyText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Owner-last policy is missing required invariant: $literal"
        }
    }

    $queue = Get-QueueDocumentFromText $QueueText
    if ($queue.schemaVersion -ne 1) {
        throw "Unsupported owner queue schemaVersion '$($queue.schemaVersion)'."
    }

    $items = @($queue.items)
    if ($items.Count -lt 1) {
        throw 'Owner acceptance queue must contain at least the currently known P04 target obligation.'
    }

    $ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $allowedClassifications = @('REAL_TARGET', 'MANUAL_VISUAL', 'INSTALLER_LIFECYCLE', 'CLEAN_MACHINE', 'EXTERNAL_HARDWARE')
    $allowedStates = @('QUEUED', 'PASS_INTEGRATED')
    $queuedCount = 0

    foreach ($item in $items) {
        $itemId = if ($item.PSObject.Properties.Name -contains 'id') { [string]$item.id } else { '<missing-id>' }
        foreach ($property in @(
            'id', 'sourceTask', 'sourcePhase', 'classification', 'state', 'whyOwnerOnly',
            'cloudEvidence', 'command', 'prerequisites', 'expectedEvidencePath', 'passCriteria',
            'reconciliationRule', 'releaseBlocking'
        )) {
            Assert-RequiredProperty $item $property $itemId
        }

        if (-not $ids.Add([string]$item.id)) {
            throw "Duplicate owner queue id '$($item.id)'."
        }
        if ([string]$item.sourceTask -notmatch '^FCCD-P\d{2}-\d{3}$') {
            throw "Invalid source task id for '$($item.id)': $($item.sourceTask)"
        }
        if ([string]$item.sourcePhase -notmatch '^P\d{2}$') {
            throw "Invalid source phase for '$($item.id)': $($item.sourcePhase)"
        }
        if ($allowedClassifications -notcontains [string]$item.classification) {
            throw "Owner queue classification '$($item.classification)' is not an environment-bound classification."
        }
        if ($allowedStates -notcontains [string]$item.state) {
            throw "Owner queue state '$($item.state)' is unsupported."
        }
        if (-not [bool]$item.releaseBlocking) {
            throw "Owner queue item '$($item.id)' must remain releaseBlocking=true."
        }
        if ([string]$item.whyOwnerOnly -match '(?i)failed CI|code defect|missing implementation|missing test|security defect|data-integrity defect') {
            throw "Owner queue item '$($item.id)' appears to classify a repairable product/CI defect as owner-only."
        }

        $commandText = ([string]$item.command).Replace('/', '\')
        if (-not $commandText.StartsWith('.\tools\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $commandText.EndsWith('.ps1', [StringComparison]::OrdinalIgnoreCase) -or
            $commandText.Contains(' ', [StringComparison]::Ordinal)) {
            throw "Owner queue command for '$($item.id)' must be one tracked PowerShell script under .\tools\ with no inline shell arguments."
        }

        if ([string]$item.expectedEvidencePath -notmatch '^evidence/') {
            throw "Expected evidence path for '$($item.id)' must remain under evidence/."
        }
        if (@($item.prerequisites).Count -lt 1) {
            throw "Owner queue item '$($item.id)' must record prerequisites."
        }

        if ($CheckFilesystem) {
            $commandPath = Resolve-RepositoryPath $Root $commandText.Substring(2) "Command for $($item.id)"
            if (-not (Test-Path -LiteralPath $commandPath -PathType Leaf)) {
                throw "Tracked owner command is missing for '$($item.id)': $commandPath"
            }
            $cloudEvidencePath = Resolve-RepositoryPath $Root ([string]$item.cloudEvidence) "Cloud evidence for $($item.id)"
            if (-not (Test-Path -LiteralPath $cloudEvidencePath -PathType Leaf)) {
                throw "Cloud-complete evidence is missing for '$($item.id)': $cloudEvidencePath"
            }
            [void](Resolve-RepositoryPath $Root ([string]$item.expectedEvidencePath) "Expected evidence for $($item.id)")
        }

        if ($item.state -eq 'QUEUED') {
            $queuedCount++
            $closedPattern = '(?m)^\|\s*' + [regex]::Escape([string]$item.sourceTask) + '\s*\|.*\|\s*CLOSED\s*\|\s*$'
            if ([regex]::IsMatch($LedgerText, $closedPattern)) {
                throw "Queued owner source task '$($item.sourceTask)' is falsely marked CLOSED in the canonical ledger."
            }
        }
        else {
            if ($item.PSObject.Properties.Name -notcontains 'integratedEvidence' -or
                [string]::IsNullOrWhiteSpace([string]$item.integratedEvidence)) {
                throw "PASS_INTEGRATED owner item '$($item.id)' must record integratedEvidence."
            }
            if ($CheckFilesystem) {
                $integratedPath = Resolve-RepositoryPath $Root ([string]$item.integratedEvidence) "Integrated evidence for $($item.id)"
                if (-not (Test-Path -LiteralPath $integratedPath -PathType Leaf)) {
                    throw "PASS_INTEGRATED evidence is missing for '$($item.id)': $integratedPath"
                }
            }
        }
    }

    $p04Items = @($items | Where-Object id -eq 'OWNER-P04-008-REAL-TARGET')
    if ($p04Items.Count -ne 1 -or $p04Items[0].sourceTask -ne 'FCCD-P04-008' -or
        $p04Items[0].classification -ne 'REAL_TARGET') {
        throw 'Current P04-008 REAL_TARGET obligation is missing or has been weakened in the owner queue.'
    }

    if ($queuedCount -gt 0 -and $PhaseText.Contains('VERIFIED_FINAL_COMPLETE: true', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'VERIFIED_FINAL_COMPLETE cannot be true while owner acceptance items remain QUEUED.'
    }

    foreach ($literal in @(
        'docs\FINAL_OWNER_ACCEPTANCE_QUEUE.md',
        'OWNER-P04-008-REAL-TARGET',
        'FCCD-P04-008',
        'REAL_TARGET'
    )) {
        if (-not $TargetRunnerText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) {
            throw "P04 target runner is missing queued owner authorization guard: $literal"
        }
    }
    if (-not $TargetRunnerText.Contains('isP04Current', [StringComparison]::Ordinal) -or
        -not $TargetRunnerText.Contains('isQueuedOwnerAcceptance', [StringComparison]::Ordinal)) {
        throw 'P04 target runner must authorize execution only from current P04 or a valid queued owner item.'
    }

    foreach ($literal in @(
        'Final owner acceptance must run on the authoritative owner Windows environment.',
        'FINAL_OWNER_EXECUTION_COMPLETE_RECONCILIATION_REQUIRED',
        'queue state remains QUEUED',
        'testedRepoSha',
        'overallStatus'
    )) {
        if (-not $FinalRunnerText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Final owner runner is missing fail-closed behavior: $literal"
        }
    }
}

function Assert-Rejected {
    param(
        [scriptblock]$Action,
        [string]$Label
    )

    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) {
        throw "Owner-last negative fixture was not rejected: $Label"
    }
}

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$policyPath = Join-Path $root 'docs\OWNER_LAST_EXECUTION_POLICY.md'
$queuePath = Join-Path $root 'docs\FINAL_OWNER_ACCEPTANCE_QUEUE.md'
$ledgerPath = Join-Path $root 'docs\TASK_LEDGER.md'
$phasePath = Join-Path $root 'CURRENT_PHASE.md'
$targetRunnerPath = Join-Path $root 'tools\runtime\run-p04-runtime-target-validation.ps1'
$finalRunnerPath = Join-Path $root 'tools\final-acceptance\run-final-owner-acceptance.ps1'

foreach ($path in @($policyPath, $queuePath, $ledgerPath, $phasePath, $targetRunnerPath, $finalRunnerPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required owner-last governance path is missing: $path"
    }
}

$policyText = Get-Content -LiteralPath $policyPath -Raw
$queueText = Get-Content -LiteralPath $queuePath -Raw
$ledgerText = Get-Content -LiteralPath $ledgerPath -Raw
$phaseText = Get-Content -LiteralPath $phasePath -Raw
$targetRunnerText = Get-Content -LiteralPath $targetRunnerPath -Raw
$finalRunnerText = Get-Content -LiteralPath $finalRunnerPath -Raw

Assert-OwnerLastContract $root $policyText $queueText $ledgerText $phaseText $targetRunnerText $finalRunnerText $true
Write-Host 'Static owner-last execution governance validation: PASS.'

if ($RunNegativeFixtures) {
    Assert-Rejected {
        Assert-OwnerLastContract $root $policyText ($queueText.Replace('OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN', 'OWNER_QUEUE_REMOVED')) $ledgerText $phaseText $targetRunnerText $finalRunnerText $false
    } 'missing canonical queue JSON markers'

    Assert-Rejected {
        $badQueue = $queueText.Replace('"classification": "REAL_TARGET"', '"classification": "CODE_DEFECT"')
        Assert-OwnerLastContract $root $policyText $badQueue $ledgerText $phaseText $targetRunnerText $finalRunnerText $false
    } 'repairable defect classification'

    Assert-Rejected {
        $badQueue = $queueText.Replace('"command": ".\\tools\\runtime\\run-p04-runtime-target-validation.ps1"', '"command": ""')
        Assert-OwnerLastContract $root $policyText $badQueue $ledgerText $phaseText $targetRunnerText $finalRunnerText $false
    } 'missing tracked owner command'

    Assert-Rejected {
        $badQueue = $queueText.Replace('"state": "QUEUED"', '"state": "PASS_INTEGRATED"')
        Assert-OwnerLastContract $root $policyText $badQueue $ledgerText $phaseText $targetRunnerText $finalRunnerText $false
    } 'PASS_INTEGRATED without integrated evidence'

    Assert-Rejected {
        $badLedger = $ledgerText.Replace('| FCCD-P04-008 | Runtime contract suite | PENDING |', '| FCCD-P04-008 | Runtime contract suite | CLOSED |')
        Assert-OwnerLastContract $root $policyText $queueText $badLedger $phaseText $targetRunnerText $finalRunnerText $false
    } 'queued source task falsely closed'

    Assert-Rejected {
        $badPhase = $phaseText.Replace('VERIFIED_FINAL_COMPLETE: false', 'VERIFIED_FINAL_COMPLETE: true')
        Assert-OwnerLastContract $root $policyText $queueText $ledgerText $badPhase $targetRunnerText $finalRunnerText $false
    } 'final completion while queue unresolved'

    Assert-Rejected {
        $badTargetRunner = $targetRunnerText.Replace('OWNER-P04-008-REAL-TARGET', 'OWNER-P04-008-REMOVED')
        Assert-OwnerLastContract $root $policyText $queueText $ledgerText $phaseText $badTargetRunner $finalRunnerText $false
    } 'P04 target runner queue authorization removed'

    Write-Host 'Owner-last negative fixtures: PASS.'
}
