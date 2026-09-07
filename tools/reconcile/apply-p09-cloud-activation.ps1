[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sourceMain = 'e28e32b0b3fb1c5305d12ec0ebbf021c51fd75e5'

git -C $root fetch origin main --quiet
if ($LASTEXITCODE -ne 0) { throw 'Failed to fetch canonical main.' }
$originMain = (git -C $root rev-parse origin/main).Trim()
if ($originMain -ne $sourceMain) { throw "Canonical main moved: expected $sourceMain, got $originMain" }
$mergeBase = (git -C $root merge-base HEAD origin/main).Trim()
if ($mergeBase -ne $sourceMain) { throw "Activation branch is not based on exact accepted P08 closure main: $mergeBase" }

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
    if ($first -lt 0) { throw "Missing expected text: $Label" }
    $second = $text.IndexOf($Old, $first + $Old.Length, [StringComparison]::Ordinal)
    if ($second -ge 0) { throw "Expected exactly one occurrence: $Label" }
    $updated = $text.Substring(0, $first) + $New + $text.Substring($first + $Old.Length)
    [IO.File]::WriteAllText($full, $updated, [Text.UTF8Encoding]::new($false))
}

$oldState = @'
CURRENT_PHASE: P08
CURRENT_PHASE_NAME: Terminal/process supervision
CURRENT_PHASE_STATE: CLOSED
NEXT_PHASE: P09
PHASE_EXIT_GATE: PASS
'@
$newState = @'
CURRENT_PHASE: P09
CURRENT_PHASE_NAME: External Tool Gateway
CURRENT_PHASE_STATE: IN_PROGRESS
NEXT_PHASE: P10
PHASE_EXIT_GATE: NOT_RUN
'@

Replace-ExactOnce -Path 'CURRENT_PHASE.md' -Old $oldState -New $newState -Label 'CURRENT_PHASE state block'
Replace-ExactOnce -Path 'PROJECT_CONTROL.md' -Old $oldState -New $newState -Label 'PROJECT_CONTROL state block'

Replace-ExactOnce -Path 'CURRENT_PHASE.md' `
    -Old '`CURRENT_PHASE` deliberately remains `P08` after closure. P09 is not active yet. A separate governance transition may activate `CURRENT_PHASE=P09` only after this closure state is normally merged and the resulting exact canonical `main` remains green. No P09 or later implementation is authorized inside this closure state.' `
    -New 'P09 — External Tool Gateway — is now the sole legal cloud implementation/convergence phase. Only dependency-valid, unclaimed P09 work may begin. P10 and later implementation remain prohibited until P09 is truthfully closed with its exit gate resolved under canonical governance.' `
    -Label 'CURRENT_PHASE activation paragraph'

Replace-ExactOnce -Path 'CURRENT_PHASE.md' `
    -Old '- P08 is CLOSED and retained as the current closure checkpoint until a separate, validated transition activates P09; no later-phase implementation is authorized yet.' `
    -New '- Exactly one cloud implementation/convergence phase is active: P09.' `
    -Label 'CURRENT_PHASE owner-last invariant'

$inventory = @'
## P09 cloud task inventory

- `FCCD-P09-001` — `IExternalToolAdapter` contract — PENDING.
- `FCCD-P09-002` — Tool discovery/capability registry — PENDING.
- `FCCD-P09-003` — Structured invocation/result contracts — PENDING.
- `FCCD-P09-004` — Tool resource locking — PENDING.
- `FCCD-P09-005` — Artifact manifest/validation framework — PENDING.
- `FCCD-P09-006` — Tool diagnostics/health framework — PENDING.
- `FCCD-P09-007` — CLI/process generic adapter primitives — PENDING.
- `FCCD-P09-008` — Optional protocol adapter seam (DAP/MCP/etc.) without core coupling — PENDING.

## P09 cloud activation provenance

- Source closed-phase canonical main: `e28e32b0b3fb1c5305d12ec0ebbf021c51fd75e5`.
- P08 closure integration: PR #211, normal merge.
- Dedicated P08 exact-candidate phase-exit gate: run `34165022901` / job `101874255929` — SUCCESS on immutable candidate `bb372da0a4506b3edc508156f06fc60ced8cc3d4`.
- Exact post-closure main Windows CI: run `34166334093` / #567 — SUCCESS.
- Exact post-closure main P06-007 Workspace Search: run `34166334113` / #296 — SUCCESS.
- Exact post-closure main P06-008 Large Workspace Safeguards: run `34166334110` / #280 — SUCCESS.
- Pre-activation live claim scan: no open PR and no P09 branch/claim was present.
- `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; P05 remains `PASS_INTEGRATED`; `P04=NOT_RUN`.
- `VERIFIED_FINAL_COMPLETE` remains `false`; P22 remains prohibited while any required owner queue item is unresolved.
- This is scheduling/governance activation only; no P09 product implementation is included.

'@
Replace-ExactOnce -Path 'CURRENT_PHASE.md' -Old '## P08 cloud task inventory' -New ($inventory + '## P08 cloud task inventory') -Label 'P09 inventory insertion point'

$oldProjectParagraph = 'P08 — Terminal/process supervision — is canonically CLOSED in this closure state. `FCCD-P08-001` through `FCCD-P08-008` are CLOSED after implementation, focused validation, normal integration, exact post-merge verification, and durable task reconciliation. Exact immutable phase candidate `bb372da0a4506b3edc508156f06fc60ced8cc3d4` passed pre-closure Windows CI `34164500457` / #560, Workspace Search `34164500513` / #289, and Large Workspace Safeguards `34164500496` / #273; dedicated P08 phase-exit run `34165022901` / job `101874255929` completed SUCCESS with the full Windows baseline, interactive terminal UX runtime validation, process/terminal safety validation, and exact-SHA/clean-worktree guards. Closure evidence is `evidence/phases/P08/CLOSURE.md`. `CURRENT_PHASE` deliberately remains P08 until this closure change is normally integrated and the resulting exact canonical `main` remains green; only then may a separate governance transition activate P09. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item, P05 remains `PASS_INTEGRATED`, `P04=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all mandatory owner and release acceptance requirements pass.'
$newProjectParagraph = 'P08 — Terminal/process supervision — is canonically CLOSED. `FCCD-P08-001` through `FCCD-P08-008` are CLOSED after implementation, focused validation, normal integration, exact post-merge verification, and durable task reconciliation. Exact immutable phase candidate `bb372da0a4506b3edc508156f06fc60ced8cc3d4` passed pre-closure Windows CI `34164500457` / #560, Workspace Search `34164500513` / #289, and Large Workspace Safeguards `34164500496` / #273; dedicated P08 phase-exit run `34165022901` / job `101874255929` completed SUCCESS; closure PR #211 was normally merged as `e28e32b0b3fb1c5305d12ec0ebbf021c51fd75e5`; and exact post-closure Windows CI `34166334093` / #567, Workspace Search `34166334113` / #296, and Large Workspace Safeguards `34166334110` / #280 all completed SUCCESS. Closure evidence is `evidence/phases/P08/CLOSURE.md`. P09 — External Tool Gateway — is now the single active cloud implementation/convergence phase; all eight mandatory P09 ledger tasks remain PENDING at activation. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item, P05 remains `PASS_INTEGRATED`, `P04=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all mandatory owner and release acceptance requirements pass.'
Replace-ExactOnce -Path 'PROJECT_CONTROL.md' -Old $oldProjectParagraph -New $newProjectParagraph -Label 'PROJECT_CONTROL P08/P09 transition paragraph'

$evidencePath = Join-Path $root 'evidence\governance\OWNER_LAST_P09_CLOUD_ACTIVATION_2026-09-08.md'
$evidence = @'
# Owner-Last P09 Cloud Activation — 2026-09-08

```text
SOURCE_MAIN_SHA: e28e32b0b3fb1c5305d12ec0ebbf021c51fd75e5
SOURCE_PHASE: P08
SOURCE_PHASE_STATE: CLOSED
SOURCE_PHASE_EXIT_GATE: PASS
ACTIVATED_PHASE: P09
ACTIVATED_PHASE_NAME: External Tool Gateway
ACTIVATED_PHASE_STATE: IN_PROGRESS
NEXT_PHASE: P10
ACTIVATED_PHASE_EXIT_GATE: NOT_RUN
KNOWN_PHASE_BLOCKERS: 0
KNOWN_RELEASE_BLOCKERS: 1
OWNER_LAST_MODE: ACTIVE
VERIFIED_FINAL_COMPLETE: false
```

## Eligibility

P08 is canonically closed and integrated. PR #211 was normally merged as `e28e32b0b3fb1c5305d12ec0ebbf021c51fd75e5` after dedicated exact-candidate P08 phase-exit gate `34165022901` / job `101874255929` passed on immutable product candidate `bb372da0a4506b3edc508156f06fc60ced8cc3d4`.

The exact resulting canonical main passed all permanent shared gates:

- Windows CI `34166334093` / #567 — SUCCESS.
- P06-007 Workspace Search `34166334113` / #296 — SUCCESS.
- P06-008 Large Workspace Safeguards `34166334110` / #280 — SUCCESS.

No P08 cloud-actionable defect or P08 owner-only residual remains.

## Owner-last preservation

`OWNER-P04-008-REAL-TARGET` remains the sole unresolved `QUEUED`, `releaseBlocking=true` owner item. P05 remains `PASS_INTEGRATED`, `P04=NOT_RUN`, `KNOWN_RELEASE_BLOCKERS=1`, and `VERIFIED_FINAL_COMPLETE=false`. No owner evidence is manufactured or reclassified by this transition.

## Concurrency / claim check

Immediately before the first transition write, canonical main was exactly `e28e32b0b3fb1c5305d12ec0ebbf021c51fd75e5`, there were no open pull requests, and no P09 branch/claim existed.

## Transition

This governance-only change activates P09 as the single current cloud implementation/convergence phase:

- `CURRENT_PHASE=P09`
- `CURRENT_PHASE_NAME=External Tool Gateway`
- `CURRENT_PHASE_STATE=IN_PROGRESS`
- `NEXT_PHASE=P10`
- `PHASE_EXIT_GATE=NOT_RUN`
- `FCCD-P09-001` through `FCCD-P09-008` remain PENDING

No P09 product implementation is included. P10 and later implementation remain prohibited until P09 is canonically closed under the normal phase gate.

## Validation rule

This activation must pass exact-head Windows CI, Workspace Search, and Large Workspace Safeguards, then be normally merged. P09 implementation may begin only after the resulting exact canonical main passes those same permanent gates.
'@
[IO.Directory]::CreateDirectory((Split-Path -Parent $evidencePath)) | Out-Null
[IO.File]::WriteAllText($evidencePath, $evidence.TrimEnd() + "`n", [Text.UTF8Encoding]::new($false))

$current = Get-Content -LiteralPath (Join-Path $root 'CURRENT_PHASE.md') -Raw
foreach ($required in @(
    'CURRENT_PHASE: P09',
    'CURRENT_PHASE_NAME: External Tool Gateway',
    'CURRENT_PHASE_STATE: IN_PROGRESS',
    'NEXT_PHASE: P10',
    'PHASE_EXIT_GATE: NOT_RUN',
    'KNOWN_RELEASE_BLOCKERS: 1',
    'VERIFIED_FINAL_COMPLETE: false',
    '`FCCD-P09-001` — `IExternalToolAdapter` contract — PENDING.'
)) {
    if (-not $current.Contains($required, [StringComparison]::Ordinal)) { throw "Missing activation invariant: $required" }
}

$project = Get-Content -LiteralPath (Join-Path $root 'PROJECT_CONTROL.md') -Raw
foreach ($required in @(
    'CURRENT_PHASE: P09',
    'CURRENT_PHASE_NAME: External Tool Gateway',
    'CURRENT_PHASE_STATE: IN_PROGRESS',
    'NEXT_PHASE: P10',
    'PHASE_EXIT_GATE: NOT_RUN'
)) {
    if (-not $project.Contains($required, [StringComparison]::Ordinal)) { throw "Missing project-control invariant: $required" }
}

git -C $root diff --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed.' }
