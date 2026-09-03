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
$utf8 = [Text.UTF8Encoding]::new($false)

$phase = Get-Content -LiteralPath $phasePath -Raw
$phase = Replace-Exact $phase 'LAST_RECONCILED: 2026-09-02' 'LAST_RECONCILED: 2026-09-03' 'CURRENT_PHASE last reconciled date'
$phase = Replace-Exact $phase 'P01 is now the only legal implementation phase. All P01 tasks remain PENDING at this transition boundary; workers must apply `docs/WORKER_PROTOCOL.md` before claiming or implementing any P01 task. P02 implementation remains prohibited until every mandatory P01 task is CLOSED and the P01 exit gate passes with exact-head closure evidence.' 'P01 is the only legal implementation phase. `FCCD-P01-001` through `FCCD-P01-006` are CLOSED from validated canonical integration. The P01 exit gate remains NOT_RUN, so P02 implementation is still prohibited until the complete exact-head P01 exit gate passes and closure evidence is integrated.' 'CURRENT_PHASE active rule'
$statusOld = '- `P01` — IN_PROGRESS as the sole current phase; `FCCD-P01-001` through `FCCD-P01-006` remain PENDING at the transition boundary.'
$statusNew = @'
- `P01` — IN_PROGRESS as the sole current phase; `FCCD-P01-001` through `FCCD-P01-006` are CLOSED from validated canonical integration.
- P01 integrated-task reconciliation evidence: `evidence/phases/P01/INTEGRATED_TASK_RECONCILIATION_2026-09-03.md`.
- P01 exit gate remains `NOT_RUN`; phase closure is not claimed by this reconciliation.
'@
$phase = Replace-Exact $phase $statusOld $statusNew.TrimEnd() 'CURRENT_PHASE current status'
$phase = Replace-Exact $phase 'Apply `docs/WORKER_PROTOCOL.md` within P01 and begin only legitimate P01 work after this transition is integrated. Do not open P02 work. `VERIFIED_FINAL_COMPLETE` remains false; P00 closure is only the foundational phase gate, not product completion.' 'Apply `docs/WORKER_PROTOCOL.md` within P01 and run the complete P01 exit gate on an exact current `main` candidate. Do not begin P02 unless that gate passes, `evidence/phases/P01/CLOSURE.md` is integrated, the phase is canonically CLOSED, and `main` remains green. `VERIFIED_FINAL_COMPLETE` remains false.' 'CURRENT_PHASE next legal action'
$phase = Replace-Exact $phase '8. Treat P01 as the sole legal implementation phase; build the live claim/recovery map and claim only legitimate P01 work.' '8. Treat P01 as the sole legal phase; all mandatory P01 implementation tasks are CLOSED, so the next current-phase action is exact-head P01 exit-gate verification and closure reconciliation, not new feature work.' 'CURRENT_PHASE resume step'
[IO.File]::WriteAllText($phasePath, $phase, $utf8)

$ledger = Get-Content -LiteralPath $ledgerPath -Raw
$rows = [ordered]@{
    '| FCCD-P01-001 | Create .NET 10 solution/projects with clean boundaries | PENDING |' = '| FCCD-P01-001 | Create .NET 10 solution/projects with clean boundaries | CLOSED |'
    '| FCCD-P01-002 | Configure nullable/analyzers/style/quality policy | PENDING |' = '| FCCD-P01-002 | Configure nullable/analyzers/style/quality policy | CLOSED |'
    '| FCCD-P01-003 | Dependency pinning/lock strategy | PENDING |' = '| FCCD-P01-003 | Dependency pinning/lock strategy | CLOSED |'
    '| FCCD-P01-004 | Unit/integration test infrastructure | PENDING |' = '| FCCD-P01-004 | Unit/integration test infrastructure | CLOSED |'
    '| FCCD-P01-005 | Windows CI Release build/test pipeline | PENDING |' = '| FCCD-P01-005 | Windows CI Release build/test pipeline | CLOSED |'
    '| FCCD-P01-006 | Build metadata/version service | PENDING |' = '| FCCD-P01-006 | Build metadata/version service | CLOSED |'
}
foreach ($old in $rows.Keys) {
    $ledger = Replace-Exact $ledger $old $rows[$old] $old
}

$closedMarker = '| FCCD-P01-006 | Build metadata/version service | CLOSED |'
$closureNote = @'
Closure evidence for `FCCD-P01-001` through `FCCD-P01-006` is recorded in `evidence/phases/P01/INTEGRATED_TASK_RECONCILIATION_2026-09-03.md`. This closes the six task rows only from implementation + task-specific/cloud validation + normal canonical integration + exact-current-main non-regression CI. P01 itself remains IN_PROGRESS and its exit gate remains NOT_RUN.
'@
$ledger = $ledger.Replace($closedMarker, $closedMarker + "`n`n" + $closureNote.TrimEnd())

$currentNext = [regex]::Match($ledger, '(?ms)^## Current next action\s+.*\z')
if (-not $currentNext.Success) { throw 'Could not locate ledger Current next action block.' }
if ($currentNext.Value -notmatch 'CURRENT_PHASE = P00') { throw 'Stale ledger checkpoint no longer matches expected P00 state.' }
$newNext = @'
## Current next action

`CURRENT_PHASE = P01` and P01 remains `IN_PROGRESS`.

`FCCD-P01-001` through `FCCD-P01-006` are CLOSED from validated canonical integration. The durable task evidence map is `evidence/phases/P01/INTEGRATED_TASK_RECONCILIATION_2026-09-03.md`. The permanent Windows CI baseline is green on exact current `main` SHA `416651579fb8ee42442d961b469b16266810138a` (run `33719564337`).

The next legitimate current-phase action is the complete P01 exit gate: verify a fresh/exact checkout can restore, format, Release-build and run the full unit/integration and P01 policy baseline using the documented commands; record exact-head `evidence/phases/P01/CLOSURE.md`; keep P01 open if any check fails. Do not begin P02 until that closure is integrated and `PHASE_EXIT_GATE=PASS`. `VERIFIED_FINAL_COMPLETE` remains false.
'@
$ledger = $ledger.Substring(0, $currentNext.Index) + $newNext.TrimEnd() + "`n"
[IO.File]::WriteAllText($ledgerPath, $ledger, $utf8)

New-Item -ItemType Directory -Force -Path (Split-Path $evidencePath) | Out-Null
$evidence = @'
# P01 Integrated Task Reconciliation — 2026-09-03

## Scope

This record reconciles validated, already-integrated P01 implementation after a fresh live repository inspection. It is **not** the P01 phase-closure artifact, does not run or claim the P01 exit gate, does not advance to P02, does not change release-level acceptance rows, and keeps `VERIFIED_FINAL_COMPLETE=false`.

Reconciliation baseline before this record: exact `main` SHA `416651579fb8ee42442d961b469b16266810138a`.

## Live recovery map

- Open PRs: none.
- Open P01 issues: none.
- `worker/fccd-p01-001-solution-foundation` through `worker/fccd-p01-006-build-metadata`: all are fully contained in `main` (`ahead=0`).
- The old `reconcile/p01-integrated-task-state-9d3098e` target branch was stale/behind and was fast-forwarded to the live reconciliation baseline before regeneration.
- `validation/p01-reconciliation-9d3098e` contained a failed one-off generator. Its useful reconciliation logic was recovered; the helper workflow itself is not product/canonical state and is not merged by this record.
- `evidence/phases/P01/` did not exist on the reconciliation baseline; this record is the first P01 durable task-reconciliation evidence and is deliberately distinct from `CLOSURE.md`.

## Why the task rows are eligible for CLOSED

Task closure is not inferred from file existence. Each row below has implementation, focused verification, normal canonical integration, and no task-local regression on exact current main. The current-main Windows CI run `33719564337` checked out exact SHA `416651579fb8ee42442d961b469b16266810138a` and passed locked restore, format verification, Release build with 0 warnings / 0 errors, unit tests 9/9, integration tests 3/3, build-metadata validation, dependency-policy validation, quality-policy validation, test-infrastructure validation, negative fixtures and recovery checks.

| Task | Canonical integration / focused evidence | Reconciliation result |
|---|---|---|
| `FCCD-P01-001` | PR #44 normal integration established the 16-project .NET 10 foundation. Post-integration Windows run `33667913175` passed exact integrated-main checkout, .NET 10 setup, restore and Release build. Current-main run `33719564337` still builds the integrated solution cleanly. | CLOSED |
| `FCCD-P01-002` | PR #45 normal integration added nullable/analyzer/style policy. Focused Windows run `33670390900` completed SUCCESS with the quality-policy negative/recovery lane; current-main run `33719564337` passes static/executable quality validation. | CLOSED |
| `FCCD-P01-003` | PR #46 normal integration added exact SDK/dependency/lock policy. Focused Windows run `33675303436` passed the dependency-policy and lock negative/recovery suite; current-main run `33719564337` passes locked restore and static/executable dependency validation. | CLOSED |
| `FCCD-P01-004` | PR #47 normal integration added deterministic unit/integration infrastructure. Final focused Windows run `33678829517` passed the exact-head infrastructure lane; current-main run `33719564337` passes unit 9/9, integration 3/3 and executable infrastructure validation including cancellation/recovery behavior. | CLOSED |
| `FCCD-P01-005` | PRs #48/#49 normally integrated the permanent Windows Release pipeline and current supported action runtimes. Current-main run `33719564337` completed SUCCESS using that canonical pipeline. | CLOSED |
| `FCCD-P01-006` | PR #50 normally integrated deterministic build/version/provenance metadata after fixing lock-integrity, nullable and analyzer failures rather than weakening policy. Exact PR candidate Windows run `33719303106` passed; exact current-main run `33719564337` passes build-metadata static/executable positive/negative fixtures. | CLOSED |

## Reconciled state

- `FCCD-P01-001` — CLOSED.
- `FCCD-P01-002` — CLOSED.
- `FCCD-P01-003` — CLOSED.
- `FCCD-P01-004` — CLOSED.
- `FCCD-P01-005` — CLOSED.
- `FCCD-P01-006` — CLOSED.
- `CURRENT_PHASE` — P01.
- `CURRENT_PHASE_STATE` — IN_PROGRESS.
- `PHASE_EXIT_GATE` — NOT_RUN.
- P01 phase closure — NOT CLAIMED.
- P02 implementation — PROHIBITED.
- `VERIFIED_FINAL_COMPLETE` — false.

Release acceptance rows remain governed by `docs/ACCEPTANCE_MATRIX.md` and remain `NOT_RUN` until their release-candidate phase. This task reconciliation does not convert release-level acceptance to PASS.

## Next legitimate current-phase action

Run the complete P01 exit gate on an exact current-main candidate using the documented fresh-checkout baseline. If and only if it passes, create `evidence/phases/P01/CLOSURE.md`, reconcile `PHASE_EXIT_GATE=PASS` and P01 CLOSED, verify main remains green, and only then perform the separate P01→P02 transition. Any exit-gate failure keeps P01 open and must be fixed before phase advancement.
'@
[IO.File]::WriteAllText($evidencePath, $evidence.TrimEnd() + "`n", $utf8)

$finalPhase = Get-Content -LiteralPath $phasePath -Raw
if (-not $finalPhase.Contains('exact-head P01 exit-gate verification')) { throw 'CURRENT_PHASE next-action assertion failed.' }
$finalLedger = Get-Content -LiteralPath $ledgerPath -Raw
foreach ($closedRow in $rows.Values) {
    if (-not $finalLedger.Contains($closedRow)) { throw "Ledger closure assertion failed: $closedRow" }
}
if ($finalLedger -match '\| FCCD-P01-00[1-6] \|[^\r\n]+\| PENDING \|') { throw 'A mandatory P01 row remains PENDING after task reconciliation.' }
if ($finalLedger -match 'CURRENT_PHASE = P00') { throw 'Stale P00 next-action checkpoint remains in ledger.' }

Write-Host 'P01 integrated-task reconciliation generation: PASS.'
