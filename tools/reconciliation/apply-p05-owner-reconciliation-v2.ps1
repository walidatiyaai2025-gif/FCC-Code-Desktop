[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$testedSha = '60b6ef491e9dde3ca195b377a2ce07442452a6ce'
$testedTree = '221ebaf238346bdcc91cdc2fe2a1524637e312f1'
$integratedEvidence = 'evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md'

function Read-RepoText {
    param([Parameter(Mandatory)][string]$RelativePath)
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required file missing: $RelativePath" }
    return [IO.File]::ReadAllText($path)
}

function Write-RepoText {
    param([Parameter(Mandatory)][string]$RelativePath, [Parameter(Mandatory)][string]$Content)
    $path = Join-Path $root $RelativePath
    $directory = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    [IO.File]::WriteAllText($path, $Content, [Text.UTF8Encoding]::new($false))
}

function Replace-Required {
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string]$Old,
        [Parameter(Mandatory)][string]$New,
        [Parameter(Mandatory)][string]$Label
    )
    if ($Text.Contains($Old, [StringComparison]::Ordinal)) {
        return $Text.Replace($Old, $New, [StringComparison]::Ordinal)
    }
    if ($Text.Contains($New, [StringComparison]::Ordinal)) { return $Text }
    throw "Expected canonical text not found for: $Label"
}

# Owner acceptance queue.
$queuePath = 'docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md'
$queueText = Read-RepoText $queuePath
$queuePattern = '(?s)<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->\s*```json\s*(.*?)\s*```\s*<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->'
$queueMatch = [regex]::Match($queueText, $queuePattern)
if (-not $queueMatch.Success) { throw 'Canonical owner queue JSON block is missing.' }
$queue = $queueMatch.Groups[1].Value | ConvertFrom-Json -Depth 30
$p05Items = @($queue.items | Where-Object { $_.id -eq 'OWNER-P05-EXIT-REAL-TARGET' })
if ($p05Items.Count -ne 1) { throw 'OWNER-P05-EXIT-REAL-TARGET was not found exactly once.' }
$p05 = $p05Items[0]
if ($p05.sourceKind -ne 'PHASE_GATE' -or $p05.sourceRequirement -ne 'P05_EXIT_GATE') { throw 'P05 queue source contract drifted.' }
if ($p05.state -notin @('QUEUED','PASS_INTEGRATED')) { throw "Unexpected P05 queue state '$($p05.state)'." }
$p05.state = 'PASS_INTEGRATED'
if ($p05.PSObject.Properties.Name -contains 'integratedEvidence') { $p05.integratedEvidence = $integratedEvidence }
else { $p05 | Add-Member -NotePropertyName integratedEvidence -NotePropertyValue $integratedEvidence }
$queueJson = $queue | ConvertTo-Json -Depth 30
$newQueueBlock = "<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->`n```json`n$queueJson`n```n<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->"
$queueText = $queueText.Substring(0, $queueMatch.Index) + $newQueueBlock + $queueText.Substring($queueMatch.Index + $queueMatch.Length)

$oldQueueProse = @'
### OWNER-P05-EXIT-REAL-TARGET

The P05 mandatory implementation tasks are all integrated and `CLOSED`, and exact canonical-main Windows CI is green. The P05 **phase exit gate** nevertheless requires a user to issue a real provider-backed task through FCC Code Desktop, observe structured execution, exercise stop/retry, close/reopen the application, and resume durable state. GitHub-hosted CI cannot truthfully provide that owner Windows/FCC/provider interaction.

This is a phase-gate obligation, not a hidden ninth P05 task. `P05` therefore remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`. The tracked owner runner performs deterministic prerequisites first, then launches the real application twice and records only sanitized boolean observations/provenance. A failed observation remains a product/recovery blocker and never becomes a waiver.
'@
$newQueueProse = @'
### OWNER-P05-EXIT-REAL-TARGET — PASS_INTEGRATED

The owner completed the P05 real-target phase-exit interaction on exact repository SHA `60b6ef491e9dde3ca195b377a2ce07442452a6ce`: a provider-backed task executed in the conversation surface, structured runtime activity was observed, Stop and Retry succeeded, the application closed/reopened, and the same session resumed with durable prior state. Sanitized machine-readable evidence is stored at `evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json` and reconciliation review is stored at `evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md`.

The tested SHA passed exact-main Windows CI `34092682076`, Workspace Search `34092682081`, and Large Workspace Safeguards `34092682122`. Later history-only repository commits before reconciliation retained the same Git tree `221ebaf238346bdcc91cdc2fe2a1524637e312f1`, so no source/configuration/packaging bytes changed. The P05 queue item is therefore reconciled as `PASS_INTEGRATED`; it is no longer an unresolved release blocker. P04 remains unresolved and release-blocking.
'@
$queueText = Replace-Required $queueText $oldQueueProse $newQueueProse 'P05 queue prose'
Write-RepoText $queuePath $queueText

# CURRENT_PHASE.md — P08 remains current, only P04 remains deferred.
$currentPath = 'CURRENT_PHASE.md'
$current = Read-RepoText $currentPath
$current = Replace-Required $current 'KNOWN_RELEASE_BLOCKERS: 2' 'KNOWN_RELEASE_BLOCKERS: 1' 'CURRENT_PHASE blocker count'
$current = Replace-Required $current 'DEFERRED_OWNER_ACCEPTANCE_COUNT: 2' 'DEFERRED_OWNER_ACCEPTANCE_COUNT: 1' 'CURRENT_PHASE deferred count'
$current = Replace-Required $current 'DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET;OWNER-P05-EXIT-REAL-TARGET' 'DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET' 'CURRENT_PHASE deferred ids'
$current = Replace-Required $current 'DEFERRED_PHASE_GATES: P04=NOT_RUN;P05=NOT_RUN' 'DEFERRED_PHASE_GATES: P04=NOT_RUN' 'CURRENT_PHASE deferred gates'

$oldCurrentP05 = @'
P05 cloud implementation is complete: `FCCD-P05-001` through `FCCD-P05-008` are normally integrated and exact-main verified. Its mandatory exit observation still requires genuine owner Windows/FCC/provider interaction: a real task in the application conversation surface, structured execution, stop/retry, close/reopen, and durable session resume. That standalone phase-gate requirement is queued as `OWNER-P05-EXIT-REAL-TARGET`, remains `releaseBlocking=true`, and P05 remains deferred as `P05=NOT_RUN`; no P05 `CLOSURE.md` PASS is claimed.
'@
$newCurrentP05 = @'
P05 is now canonically closed at the phase level. `FCCD-P05-001` through `FCCD-P05-008` are normally integrated and exact-main verified, and the owner completed the required genuine Windows/FCC/provider interaction on exact tested SHA `60b6ef491e9dde3ca195b377a2ce07442452a6ce`. Provider-backed conversation execution, structured activity, Stop/Retry, close/reopen, and durable session resume all passed. `OWNER-P05-EXIT-REAL-TARGET` is reconciled as `PASS_INTEGRATED`, the P05 exit gate is `PASS`, and canonical closure evidence is `evidence/phases/P05/CLOSURE.md`.
'@
$current = Replace-Required $current $oldCurrentP05 $newCurrentP05 'CURRENT_PHASE P05 status paragraph'
$current = Replace-Required $current 'The owner-last policy permits sequential cloud advancement despite those two earlier environment-bound obligations only because their cloud preparation is complete and they are represented one-to-one in the canonical release-blocking owner queue.' 'The owner-last policy continues to permit sequential cloud advancement despite the remaining earlier P04 environment-bound obligation because its cloud preparation is complete and it remains represented one-to-one in the canonical release-blocking owner queue. The former P05 owner obligation has passed and is integrated.' 'CURRENT_PHASE owner-last summary'

$p05SectionPattern = '(?s)### OWNER-P05-EXIT-REAL-TARGET\r?\n\r?\n.*?(?=\r?\n## P08 cloud task inventory)'
$newP05Section = @'
### OWNER-P05-EXIT-REAL-TARGET — PASS_INTEGRATED

- Source kind: phase gate.
- Source requirement: `P05_EXIT_GATE`.
- Source phase: P05.
- P05 task rows: 8/8 CLOSED.
- P05 exit gate: `PASS`.
- Classification: `REAL_TARGET`.
- Tested repository SHA: `60b6ef491e9dde3ca195b377a2ce07442452a6ce`.
- Tested tree SHA: `221ebaf238346bdcc91cdc2fe2a1524637e312f1`.
- Owner observations: provider-backed task PASS; structured execution PASS; Stop/Retry PASS; close/reopen PASS; durable session resume PASS.
- Exact tested-main Windows CI: `34092682076` — SUCCESS.
- Exact tested-main Workspace Search: `34092682081` — SUCCESS.
- Exact tested-main Large Workspace Safeguards: `34092682122` — SUCCESS.
- Machine-readable evidence: `evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json`.
- Reconciliation evidence: `evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md`.
- Canonical phase closure: `evidence/phases/P05/CLOSURE.md`.
- Queue state: `PASS_INTEGRATED`.
- Release status: this P05 obligation is resolved; `OWNER-P04-008-REAL-TARGET` remains release-blocking.
'@
if ([regex]::IsMatch($current, $p05SectionPattern)) { $current = [regex]::Replace($current, $p05SectionPattern, $newP05Section) }
elseif (-not $current.Contains('### OWNER-P05-EXIT-REAL-TARGET — PASS_INTEGRATED', [StringComparison]::Ordinal)) { throw 'CURRENT_PHASE P05 owner section was not found.' }
Write-RepoText $currentPath $current

# PROJECT_CONTROL.md must match the live state block.
$controlPath = 'PROJECT_CONTROL.md'
$control = Read-RepoText $controlPath
$control = Replace-Required $control 'KNOWN_RELEASE_BLOCKERS: 2' 'KNOWN_RELEASE_BLOCKERS: 1' 'PROJECT_CONTROL blocker count'
$control = Replace-Required $control 'DEFERRED_OWNER_ACCEPTANCE_COUNT: 2' 'DEFERRED_OWNER_ACCEPTANCE_COUNT: 1' 'PROJECT_CONTROL deferred count'
$control = Replace-Required $control 'DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET;OWNER-P05-EXIT-REAL-TARGET' 'DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET' 'PROJECT_CONTROL deferred ids'
$control = Replace-Required $control 'DEFERRED_PHASE_GATES: P04=NOT_RUN;P05=NOT_RUN' 'DEFERRED_PHASE_GATES: P04=NOT_RUN' 'PROJECT_CONTROL deferred gates'

$oldControlP05 = @'
P05 cloud implementation is complete and integrated: `FCCD-P05-001` through `FCCD-P05-008` are CLOSED, PR #140 normally merged as `6e85cc2941612937365bbaedc9e4370e9e1510e6`, and exact post-merge Windows CI run `33988198377` completed SUCCESS. The only remaining P05 exit-gate evidence requires genuine owner Windows/FCC/provider interaction: a real application task, structured execution, stop/retry, close/reopen, and durable session resume. That phase-gate obligation is queued as `OWNER-P05-EXIT-REAL-TARGET` with `releaseBlocking=true`; P05's exit gate remains `NOT_RUN` and no P05 phase PASS is claimed.
'@
$newControlP05 = @'
P05 is canonically CLOSED at the phase level. `FCCD-P05-001` through `FCCD-P05-008` are CLOSED, PR #140 normally merged as `6e85cc2941612937365bbaedc9e4370e9e1510e6`, and the owner completed the required genuine Windows/FCC/provider phase-exit interaction on exact tested SHA `60b6ef491e9dde3ca195b377a2ce07442452a6ce`. Provider-backed execution, structured activity, Stop/Retry, close/reopen, and durable session resume all passed; exact tested-main Windows CI `34092682076`, Workspace Search `34092682081`, and Large Workspace Safeguards `34092682122` were SUCCESS. `OWNER-P05-EXIT-REAL-TARGET` is now `PASS_INTEGRATED`, P05's exit gate is `PASS`, and closure evidence is `evidence/phases/P05/CLOSURE.md`. P04 remains the sole unresolved owner-last release blocker.
'@
$control = Replace-Required $control $oldControlP05 $newControlP05 'PROJECT_CONTROL P05 status paragraph'
$control = Replace-Required $control 'The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`.' 'The remaining owner-last queue obligation is `OWNER-P04-008-REAL-TARGET`; `P04=NOT_RUN`, while P05 is reconciled PASS/`PASS_INTEGRATED`, and `VERIFIED_FINAL_COMPLETE=false`.' 'PROJECT_CONTROL P08 owner-last summary'
Write-RepoText $controlPath $control

# P05 phase closure record.
$closurePath = 'evidence/phases/P05/CLOSURE.md'
$closure = @'
# P05 — Phase Closure

```text
PHASE: P05
PHASE_NAME: Conversation + session + task experience
CANDIDATE_SHA: {TESTED_SHA}
CANDIDATE_TREE_SHA: {TESTED_TREE}
DATE: 2026-09-07
MANDATORY_TASKS: 8/8 CLOSED
CLOUD_BASELINE: PASS
OWNER_REAL_TARGET: PASS
OWNER_EVIDENCE: evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json
OWNER_RECONCILIATION: evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md
EXACT_MAIN_WINDOWS_CI: 34092682076 / SUCCESS
EXACT_MAIN_WORKSPACE_SEARCH: 34092682081 / SUCCESS
EXACT_MAIN_LARGE_WORKSPACE: 34092682122 / SUCCESS
KNOWN_PHASE_BLOCKERS: NONE
KNOWN_PHASE_REGRESSIONS: NONE
EXIT_GATE: PASS
VERIFIED_FINAL_COMPLETE: false
```

## Decision

P05 is canonically closed. All eight mandatory P05 task rows were already integrated and `CLOSED`. On the authoritative owner Windows environment, exact repository SHA `{TESTED_SHA}` then passed the required real provider-backed interaction: a task executed in the conversation surface, structured runtime activity was observed, Stop and Retry worked, the application closed and reopened, and the same session resumed with prior durable conversation/task state intact.

The owner evidence is sanitized and records no prompt/provider content, credentials, environment variables, or owner project contents. Machine-readable evidence is `evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json`.

## Exact-candidate applicability

The tested SHA resolved to tree `{TESTED_TREE}` and passed Windows CI `34092682076`, Workspace Search `34092682081`, and Large Workspace Safeguards `34092682122`. Repository history between that tested commit and the evidence-reconciliation base changed no files and retained the same tree SHA, so no source/configuration/packaging bytes changed before reconciliation.

## Boundary

This closure resolves only the P05 phase-exit obligation. `OWNER-P04-008-REAL-TARGET` remains unresolved/release-blocking, P08 remains the active cloud phase, and `VERIFIED_FINAL_COMPLETE` remains false.
'@
$closure = $closure.Replace('{TESTED_SHA}', $testedSha, [StringComparison]::Ordinal).Replace('{TESTED_TREE}', $testedTree, [StringComparison]::Ordinal)
Write-RepoText $closurePath $closure

# Task ledger phase-level provenance; task rows remain unchanged.
$ledgerPath = 'docs/TASK_LEDGER.md'
$ledger = Read-RepoText $ledgerPath
$ledgerNote = @'

P05 is canonically CLOSED at the phase level after genuine owner REAL_TARGET exit acceptance on exact tested repository SHA `60b6ef491e9dde3ca195b377a2ce07442452a6ce`. The owner observed provider-backed conversation execution, structured runtime activity, Stop/Retry, close/reopen, and durable session resume; all passed. Exact tested-main Windows CI `34092682076`, Workspace Search `34092682081`, and Large Workspace Safeguards `34092682122` were SUCCESS. Machine-readable evidence is `evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json`; reconciliation is `evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md`; phase closure is `evidence/phases/P05/CLOSURE.md`. `OWNER-P05-EXIT-REAL-TARGET` is `PASS_INTEGRATED`. This does not resolve `FCCD-P04-008`, close P08, or imply release eligibility / `VERIFIED_FINAL_COMPLETE=true`.

'@
if (-not $ledger.Contains('P05 is canonically CLOSED at the phase level after genuine owner REAL_TARGET exit acceptance', [StringComparison]::Ordinal)) {
    $marker = '## P06 — Projects/files/editor/search'
    if (-not $ledger.Contains($marker, [StringComparison]::Ordinal)) { throw 'P06 ledger marker not found.' }
    $ledger = $ledger.Replace($marker, $ledgerNote + $marker, [StringComparison]::Ordinal)
}
Write-RepoText $ledgerPath $ledger

# Owner-last negative fixtures must reflect one unresolved owner item.
$validatorPath = 'tools/final-acceptance/owner-last-policy-validator.ps1'
$validator = Read-RepoText $validatorPath
$oldBlockerFixture = @'
($controlText.Replace('KNOWN_RELEASE_BLOCKERS: 2','KNOWN_RELEASE_BLOCKERS: 1'))
'@.Trim()
$newBlockerFixture = @'
($controlText.Replace('KNOWN_RELEASE_BLOCKERS: 1','KNOWN_RELEASE_BLOCKERS: 0'))
'@.Trim()
$validator = Replace-Required $validator $oldBlockerFixture $newBlockerFixture 'owner-last blocker negative fixture'

$oldGateFixture = @'
($phaseText.Replace('DEFERRED_PHASE_GATES: P04=NOT_RUN;P05=NOT_RUN','DEFERRED_PHASE_GATES: P04=NOT_RUN')) $controlText $targetText $finalText $false } 'queued P05 gate omitted from phase map'
'@.Trim()
$newGateFixture = @'
($phaseText.Replace('DEFERRED_PHASE_GATES: P04=NOT_RUN','DEFERRED_PHASE_GATES: P04=NOT_RUN;P05=NOT_RUN')) $controlText $targetText $finalText $false } 'resolved P05 gate falsely deferred'
'@.Trim()
$validator = Replace-Required $validator $oldGateFixture $newGateFixture 'owner-last P05 deferred-gate negative fixture'
Write-RepoText $validatorPath $validator

Write-Host 'P05 owner reconciliation files applied successfully.'
