[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Replace-ExactOnce {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Old,
        [Parameter(Mandatory)][string]$New,
        [Parameter(Mandatory)][string]$Label
    )

    $full = Join-Path $root $Path
    $text = Get-Content -LiteralPath $full -Raw
    $first = $text.IndexOf($Old, [StringComparison]::Ordinal)
    if ($first -lt 0) { throw "Missing expected stale provenance: $Label" }
    $second = $text.IndexOf($Old, $first + $Old.Length, [StringComparison]::Ordinal)
    if ($second -ge 0) { throw "Expected exactly one stale provenance occurrence: $Label" }
    $updated = $text.Substring(0, $first) + $New + $text.Substring($first + $Old.Length)
    [IO.File]::WriteAllText($full, $updated, [Text.UTF8Encoding]::new($false))
}

Replace-ExactOnce -Path 'CURRENT_PHASE.md' `
    -Old '- P08 remains `IN_PROGRESS` and `PHASE_EXIT_GATE=NOT_RUN`. With P08-001 through P08-008 CLOSED in this candidate, the only legal next action after normal integration and exact-main verification is P08 phase-exit convergence. P09 and later implementation remain prohibited until P08 itself is canonically CLOSED with gate PASS.' `
    -New '- At the P08-008 task-reconciliation checkpoint, P08 remained `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN` while all eight task rows were CLOSED. That historical checkpoint is superseded by the P08 phase-exit closure provenance below; P09 remains inactive until a separate post-closure governance transition is integrated and exact-main verified.' `
    -Label 'CURRENT_PHASE P08-008 checkpoint'

Replace-ExactOnce -Path 'docs/TASK_LEDGER.md' `
    -Old 'No owner-only evidence is required. P08 remains `IN_PROGRESS`; all P08 task rows are CLOSED in this reconciliation candidate; `PHASE_EXIT_GATE=NOT_RUN`; P09 and later implementation remain prohibited until separate P08 phase-exit closure.' `
    -New 'No owner-only evidence is required. At this P08-008 task-reconciliation checkpoint, P08 remained `IN_PROGRESS`, all P08 task rows were CLOSED, and `PHASE_EXIT_GATE=NOT_RUN`; that historical checkpoint is superseded by the canonical P08 phase-closure record below.' `
    -Label 'TASK_LEDGER P08-008 checkpoint'

Replace-ExactOnce -Path 'docs/TASK_LEDGER.md' `
    -Old '- P08 remains `IN_PROGRESS`; `FCCD-P08-007` and `FCCD-P08-008` remain `PENDING`; `PHASE_EXIT_GATE=NOT_RUN`; no later phase is authorized by this task closure.' `
    -New '- At the P08-004 task-closure checkpoint, P08 remained `IN_PROGRESS`, P08-007/P08-008 were still `PENDING`, and `PHASE_EXIT_GATE=NOT_RUN`. This is retained as historical task provenance and is superseded by the later P08 task reconciliations and canonical P08 phase closure above.' `
    -Label 'TASK_LEDGER P08-004 historical checkpoint'

$current = Get-Content -LiteralPath (Join-Path $root 'CURRENT_PHASE.md') -Raw
foreach ($required in @(
    'CURRENT_PHASE: P08',
    'CURRENT_PHASE_STATE: CLOSED',
    'NEXT_PHASE: P09',
    'PHASE_EXIT_GATE: PASS',
    'KNOWN_RELEASE_BLOCKERS: 1',
    'VERIFIED_FINAL_COMPLETE: false',
    'Dedicated exact-candidate P08 phase-exit gate: run `34165022901` / job `101874255929` — SUCCESS.'
)) {
    if (-not $current.Contains($required, [StringComparison]::Ordinal)) { throw "Missing required closure invariant: $required" }
}

$ledger = Get-Content -LiteralPath (Join-Path $root 'docs\TASK_LEDGER.md') -Raw
if ($ledger.Contains('`FCCD-P08-007` and `FCCD-P08-008` remain `PENDING`', [StringComparison]::Ordinal)) {
    throw 'Stale P08-004 pending-task text remains in TASK_LEDGER.'
}
if (-not $ledger.Contains('P08 is canonically CLOSED at the phase level', [StringComparison]::Ordinal)) {
    throw 'Canonical P08 phase closure ledger statement is missing.'
}

Write-Host 'P08 closure stale-provenance repair: PASS.'
