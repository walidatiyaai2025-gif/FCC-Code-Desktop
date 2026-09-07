[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root
$sourceBranch = 'owner/p05-exit-real-target-evidence-20260907'
$testedSha = '60b6ef491e9dde3ca195b377a2ce07442452a6ce'
$currentMainBase = 'c9e396e5788ac4ab2d4e98106529ea6d1ea50679'
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Read-RepoText([string]$Path) {
    return [IO.File]::ReadAllText((Join-Path $root $Path))
}

function Write-RepoText([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText((Join-Path $root $Path), $Text, $utf8)
}

function Copy-FromSource([string]$Path) {
    $lines = @(& git show "FETCH_HEAD`:$Path")
    if ($LASTEXITCODE -ne 0) { throw "Failed to read $Path from $sourceBranch." }
    $text = ($lines -join "`n") + "`n"
    $destination = Join-Path $root $Path
    $directory = Split-Path -Parent $destination
    if ($directory) { [IO.Directory]::CreateDirectory($directory) | Out-Null }
    [IO.File]::WriteAllText($destination, $text, $utf8)
}

function Replace-FirstRegex([string]$Text, [string]$Pattern, [string]$Replacement, [string]$Label) {
    $rx = [regex]::new($Pattern, [Text.RegularExpressions.RegexOptions]::Multiline)
    $match = $rx.Match($Text)
    if (-not $match.Success) { throw "Required reconciliation target not found: $Label" }
    return $Text.Substring(0, $match.Index) + $Replacement + $Text.Substring($match.Index + $match.Length)
}

function Replace-FirstSingleline([string]$Text, [string]$Pattern, [string]$Replacement, [string]$Label) {
    $rx = [regex]::new($Pattern, [Text.RegularExpressions.RegexOptions]::Singleline)
    $match = $rx.Match($Text)
    if (-not $match.Success) { throw "Required reconciliation paragraph not found: $Label" }
    return $Text.Substring(0, $match.Index) + $Replacement + $Text.Substring($match.Index + $match.Length)
}

$head = (& git rev-parse HEAD).Trim()
if ($head -ne $currentMainBase) { throw "Recovery branch must start from exact main $currentMainBase; got $head." }

& git fetch origin $sourceBranch --depth=1
if ($LASTEXITCODE -ne 0) { throw "Could not fetch source evidence branch $sourceBranch." }

foreach ($path in @(
    'docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md',
    'tools/final-acceptance/owner-last-policy-validator.ps1',
    'evidence/phases/P05/CLOSURE.md',
    'evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md',
    'evidence/phases/P05/owner/P05_OWNER_OBSERVATION_2026-09-07.md',
    'evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json'
)) { Copy-FromSource $path }

$current = Read-RepoText 'CURRENT_PHASE.md'
$current = Replace-FirstRegex $current '^KNOWN_RELEASE_BLOCKERS:\s*2\s*$' 'KNOWN_RELEASE_BLOCKERS: 1' 'CURRENT_PHASE blocker count'
$current = Replace-FirstRegex $current '^DEFERRED_OWNER_ACCEPTANCE_COUNT:\s*2\s*$' 'DEFERRED_OWNER_ACCEPTANCE_COUNT: 1' 'CURRENT_PHASE deferred count'
$current = Replace-FirstRegex $current '^DEFERRED_OWNER_ACCEPTANCE_ITEMS:\s*OWNER-P04-008-REAL-TARGET;OWNER-P05-EXIT-REAL-TARGET\s*$' 'DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET' 'CURRENT_PHASE deferred items'
$current = Replace-FirstRegex $current '^DEFERRED_PHASE_GATES:\s*P04=NOT_RUN;P05=NOT_RUN\s*$' 'DEFERRED_PHASE_GATES: P04=NOT_RUN' 'CURRENT_PHASE deferred gates'
$currentClosed = "P05 is canonically closed at the phase level. ``FCCD-P05-001`` through ``FCCD-P05-008`` are normally integrated and exact-main verified, and the owner completed the required genuine Windows/FCC/provider interaction on exact tested SHA ``$testedSha``. Provider-backed conversation execution, structured activity, Stop/Retry, close/reopen, and durable session resume all passed. ``OWNER-P05-EXIT-REAL-TARGET`` is reconciled as ``PASS_INTEGRATED``, the P05 exit gate is ``PASS``, and canonical closure evidence is ``evidence/phases/P05/CLOSURE.md``.`n`n"
$current = Replace-FirstSingleline $current 'P05 cloud implementation is complete:.*?no P05 `CLOSURE\.md` PASS is claimed\.\s*\r?\n\r?\n' $currentClosed 'CURRENT_PHASE P05 unresolved summary'
$current = Replace-FirstSingleline $current '### OWNER-P05-EXIT-REAL-TARGET\s*\r?\n\r?\n.*?(?=## P08 cloud task inventory)' @"
### OWNER-P05-EXIT-REAL-TARGET — PASS_INTEGRATED

- Source kind: phase gate.
- Source requirement: ``P05_EXIT_GATE``.
- Source phase: P05.
- P05 task rows: 8/8 CLOSED.
- P05 exit gate: ``PASS``.
- Classification: ``REAL_TARGET``.
- Tested repository SHA: ``$testedSha``.
- Tested tree SHA: ``221ebaf238346bdcc91cdc2fe2a1524637e312f1``.
- Owner observations: provider-backed task PASS; structured execution PASS; Stop/Retry PASS; close/reopen PASS; durable session resume PASS.
- Exact tested-main Windows CI: ``34092682076`` — SUCCESS.
- Exact tested-main Workspace Search: ``34092682081`` — SUCCESS.
- Exact tested-main Large Workspace Safeguards: ``34092682122`` — SUCCESS.
- Machine-readable evidence: ``evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json``.
- Reconciliation evidence: ``evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md``.
- Canonical phase closure: ``evidence/phases/P05/CLOSURE.md``.
- Queue state: ``PASS_INTEGRATED``.
- Release status: this P05 obligation is resolved; ``OWNER-P04-008-REAL-TARGET`` remains release-blocking.

"@ 'CURRENT_PHASE P05 owner queue section'
if (-not $current.Contains('## P05 owner real-target recovery on current main')) {
    $current += @"

## P05 owner real-target recovery on current main

PR #202 carried legitimate owner REAL_TARGET evidence but its branch diverged from canonical main and became non-mergeable. This recovery reapplies only that P05 evidence/reconciliation on exact recovery base ``$currentMainBase`` while preserving all later P08-004 canonical provenance. Compare review from tested SHA ``$testedSha`` to the recovery base shows no changes to P05 conversation/session/task implementation, persistence, runtime integration, application wiring, configuration, or packaging; the only product-source additions are the later P08-004 ConPTY terminal contract/host surface and its dedicated tests/docs. Under the affected-evidence rule, the historical P05 exit observation remains applicable to the P05 phase gate. P08 remains the sole current cloud implementation phase; P04 remains the only unresolved owner-last release blocker; ``VERIFIED_FINAL_COMPLETE=false``.
"@
}
Write-RepoText 'CURRENT_PHASE.md' $current

$control = Read-RepoText 'PROJECT_CONTROL.md'
$control = Replace-FirstRegex $control '^KNOWN_RELEASE_BLOCKERS:\s*2\s*$' 'KNOWN_RELEASE_BLOCKERS: 1' 'PROJECT_CONTROL blocker count'
$control = Replace-FirstRegex $control '^DEFERRED_OWNER_ACCEPTANCE_COUNT:\s*2\s*$' 'DEFERRED_OWNER_ACCEPTANCE_COUNT: 1' 'PROJECT_CONTROL deferred count'
$control = Replace-FirstRegex $control '^DEFERRED_OWNER_ACCEPTANCE_ITEMS:\s*OWNER-P04-008-REAL-TARGET;OWNER-P05-EXIT-REAL-TARGET\s*$' 'DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET' 'PROJECT_CONTROL deferred items'
$control = Replace-FirstRegex $control '^DEFERRED_PHASE_GATES:\s*P04=NOT_RUN;P05=NOT_RUN\s*$' 'DEFERRED_PHASE_GATES: P04=NOT_RUN' 'PROJECT_CONTROL deferred gates'
$controlClosed = "P05 is canonically CLOSED at the phase level. ``FCCD-P05-001`` through ``FCCD-P05-008`` are CLOSED, PR #140 normally merged as ``6e85cc2941612937365bbaedc9e4370e9e1510e6``, and the owner completed the required genuine Windows/FCC/provider phase-exit interaction on exact tested SHA ``$testedSha``. Provider-backed execution, structured activity, Stop/Retry, close/reopen, and durable session resume all passed; exact tested-main Windows CI ``34092682076``, Workspace Search ``34092682081``, and Large Workspace Safeguards ``34092682122`` were SUCCESS. ``OWNER-P05-EXIT-REAL-TARGET`` is now ``PASS_INTEGRATED``, P05's exit gate is ``PASS``, and closure evidence is ``evidence/phases/P05/CLOSURE.md``. P04 remains the sole unresolved owner-last release blocker.`n`n"
$control = Replace-FirstSingleline $control 'P05 cloud implementation is complete and integrated:.*?no P05 phase PASS is claimed\.\s*\r?\n\r?\n' $controlClosed 'PROJECT_CONTROL P05 unresolved summary'
$control = $control.Replace('The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`.', 'The remaining earlier owner-last queue obligation is `OWNER-P04-008-REAL-TARGET`, which remains unresolved/release-blocking with `P04=NOT_RUN`; P05 is `PASS_INTEGRATED` with exit gate `PASS`, and `VERIFIED_FINAL_COMPLETE=false`.')
if (-not $control.Contains('## P05 owner real-target recovery on current main')) {
    $control += @"

## P05 owner real-target recovery on current main

The stale/non-mergeable owner evidence PR #202 is recovered onto canonical base ``$currentMainBase`` without carrying its divergent history or temporary reconciliation helpers. Genuine P05 REAL_TARGET evidence remains bound to exact tested SHA ``$testedSha`` and its successful exact-main CI runs. The current recovery changes governance/evidence only, preserves later P08-004 product work and closure provenance, leaves ``CURRENT_PHASE=P08``, and reduces unresolved owner-last release blockers from two to one: ``OWNER-P04-008-REAL-TARGET``.
"@
}
Write-RepoText 'PROJECT_CONTROL.md' $control

$ledger = Read-RepoText 'docs/TASK_LEDGER.md'
if (-not $ledger.Contains('P05 is canonically CLOSED at the phase level after genuine owner REAL_TARGET exit acceptance')) {
    $note = @"

P05 is canonically CLOSED at the phase level after genuine owner REAL_TARGET exit acceptance on exact tested repository SHA ``$testedSha``. The owner observed provider-backed conversation execution, structured runtime activity, Stop/Retry, close/reopen, and durable session resume; all passed. Exact tested-main Windows CI ``34092682076``, Workspace Search ``34092682081``, and Large Workspace Safeguards ``34092682122`` were SUCCESS. Machine-readable evidence is ``evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json``; reconciliation is ``evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md``; phase closure is ``evidence/phases/P05/CLOSURE.md``. ``OWNER-P05-EXIT-REAL-TARGET`` is ``PASS_INTEGRATED``. This does not resolve ``FCCD-P04-008``, close P08, or imply release eligibility / ``VERIFIED_FINAL_COMPLETE=true``.

"@
    $needle = '## P06 — Projects/files/editor/search'
    if (-not $ledger.Contains($needle)) { throw 'P06 ledger heading not found for P05 closure insertion.' }
    $ledger = $ledger.Replace($needle, $note + $needle)
}
Write-RepoText 'docs/TASK_LEDGER.md' $ledger

$reconPath = 'evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md'
$recon = Read-RepoText $reconPath
if (-not $recon.Contains('## Current-main recovery applicability')) {
    $recon += @"

## Current-main recovery applicability

The original PR #202 branch later diverged and was not safely mergeable. Recovery therefore starts from canonical main ``$currentMainBase``. Repository compare from tested SHA ``$testedSha`` to this recovery base shows only later P08-004 ConPTY terminal contract/host source plus its dedicated tests/docs/workflow and canonical reconciliation documents; no P05 conversation/session/task implementation, persistence layer, provider runtime path, application conversation wiring, configuration, or packaging file changed. The owner observation is therefore not affected by those later changes for the P05 phase-exit scope. This recovery does not claim that the historical owner run is final-release-candidate acceptance; final owner/release gates remain governed by their own exact-candidate requirements.
"@
}
Write-RepoText $reconPath $recon

$closurePath = 'evidence/phases/P05/CLOSURE.md'
$closure = Read-RepoText $closurePath
if (-not $closure.Contains('## Recovery integration note')) {
    $closure += @"

## Recovery integration note

Canonical integration was delayed because PR #202 became non-mergeable after later main history. The closure evidence is recovered on base ``$currentMainBase`` after verifying that post-test product changes are confined to the P08-004 ConPTY terminal surface and do not modify the P05 conversation/session/task acceptance surface. P05 historical phase closure therefore remains valid; this is not a claim of final-release-candidate owner acceptance.
"@
}
Write-RepoText $closurePath $closure

$evidence = Get-Content (Join-Path $root 'evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json') -Raw | ConvertFrom-Json
if ($evidence.evidenceClassification -ne 'REAL_TARGET') { throw 'P05 evidence classification is not REAL_TARGET.' }
if ($evidence.testedRepoSha -ne $testedSha) { throw 'P05 evidence tested SHA mismatch.' }
if ($evidence.overallStatus -ne 'PASS' -or -not $evidence.sanitized) { throw 'P05 evidence is not sanitized PASS.' }
foreach ($name in @('realProviderTaskCompleted','structuredExecutionObserved','stopRetryObserved','appClosedAndReopened','sessionResumedWithDurableState')) {
    if (-not [bool]$evidence.observations.$name) { throw "P05 owner observation '$name' is not PASS." }
}

& .\tools\final-acceptance\validate-owner-last-policy.ps1 -RunNegativeFixtures
if ($LASTEXITCODE -ne 0) { throw 'Owner-last validator failed.' }

$phaseText = Read-RepoText 'CURRENT_PHASE.md'
$controlText = Read-RepoText 'PROJECT_CONTROL.md'
$ledgerText = Read-RepoText 'docs/TASK_LEDGER.md'
foreach ($required in @(
    'CURRENT_PHASE: P08',
    'KNOWN_RELEASE_BLOCKERS: 1',
    'DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET',
    'DEFERRED_PHASE_GATES: P04=NOT_RUN',
    'OWNER-P05-EXIT-REAL-TARGET — PASS_INTEGRATED',
    'FCCD-P08-007` — Interactive terminal UX — PENDING',
    'FCCD-P08-008` — Process/terminal safety tests — PENDING'
)) {
    if (-not $phaseText.Contains($required)) { throw "CURRENT_PHASE reconciliation assertion failed: $required" }
}
if (-not $controlText.Contains('CURRENT_PHASE: P08')) { throw 'PROJECT_CONTROL did not preserve P08.' }
if (-not $ledgerText.Contains('P05 is canonically CLOSED at the phase level after genuine owner REAL_TARGET exit acceptance')) { throw 'TASK_LEDGER P05 phase closure note missing.' }

& git diff --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed.' }

Write-Host 'P05 owner REAL_TARGET recovery reconciliation: PASS'
