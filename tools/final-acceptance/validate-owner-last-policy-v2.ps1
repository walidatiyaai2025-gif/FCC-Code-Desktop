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
    if (-not $match.Success) { throw 'Canonical owner acceptance queue JSON block is missing or malformed.' }
    try { return ($match.Groups[1].Value | ConvertFrom-Json -Depth 30) }
    catch { throw "Canonical owner acceptance queue JSON is invalid: $($_.Exception.Message)" }
}

function Assert-RequiredProperty {
    param([object]$Object, [string]$Name, [string]$ItemId)
    if ($Object.PSObject.Properties.Name -notcontains $Name) {
        throw "Owner queue item '$ItemId' is missing required property '$Name'."
    }
    $value = $Object.$Name
    if ($null -eq $value -or ($value -is [string] -and [string]::IsNullOrWhiteSpace($value))) {
        throw "Owner queue item '$ItemId' has an empty required property '$Name'."
    }
}

function Resolve-RepositoryPath {
    param([string]$Root, [string]$RelativePath, [string]$Label)
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

function Get-CanonicalField {
    param([string]$Text, [string]$Name, [string]$DocumentName)
    $match = [regex]::Match($Text, '(?m)^' + [regex]::Escape($Name) + ':\s*(.*?)\s*$')
    if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) {
        throw "$DocumentName is missing required field '$Name'."
    }
    return $match.Groups[1].Value.Trim()
}

function Convert-PhaseToNumber {
    param([string]$Phase, [string]$Label)
    $match = [regex]::Match($Phase, '^P(\d{2})$')
    if (-not $match.Success) { throw "$Label has invalid phase value '$Phase'." }
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
    if ($rows.Count -eq 0) { throw 'Canonical task ledger rows could not be parsed.' }
    return $rows
}

function Get-DeferredPhaseGateMap {
    param([string]$PhaseText)
    $value = Get-CanonicalField $PhaseText 'DEFERRED_PHASE_GATES' 'CURRENT_PHASE.md'
    $map = @{}
    foreach ($token in ($value -split ';')) {
        $trimmed = $token.Trim()
        if (-not $trimmed) { continue }
        $parts = $trimmed -split '=', 2
        if ($parts.Count -ne 2 -or $parts[0] -notmatch '^P\d{2}$' -or [string]::IsNullOrWhiteSpace($parts[1])) {
            throw "Malformed DEFERRED_PHASE_GATES entry '$trimmed'."
        }
        if ($map.ContainsKey($parts[0])) { throw "Duplicate deferred phase gate '$($parts[0])'." }
        $map[$parts[0]] = $parts[1].Trim()
    }
    return $map
}

function Assert-ProjectControlAligned {
    param([string]$PhaseText, [string]$ProjectControlText)
    foreach ($field in @(
        'CURRENT_PHASE','CURRENT_PHASE_NAME','CURRENT_PHASE_STATE','NEXT_PHASE','PHASE_EXIT_GATE',
        'KNOWN_RELEASE_BLOCKERS','VERIFIED_FINAL_COMPLETE','OWNER_LAST_MODE',
        'DEFERRED_OWNER_ACCEPTANCE_COUNT','DEFERRED_OWNER_ACCEPTANCE_ITEMS','DEFERRED_PHASE_GATES'
    )) {
        $phaseValue = Get-CanonicalField $PhaseText $field 'CURRENT_PHASE.md'
        $controlValue = Get-CanonicalField $ProjectControlText $field 'PROJECT_CONTROL.md'
        if (-not $phaseValue.Equals($controlValue, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Canonical state drift: CURRENT_PHASE.md has $field='$phaseValue' but PROJECT_CONTROL.md has '$controlValue'."
        }
    }

    foreach ($literal in @(
        'docs/OWNER_LAST_EXECUTION_POLICY.md',
        'docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md',
        'P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible'
    )) {
        if (-not $ProjectControlText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) {
            throw "PROJECT_CONTROL.md is missing required owner-last invariant: $literal"
        }
    }
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
        'one current cloud implementation phase',
        'source task or phase-gate requirement',
        'phase exit gate',
        'docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md',
        'VERIFIED_FINAL_COMPLETE=true',
        'P22 is the final release/acceptance closure phase'
    )) {
        if (-not $PolicyText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Owner-last policy is missing required invariant: $literal"
        }
    }

    $queue = Get-QueueDocumentFromText $QueueText
    if ($queue.schemaVersion -ne 1) { throw "Unsupported owner queue schemaVersion '$($queue.schemaVersion)'." }

    $items = @($queue.items)
    if ($items.Count -lt 1) { throw 'Owner acceptance queue must contain at least one unresolved or integrated owner obligation.' }

    $allowedClassifications = @('REAL_TARGET','MANUAL_VISUAL','INSTALLER_LIFECYCLE','CLEAN_MACHINE','EXTERNAL_HARDWARE')
    $allowedStates = @('QUEUED','PASS_INTEGRATED')
    $allowedSourceKinds = @('TASK','PHASE_GATE')
    $ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $queuedItems = [System.Collections.Generic.List[object]]::new()
    $ledgerRows = Get-LedgerRows $LedgerText
    $currentPhase = Get-CanonicalField $PhaseText 'CURRENT_PHASE' 'CURRENT_PHASE.md'
    $currentPhaseNumber = Convert-PhaseToNumber $currentPhase 'CURRENT_PHASE'
    $deferredPhaseGates = Get-DeferredPhaseGateMap $PhaseText

    foreach ($item in $items) {
        $itemId = if ($item.PSObject.Properties.Name -contains 'id') { [string]$item.id } else { '<missing-id>' }
        foreach ($property in @(
            'id','sourceKind','sourcePhase','classification','state','whyOwnerOnly','cloudEvidence',
            'command','prerequisites','expectedEvidencePath','passCriteria','reconciliationRule','releaseBlocking'
        )) {
            Assert-RequiredProperty $item $property $itemId
        }

        if (-not $ids.Add([string]$item.id)) { throw "Duplicate owner queue id '$($item.id)'." }
        if ($allowedSourceKinds -notcontains [string]$item.sourceKind) { throw "Unsupported sourceKind '$($item.sourceKind)' for '$($item.id)'." }
        if ([string]$item.sourcePhase -notmatch '^P\d{2}$') { throw "Invalid source phase for '$($item.id)': $($item.sourcePhase)" }
        if ($allowedClassifications -notcontains [string]$item.classification) { throw "Owner queue classification '$($item.classification)' is not environment-bound." }
        if ($allowedStates -notcontains [string]$item.state) { throw "Owner queue state '$($item.state)' is unsupported." }
        if (-not [bool]$item.releaseBlocking) { throw "Owner queue item '$($item.id)' must remain releaseBlocking=true." }
        if ([string]$item.whyOwnerOnly -match '(?i)failed\s+CI|code\s+defect|missing\s+implementation|missing\s+(automated\s+)?test|security\s+defect|data[- ]integrity\s+defect|repairable\s+repository') {
            throw "Owner queue item '$($item.id)' appears to classify a repairable product/CI defect as owner-only."
        }

        $sourcePhaseNumber = Convert-PhaseToNumber ([string]$item.sourcePhase) "sourcePhase for $($item.id)"
        if ($sourcePhaseNumber -gt $currentPhaseNumber) {
            throw "Owner item '$($item.id)' belongs to future phase '$($item.sourcePhase)' and was queued before that phase became cloud-current."
        }

        if ($item.sourceKind -eq 'TASK') {
            Assert-RequiredProperty $item 'sourceTask' $itemId
            if ([string]$item.sourceTask -notmatch '^FCCD-P\d{2}-\d{3}$') { throw "Invalid source task id for '$($item.id)': $($item.sourceTask)" }
            $sourceRows = @($ledgerRows | Where-Object TaskId -eq [string]$item.sourceTask)
            if ($sourceRows.Count -ne 1) { throw "Owner item '$($item.id)' source task '$($item.sourceTask)' was not found exactly once in the ledger." }
            if ($sourceRows[0].PhaseNumber -ne $sourcePhaseNumber) { throw "Owner item '$($item.id)' source task phase does not match sourcePhase." }
            if ($item.state -eq 'QUEUED' -and $sourceRows[0].State -eq 'CLOSED') { throw "Queued owner source task '$($item.sourceTask)' is falsely marked CLOSED." }
        }
        else {
            Assert-RequiredProperty $item 'sourceRequirement' $itemId
            if ([string]$item.sourceRequirement -notmatch '^(P\d{2})_EXIT_GATE$') { throw "Invalid phase-gate sourceRequirement '$($item.sourceRequirement)' for '$($item.id)'." }
            if ($Matches[1] -ne [string]$item.sourcePhase) { throw "Phase-gate sourceRequirement/sourcePhase mismatch for '$($item.id)'." }
            if ($item.state -eq 'QUEUED') {
                if (-not $deferredPhaseGates.ContainsKey([string]$item.sourcePhase)) { throw "Queued phase gate '$($item.id)' is not recorded in DEFERRED_PHASE_GATES." }
                if ($deferredPhaseGates[[string]$item.sourcePhase] -eq 'PASS') { throw "Queued phase gate '$($item.id)' cannot be represented as PASS." }
            }
        }

        $commandText = ([string]$item.command).Replace('/', '\')
        if (-not $commandText.StartsWith('.\tools\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $commandText.EndsWith('.ps1', [StringComparison]::OrdinalIgnoreCase) -or
            $commandText.Contains(' ', [StringComparison]::Ordinal)) {
            throw "Owner queue command for '$($item.id)' must be one tracked PowerShell script under .\\tools\\ with no inline shell arguments."
        }
        if ([string]$item.expectedEvidencePath -notmatch '^evidence/') { throw "Expected evidence path for '$($item.id)' must remain under evidence/." }
        if (@($item.prerequisites).Count -lt 1) { throw "Owner queue item '$($item.id)' must record prerequisites." }

        if ($CheckFilesystem) {
            $commandPath = Resolve-RepositoryPath $Root $commandText.Substring(2) "Command for $($item.id)"
            if (-not (Test-Path -LiteralPath $commandPath -PathType Leaf)) { throw "Tracked owner command is missing for '$($item.id)': $commandPath" }
            $cloudEvidencePath = Resolve-RepositoryPath $Root ([string]$item.cloudEvidence) "Cloud evidence for $($item.id)"
            if (-not (Test-Path -LiteralPath $cloudEvidencePath -PathType Leaf)) { throw "Cloud-complete evidence is missing for '$($item.id)': $cloudEvidencePath" }
            [void](Resolve-RepositoryPath $Root ([string]$item.expectedEvidencePath) "Expected evidence for $($item.id)")
        }

        if ($item.state -eq 'QUEUED') {
            $queuedItems.Add($item)
        }
        else {
            Assert-RequiredProperty $item 'integratedEvidence' $itemId
            if ($CheckFilesystem) {
                $integratedPath = Resolve-RepositoryPath $Root ([string]$item.integratedEvidence) "Integrated evidence for $($item.id)"
                if (-not (Test-Path -LiteralPath $integratedPath -PathType Leaf)) { throw "PASS_INTEGRATED evidence is missing for '$($item.id)'." }
            }
        }
    }

    $p04Items = @($items | Where-Object id -eq 'OWNER-P04-008-REAL-TARGET')
    if ($p04Items.Count -ne 1 -or $p04Items[0].sourceKind -ne 'TASK' -or $p04Items[0].sourceTask -ne 'FCCD-P04-008' -or $p04Items[0].classification -ne 'REAL_TARGET') {
        throw 'P04-008 REAL_TARGET obligation is missing or weakened in the owner queue.'
    }

    foreach ($row in $ledgerRows) {
        if ($row.PhaseNumber -ge $currentPhaseNumber -or $row.State -eq 'CLOSED') { continue }
        $matches = @($queuedItems | Where-Object { $_.sourceKind -eq 'TASK' -and $_.sourceTask -eq $row.TaskId })
        if ($matches.Count -ne 1) {
            throw "Earlier unresolved task '$($row.TaskId)' in state '$($row.State)' has $($matches.Count) QUEUED owner mappings; exactly one is required."
        }
    }

    foreach ($phaseName in $deferredPhaseGates.Keys) {
        $phaseNumber = Convert-PhaseToNumber $phaseName 'DEFERRED_PHASE_GATES phase'
        if ($phaseNumber -gt $currentPhaseNumber) { throw "Future phase gate '$phaseName' cannot be deferred while current phase is $currentPhase." }
        $matches = @($queuedItems | Where-Object { $_.sourceKind -eq 'PHASE_GATE' -and $_.sourcePhase -eq $phaseName })
        if ($matches.Count -ne 1) { throw "Deferred phase gate '$phaseName' has $($matches.Count) QUEUED owner mappings; exactly one is required." }
    }

    $knownReleaseBlockersText = Get-CanonicalField $PhaseText 'KNOWN_RELEASE_BLOCKERS' 'CURRENT_PHASE.md'
    $deferredCountText = Get-CanonicalField $PhaseText 'DEFERRED_OWNER_ACCEPTANCE_COUNT' 'CURRENT_PHASE.md'
    $deferredItemsText = Get-CanonicalField $PhaseText 'DEFERRED_OWNER_ACCEPTANCE_ITEMS' 'CURRENT_PHASE.md'
    $ownerLastMode = Get-CanonicalField $PhaseText 'OWNER_LAST_MODE' 'CURRENT_PHASE.md'

    $knownReleaseBlockers = 0
    if (-not [int]::TryParse($knownReleaseBlockersText, [ref]$knownReleaseBlockers) -or $knownReleaseBlockers -lt 0) { throw 'KNOWN_RELEASE_BLOCKERS must be a non-negative integer.' }
    $deferredCount = 0
    if (-not [int]::TryParse($deferredCountText, [ref]$deferredCount) -or $deferredCount -lt 0) { throw 'DEFERRED_OWNER_ACCEPTANCE_COUNT must be a non-negative integer.' }
    if ($knownReleaseBlockers -lt $queuedItems.Count) { throw "KNOWN_RELEASE_BLOCKERS=$knownReleaseBlockers is below unresolved queue count $($queuedItems.Count)." }
    if ($deferredCount -ne $queuedItems.Count) { throw "DEFERRED_OWNER_ACCEPTANCE_COUNT=$deferredCount does not match unresolved queue count $($queuedItems.Count)." }
    if ($queuedItems.Count -gt 0 -and $ownerLastMode -ne 'ACTIVE') { throw 'OWNER_LAST_MODE must be ACTIVE while queued owner acceptance remains.' }

    foreach ($item in $queuedItems) {
        if (-not $deferredItemsText.Contains([string]$item.id, [StringComparison]::OrdinalIgnoreCase)) { throw "CURRENT_PHASE.md omits queued owner item '$($item.id)'." }
    }
    if ($currentPhase -eq 'P22' -and $queuedItems.Count -gt 0) { throw 'P22 cannot become CURRENT_PHASE while required owner acceptance remains QUEUED.' }
    if ($queuedItems.Count -gt 0 -and $PhaseText.Contains('VERIFIED_FINAL_COMPLETE: true', [StringComparison]::OrdinalIgnoreCase)) { throw 'VERIFIED_FINAL_COMPLETE cannot be true while owner acceptance remains QUEUED.' }

    foreach ($literal in @('docs\FINAL_OWNER_ACCEPTANCE_QUEUE.md','OWNER-P04-008-REAL-TARGET','FCCD-P04-008','REAL_TARGET')) {
        if (-not $TargetRunnerText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) { throw "P04 target runner is missing authorization guard: $literal" }
    }
    if (-not $TargetRunnerText.Contains('isP04Current', [StringComparison]::Ordinal) -or -not $TargetRunnerText.Contains('isQueuedOwnerAcceptance', [StringComparison]::Ordinal)) {
        throw 'P04 target runner must authorize only current P04 or a valid queued owner item.'
    }

    foreach ($literal in @(
        'Final owner acceptance must run on the authoritative owner Windows environment.',
        'FINAL_OWNER_EXECUTION_COMPLETE_RECONCILIATION_REQUIRED',
        'queue state remains QUEUED',
        'testedRepoSha','overallStatus'
    )) {
        if (-not $FinalRunnerText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) { throw "Final owner runner is missing fail-closed behavior: $literal" }
    }
}

function Assert-Rejected {
    param([scriptblock]$Action, [string]$Label)
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw "Owner-last negative fixture was not rejected: $Label" }
}

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$policyPath = Join-Path $root 'docs\OWNER_LAST_EXECUTION_POLICY.md'
$queuePath = Join-Path $root 'docs\FINAL_OWNER_ACCEPTANCE_QUEUE.md'
$ledgerPath = Join-Path $root 'docs\TASK_LEDGER.md'
$phasePath = Join-Path $root 'CURRENT_PHASE.md'
$projectControlPath = Join-Path $root 'PROJECT_CONTROL.md'
$targetRunnerPath = Join-Path $root 'tools\runtime\run-p04-runtime-target-validation.ps1'
$finalRunnerPath = Join-Path $root 'tools\final-acceptance\run-final-owner-acceptance.ps1'

foreach ($path in @($policyPath,$queuePath,$ledgerPath,$phasePath,$projectControlPath,$targetRunnerPath,$finalRunnerPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required owner-last governance path is missing: $path" }
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
Write-Host 'Static owner-last execution governance validation v2: PASS.'

if ($RunNegativeFixtures) {
    Assert-Rejected { Assert-ProjectControlAligned $phaseText ($projectControlText.Replace('KNOWN_RELEASE_BLOCKERS: 2','KNOWN_RELEASE_BLOCKERS: 1')) } 'PROJECT_CONTROL release-blocker drift'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText ($queueText.Replace('OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN','OWNER_QUEUE_REMOVED')) $ledgerText $phaseText $targetRunnerText $finalRunnerText $false } 'missing queue markers'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText ($queueText.Replace('"classification": "REAL_TARGET"','"classification": "CODE_DEFECT"')) $ledgerText $phaseText $targetRunnerText $finalRunnerText $false } 'repairable classification'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText ($queueText.Replace('"state": "QUEUED"','"state": "PASS_INTEGRATED"')) $ledgerText $phaseText $targetRunnerText $finalRunnerText $false } 'PASS_INTEGRATED without evidence'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText $queueText ($ledgerText.Replace('| FCCD-P04-008 | Runtime contract suite | PENDING |','| FCCD-P04-008 | Runtime contract suite | CLOSED |')) $phaseText $targetRunnerText $finalRunnerText $false } 'queued task falsely closed'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText $queueText ($ledgerText.Replace('| FCCD-P04-007 | Start/stop/retry supervision | CLOSED |','| FCCD-P04-007 | Start/stop/retry supervision | PENDING |')) $phaseText $targetRunnerText $finalRunnerText $false } 'earlier unresolved task without queue mapping'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText $queueText $ledgerText ($phaseText.Replace('DEFERRED_PHASE_GATES: P04=NOT_RUN;P05=NOT_RUN','DEFERRED_PHASE_GATES: P04=NOT_RUN')) $targetRunnerText $finalRunnerText $false } 'queued P05 phase gate omitted from canonical phase-gate map'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText ($queueText.Replace('"sourceRequirement": "P05_EXIT_GATE"','"sourceRequirement": "P06_EXIT_GATE"')) $ledgerText $phaseText $targetRunnerText $finalRunnerText $false } 'phase gate requirement/source phase mismatch'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText $queueText $ledgerText ($phaseText.Replace('OWNER_LAST_MODE: ACTIVE','OWNER_LAST_MODE: DISABLED')) $targetRunnerText $finalRunnerText $false } 'owner-last disabled with queue'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText $queueText $ledgerText ($phaseText.Replace('VERIFIED_FINAL_COMPLETE: false','VERIFIED_FINAL_COMPLETE: true')) $targetRunnerText $finalRunnerText $false } 'false final completion'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText $queueText $ledgerText ($phaseText.Replace('CURRENT_PHASE: P05','CURRENT_PHASE: P22')) $targetRunnerText $finalRunnerText $false } 'P22 with unresolved queue'
    Assert-Rejected { Assert-OwnerLastContract $root $policyText $queueText $ledgerText $phaseText ($targetRunnerText.Replace('OWNER-P04-008-REAL-TARGET','OWNER-P04-008-REMOVED')) $finalRunnerText $false } 'P04 target authorization removed'
    Write-Host 'Owner-last negative fixtures v2: PASS.'
}
