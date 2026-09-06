$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $repoRoot

$implementationCandidate = '1341412ee80a8141ed3a7ea462c6e280e7017ea0'
$mergeSha = '9712e84c4596e18d0d80b0cfbd93b37ad65fb73d'
$evidencePath = 'evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md'

function Replace-ExactOnce {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Old,
        [Parameter(Mandatory)][string]$New
    )

    $text = [IO.File]::ReadAllText((Join-Path $repoRoot $Path))
    $matches = [regex]::Matches($text, [regex]::Escape($Old))
    if ($matches.Count -ne 1) {
        throw "Expected exactly one guarded match in '$Path'; found $($matches.Count)."
    }

    $updated = $text.Replace($Old, $New)
    [IO.File]::WriteAllText((Join-Path $repoRoot $Path), $updated, [Text.UTF8Encoding]::new($false))
}

# CURRENT_PHASE.md: close only P07-002 and add exact integration provenance.
Replace-ExactOnce -Path 'CURRENT_PHASE.md' `
    -Old '- `FCCD-P07-002` — Status/changed-files surface — PENDING.' `
    -New '- `FCCD-P07-002` — Status/changed-files surface — CLOSED.'

$currentPhaseMarker = "## P07 cloud activation provenance"
$currentPhaseInsertion = @'
## P07-002 integration provenance

- Exact implementation candidate: `1341412ee80a8141ed3a7ea462c6e280e7017ea0` from PR #167 (`worker/fccd-p07-002-status-changed-files`).
- PR #167 exact-head Windows CI: run `34039544202` / run #378 — SUCCESS.
- PR #167 exact-head P06-007 Workspace Search: run `34039544201` / run #107 — SUCCESS.
- PR #167 exact-head P06-008 Large Workspace Safeguards: run `34039544196` / run #91 — SUCCESS.
- Normal merge commit: `9712e84c4596e18d0d80b0cfbd93b37ad65fb73d`.
- Exact post-merge canonical-main Windows CI: run `34040051645` / run #379 — SUCCESS.
- Exact post-merge P06-007 Workspace Search: run `34040051726` / run #108 — SUCCESS.
- Exact post-merge P06-008 Large Workspace Safeguards: run `34040051678` / run #92 — SUCCESS.
- Integrated evidence: `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`.
- Evidence class remains cloud/self-test for bounded read-only Git status/changed-file enumeration plus canonical integration provenance; no stage/unstage, diff, branch/fetch/pull, commit/push, P07 phase closure, P08 authorization, owner-only evidence, release eligibility, or `VERIFIED_FINAL_COMPLETE` is implied.

'@
Replace-ExactOnce -Path 'CURRENT_PHASE.md' -Old $currentPhaseMarker -New ($currentPhaseInsertion + $currentPhaseMarker)

# PROJECT_CONTROL.md: reconcile the active P07 summary without changing phase or owner-last state.
$oldProjectControl = @'
P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection` is CLOSED after PR #163 exact-head validation, normal merge `9c3b0437f92a547453e8fdcdce22ab96d0084ade`, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-002` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes.
'@
$newProjectControl = @'
P07 — Change review + Git — is the single active cloud implementation/convergence phase. `FCCD-P07-001 — IGitService and repository detection` and `FCCD-P07-002 — Status/changed-files surface` are CLOSED after exact PR-head validation, normal merge integration, and exact post-merge canonical-main Windows CI / Workspace Search / Large Workspace validation all passed. `FCCD-P07-003` through `FCCD-P07-011` remain PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards. Integrated task evidence is `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md` and `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`. P08 and later implementation remain prohibited until P07 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes.
'@
Replace-ExactOnce -Path 'PROJECT_CONTROL.md' -Old $oldProjectControl -New $newProjectControl

# TASK_LEDGER.md: close P07-002, add provenance, and advance only the current-phase next action.
Replace-ExactOnce -Path 'docs/TASK_LEDGER.md' `
    -Old '| FCCD-P07-002 | Status/changed-files surface | PENDING |' `
    -New '| FCCD-P07-002 | Status/changed-files surface | CLOSED |'

$ledgerMarker = "## P08 — Terminal/process supervision"
$ledgerInsertion = @'
`FCCD-P07-002` is CLOSED from the production read-only Git status/changed-files surface integrated in PR #167. Exact implementation candidate `1341412ee80a8141ed3a7ea462c6e280e7017ea0` passed Windows CI `34039544202` / #378, P06-007 Workspace Search `34039544201` / #107, and P06-008 Large Workspace Safeguards `34039544196` / #91. PR #167 was normally merged as `9712e84c4596e18d0d80b0cfbd93b37ad65fb73d`; that exact canonical main passed Windows CI `34040051645` / #379, P06-007 Workspace Search `34040051726` / #108, and P06-008 Large Workspace Safeguards `34040051678` / #92. Coverage includes typed success/non-repository/bare/unavailable/query-failed results; staged versus work-tree state; untracked, rename/copy and conflict classification; NUL-safe porcelain-v2 parsing; canonical repository-relative forward-slash paths; explicit UTF-8 Git output decoding for Arabic/Unicode/space-containing names; `GIT_OPTIONAL_LOCKS=0`; non-interactive execution; bounded timeout/cancellation and process-tree cleanup; real disposable Git fixtures; index non-mutation; and Windows-safe fixture cleanup. Task evidence: `evidence/phases/P07/P07_002_INTEGRATED_RECONCILIATION_2026-09-06.md`. No mutation/diff/later-P07 surface or new owner-only obligation is claimed; P07 remains `IN_PROGRESS`, P07-003 through P07-011 remain PENDING, P08+ remain prohibited, and `VERIFIED_FINAL_COMPLETE=false`.

'@
Replace-ExactOnce -Path 'docs/TASK_LEDGER.md' -Old $ledgerMarker -New ($ledgerInsertion + $ledgerMarker)

$oldNextState = '`CURRENT_PHASE = P07` and P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`. `FCCD-P07-001` is CLOSED after exact PR-head validation, normal merge integration as `9c3b0437f92a547453e8fdcdce22ab96d0084ade`, exact post-merge canonical-main validation, and durable reconciliation evidence. `FCCD-P07-002` through `FCCD-P07-011` remain PENDING.'
$newNextState = '`CURRENT_PHASE = P07` and P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`. `FCCD-P07-001` and `FCCD-P07-002` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable reconciliation evidence. `FCCD-P07-003` through `FCCD-P07-011` remain PENDING.'
Replace-ExactOnce -Path 'docs/TASK_LEDGER.md' -Old $oldNextState -New $newNextState

$oldNextAction = 'After this P07-001 reconciliation is integrated and exact resulting `main` remains green, re-run the Worker Protocol claim map. Recover/integrate any newly surfaced higher-priority legitimate defect first. Otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-002 — Status/changed-files surface`. Do not advance to P08 until every mandatory P07 task is CLOSED and the P07 phase exit gate is truthfully resolved under canonical governance. Only a genuinely owner-environment-bound residual may be queued under owner-last; do not fabricate target/manual evidence.'
$newNextAction = 'After this P07-002 reconciliation is integrated and exact resulting `main` remains green, re-run the Worker Protocol claim map. Recover/integrate any newly surfaced higher-priority legitimate defect first. Otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-003 — Diff viewer` if still unclaimed and dependency-valid. Do not advance to P08 until every mandatory P07 task is CLOSED and the P07 phase exit gate is truthfully resolved under canonical governance. Only a genuinely owner-environment-bound residual may be queued under owner-last; do not fabricate target/manual evidence.'
Replace-ExactOnce -Path 'docs/TASK_LEDGER.md' -Old $oldNextAction -New $newNextAction

# Durable integrated evidence.
$evidence = @'
# FCCD-P07-002 Integrated Reconciliation — 2026-09-06

## Decision

`FCCD-P07-002 — Status/changed-files surface` is **CLOSED** as a cloud-actionable task. Its production implementation is normally integrated and exact-main verified. P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`; this task closure does not advance P08 or authorize any P10 work.

## Production integration

The accepted implementation candidate is `1341412ee80a8141ed3a7ea462c6e280e7017ea0` from PR #167 (`worker/fccd-p07-002-status-changed-files`). It extends the Application-owned `IGitService` with a typed read-only status query and implements a bounded Git CLI adapter over `git status --porcelain=v2 -z --untracked-files=all --renames`.

The contract classifies success, non-repository, bare-repository, Git-unavailable and query-failed outcomes. Per-file results distinguish index versus work-tree state and represent modified, added, deleted, renamed, copied, type-changed, unmerged and untracked paths. Status parsing is NUL-delimited rather than shell/quote parsed, repository-relative paths are normalized to `/`, rename source paths are retained, Git stdout/stderr are explicitly decoded as UTF-8, prompts are disabled, `GIT_OPTIONAL_LOCKS=0` prevents status from intentionally refreshing the index, and timeout/cancellation terminate only the owned process tree.

Exact PR-head gates on `1341412ee80a8141ed3a7ea462c6e280e7017ea0` all completed SUCCESS:

- Windows CI run `34039544202` / run #378 — SUCCESS.
- P06-007 Workspace Search run `34039544201` / run #107 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34039544196` / run #91 — SUCCESS.

PR #167 was normally merged without squash/rebase as `9712e84c4596e18d0d80b0cfbd93b37ad65fb73d`, preserving tested implementation ancestry and canonical main ancestry.

Exact post-merge canonical-main gates on `9712e84c4596e18d0d80b0cfbd93b37ad65fb73d` all completed SUCCESS:

- Windows CI run `34040051645` / run #379 — SUCCESS.
- P06-007 Workspace Search run `34040051726` / run #108 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34040051678` / run #92 — SUCCESS.

No task-local product defect or exact-main regression remained after integration.

## Cloud repair and validation evidence

The real-Git test suite covers a clean repository queried from a nested path; staged and work-tree changes; deletes; renames; Arabic/Unicode and space-containing untracked paths; bare and ordinary non-repositories; Git-unavailable classification; cancellation; timeout-bound validation; and verification that a status query does not intentionally refresh/rewrite the Git index.

CI exposed two cloud-repairable Windows defects and both were repaired rather than deferred. First, disposable real-Git fixtures could leave read-only loose object files that prevented recursive temp cleanup on Windows; the shared temporary-directory cleanup now clears read-only attributes before deletion. Second, the Arabic path fixture exposed an output-decoding boundary in Git for Windows; the CLI adapter now explicitly decodes stdout/stderr as UTF-8. The final exact-head and exact-main gates above prove those repairs on the permanent Windows lane.

## Cloud evidence boundary

This evidence proves the bounded read-only status/changed-files contract and canonical integration provenance. It does not claim P07-003 diff-viewer behavior, stage/unstage, branch create/checkout, fetch/pull, commit/push, history, dirty-tree provenance policy, destructive-operation safeguards, P07 phase closure, P08 authorization, P10 Unity functionality, or release readiness.

## Owner-last classification

P07-002 introduces no genuinely owner-only acceptance requirement. No manual/target evidence was fabricated or newly queued. `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` remains unchanged with exactly the two pre-existing release-blocking obligations:

- `OWNER-P04-008-REAL-TARGET`.
- `OWNER-P05-EXIT-REAL-TARGET`.

`KNOWN_RELEASE_BLOCKERS=2` and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.

## Remaining phase state

- `CURRENT_PHASE=P07`.
- `CURRENT_PHASE_STATE=IN_PROGRESS`.
- `PHASE_EXIT_GATE=NOT_RUN`.
- `FCCD-P07-001` and `FCCD-P07-002` are CLOSED.
- `FCCD-P07-003` through `FCCD-P07-011` remain PENDING.
- P08 and later implementation remain prohibited until P07 is truthfully closed under canonical governance.

## Reconciliation candidate hygiene

The temporary branch-only reconciliation workflow and helper script are orchestration-only and must be removed before permanent validation. The durable reconciliation diff must be limited to `CURRENT_PHASE.md`, `PROJECT_CONTROL.md`, `docs/TASK_LEDGER.md`, and this evidence artifact.

## Next legal cloud action

After this reconciliation is normally integrated and its exact resulting main remains green, re-fetch live claims. Recover any newly surfaced higher-priority legitimate defect or integration-pending work first; otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-003 — Diff viewer` if still unclaimed. Do not start P10 while P07/P08/P09 remain incomplete.
'@
$evidenceFullPath = Join-Path $repoRoot $evidencePath
if (Test-Path $evidenceFullPath) {
    throw "Evidence path already exists: $evidencePath"
}
[IO.Directory]::CreateDirectory((Split-Path $evidenceFullPath -Parent)) | Out-Null
[IO.File]::WriteAllText($evidenceFullPath, $evidence, [Text.UTF8Encoding]::new($false))

# Guard owner-last queue and exact durable scope.
git diff --exit-code -- docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md
if ($LASTEXITCODE -ne 0) { throw 'Owner acceptance queue changed unexpectedly.' }

$expected = @(
    'CURRENT_PHASE.md',
    'PROJECT_CONTROL.md',
    'docs/TASK_LEDGER.md',
    $evidencePath
) | Sort-Object
$actual = @(git status --short | ForEach-Object { $_.Substring(3) }) | Sort-Object
if (@(Compare-Object -ReferenceObject $expected -DifferenceObject $actual).Count -ne 0) {
    throw "Unexpected reconciliation scope. Expected: $($expected -join ', '); actual: $($actual -join ', ')"
}

git diff --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed.' }

git config user.name 'github-actions[bot]'
git config user.email '41898282+github-actions[bot]@users.noreply.github.com'
git add -- CURRENT_PHASE.md PROJECT_CONTROL.md docs/TASK_LEDGER.md $evidencePath
git commit -m 'docs: reconcile P07-002 integrated closure'
if ($LASTEXITCODE -ne 0) { throw 'Reconciliation commit failed.' }
git push origin "HEAD:$env:GITHUB_REF_NAME"
if ($LASTEXITCODE -ne 0) { throw 'Reconciliation push failed.' }
