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

function Get-PhaseField {
    param(
        [string]$PhaseText,
        [string]$Name
    )

    $match = [regex]::Match($PhaseText, '(?m)^' + [regex]::Escape($Name) + ':\s*(.*?)\s*$')
    if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) {
        throw "CURRENT_PHASE.md is missing required field '$Name'."
    }

    return $match.Groups[1].Value.Trim()
}

function Get-ControlField {
    param(
        [string]$ControlText,
        [string]$Name
    )

    $match = [regex]::Match($ControlText, '(?m)^' + [regex]::Escape($Name) + ':\s*(.*?)\s*$')
    if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) {
        throw "PROJECT_CONTROL.md is missing required owner-last state field '$Name'."
    }

    return $match.Groups[1].Value.Trim()
}

function Assert-ProjectControlAligned {
    param(
        [string]$PhaseText,
        [string]$ProjectControlText
    )

    foreach ($field in @(
        'CURRENT_PHASE',
        'CURRENT_PHASE_NAME',
        'CURRENT_PHASE_STATE',
        'NEXT_PHASE',
        'PHASE_EXIT_GATE',
        'KNOWN_RELEASE_BLOCKERS',
        'VERIFIED_FINAL_COMPLETE',
        'OWNER_LAST_MODE',
        'DEFERRED_OWNER_ACCEPTANCE_COUNT',
        'DEFERRED_OWNER_ACCEPTANCE_ITEMS'
    )) {
        $phaseValue = Get-PhaseField $PhaseText $field
        $controlValue = Get-ControlField $ProjectControlText $field
        if (-not $phaseValue.Equals($controlValue, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Canonical state drift: CURRENT_PHASE.md has $field='$phaseValue' but PROJECT_CONTROL.md has '$controlValue'."
        }
    }

    if (-not $ProjectControlText.Contains('docs/OWNER_LAST_EXECUTION_POLICY.md', [StringComparison]::OrdinalIgnoreCase) -or
        -not $ProjectControlText.Contains('docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md', [StringComparison]::OrdinalIgnoreCase) -or
        -not $ProjectControlText.Contains('P22 remains prohibited while any required owner queue item is `QUEUED`', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'PROJECT_CONTROL.md is missing required owner-last scheduling/release-blocking invariants.'
    }
}

function Convert-PhaseToNumber {
    param(
        [string]$Phase,
        [string]$Label
    )

    $match = [regex]::Match($Phase, '^P(\d{2})$')
    if (-not $match.Success) {
        throw "$Label has invalid phase value '$Phase'."
    }

    return [int]$match.Groups[1].Value
}

function Get-LedgerRows {
    param([string]$LedgerText)

    $pattern = '(?m)^\|\s*(FCCD-P(\d{2})-\d{3})\s*\|[^|\r\n]*\|\s*(PENDING|CLAIMED|IN_PROGRESS|BLOCKED|IMPLEMENTED|VERIFIED|CLOSED)\s*\|\s*$'
    $rows = [System.Collections.Generic.List[object]]::new()
    foreach ($match in [regex]::Matches($LedgerText, $pattern)) {
        $rows.Add([pscustomobject]@{
            TaskId = $match.Groups[1].Value
            PhaseNumber = [int]$match.Groups[2].Value
            State = $match.Groups[3].Value
        })
    }

    if ($rows.Count -eq 0) {
        throw 'Canonical task ledger rows could not be parsed.'
    }

    return $rows
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
        'controls **scheduling only**',
        'CURRENT_PHASE.md',
        'PROJECT_CONTROL.md',
        'docs/TASK_LEDGER.md',
        'one current cloud implementation phase',
        'every earlier non-CLOSED task has exactly one valid `QUEUED` owner item',
        'P22 is the final release/acceptance closure phase',
        'A deferred source task is not `CLOSED`',
        'docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md',
        'VERIFIED_FINAL_COMPLETE=true'
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
    $queuedItems = [System.Collections.Generic.List[object]]::new()

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
        if ([string]$item.whyOwnerOnly -match '(?i)failed\s+CI|code\s+defect|missing\s+implementation|missing\s+(automated\s+)?test|security\s+defect|data[- ]integrity\s+defect|repairable\s+repository') {
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
            $queuedItems.Add($item)
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

    $currentPhase = Get-PhaseField $PhaseText 'CURRENT_PHASE'
    $currentPhaseNumber = Convert-PhaseToNumber $currentPhase 'CURRENT_PHASE'
    $ownerLastMode = Get-PhaseField $PhaseText 'OWNER_LAST_MODE'
    $knownReleaseBlockersText = Get-PhaseField $PhaseText 'KNOWN_RELEASE_BLOCKERS'
    $deferredCountText = Get-PhaseField $PhaseText 'DEFERRED_OWNER_ACCEPTANCE_COUNT'
    $deferredItemsText = Get-PhaseField $PhaseText 'DEFERRED_OWNER_ACCEPTANCE_ITEMS'

    $knownReleaseBlockers = 0
    if (-not [int]::TryParse($knownReleaseBlockersText, [ref]$knownReleaseBlockers) -or $knownReleaseBlockers -lt 0) {
        throw "KNOWN_RELEASE_BLOCKERS must be a non-negative integer, got '$knownReleaseBlockersText'."
    }

    $deferredCount = 0
    if (-not [int]::TryParse($deferredCountText, [ref]$deferredCount) -or $deferredCount -lt 0) {
        throw "DEFERRED_OWNER_ACCEPTANCE_COUNT must be a non-negative integer, got '$deferredCountText'."
    }

    if ($knownReleaseBlockers -lt $queuedItems.Count) {
        throw "KNOWN_RELEASE_BLOCKERS=$knownReleaseBlockers is below unresolved owner queue count $($queuedItems.Count)."
    }
    if ($deferredCount -ne $queuedItems.Count) {
        throw "DEFERRED_OWNER_ACCEPTANCE_COUNT=$deferredCount does not match unresolved owner queue count $($queuedItems.Count)."
    }

    if ($queuedItems.Count -gt 0 -and $ownerLastMode -ne 'ACTIVE') {
        throw 'OWNER_LAST_MODE must be ACTIVE while owner queue items remain QUEUED and cloud progression is recorded.'
    }
    foreach ($item in $queuedItems) {
        if (-not $deferredItemsText.Contains([string]$item.id, [StringComparison]::OrdinalIgnoreCase)) {
            throw "CURRENT_PHASE.md does not record queued owner item '$($item.id)' in DEFERRED_OWNER_ACCEPTANCE_ITEMS."
        }
    }

    if ($currentPhase -eq 'P22' -and $queuedItems.Count -gt 0) {
        throw 'P22 cannot become CURRENT_PHASE while required owner acceptance items remain QUEUED.'
    }
    if ($queuedItems.Count -gt 0 -and $PhaseText.Contains('VERIFIED_FINAL_COMPLETE: true', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'VERIFIED_FINAL_COMPLETE cannot be true while owner acceptance items remain QUEUED.'
    }

    $ledgerRows = Get-LedgerRows $LedgerText
    foreach ($row in $ledgerRows) {
        if ($row.PhaseNumber -ge $currentPhaseNumber -or $row.State -eq 'CLOSED') {
            continue
        }

        $matches = @($queuedItems | Where-Object sourceTask -eq $row.TaskId)
        if ($matches.Count -ne 1) {
            throw "Earlier unresolved task '$($row.TaskId)' in state '$($row.State)' has $($matches.Count) QUEUED owner mappings; exactly one is required before cloud phase $currentPhase may be active."
        }
    }

    foreach ($item in $queuedItems) {
        $sourcePhaseNumber = Convert-PhaseToNumber ([string]$item.sourcePhase) "sourcePhase for $($item.id)"
        if ($sourcePhaseNumber -gt $currentPhaseNumber) {
            throw "Owner item '$($item.id)' belongs to future phase '$($item.sourcePhase)' and was queued before that phase became cloud-current."
        }

        $sourceTaskId = [string]$item.sourceTask
        $sourceRows = @($ledgerRows | Where-Object { $_.TaskId -eq $sourceTaskId })
        if ($sourceRows.Count -ne 1) {
            throw "Owner item '$($item.id)' source task '$sourceTaskId' was not found exactly once in the canonical ledger."
        }
        if ($sourceRows[0].State -eq 'CLOSED') {
            throw "Queued owner source task '$sourceTaskId' is falsely marked CLOSED in the canonical ledger."
        }
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
    try {
        & $Action
    }
    catch {
        $rejected = $true
    }

    if (-not $rejected) {
        throw "Owner-last negative fixture was not rejected: $Label"
    }
}

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$policyPath = Join-Path $root 'docs\OWNER_LAST_EXECUTION_POLICY.md'
$queuePath = Join-Path $root 'docs\FINAL_OWNER_ACCEPTANCE_QUEUE.md'
$ledgerPath = Join-Path $root 'docs\TASK_LEDGER.md'
$phasePath = Join-Path $root 'CURRENT_PHASE.md'
$projectControlPath = Join-Path $root 'PROJECT_CONTROL.md'
$targetRunnerPath = Join-Path $root 'tools\runtime\run-p04-runtime-target-validation.ps1'
$finalRunnerPath = Join-Path $root 'tools\final-acceptance\run-final-owner-acceptance.ps1'

foreach ($path in @($policyPath, $queuePath, $ledgerPath, $phasePath, $projectControlPath, $targetRunnerPath, $finalRunnerPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required owner-last governance path is missing: $path"
    }
}

$policyText = Get-Content -LiteralPath $policyPath -Raw
$queueText = Get-Content -LiteralPath $queuePath -Raw
$ledgerText = Get-Content -LiteralPath $ledgerPath -Raw
$phaseText = Get-Content -LiteralPath $phasePath -Raw
$projectControlText = Get-Content -LiteralPath $projectControlPath -Raw
$targetRunnerText = Get-Content -LiteralPath $targetRunnerPath -Raw
$finalRunnerText = Get-Content -LiteralPath $finalRunnerPath -Raw

Assert-ProjectControlAligned $phaseText $projectControlText
Assert-OwnerLastContract $root $policyText $queueText $ledgerText $phaseText $targetRunnerText $finalRunnerText $true
Write-Host 'Static owner-last execution governance validation: PASS.'

if ($RunNegativeFixtures) {
    Assert-Rejected {
        Assert-ProjectControlAligned $phaseText ($projectControlText.Replace('CURRENT_PHASE: P05', 'CURRENT_PHASE: P04'))
    } 'PROJECT_CONTROL current-phase drift'

    Assert-Rejected {
        Assert-ProjectControlAligned $phaseText ($projectControlText.Replace('KNOWN_RELEASE_BLOCKERS: 1', 'KNOWN_RELEASE_BLOCKERS: 0'))
    } 'PROJECT_CONTROL release-blocker drift'

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
        $badLedger = $ledgerText.Replace('| FCCD-P04-007 | Start/stop/retry supervision | CLOSED |', '| FCCD-P04-007 | Start/stop/retry supervision | PENDING |')
        Assert-OwnerLastContract $root $policyText $queueText $badLedger $phaseText $targetRunnerText $finalRunnerText $false
    } 'earlier unresolved task without owner queue mapping'

    Assert-Rejected {
        $badPhase = $phaseText.Replace('OWNER_LAST_MODE: ACTIVE', 'OWNER_LAST_MODE: DISABLED')
        Assert-OwnerLastContract $root $policyText $queueText $ledgerText $badPhase $targetRunnerText $finalRunnerText $false
    } 'cloud phase advanced without active owner-last mode'

    Assert-Rejected {
        $badPhase = $phaseText.Replace('DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET', 'DEFERRED_OWNER_ACCEPTANCE_ITEMS: MISSING')
        Assert-OwnerLastContract $root $policyText $queueText $ledgerText $badPhase $targetRunnerText $finalRunnerText $false
    } 'queued owner id omitted from current phase state'

    Assert-Rejected {
        $badPhase = $phaseText.Replace('KNOWN_RELEASE_BLOCKERS: 1', 'KNOWN_RELEASE_BLOCKERS: 0')
        Assert-OwnerLastContract $root $policyText $queueText $ledgerText $badPhase $targetRunnerText $finalRunnerText $false
    } 'release blocker count below queued owner count'

    Assert-Rejected {
        $badPhase = $phaseText.Replace('VERIFIED_FINAL_COMPLETE: false', 'VERIFIED_FINAL_COMPLETE: true')
        Assert-OwnerLastContract $root $policyText $queueText $ledgerText $badPhase $targetRunnerText $finalRunnerText $false
    } 'final completion while queue unresolved'

    Assert-Rejected {
        $badPhase = $phaseText.Replace('CURRENT_PHASE: P05', 'CURRENT_PHASE: P22')
        Assert-OwnerLastContract $root $policyText $queueText $ledgerText $badPhase $targetRunnerText $finalRunnerText $false
    } 'P22 activation while queue unresolved'

    Assert-Rejected {
        $badTargetRunner = $targetRunnerText.Replace('OWNER-P04-008-REAL-TARGET', 'OWNER-P04-008-REMOVED')
        Assert-OwnerLastContract $root $policyText $queueText $ledgerText $phaseText $badTargetRunner $finalRunnerText $false
    } 'P04 target runner queue authorization removed'

    Write-Host 'Owner-last negative fixtures: PASS.'
}
