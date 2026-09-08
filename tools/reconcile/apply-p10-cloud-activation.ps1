$ErrorActionPreference = 'Stop'

$expectedMain = 'aa9f7fbb7fbdc1921d73fa71111fc10911313c94'
git fetch origin main --quiet
$actualMain = (git rev-parse origin/main).Trim()
if ($actualMain -ne $expectedMain) {
    throw "P10 activation base moved. Expected $expectedMain but found $actualMain."
}

$currentPath = 'CURRENT_PHASE.md'
$controlPath = 'PROJECT_CONTROL.md'
$ledgerPath = 'docs/TASK_LEDGER.md'
$current = [System.IO.File]::ReadAllText($currentPath)
$control = [System.IO.File]::ReadAllText($controlPath)
$ledger = [System.IO.File]::ReadAllText($ledgerPath)
$nl = if ($current.Contains("`r`n")) { "`r`n" } else { "`n" }

foreach ($required in @(
    'CURRENT_PHASE: P09',
    'CURRENT_PHASE_NAME: External Tool Gateway',
    'CURRENT_PHASE_STATE: CLOSED',
    'NEXT_PHASE: P10',
    'PHASE_EXIT_GATE: PASS',
    'KNOWN_RELEASE_BLOCKERS: 1',
    'DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET'
)) {
    if (-not $current.Contains($required)) { throw "CURRENT_PHASE guard missing: $required" }
}

$p10Region = [regex]::Match($ledger, '(?s)## P10 — Unity first-class adapter.*?(?=## P11 — Blender first-class adapter)')
if (-not $p10Region.Success) { throw 'P10 ledger region missing.' }
$p10Rows = [regex]::Matches($p10Region.Value, '(?m)^\| FCCD-P10-\d{3} \| .*? \| (PENDING|CLAIMED|IN_PROGRESS|BLOCKED|IMPLEMENTED|VERIFIED|CLOSED) \|$')
if ($p10Rows.Count -ne 13) { throw "Expected exactly 13 P10 rows, found $($p10Rows.Count)." }
$nonPending = @($p10Rows | Where-Object { $_.Groups[1].Value -ne 'PENDING' })
if ($nonPending.Count -ne 0) { throw 'P10 activation requires all 13 task rows to remain PENDING.' }

function Replace-Once([string]$text, [string]$old, [string]$new, [string]$label) {
    $count = ([regex]::Matches($text, [regex]::Escape($old))).Count
    if ($count -ne 1) { throw "$label expected exactly once, found $count." }
    return $text.Replace($old, $new)
}

$current = Replace-Once $current 'CURRENT_PHASE: P09' 'CURRENT_PHASE: P10' 'current phase'
$current = Replace-Once $current 'CURRENT_PHASE_NAME: External Tool Gateway' 'CURRENT_PHASE_NAME: Unity first-class adapter' 'current phase name'
$current = Replace-Once $current 'CURRENT_PHASE_STATE: CLOSED' 'CURRENT_PHASE_STATE: IN_PROGRESS' 'current phase state'
$current = Replace-Once $current 'NEXT_PHASE: P10' 'NEXT_PHASE: P11' 'next phase'
$current = Replace-Once $current 'PHASE_EXIT_GATE: PASS' 'PHASE_EXIT_GATE: NOT_RUN' 'phase exit gate'

$oldCurrentP09 = 'P09 — External Tool Gateway — is canonically CLOSED in this closure state. All eight mandatory P09 tasks are normally integrated and exact-main verified on candidate `499b042f93efe764465aa1d2ea0f2d0c62188a4b`; dedicated P09 exit run `34195027724` passed on that exact candidate. Canonical closure evidence is `evidence/phases/P09/CLOSURE.md`. P10 is the authorized next phase but remains inactive until a separate governance transition is normally integrated and exact-main verified.'
$newCurrentP10 = 'P09 — External Tool Gateway — is canonically CLOSED. All eight mandatory P09 tasks are normally integrated and reconciled; closure PR #230 was normally merged as `aa9f7fbb7fbdc1921d73fa71111fc10911313c94`, whose exact post-closure Windows CI `34197103477` / #637, Workspace Search `34197103520` / #366, Large Workspace Safeguards `34197103493` / #350, and P09 External Tool Gateway Exit `34197103570` / #6 all completed SUCCESS. Canonical closure evidence is `evidence/phases/P09/CLOSURE.md`.' + $nl + $nl + 'P10 — Unity first-class adapter — is now the sole legal cloud implementation/convergence phase. Only dependency-valid, unclaimed P10 work may begin. P11 and later implementation remain prohibited until P10 is truthfully closed with its exit gate resolved under canonical governance.'
$current = Replace-Once $current $oldCurrentP09 $newCurrentP10 'P09 closure/P10 activation paragraph'
$current = Replace-Once $current '- P09 is CLOSED and retained as the current closure checkpoint until a separate validated transition activates P10; no P10 or later implementation is authorized yet.' '- Exactly one cloud implementation/convergence phase is active: P10.' 'owner-last active-phase invariant'

if ($current.Contains('## P10 cloud task inventory')) { throw 'P10 activation inventory already exists.' }
$inventory = @(
    '## P10 cloud task inventory',
    '',
    '- `FCCD-P10-001` — Unity project/version detector — PENDING.',
    '- `FCCD-P10-002` — Unity install/Hub editor resolver — PENDING.',
    '- `FCCD-P10-003` — Strongly typed Unity CLI command builder — PENDING.',
    '- `FCCD-P10-004` — Unity process/project resource locking — PENDING.',
    '- `FCCD-P10-005` — Dedicated Unity log capture/parser — PENDING.',
    '- `FCCD-P10-006` — Compile validation — PENDING.',
    '- `FCCD-P10-007` — EditMode test integration — PENDING.',
    '- `FCCD-P10-008` — PlayMode test integration — PENDING.',
    '- `FCCD-P10-009` — Project-owned Editor automation invocation — PENDING.',
    '- `FCCD-P10-010` — Build target execution/artifact validation — PENDING.',
    '- `FCCD-P10-011` — Unity structured UI events — PENDING.',
    '- `FCCD-P10-012` — Unity cancellation/recovery — PENDING.',
    '- `FCCD-P10-013` — Unity contract fixture/suite — PENDING.',
    '',
    '## P10 cloud activation provenance',
    '',
    '- Source closed-phase canonical main: `aa9f7fbb7fbdc1921d73fa71111fc10911313c94`.',
    '- P09 closure integration: PR #230, normal merge.',
    '- Exact post-closure main Windows CI: run `34197103477` / #637 — SUCCESS.',
    '- Exact post-closure main P06-007 Workspace Search: run `34197103520` / #366 — SUCCESS.',
    '- Exact post-closure main P06-008 Large Workspace Safeguards: run `34197103493` / #350 — SUCCESS.',
    '- Exact post-closure main P09 External Tool Gateway Exit: run `34197103570` / #6 — SUCCESS.',
    '- Pre-activation live claim scan: no open PR and no P10 branch/claim was present.',
    '- `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; P05 remains `PASS_INTEGRATED`; `P04=NOT_RUN`.',
    '- `VERIFIED_FINAL_COMPLETE` remains `false`; P22 remains prohibited while any required owner queue item is unresolved.',
    '- This is scheduling/governance activation only; no P10 product implementation and no P10 ledger-state change is included.',
    '',
    ''
) -join $nl
$anchor = '## P09 cloud task inventory'
if (-not $current.Contains($anchor)) { throw 'CURRENT_PHASE insertion anchor missing.' }
$current = $current.Replace($anchor, $inventory + $anchor)

foreach ($required in @(
    'CURRENT_PHASE: P09',
    'CURRENT_PHASE_NAME: External Tool Gateway',
    'CURRENT_PHASE_STATE: CLOSED',
    'NEXT_PHASE: P10',
    'PHASE_EXIT_GATE: PASS'
)) {
    if (-not $control.Contains($required)) { throw "PROJECT_CONTROL guard missing: $required" }
}
$control = Replace-Once $control 'CURRENT_PHASE: P09' 'CURRENT_PHASE: P10' 'control current phase'
$control = Replace-Once $control 'CURRENT_PHASE_NAME: External Tool Gateway' 'CURRENT_PHASE_NAME: Unity first-class adapter' 'control phase name'
$control = Replace-Once $control 'CURRENT_PHASE_STATE: CLOSED' 'CURRENT_PHASE_STATE: IN_PROGRESS' 'control phase state'
$control = Replace-Once $control 'NEXT_PHASE: P10' 'NEXT_PHASE: P11' 'control next phase'
$control = Replace-Once $control 'PHASE_EXIT_GATE: PASS' 'PHASE_EXIT_GATE: NOT_RUN' 'control phase gate'

$oldControlP09 = 'P09 — External Tool Gateway — is canonically CLOSED on exact candidate `499b042f93efe764465aa1d2ea0f2d0c62188a4b` after all eight tasks were integrated/reconciled and exact candidate Windows CI `34195027792`, Workspace Search `34195027743`, Large Workspace Safeguards `34195027756`, and dedicated P09 exit run `34195027724` completed SUCCESS. `CURRENT_PHASE` deliberately remains P09 until a separate post-closure transition activates P10. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release blocker and `VERIFIED_FINAL_COMPLETE=false`.'
$newControlP10 = 'P09 — External Tool Gateway — is canonically CLOSED. Closure PR #230 was normally merged as `aa9f7fbb7fbdc1921d73fa71111fc10911313c94`; exact post-closure Windows CI `34197103477` / #637, Workspace Search `34197103520` / #366, Large Workspace Safeguards `34197103493` / #350, and P09 External Tool Gateway Exit `34197103570` / #6 all completed SUCCESS on that exact main SHA. P10 — Unity first-class adapter — is now the single active cloud implementation/convergence phase with all thirteen P10 task rows still PENDING at activation. P11 and later implementation remain prohibited until P10 is truthfully closed. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release blocker and `VERIFIED_FINAL_COMPLETE=false`.'
$control = Replace-Once $control $oldControlP09 $newControlP10 'PROJECT_CONTROL P09/P10 activation paragraph'
$control = $control.Replace('P09 — External Tool Gateway — is now the single active cloud implementation/convergence phase; all eight mandatory P09 ledger tasks remain PENDING at activation.', 'P09 — External Tool Gateway — is canonically CLOSED; P10 — Unity first-class adapter — is now the single active cloud implementation/convergence phase with all thirteen mandatory P10 ledger tasks PENDING at activation.')

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($currentPath, $current, $utf8)
[System.IO.File]::WriteAllText($controlPath, $control, $utf8)

$evidenceDir = 'evidence/governance'
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
$evidencePath = Join-Path $evidenceDir 'OWNER_LAST_P10_CLOUD_ACTIVATION_2026-09-08.md'
$evidence = @(
    '# P10 cloud activation — Unity first-class adapter',
    '',
    '```text',
    'SOURCE_CLOSED_PHASE: P09',
    'SOURCE_MAIN_SHA: aa9f7fbb7fbdc1921d73fa71111fc10911313c94',
    'P09_PHASE_STATE: CLOSED',
    'P09_EXIT_GATE: PASS',
    'P10_PHASE_STATE: IN_PROGRESS',
    'P10_TASK_ROWS_AT_ACTIVATION: 13/13 PENDING',
    'KNOWN_RELEASE_BLOCKERS: 1',
    'OWNER_PENDING: OWNER-P04-008-REAL-TARGET',
    'VERIFIED_FINAL_COMPLETE: false',
    '```',
    '',
    'P09 closure PR #230 was normally merged to exact main `aa9f7fbb7fbdc1921d73fa71111fc10911313c94`.',
    '',
    'Post-closure exact-main verification on that SHA:',
    '- Windows CI `34197103477` / #637 — SUCCESS.',
    '- Workspace Search `34197103520` / #366 — SUCCESS.',
    '- Large Workspace Safeguards `34197103493` / #350 — SUCCESS.',
    '- P09 External Tool Gateway Exit `34197103570` / #6 — SUCCESS.',
    '',
    'A live pre-activation scan found no open PR and no P10 branch/claim. The canonical P10 ledger region contains exactly thirteen task rows and every row remained `PENDING`; this activation does not edit `docs/TASK_LEDGER.md`.',
    '',
    'The owner-last amendment permits P10 cloud execution while `OWNER-P04-008-REAL-TARGET` remains queued and release-blocking. This transition does not satisfy, weaken, or relabel that owner requirement and does not claim release eligibility.',
    '',
    'P10 is therefore the sole legal cloud implementation/convergence phase after normal integration and exact-main verification of this governance transition. P11 and later implementation remain prohibited until P10 is canonically closed.'
) -join $nl
[System.IO.File]::WriteAllText($evidencePath, $evidence + $nl, $utf8)

if ([System.IO.File]::ReadAllText($ledgerPath) -ne $ledger) { throw 'Activation modified TASK_LEDGER unexpectedly.' }
Write-Host 'P10 activation guards and durable edits completed.'
