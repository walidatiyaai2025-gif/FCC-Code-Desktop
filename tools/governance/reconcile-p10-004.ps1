[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Replace-ExactlyOnce {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$OldText,
        [Parameter(Mandatory)] [string]$NewText
    )

    $content = [System.IO.File]::ReadAllText($Path)
    $first = $content.IndexOf($OldText, [System.StringComparison]::Ordinal)
    if ($first -lt 0) {
        throw "Expected canonical text was not found in $Path."
    }

    $second = $content.IndexOf($OldText, $first + $OldText.Length, [System.StringComparison]::Ordinal)
    if ($second -ge 0) {
        throw "Expected canonical text appears more than once in $Path."
    }

    $updated = $content.Substring(0, $first) + $NewText + $content.Substring($first + $OldText.Length)
    [System.IO.File]::WriteAllText($Path, $updated, $utf8NoBom)
}

$currentPhase = 'CURRENT_PHASE.md'
$ledger = 'docs/TASK_LEDGER.md'

Replace-ExactlyOnce -Path $currentPhase `
    -OldText '- `FCCD-P10-004` — Unity process/project resource locking — PENDING.' `
    -NewText '- `FCCD-P10-004` — Unity process/project resource locking — CLOSED.'

Replace-ExactlyOnce -Path $ledger `
    -OldText '| FCCD-P10-004 | Unity process/project resource locking | PENDING |' `
    -NewText '| FCCD-P10-004 | Unity process/project resource locking | CLOSED |'

$currentContent = [System.IO.File]::ReadAllText($currentPhase)
if ($currentContent.Contains('## P10-004 integration provenance', [System.StringComparison]::Ordinal)) {
    throw 'P10-004 provenance already exists in CURRENT_PHASE.md; refusing duplicate insertion.'
}

$currentMarker = '## P10-001 integration provenance'
$currentMarkerIndex = $currentContent.IndexOf($currentMarker, [System.StringComparison]::Ordinal)
if ($currentMarkerIndex -lt 0) {
    throw 'P10-001 provenance marker was not found in CURRENT_PHASE.md.'
}

$currentProvenance = @'
## P10-004 integration provenance

- Task: `FCCD-P10-004 — Unity process/project resource locking` — `CLOSED` in this reconciliation candidate.
- Selection boundary: scheduling hint `FCCD-P19-002` was future work while canonical `CURRENT_PHASE=P10`; live selection found P10-004 PENDING with no competing P10-004 PR/branch/claim.
- Implementation PR: #239 (`worker/fccd-p10-004-unity-resource-locking`).
- Exact accepted cloud candidate: `5956e00286fdaff230c2b13f0e1919c208da60ed`.
- Exact candidate P10-004 Unity Resource Locking: run `34213834311` — SUCCESS.
- Exact candidate Windows CI: run `34213834508` — SUCCESS.
- Exact candidate Workspace Search: run `34213834319` — SUCCESS.
- Exact candidate Large Workspace Safeguards: run `34213834412` — SUCCESS.
- Initial candidate `28ebb1a96bd3bcc74a5791b8e0f3ba7203622526` exposed fixture-only analyzer `CA1859`; it was repaired on the same branch without suppression, warning demotion, test removal, or safety weakening.
- Implementation reuses the P09 provider-neutral lock manager and acquires both a logical Unity process slot and a hashed canonical physical-project lock; equivalent Windows project-path representations collide intentionally while unrelated projects can run concurrently.
- Fixture acceptance covers contention by logical identity and physical root, cancellation propagation, partial-acquisition cleanup, lease release/reacquisition, independent-project concurrency, non-Unity rejection, path canonicalization, and raw-path non-disclosure.
- Integrated cloud evidence: `evidence/phases/P10/P10_004_INTEGRATED_RECONCILIATION_2026-09-08.md`.
- No owner-only evidence is required or added. P10 remains `IN_PROGRESS`; P10-005 through P10-013 remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P11+ remain prohibited; `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `VERIFIED_FINAL_COMPLETE=false`.

'@

$currentContent = $currentContent.Insert($currentMarkerIndex, $currentProvenance)
[System.IO.File]::WriteAllText($currentPhase, $currentContent, $utf8NoBom)

$ledgerContent = [System.IO.File]::ReadAllText($ledger)
$ledgerProvenanceMarker = '`FCCD-P10-001` is CLOSED'
if ($ledgerContent.Contains('`FCCD-P10-004` is CLOSED', [System.StringComparison]::Ordinal)) {
    throw 'P10-004 provenance already exists in TASK_LEDGER.md; refusing duplicate insertion.'
}

$ledgerMarkerIndex = $ledgerContent.IndexOf($ledgerProvenanceMarker, [System.StringComparison]::Ordinal)
if ($ledgerMarkerIndex -lt 0) {
    throw 'P10-001 explanatory marker was not found in TASK_LEDGER.md.'
}

$ledgerParagraph = @'
`FCCD-P10-004` is CLOSED from Unity process/project resource locking implemented in PR #239. Exact accepted cloud candidate `5956e00286fdaff230c2b13f0e1919c208da60ed` passed P10-004 Unity Resource Locking `34213834311`, Windows CI `34213834508`, Workspace Search `34213834319`, and Large Workspace Safeguards `34213834412`. The implementation reuses the P09 lock manager, declares a logical Unity process lock plus a SHA-256-fingerprinted canonical physical-project lock, prevents duplicate logical or physical project execution, preserves cancellation/partial-acquisition cleanup and lease release, permits unrelated-project concurrency, and does not disclose raw project paths. Initial fixture analyzer `CA1859` was repaired without suppression or weakening gates. Task evidence: `evidence/phases/P10/P10_004_INTEGRATED_RECONCILIATION_2026-09-08.md`. No owner-only evidence is required or queued. P10 remains `IN_PROGRESS`; P10-005 through P10-013 remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P11+ remain prohibited; `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `VERIFIED_FINAL_COMPLETE=false`.

'@

$ledgerContent = $ledgerContent.Insert($ledgerMarkerIndex, $ledgerParagraph)
[System.IO.File]::WriteAllText($ledger, $ledgerContent, $utf8NoBom)

Write-Host 'P10-004 canonical reconciliation patch prepared successfully.'
