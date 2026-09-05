[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RunNegativeFixtures
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-EmbeddedQueue {
    param([string]$Text)
    $pattern = '(?s)<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->\s*```json\s*(.*?)\s*```\s*<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) { throw 'Canonical owner acceptance queue JSON block is missing or malformed.' }
    try { return ($match.Groups[1].Value | ConvertFrom-Json -Depth 30) }
    catch { throw "Canonical owner acceptance queue JSON is invalid: $($_.Exception.Message)" }
}

function Get-Field {
    param([string]$Text, [string]$Name, [string]$Document)
    $match = [regex]::Match($Text, '(?m)^' + [regex]::Escape($Name) + ':\s*(.*?)\s*$')
    if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) {
        throw "$Document is missing required field '$Name'."
    }
    return $match.Groups[1].Value.Trim()
}

function Get-PhaseNumber {
    param([string]$Phase, [string]$Label)
    if ($Phase -notmatch '^P(\d{2})$') { throw "$Label has invalid phase '$Phase'." }
    return [int]$Matches[1]
}

function Get-LedgerRows {
    param([string]$Text)
    $pattern = '(?m)^\|\s*(FCCD-P(\d{2})-\d{3})\s*\|[^|\r\n]*\|\s*(PENDING|CLAIMED|IN_PROGRESS|BLOCKED|IMPLEMENTED|VERIFIED|CLOSED)\s*\|\s*$'
    $rows = @(
        foreach ($match in [regex]::Matches($Text, $pattern)) {
            [pscustomobject]@{
                TaskId = $match.Groups[1].Value
                Phase = 'P' + $match.Groups[2].Value
                PhaseNumber = [int]$match.Groups[2].Value
                State = $match.Groups[3].Value
            }
        }
    )
    if ($rows.Count -eq 0) { throw 'Canonical task ledger rows could not be parsed.' }
    return $rows
}

function Resolve-RepoPath {
    param([string]$Root, [string]$RelativePath, [string]$Label)
    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath.Contains('..', [StringComparison]::Ordinal)) {
        throw "$Label must be repository-relative without traversal: $RelativePath"
    }
    $full = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label escaped the repository root: $RelativePath"
    }
    return $full
}

function Assert-Property {
    param([object]$Item, [string]$Name, [string]$Id)
    if ($Item.PSObject.Properties.Name -notcontains $Name) { throw "Owner queue item '$Id' is missing '$Name'." }
    $value = $Item.$Name
    if ($null -eq $value -or ($value -is [string] -and [string]::IsNullOrWhiteSpace($value))) {
        throw "Owner queue item '$Id' has empty '$Name'."
    }
}

function Get-DeferredGateMap {
    param([string]$PhaseText)
    $map = @{}
    foreach ($token in ((Get-Field $PhaseText 'DEFERRED_PHASE_GATES' 'CURRENT_PHASE.md') -split ';')) {
        $token = $token.Trim()
        if (-not $token) { continue }
        $parts = $token -split '=', 2
        if ($parts.Count -ne 2 -or $parts[0] -notmatch '^P\d{2}$' -or [string]::IsNullOrWhiteSpace($parts[1])) {
            throw "Malformed DEFERRED_PHASE_GATES entry '$token'."
        }
        if ($map.ContainsKey($parts[0])) { throw "Duplicate deferred phase gate '$($parts[0])'." }
        $map[$parts[0]] = $parts[1].Trim()
    }
    return $map
}

function Assert-SetMatches {
    param([string[]]$Expected, [string[]]$Actual, [string]$Label)
    $expectedSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $actualSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($value in $Expected) { if ($value) { [void]$expectedSet.Add($value) } }
    foreach ($value in $Actual) { if ($value) { [void]$actualSet.Add($value) } }
    if ($expectedSet.Count -ne $actualSet.Count) { throw "$Label count mismatch." }
    foreach ($value in $expectedSet) {
        if (-not $actualSet.Contains($value)) { throw "$Label is missing '$value'." }
    }
}

function Assert-Contract {
    param(
        [string]$Root,
        [string]$PolicyText,
        [string]$QueueText,
        [string]$LedgerText,
        [string]$PhaseText,
        [string]$ControlText,
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
            throw "Owner-last policy is missing invariant: $literal"
        }
    }

    foreach ($field in @(
        'CURRENT_PHASE','CURRENT_PHASE_NAME','CURRENT_PHASE_STATE','NEXT_PHASE','PHASE_EXIT_GATE',
        'KNOWN_RELEASE_BLOCKERS','VERIFIED_FINAL_COMPLETE','OWNER_LAST_MODE',
        'DEFERRED_OWNER_ACCEPTANCE_COUNT','DEFERRED_OWNER_ACCEPTANCE_ITEMS','DEFERRED_PHASE_GATES'
    )) {
        $phaseValue = Get-Field $PhaseText $field 'CURRENT_PHASE.md'
        $controlValue = Get-Field $ControlText $field 'PROJECT_CONTROL.md'
        if (-not $phaseValue.Equals($controlValue, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Canonical state drift for ${field}: CURRENT_PHASE='$phaseValue', PROJECT_CONTROL='$controlValue'."
        }
    }

    foreach ($literal in @(
        'docs/OWNER_LAST_EXECUTION_POLICY.md',
        'docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md',
        'P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible'
    )) {
        if (-not $ControlText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) {
            throw "PROJECT_CONTROL.md is missing owner-last invariant: $literal"
        }
    }

    $queue = Get-EmbeddedQueue $QueueText
    if ($queue.schemaVersion -ne 1) { throw "Unsupported queue schemaVersion '$($queue.schemaVersion)'." }
    $items = @($queue.items)
    if ($items.Count -lt 1) { throw 'Owner acceptance queue must not be empty while owner-last obligations exist.' }

    $ledger = Get-LedgerRows $LedgerText
    $currentPhase = Get-Field $PhaseText 'CURRENT_PHASE' 'CURRENT_PHASE.md'
    $currentPhaseNumber = Get-PhaseNumber $currentPhase 'CURRENT_PHASE'
    $gateMap = Get-DeferredGateMap $PhaseText
    $allowedKinds = @('TASK','PHASE_GATE')
    $allowedClasses = @('REAL_TARGET','MANUAL_VISUAL','INSTALLER_LIFECYCLE','CLEAN_MACHINE','EXTERNAL_HARDWARE')
    $allowedStates = @('QUEUED','PASS_INTEGRATED')
    $ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $queued = [System.Collections.Generic.List[object]]::new()

    foreach ($item in $items) {
        $id = if ($item.PSObject.Properties.Name -contains 'id') { [string]$item.id } else { '<missing-id>' }
        foreach ($name in @(
            'id','sourceKind','sourcePhase','classification','state','whyOwnerOnly','cloudEvidence','command',
            'prerequisites','expectedEvidencePath','passCriteria','reconciliationRule','releaseBlocking'
        )) { Assert-Property $item $name $id }

        if (-not $ids.Add([string]$item.id)) { throw "Duplicate owner queue id '$($item.id)'." }
        if ($allowedKinds -notcontains [string]$item.sourceKind) { throw "Unsupported sourceKind '$($item.sourceKind)' for '$($item.id)'." }
        if ($allowedClasses -notcontains [string]$item.classification) { throw "Non-environment classification '$($item.classification)' for '$($item.id)'." }
        if ($allowedStates -notcontains [string]$item.state) { throw "Unsupported state '$($item.state)' for '$($item.id)'." }
        if (-not [bool]$item.releaseBlocking) { throw "Owner queue item '$($item.id)' must be releaseBlocking=true." }
        if ([string]$item.whyOwnerOnly -match '(?i)failed\s+CI|code\s+defect|missing\s+implementation|missing\s+(automated\s+)?test|security\s+defect|data[- ]integrity\s+defect|repairable\s+repository') {
            throw "Owner queue item '$($item.id)' appears to defer repairable work."
        }

        $sourcePhase = [string]$item.sourcePhase
        $sourcePhaseNumber = Get-PhaseNumber $sourcePhase "sourcePhase for $($item.id)"
        if ($sourcePhaseNumber -gt $currentPhaseNumber) { throw "Future-phase owner item '$($item.id)' is not allowed." }

        if ($item.sourceKind -eq 'TASK') {
            Assert-Property $item 'sourceTask' $id
            $sourceTask = [string]$item.sourceTask
            if ($sourceTask -notmatch '^FCCD-P\d{2}-\d{3}$') { throw "Invalid sourceTask '$sourceTask'." }
            $rows = @($ledger | Where-Object { $_.TaskId -eq $sourceTask })
            if ($rows.Count -ne 1) { throw "Source task '$sourceTask' was not found exactly once." }
            if ($rows[0].Phase -ne $sourcePhase) { throw "Source phase mismatch for '$($item.id)'." }
            if ($item.state -eq 'QUEUED' -and $rows[0].State -eq 'CLOSED') { throw "Queued source task '$sourceTask' is falsely CLOSED." }
        }
        else {
            Assert-Property $item 'sourceRequirement' $id
            $requirement = [string]$item.sourceRequirement
            if ($requirement -notmatch '^(P\d{2})_EXIT_GATE$' -or $Matches[1] -ne $sourcePhase) {
                throw "Invalid phase-gate source requirement '$requirement' for phase '$sourcePhase'."
            }
            if ($item.state -eq 'QUEUED') {
                if (-not $gateMap.ContainsKey($sourcePhase)) { throw "Queued phase gate '$($item.id)' is absent from DEFERRED_PHASE_GATES." }
                if ($gateMap[$sourcePhase].Equals('PASS', [StringComparison]::OrdinalIgnoreCase)) { throw "Queued phase gate '$($item.id)' cannot be represented as PASS." }
            }
        }

        $command = ([string]$item.command).Replace('/', '\')
        if (-not $command.StartsWith('.\tools\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $command.EndsWith('.ps1', [StringComparison]::OrdinalIgnoreCase) -or
            $command.Contains(' ', [StringComparison]::Ordinal)) {
            throw "Owner command for '$($item.id)' must be one tracked .\\tools\\*.ps1 path without inline arguments."
        }
        if ([string]$item.expectedEvidencePath -notmatch '^evidence/') { throw "Evidence path for '$($item.id)' must remain under evidence/." }
        if (@($item.prerequisites).Count -lt 1) { throw "Owner queue item '$($item.id)' requires prerequisites." }

        if ($CheckFilesystem) {
            $commandPath = Resolve-RepoPath $Root $command.Substring(2) "Command for $($item.id)"
            if (-not (Test-Path -LiteralPath $commandPath -PathType Leaf)) { throw "Tracked owner command is missing for '$($item.id)'." }
            $cloudPath = Resolve-RepoPath $Root ([string]$item.cloudEvidence) "Cloud evidence for $($item.id)"
            if (-not (Test-Path -LiteralPath $cloudPath -PathType Leaf)) { throw "Cloud evidence is missing for '$($item.id)'." }
            [void](Resolve-RepoPath $Root ([string]$item.expectedEvidencePath) "Expected evidence for $($item.id)")
        }

        if ($item.state -eq 'QUEUED') {
            $queued.Add($item)
        }
        else {
            Assert-Property $item 'integratedEvidence' $id
            if ($CheckFilesystem) {
                $integratedPath = Resolve-RepoPath $Root ([string]$item.integratedEvidence) "Integrated evidence for $($item.id)"
                if (-not (Test-Path -LiteralPath $integratedPath -PathType Leaf)) { throw "Integrated evidence is missing for '$($item.id)'." }
            }
        }
    }

    $p04 = @($items | Where-Object { $_.id -eq 'OWNER-P04-008-REAL-TARGET' })
    if ($p04.Count -ne 1 -or $p04[0].sourceKind -ne 'TASK' -or $p04[0].sourceTask -ne 'FCCD-P04-008' -or $p04[0].classification -ne 'REAL_TARGET') {
        throw 'P04-008 REAL_TARGET obligation is missing or weakened.'
    }

    foreach ($row in $ledger) {
        if ($row.PhaseNumber -ge $currentPhaseNumber -or $row.State -eq 'CLOSED') { continue }
        $matches = @($queued | Where-Object { $_.sourceKind -eq 'TASK' -and $_.sourceTask -eq $row.TaskId })
        if ($matches.Count -ne 1) { throw "Earlier unresolved task '$($row.TaskId)' must have exactly one QUEUED owner item." }
    }

    foreach ($phase in $gateMap.Keys) {
        $phaseNumber = Get-PhaseNumber $phase 'DEFERRED_PHASE_GATES phase'
        if ($phaseNumber -gt $currentPhaseNumber) { throw "Future gate '$phase' cannot be deferred while current phase is $currentPhase." }
        if ($gateMap[$phase].Equals('PASS', [StringComparison]::OrdinalIgnoreCase)) { throw "Deferred phase gate '$phase' cannot be PASS." }

        $unresolvedRows = @($ledger | Where-Object { $_.Phase -eq $phase -and $_.State -ne 'CLOSED' })
        $gateItems = @($queued | Where-Object { $_.sourceKind -eq 'PHASE_GATE' -and $_.sourcePhase -eq $phase })
        if ($unresolvedRows.Count -eq 0) {
            if ($gateItems.Count -ne 1) { throw "Deferred gate '$phase' with all tasks CLOSED requires exactly one standalone QUEUED PHASE_GATE item." }
        }
        elseif ($gateItems.Count -gt 1) {
            throw "Deferred gate '$phase' has duplicate standalone PHASE_GATE items."
        }
    }

    $knownBlockers = 0
    $deferredCount = 0
    if (-not [int]::TryParse((Get-Field $PhaseText 'KNOWN_RELEASE_BLOCKERS' 'CURRENT_PHASE.md'), [ref]$knownBlockers) -or $knownBlockers -lt 0) {
        throw 'KNOWN_RELEASE_BLOCKERS must be a non-negative integer.'
    }
    if (-not [int]::TryParse((Get-Field $PhaseText 'DEFERRED_OWNER_ACCEPTANCE_COUNT' 'CURRENT_PHASE.md'), [ref]$deferredCount) -or $deferredCount -lt 0) {
        throw 'DEFERRED_OWNER_ACCEPTANCE_COUNT must be a non-negative integer.'
    }
    if ($knownBlockers -lt $queued.Count) { throw 'KNOWN_RELEASE_BLOCKERS is below unresolved owner queue count.' }
    if ($deferredCount -ne $queued.Count) { throw 'DEFERRED_OWNER_ACCEPTANCE_COUNT does not match unresolved owner queue count.' }
    if ($queued.Count -gt 0 -and (Get-Field $PhaseText 'OWNER_LAST_MODE' 'CURRENT_PHASE.md') -ne 'ACTIVE') { throw 'OWNER_LAST_MODE must remain ACTIVE.' }

    $recordedIds = @((Get-Field $PhaseText 'DEFERRED_OWNER_ACCEPTANCE_ITEMS' 'CURRENT_PHASE.md') -split ';' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    Assert-SetMatches -Expected @($queued | ForEach-Object { [string]$_.id }) -Actual $recordedIds -Label 'DEFERRED_OWNER_ACCEPTANCE_ITEMS'

    if ($currentPhase -eq 'P22' -and $queued.Count -gt 0) { throw 'P22 cannot be current while owner items remain QUEUED.' }
    if ($queued.Count -gt 0 -and $PhaseText.Contains('VERIFIED_FINAL_COMPLETE: true', [StringComparison]::OrdinalIgnoreCase)) { throw 'VERIFIED_FINAL_COMPLETE cannot be true while owner items remain QUEUED.' }

    foreach ($literal in @('docs\FINAL_OWNER_ACCEPTANCE_QUEUE.md','OWNER-P04-008-REAL-TARGET','FCCD-P04-008','REAL_TARGET')) {
        if (-not $TargetRunnerText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) { throw "P04 target runner lost authorization guard '$literal'." }
    }
    if (-not $TargetRunnerText.Contains('isP04Current', [StringComparison]::Ordinal) -or -not $TargetRunnerText.Contains('isQueuedOwnerAcceptance', [StringComparison]::Ordinal)) {
        throw 'P04 target runner authorization logic is incomplete.'
    }

    foreach ($literal in @(
        'Final owner acceptance must run on the authoritative owner Windows environment.',
        'FINAL_OWNER_EXECUTION_COMPLETE_RECONCILIATION_REQUIRED','queue state remains QUEUED','testedRepoSha','overallStatus'
    )) {
        if (-not $FinalRunnerText.Contains($literal, [StringComparison]::OrdinalIgnoreCase)) { throw "Final owner runner lost fail-closed invariant '$literal'." }
    }
}

function Assert-Rejected {
    param([scriptblock]$Action, [string]$Label)
    try { & $Action } catch { return }
    throw "Negative owner-last fixture was not rejected: $Label"
}

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$paths = @{
    Policy = Join-Path $root 'docs\OWNER_LAST_EXECUTION_POLICY.md'
    Queue = Join-Path $root 'docs\FINAL_OWNER_ACCEPTANCE_QUEUE.md'
    Ledger = Join-Path $root 'docs\TASK_LEDGER.md'
    Phase = Join-Path $root 'CURRENT_PHASE.md'
    Control = Join-Path $root 'PROJECT_CONTROL.md'
    Target = Join-Path $root 'tools\runtime\run-p04-runtime-target-validation.ps1'
    Final = Join-Path $root 'tools\final-acceptance\run-final-owner-acceptance.ps1'
}
foreach ($path in $paths.Values) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required owner-last path is missing: $path" } }

$policyText = Get-Content -LiteralPath $paths.Policy -Raw
$queueText = Get-Content -LiteralPath $paths.Queue -Raw
$ledgerText = Get-Content -LiteralPath $paths.Ledger -Raw
$phaseText = Get-Content -LiteralPath $paths.Phase -Raw
$controlText = Get-Content -LiteralPath $paths.Control -Raw
$targetText = Get-Content -LiteralPath $paths.Target -Raw
$finalText = Get-Content -LiteralPath $paths.Final -Raw

Assert-Contract $root $policyText $queueText $ledgerText $phaseText $controlText $targetText $finalText $true
Write-Host 'Static owner-last execution governance validation: PASS.'

if ($RunNegativeFixtures) {
    Assert-Rejected { Assert-Contract $root $policyText $queueText $ledgerText $phaseText ($controlText.Replace('KNOWN_RELEASE_BLOCKERS: 2','KNOWN_RELEASE_BLOCKERS: 1')) $targetText $finalText $false } 'project-control blocker drift'
    Assert-Rejected { Assert-Contract $root $policyText ($queueText.Replace('OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN','OWNER_QUEUE_REMOVED')) $ledgerText $phaseText $controlText $targetText $finalText $false } 'missing queue markers'
    Assert-Rejected { Assert-Contract $root $policyText ($queueText.Replace('"classification": "REAL_TARGET"','"classification": "CODE_DEFECT"')) $ledgerText $phaseText $controlText $targetText $finalText $false } 'bad classification'
    Assert-Rejected { Assert-Contract $root $policyText ($queueText.Replace('"state": "QUEUED"','"state": "PASS_INTEGRATED"')) $ledgerText $phaseText $controlText $targetText $finalText $false } 'false PASS_INTEGRATED'
    Assert-Rejected { Assert-Contract $root $policyText $queueText ($ledgerText.Replace('| FCCD-P04-008 | Runtime contract suite | PENDING |','| FCCD-P04-008 | Runtime contract suite | CLOSED |')) $phaseText $controlText $targetText $finalText $false } 'queued task falsely closed'
    Assert-Rejected { Assert-Contract $root $policyText $queueText ($ledgerText.Replace('| FCCD-P04-007 | Start/stop/retry supervision | CLOSED |','| FCCD-P04-007 | Start/stop/retry supervision | PENDING |')) $phaseText $controlText $targetText $finalText $false } 'earlier unresolved task without queue mapping'
    Assert-Rejected { Assert-Contract $root $policyText $queueText $ledgerText ($phaseText.Replace('DEFERRED_PHASE_GATES: P04=NOT_RUN;P05=NOT_RUN','DEFERRED_PHASE_GATES: P04=NOT_RUN')) $controlText $targetText $finalText $false } 'queued P05 gate omitted from phase map'
    Assert-Rejected { Assert-Contract $root $policyText ($queueText.Replace('"sourceRequirement": "P05_EXIT_GATE"','"sourceRequirement": "P06_EXIT_GATE"')) $ledgerText $phaseText $controlText $targetText $finalText $false } 'phase-gate source mismatch'
    Assert-Rejected { Assert-Contract $root $policyText $queueText $ledgerText ($phaseText.Replace('OWNER_LAST_MODE: ACTIVE','OWNER_LAST_MODE: DISABLED')) $controlText $targetText $finalText $false } 'owner-last disabled'
    Assert-Rejected { Assert-Contract $root $policyText $queueText $ledgerText ($phaseText.Replace('VERIFIED_FINAL_COMPLETE: false','VERIFIED_FINAL_COMPLETE: true')) $controlText $targetText $finalText $false } 'false final completion'
    Assert-Rejected { Assert-Contract $root $policyText $queueText $ledgerText ($phaseText.Replace('CURRENT_PHASE: P06','CURRENT_PHASE: P22')) $controlText $targetText $finalText $false } 'P22 with queue unresolved'
    Assert-Rejected { Assert-Contract $root $policyText $queueText $ledgerText ($phaseText.Replace('CURRENT_PHASE: P06','CURRENT_PHASE: P07')) ($controlText.Replace('CURRENT_PHASE: P06','CURRENT_PHASE: P07')) $targetText $finalText $false } 'skip P06 with unqueued current-phase work'
    Assert-Rejected { Assert-Contract $root $policyText $queueText $ledgerText $phaseText $controlText ($targetText.Replace('OWNER-P04-008-REAL-TARGET','OWNER-P04-008-REMOVED')) $finalText $false } 'P04 authorization removed'
    Write-Host 'Owner-last negative fixtures: PASS.'
}
