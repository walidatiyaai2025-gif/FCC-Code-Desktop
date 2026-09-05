[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$QueuePath = 'docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-LastExitCode {
    param([string]$Stage)
    if ($LASTEXITCODE -ne 0) {
        throw "$Stage failed with exit code $LASTEXITCODE."
    }
}

function Read-OwnerQueue {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Canonical owner acceptance queue is missing: $Path"
    }

    $text = Get-Content -LiteralPath $Path -Raw
    $pattern = '(?s)<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->\s*```json\s*(.*?)\s*```\s*<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->'
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) {
        throw 'Canonical owner acceptance queue JSON block is missing or malformed.'
    }

    try {
        return ($match.Groups[1].Value | ConvertFrom-Json -Depth 20)
    }
    catch {
        throw "Canonical owner acceptance queue JSON is invalid: $($_.Exception.Message)"
    }
}

function Resolve-RepositoryPath {
    param(
        [string]$Root,
        [string]$RelativePath,
        [string]$Label
    )

    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath.Contains('..', [StringComparison]::Ordinal)) {
        throw "$Label must be a repository-relative path without traversal: $RelativePath"
    }

    $fullPath = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    $rootPrefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label escaped the repository root: $RelativePath"
    }
    return $fullPath
}

if (-not $IsWindows) {
    throw 'Final owner acceptance must run on the authoritative owner Windows environment.'
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required for final owner acceptance provenance.'
}
if (-not (Get-Command pwsh -ErrorAction SilentlyContinue)) {
    throw 'PowerShell 7 (pwsh) is required for final owner acceptance.'
}

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$fullQueuePath = Resolve-RepositoryPath $resolvedRoot $QueuePath 'Queue path'
$queue = Read-OwnerQueue $fullQueuePath
if ($queue.schemaVersion -ne 1) {
    throw "Unsupported owner acceptance queue schemaVersion '$($queue.schemaVersion)'."
}

$queuedItems = @($queue.items | Where-Object state -eq 'QUEUED')
if ($queuedItems.Count -eq 0) {
    Write-Host 'Final owner acceptance queue has no QUEUED items. Repository reconciliation must still prove release eligibility.'
    exit 0
}

Push-Location $resolvedRoot
try {
    $gitRoot = (& git rev-parse --show-toplevel 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'Repository identity check'
    if ([IO.Path]::GetFullPath($gitRoot).TrimEnd('\') -ne [IO.Path]::GetFullPath($resolvedRoot).TrimEnd('\')) {
        throw "Final owner acceptance resolved the wrong repository. Expected '$resolvedRoot', got '$gitRoot'."
    }

    $head = (& git rev-parse HEAD 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'Exact HEAD resolution'
    if ($head -notmatch '^[0-9a-f]{40}$') {
        throw "Final owner acceptance resolved an invalid HEAD SHA: '$head'."
    }

    $allowedEvidenceRoots = [System.Collections.Generic.List[string]]::new()
    foreach ($item in $queuedItems) {
        if (-not $item.releaseBlocking) {
            throw "Queued owner item '$($item.id)' must remain releaseBlocking=true."
        }
        $evidencePath = Resolve-RepositoryPath $resolvedRoot ([string]$item.expectedEvidencePath) "Evidence path for $($item.id)"
        $allowedEvidenceRoots.Add([IO.Path]::GetFullPath((Split-Path -Parent $evidencePath)).TrimEnd('\'))
    }

    $statusLines = @(& git status --porcelain --untracked-files=all)
    Assert-LastExitCode 'Exact worktree status check'
    $disallowed = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $statusLines) {
        if (-not $line) { continue }
        $pathText = if ($line.Length -gt 3) { $line.Substring(3).Trim('"') } else { $line }
        $candidate = [IO.Path]::GetFullPath((Join-Path $resolvedRoot $pathText))
        $isAllowedEvidence = $false
        foreach ($allowedRoot in $allowedEvidenceRoots) {
            if ($candidate.Equals($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -or
                $candidate.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                $isAllowedEvidence = $true
                break
            }
        }
        if (-not $isAllowedEvidence) {
            $disallowed.Add($line)
        }
    }
    if ($disallowed.Count -gt 0) {
        throw "Final owner acceptance requires exact HEAD source/config inputs. Disallowed worktree changes: $($disallowed -join '; ')"
    }

    Write-Host "Final owner acceptance candidate: $head"
    foreach ($item in $queuedItems) {
        if ([string]::IsNullOrWhiteSpace([string]$item.command)) {
            throw "Queued owner item '$($item.id)' has no tracked command."
        }
        $commandText = ([string]$item.command).Replace('/', '\')
        if (-not $commandText.StartsWith('.\tools\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $commandText.EndsWith('.ps1', [StringComparison]::OrdinalIgnoreCase) -or
            $commandText.Contains(' ', [StringComparison]::Ordinal)) {
            throw "Queued owner item '$($item.id)' command must be one tracked PowerShell script under .\\tools\\ with no inline shell arguments."
        }

        $relativeCommand = $commandText.Substring(2)
        $scriptPath = Resolve-RepositoryPath $resolvedRoot $relativeCommand "Command for $($item.id)"
        if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
            throw "Queued owner command does not exist for '$($item.id)': $scriptPath"
        }

        Write-Host "Running queued owner item: $($item.id) [$($item.classification)]"
        & pwsh -NoProfile -File $scriptPath -RepositoryRoot $resolvedRoot
        Assert-LastExitCode "Owner acceptance item $($item.id)"

        $evidencePath = Resolve-RepositoryPath $resolvedRoot ([string]$item.expectedEvidencePath) "Evidence path for $($item.id)"
        if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) {
            throw "Owner acceptance item '$($item.id)' returned without expected evidence: $evidencePath"
        }
        if ((Get-Item -LiteralPath $evidencePath).Length -le 0) {
            throw "Owner acceptance evidence is empty for '$($item.id)'."
        }

        if ([IO.Path]::GetExtension($evidencePath).Equals('.json', [StringComparison]::OrdinalIgnoreCase)) {
            try {
                $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json -Depth 30
            }
            catch {
                throw "Owner acceptance evidence JSON is invalid for '$($item.id)': $($_.Exception.Message)"
            }
            if ($evidence.evidenceClassification -ne $item.classification) {
                throw "Owner evidence classification mismatch for '$($item.id)'. Expected '$($item.classification)', got '$($evidence.evidenceClassification)'."
            }
            if ($evidence.testedRepoSha -ne $head) {
                throw "Owner evidence SHA mismatch for '$($item.id)'. Expected '$head', got '$($evidence.testedRepoSha)'."
            }
            if ($evidence.overallStatus -ne 'PASS') {
                throw "Owner evidence did not PASS for '$($item.id)': $($evidence.overallStatus)"
            }
        }

        $rawEvidence = Get-Content -LiteralPath $evidencePath -Raw
        if ($rawEvidence -match '(?i)Authorization\s*:\s*Bearer\s+\S+' -or $rawEvidence -match '(?i)\bsk-[A-Za-z0-9_-]{12,}') {
            throw "Potential plaintext secret detected in owner evidence for '$($item.id)'."
        }

        Write-Host "Owner item execution PASS: $($item.id). Evidence still requires review/integration; queue state remains QUEUED."
    }

    Write-Host 'FINAL_OWNER_EXECUTION_COMPLETE_RECONCILIATION_REQUIRED'
}
finally {
    Pop-Location
}
