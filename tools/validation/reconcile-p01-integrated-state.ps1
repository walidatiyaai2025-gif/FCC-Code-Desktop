[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Replace-Exact {
    param([string]$Text, [string]$Old, [string]$New, [string]$Label)
    if (-not $Text.Contains($Old)) { throw "Expected reconciliation source not found: $Label" }
    $Text.Replace($Old, $New)
}

$phasePath = Join-Path $PWD 'CURRENT_PHASE.md'
$ledgerPath = Join-Path $PWD 'docs/TASK_LEDGER.md'
$evidencePath = Join-Path $PWD 'evidence/phases/P01/INTEGRATED_TASK_RECONCILIATION_2026-09-03.md'

$phase = Get-Content -LiteralPath $phasePath -Raw
$phase = Replace-Exact $phase 'LAST_RECONCILED: 2026-09-02' 'LAST_RECONCILED: 2026-09-03' 'CURRENT_PHASE last reconciled date'
$phase = Replace-Exact $phase 'P01 is now the only legal implementation phase. All P01 tasks remain PENDING at this transition boundary; workers must apply `docs/WORKER_PROTOCOL.md` before claiming or implementing any P01 task. P02 implementation remains prohibited until every mandatory P01 task is CLOSED and the P01 exit gate passes with exact-head closure evidence.' 'P01 is the only legal implementation phase. `FCCD-P01-001` through `FCCD-P01-005` are CLOSED from validated canonical integration; `FCCD-P01-006` remains PENDING. Workers must apply `docs/WORKER_PROTOCOL.md` before claiming or implementing remaining P01 work. P02 implementation remains prohibited until every mandatory P01 task is CLOSED and the P01 exit gate passes with exact-head closure evidence.' 'CURRENT_PHASE active rule'
$statusOld = '- `P01` — IN_PROGRESS as the sole current phase; `FCCD-P01-001` through `FCCD-P01-006` remain PENDING at the transition boundary.'
$statusNew = @'
- `P01` — IN_PROGRESS as the sole current phase; `FCCD-P01-001` through `FCCD-P01-005` are CLOSED and `FCCD-P01-006` remains PENDING.
- P01 integrated-task reconciliation evidence: `evidence/phases/P01/INTEGRATED_TASK_RECONCILIATION_2026-09-03.md`.
'@
$phase = Replace-Exact $phase $statusOld $statusNew.TrimEnd() 'CURRENT_PHASE current status'
$phase = Replace-Exact $phase 'Apply `docs/WORKER_PROTOCOL.md` within P01 and begin only legitimate P01 work after this transition is integrated. Do not open P02 work. `VERIFIED_FINAL_COMPLETE` remains false; P00 closure is only the foundational phase gate, not product completion.' 'Apply `docs/WORKER_PROTOCOL.md` within P01. The next legitimate mandatory implementation task is `FCCD-P01-006` (build metadata/version service). Do not open P02 work until P01-006 is CLOSED and the P01 exit gate passes. `VERIFIED_FINAL_COMPLETE` remains false.' 'CURRENT_PHASE next legal action'
$phase = Replace-Exact $phase '8. Treat P01 as the sole legal implementation phase; build the live claim/recovery map and claim only legitimate P01 work.' '8. Treat P01 as the sole legal implementation phase; `FCCD-P01-001` through `FCCD-P01-005` are CLOSED, and `FCCD-P01-006` is the remaining mandatory P01 implementation task subject to a fresh live claim map.' 'CURRENT_PHASE resume step'
Set-Content -LiteralPath $phasePath -Value $phase -Encoding utf8NoBOM

$ledger = Get-Content -LiteralPath $ledgerPath -Raw
$rows = [ordered]@{
    '| FCCD-P01-001 | Create .NET 10 solution/projects with clean boundaries | PENDING |' = '| FCCD-P01-001 | Create .NET 10 solution/projects with clean boundaries | CLOSED |'
    '| FCCD-P01-002 | Configure nullable/analyzers/style/quality policy | PENDING |' = '| FCCD-P01-002 | Configure nullable/analyzers/style/quality policy | CLOSED |'
    '| FCCD-P01-003 | Dependency pinning/lock strategy | PENDING |' = '| FCCD-P01-003 | Dependency pinning/lock strategy | CLOSED |'
    '| FCCD-P01-004 | Unit/integration test infrastructure | PENDING |' = '| FCCD-P01-004 | Unit/integration test infrastructure | CLOSED |'
    '| FCCD-P01-005 | Windows CI Release build/test pipeline | PENDING |' = '| FCCD-P01-005 | Windows CI Release build/test pipeline | CLOSED |'
}
foreach ($old in $rows.Keys) {
    $ledger = Replace-Exact $ledger $old $rows[$old] $old
}

$marker = '| FCCD-P01-006 | Build metadata/version service | PENDING |'
if (-not $ledger.Contains($marker)) { throw 'P01-006 must remain PENDING during reconciliation.' }
$closureNote = @'
Closure evidence for `FCCD-P01-001` through `FCCD-P01-005` is recorded in `evidence/phases/P01/INTEGRATED_TASK_RECONCILIATION_2026-09-03.md`. `FCCD-P01-006` remains the only unresolved mandatory P01 task; P01 itself remains IN_PROGRESS and its exit gate has not been run.
'@
$ledger = $ledger.Replace($marker, $marker + "`n`n" + $closureNote.TrimEnd())

$currentNext = [regex]::Match($ledger, '(?ms)^## Current next action\s+.*\z')
if (-not $currentNext.Success) { throw 'Could not locate ledger Current next action block.' }
if ($currentNext.Value -notmatch 'CURRENT_PHASE = P00') { throw 'Stale ledger checkpoint no longer matches expected P00 state.' }
$newNext = @'
## Current next action

`CURRENT_PHASE = P01` and P01 remains `IN_PROGRESS`.

`FCCD-P01-001` through `FCCD-P01-005` are CLOSED from exact-head/cloud validation plus normal canonical integration. The durable evidence map is `evidence/phases/P01/INTEGRATED_TASK_RECONCILIATION_2026-09-03.md`. The permanent Windows CI baseline is green on the reconciled P01-005 integration lineage.

`FCCD-P01-006` (build metadata/version service) remains PENDING and is the next legitimate mandatory P01 implementation task after a fresh live ownership check. Do not begin P02 until P01-006 is CLOSED, the P01 exit gate is run and PASS, exact-head closure evidence is recorded, and main is green. `VERIFIED_FINAL_COMPLETE` remains false.
'@
$ledger = $ledger.Substring(0, $currentNext.Index) + $newNext.TrimEnd() + "`n"
Set-Content -LiteralPath $ledgerPath -Value $ledger -Encoding utf8NoBOM

New-Item -ItemType Directory -Force -Path (Split-Path $evidencePath) | Out-Null
$evidence = @'
# P01 Integrated Task Reconciliation — 2026-09-03

## Scope

This record reconciles validated, already-integrated P01 work after live repository inspection. It is **not** the P01 phase-closure artifact, does not run or claim the P01 exit gate, does not close `FCCD-P01-006`, does not advance to P02, and does not change `VERIFIED_FINAL_COMPLETE=false`.

Reconciliation baseline before this record: `main` at `9d3098e251d237752542a4602e4014a8fa1eebc9`.

## Integrated task evidence

| Task | Canonical integration | Exact/cloud validation used for reconciliation | Result |
|---|---|---|---|
| `FCCD-P01-001` | PR #44 normal merge `2b94152362d59425b0bbb02f19eb7c68b8d24656` | Windows run `33667913175` checked out exact integrated main SHA `2b94152362d59425b0bbb02f19eb7c68b8d24656`, then restored and Release-built the solution under .NET 10 | CLOSED |
| `FCCD-P01-002` | PR #45 normal merge `0b84859edaccc4a9bb9c407b68a8c4b842d436d1` | Windows/.NET `10.0.400` run `33670390900`: restore, format, Release build, nullable/analyzer/style negative fixtures, recovery and clean-worktree checks | CLOSED |
| `FCCD-P01-003` | PR #46 normal merge `a2d52fd144a345c4d3aaefed73e46799de6dc69b` | Exact-head Windows/.NET `10.0.400` run `33675303436`: dependency policy, locked restore, Release build, stale-lock/local-version negative/recovery fixtures, quality policy and clean-worktree checks | CLOSED |
| `FCCD-P01-004` | PR #47 normal merge `1d7f99e91b399d170781aab75b732512f9494f81` | Final exact-head Windows run `33678829517`: locked restore, format, Release build, unit/integration infrastructure, dependency/quality policy, canonical solution-scope and clean-worktree checks | CLOSED |
| `FCCD-P01-005` | PR #48 normal merge `99748c2fabf02c4c27c075e4ace1ebb7f1fc8d46`; PR #49 normal merge `9d3098e251d237752542a4602e4014a8fa1eebc9` | PR #48 run `33716877372` SUCCESS; main `33717031238` SUCCESS; PR #49 run `33717169974` SUCCESS; main `33717649592` SUCCESS | CLOSED |

## Reconciled state

- `FCCD-P01-001` — CLOSED.
- `FCCD-P01-002` — CLOSED.
- `FCCD-P01-003` — CLOSED.
- `FCCD-P01-004` — CLOSED.
- `FCCD-P01-005` — CLOSED.
- `FCCD-P01-006` — PENDING.
- `CURRENT_PHASE` — P01.
- `CURRENT_PHASE_STATE` — IN_PROGRESS.
- `PHASE_EXIT_GATE` — NOT_RUN.
- P01 phase closure — NOT CLAIMED.
- P02 implementation — PROHIBITED.
- `VERIFIED_FINAL_COMPLETE` — false.

Release acceptance rows remain governed by `docs/ACCEPTANCE_MATRIX.md`; this task reconciliation does not convert release-level `NOT_RUN` rows into PASS.
'@
Set-Content -LiteralPath $evidencePath -Value $evidence.TrimEnd() -Encoding utf8NoBOM

$finalPhase = Get-Content -LiteralPath $phasePath -Raw
if ($finalPhase -notmatch 'FCCD-P01-006` \(build metadata/version service\)') { throw 'CURRENT_PHASE next-task assertion failed.' }
$finalLedger = Get-Content -LiteralPath $ledgerPath -Raw
foreach ($task in @('001','002','003','004','005')) {
    if ($finalLedger -notmatch "(?m)^\| FCCD-P01-$task \|[^\r\n]*\| CLOSED \|$") { throw "Ledger closure assertion failed for P01-$task." }
}
if ($finalLedger -notmatch '(?m)^\| FCCD-P01-006 \|[^\r\n]*\| PENDING \|$') { throw 'Ledger P01-006 PENDING assertion failed.' }
if ($finalLedger -match 'CURRENT_PHASE = P00') { throw 'Stale P00 next-action checkpoint remains in ledger.' }

Write-Host 'P01 integrated-task reconciliation generation: PASS.'
