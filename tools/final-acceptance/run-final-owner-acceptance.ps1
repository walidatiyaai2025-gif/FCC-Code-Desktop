[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$CandidateSha,
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$QueuePath = 'docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md',
    [string]$SummaryPath = 'evidence/final-owner/FINAL_OWNER_ACCEPTANCE_SUMMARY.json',
    [switch]$Resume,
    [switch]$SelfTestOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:MaximumEvidenceBytes = 32MB
$script:AllowedClassifications = @(
    'REAL_TARGET',
    'MANUAL_VISUAL',
    'INSTALLER_LIFECYCLE',
    'CLEAN_MACHINE',
    'EXTERNAL_HARDWARE'
)

function Assert-LastExitCode {
    param([string]$Stage)
    if ($LASTEXITCODE -ne 0) {
        throw "$Stage failed with exit code $LASTEXITCODE."
    }
}

function Bound-Text {
    param([AllowEmptyString()][string]$Text, [int]$MaximumCharacters = 2048)
    if ($null -eq $Text) { return '' }
    if ($Text.Length -le $MaximumCharacters) { return $Text }
    return $Text.Substring(0, $MaximumCharacters)
}

function Protect-Text {
    param([AllowEmptyString()][string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return '' }

    $protected = $Text
    $protected = [regex]::Replace(
        $protected,
        '(?i)(Authorization\s*:\s*Bearer\s+)[^\s"'';]+',
        '$1[REDACTED]')
    $protected = [regex]::Replace(
        $protected,
        '(?i)\b(?:sk|rk|pk|ghp|gho|ghu|ghs|github_pat|glpat)[-_][A-Za-z0-9_-]{12,}\b',
        '[REDACTED_TOKEN]')
    $protected = [regex]::Replace(
        $protected,
        '(?i)\b(api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|password|passwd)\b\s*[:=]\s*["'']?[A-Za-z0-9+/=_\-.]{8,}',
        '$1=[REDACTED]')
    $protected = [regex]::Replace(
        $protected,
        '(?is)-----BEGIN [^-\r\n]*PRIVATE KEY-----.*?-----END [^-\r\n]*PRIVATE KEY-----',
        '[REDACTED_PRIVATE_KEY]')
    return $protected
}

function Get-SecretIndicator {
    param([AllowEmptyString()][string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return $null }

    if ($Text -match '(?i)Authorization\s*:\s*Bearer\s+[^\s"'';]+') { return 'bearer-token' }
    if ($Text -match '(?i)\b(?:sk|rk|pk|ghp|gho|ghu|ghs|github_pat|glpat)[-_][A-Za-z0-9_-]{12,}\b') { return 'token-prefix' }
    if ($Text -match '(?i)\b(api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|password|passwd)\b\s*[:=]\s*["'']?[A-Za-z0-9+/=_\-.]{8,}') { return 'secret-assignment' }
    if ($Text -match '(?is)-----BEGIN [^-\r\n]*PRIVATE KEY-----') { return 'private-key' }
    return $null
}

function Assert-NoSecretMaterial {
    param([AllowEmptyString()][string]$Text, [string]$Label)
    $indicator = Get-SecretIndicator $Text
    if ($null -ne $indicator) {
        throw "Potential plaintext secret detected in $Label ($indicator). Evidence is rejected rather than auto-scrubbed."
    }
}

function Read-OwnerQueue {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Canonical owner acceptance queue is missing: $Path"
    }

    $text = Get-Content -LiteralPath $Path -Raw
    Assert-NoSecretMaterial $text 'canonical owner acceptance queue'
    $pattern = '(?s)<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->\s*```json\s*(.*?)\s*```\s*<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->'
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) {
        throw 'Canonical owner acceptance queue JSON block is missing or malformed.'
    }

    try {
        return ($match.Groups[1].Value | ConvertFrom-Json -Depth 30)
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

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        throw "$Label cannot be empty."
    }
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

function Get-ExactHead {
    $head = (& git rev-parse HEAD 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'Exact HEAD resolution'
    if ($head -notmatch '^[0-9a-f]{40}$') {
        throw "Final owner acceptance resolved an invalid HEAD SHA: '$head'."
    }
    return $head.ToLowerInvariant()
}

function Assert-ExactCandidate {
    param([string]$ExpectedSha)
    $actual = Get-ExactHead
    if (-not $actual.Equals($ExpectedSha, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Final owner acceptance candidate moved. Expected '$ExpectedSha', got '$actual'."
    }
}

function Test-PathUnderAllowedRoot {
    param([string]$Candidate, [string[]]$AllowedRoots)
    foreach ($allowedRoot in $AllowedRoots) {
        if ($Candidate.Equals($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -or
            $Candidate.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function Assert-CleanCandidateInputs {
    param([string]$Root, [string[]]$AllowedRoots)

    $statusLines = @(& git status --porcelain --untracked-files=all)
    Assert-LastExitCode 'Exact worktree status check'
    $disallowed = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $statusLines) {
        if (-not $line) { continue }
        $pathText = if ($line.Length -gt 3) { $line.Substring(3).Trim('"') } else { $line }
        if ($pathText.Contains(' -> ', [StringComparison]::Ordinal)) {
            $pathText = ($pathText -split ' -> ', 2)[1].Trim('"')
        }
        $candidate = [IO.Path]::GetFullPath((Join-Path $Root $pathText))
        if (-not (Test-PathUnderAllowedRoot $candidate $AllowedRoots)) {
            $disallowed.Add($line)
        }
    }
    if ($disallowed.Count -gt 0) {
        throw "Final owner acceptance requires exact HEAD source/config/packaging inputs. Disallowed worktree changes: $($disallowed -join '; ')"
    }
}

function Assert-QueueItemContract {
    param([object]$Item, [string]$Root)

    foreach ($field in @(
        'id','sourceKind','sourcePhase','classification','state','whyOwnerOnly','cloudEvidence',
        'command','prerequisites','expectedEvidencePath','passCriteria','reconciliationRule','releaseBlocking'
    )) {
        if ($Item.PSObject.Properties.Name -notcontains $field) {
            throw "Owner queue item is missing required field '$field'."
        }
    }

    if ([string]::IsNullOrWhiteSpace([string]$Item.id)) {
        throw 'Owner queue item id cannot be empty.'
    }
    if ($script:AllowedClassifications -notcontains [string]$Item.classification) {
        throw "Owner queue item '$($Item.id)' has unsupported classification '$($Item.classification)'."
    }
    if (@('QUEUED','PASS_INTEGRATED') -notcontains [string]$Item.state) {
        throw "Owner queue item '$($Item.id)' has unsupported state '$($Item.state)'."
    }
    if (-not [bool]$Item.releaseBlocking) {
        throw "Owner queue item '$($Item.id)' must remain releaseBlocking=true."
    }
    if (@($Item.prerequisites).Count -lt 1) {
        throw "Owner queue item '$($Item.id)' has no declared prerequisites."
    }

    $commandText = ([string]$Item.command).Replace('/', '\')
    if (-not $commandText.StartsWith('.\tools\', [StringComparison]::OrdinalIgnoreCase) -or
        -not $commandText.EndsWith('.ps1', [StringComparison]::OrdinalIgnoreCase) -or
        $commandText.Contains(' ', [StringComparison]::Ordinal)) {
        throw "Queued owner item '$($Item.id)' command must be one tracked PowerShell script under .\\tools\\ with no inline shell arguments."
    }
    $scriptPath = Resolve-RepositoryPath $Root $commandText.Substring(2) "Command for $($Item.id)"
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Queued owner command does not exist for '$($Item.id)': $scriptPath"
    }

    $cloudEvidence = Resolve-RepositoryPath $Root ([string]$Item.cloudEvidence) "Cloud evidence for $($Item.id)"
    if (-not (Test-Path -LiteralPath $cloudEvidence -PathType Leaf)) {
        throw "Cloud-complete evidence is missing for queued owner item '$($Item.id)'."
    }

    $expectedEvidence = Resolve-RepositoryPath $Root ([string]$Item.expectedEvidencePath) "Expected evidence for $($Item.id)"
    $relativeEvidence = [string]$Item.expectedEvidencePath
    if (-not $relativeEvidence.Replace('\','/').StartsWith('evidence/', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Owner evidence for '$($Item.id)' must remain under evidence/."
    }

    return [pscustomobject]@{
        CommandPath = $scriptPath
        EvidencePath = $expectedEvidence
    }
}

function Get-DeclaredPrerequisiteAssessment {
    param(
        [object]$Item,
        [string]$ExpectedSha,
        [string]$Root,
        [string[]]$AllowedRoots
    )

    $verified = [System.Collections.Generic.List[string]]::new()
    $delegated = [System.Collections.Generic.List[string]]::new()

    foreach ($raw in @($Item.prerequisites)) {
        $requirement = [string]$raw
        if ([string]::IsNullOrWhiteSpace($requirement)) {
            throw "Owner queue item '$($Item.id)' contains an empty prerequisite."
        }

        if ($requirement -match '(?i)^Owner Windows target$') {
            if (-not $IsWindows) { throw "Prerequisite missing for '$($Item.id)': $requirement" }
            $verified.Add($requirement)
            continue
        }
        if ($requirement -match '(?i)Git.*PATH') {
            if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "Prerequisite missing for '$($Item.id)': $requirement" }
            $verified.Add($requirement)
            continue
        }
        if ($requirement -match '(?i)PowerShell\s+7|pwsh') {
            if (-not (Get-Command pwsh -ErrorAction SilentlyContinue)) { throw "Prerequisite missing for '$($Item.id)': $requirement" }
            $verified.Add($requirement)
            continue
        }
        if ($requirement -match '(?i)\.NET SDK\s+10\.0\.400') {
            if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
                throw "Prerequisite missing for '$($Item.id)': .NET SDK 10.0.400"
            }
            $sdk = (& dotnet --version 2>&1 | Out-String).Trim()
            Assert-LastExitCode "SDK prerequisite for $($Item.id)"
            if ($sdk -ne '10.0.400') {
                throw "Prerequisite mismatch for '$($Item.id)': expected .NET SDK 10.0.400, got '$sdk'."
            }
            $verified.Add($requirement)
            continue
        }
        if ($requirement -match '(?i)Exact intended canonical candidate HEAD|Exact.*candidate') {
            Assert-ExactCandidate $ExpectedSha
            $verified.Add($requirement)
            continue
        }
        if ($requirement -match '(?i)Clean .*worktree|Clean source.*worktree') {
            Assert-CleanCandidateInputs $Root $AllowedRoots
            $verified.Add($requirement)
            continue
        }

        # Tool/provider/manual/clean-machine specifics are intentionally delegated to the
        # tracked per-item runner. That runner is the authoritative place to detect the
        # installed FCC/provider, Unity, Blender, installer lifecycle, visual, hardware,
        # or clean-machine prerequisite without the master inventing environment facts.
        $delegated.Add($requirement)
    }

    return [pscustomobject]@{
        Verified = @($verified)
        DelegatedToTrackedRunner = @($delegated)
    }
}

function Read-EvidenceText {
    param([string]$Path, [string]$ItemId)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Owner acceptance item '$ItemId' returned without expected evidence: $Path"
    }
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0) {
        throw "Owner acceptance evidence is empty for '$ItemId'."
    }
    if ($file.Length -gt $script:MaximumEvidenceBytes) {
        throw "Owner acceptance evidence exceeds the $($script:MaximumEvidenceBytes)-byte safety limit for '$ItemId'."
    }

    $raw = Get-Content -LiteralPath $Path -Raw
    Assert-NoSecretMaterial $raw "owner evidence for '$ItemId'"
    return $raw
}

function Assert-MachineEvidenceObject {
    param(
        [object]$Evidence,
        [object]$Item,
        [string]$ExpectedSha
    )

    if ($Evidence.PSObject.Properties.Name -notcontains 'evidenceClassification') {
        throw "Owner evidence is missing evidenceClassification for '$($Item.id)'."
    }
    if ($Evidence.PSObject.Properties.Name -notcontains 'testedRepoSha') {
        throw "Owner evidence is missing testedRepoSha for '$($Item.id)'."
    }
    if ($Evidence.PSObject.Properties.Name -notcontains 'overallStatus') {
        throw "Owner evidence is missing overallStatus for '$($Item.id)'."
    }
    if ($Evidence.evidenceClassification -ne $Item.classification) {
        throw "Owner evidence classification mismatch for '$($Item.id)'."
    }
    if (-not ([string]$Evidence.testedRepoSha).Equals($ExpectedSha, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Owner evidence SHA mismatch for '$($Item.id)'."
    }
    if ($Evidence.overallStatus -ne 'PASS') {
        throw "Owner evidence did not PASS for '$($Item.id)'."
    }
}

function Get-ExistingEvidenceState {
    param([string]$Path, [object]$Item, [string]$ExpectedSha)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return 'MISSING'
    }
    if (-not [IO.Path]::GetExtension($Path).Equals('.json', [StringComparison]::OrdinalIgnoreCase)) {
        [void](Read-EvidenceText $Path $Item.id)
        return 'UNVALIDATABLE'
    }

    try {
        $raw = Read-EvidenceText $Path $Item.id
        $evidence = $raw | ConvertFrom-Json -Depth 40
        Assert-MachineEvidenceObject $evidence $Item $ExpectedSha
        return 'VALID_PASS'
    }
    catch {
        if ($_.Exception.Message -match '(?i)Potential plaintext secret') { throw }
        return 'INVALID'
    }
}

function Get-OverallStatus {
    param([object[]]$Items)
    if (@($Items | Where-Object status -eq 'FAIL').Count -gt 0) { return 'FAIL' }
    if (@($Items | Where-Object status -eq 'MISSING').Count -gt 0) { return 'MISSING' }
    return 'PASS'
}

function Write-FinalSummary {
    param(
        [string]$Path,
        [string]$ExpectedSha,
        [string]$QueueRelativePath,
        [object[]]$Items,
        [string]$Mode,
        [string]$SelfTestStatus = 'NOT_RUN'
    )

    $overall = Get-OverallStatus $Items
    $summary = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        candidateSha = $ExpectedSha
        queuePath = $QueueRelativePath.Replace('\','/')
        mode = $Mode
        overallStatus = $overall
        selfTestStatus = $SelfTestStatus
        ownerExecutionClaimed = $false
        reconciliationRequired = ($Mode -eq 'OWNER_EXECUTION' -and $overall -eq 'PASS')
        items = @($Items)
    }

    $json = $summary | ConvertTo-Json -Depth 30
    Assert-NoSecretMaterial $json 'final owner machine-readable summary'
    [void](New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force)
    $temporary = "$Path.tmp"
    Set-Content -LiteralPath $temporary -Value $json -Encoding utf8NoBOM
    Move-Item -LiteralPath $temporary -Destination $Path -Force
}

function Invoke-SelfTest {
    param([string]$ExpectedSha)

    $synthetic = 'Authorization: Bearer synthetic-token-value-1234567890'
    $protected = Protect-Text $synthetic
    if ($protected -notmatch '\[REDACTED\]' -or $protected.Contains('synthetic-token-value-1234567890', [StringComparison]::Ordinal)) {
        throw 'Final owner pack self-test failed: bearer-token redaction is not fail-safe.'
    }
    if ((Get-SecretIndicator $synthetic) -ne 'bearer-token') {
        throw 'Final owner pack self-test failed: bearer-token detection did not trigger.'
    }
    if ($null -ne (Get-SecretIndicator 'safe acceptance metadata only')) {
        throw 'Final owner pack self-test failed: benign metadata produced a false secret indicator.'
    }

    $syntheticItem = [pscustomobject]@{ id = 'SELF-TEST'; classification = 'REAL_TARGET' }
    $good = [pscustomobject]@{
        evidenceClassification = 'REAL_TARGET'
        testedRepoSha = $ExpectedSha
        overallStatus = 'PASS'
    }
    Assert-MachineEvidenceObject $good $syntheticItem $ExpectedSha

    $staleRejected = $false
    try {
        $stale = [pscustomobject]@{
            evidenceClassification = 'REAL_TARGET'
            testedRepoSha = ('0' * 40)
            overallStatus = 'PASS'
        }
        Assert-MachineEvidenceObject $stale $syntheticItem $ExpectedSha
    }
    catch { $staleRejected = $true }
    if (-not $staleRejected) {
        throw 'Final owner pack self-test failed: stale SHA evidence was accepted.'
    }

    $failRejected = $false
    try {
        $failed = [pscustomobject]@{
            evidenceClassification = 'REAL_TARGET'
            testedRepoSha = $ExpectedSha
            overallStatus = 'FAIL'
        }
        Assert-MachineEvidenceObject $failed $syntheticItem $ExpectedSha
    }
    catch { $failRejected = $true }
    if (-not $failRejected) {
        throw 'Final owner pack self-test failed: FAIL evidence was accepted.'
    }
}

$normalizedCandidate = $CandidateSha.ToLowerInvariant()
$mode = if ($SelfTestOnly) { 'SELF_TEST_ONLY' } else { 'OWNER_EXECUTION' }
$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$fullQueuePath = Resolve-RepositoryPath $resolvedRoot $QueuePath 'Queue path'
$fullSummaryPath = Resolve-RepositoryPath $resolvedRoot $SummaryPath 'Summary path'
if (-not $SummaryPath.Replace('\','/').StartsWith('evidence/', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Final owner summary must stay under evidence/.'
}

$queue = Read-OwnerQueue $fullQueuePath
if ($queue.schemaVersion -ne 1) {
    throw "Unsupported owner acceptance queue schemaVersion '$($queue.schemaVersion)'."
}
$items = @($queue.items)
$itemSummaries = [System.Collections.Generic.List[object]]::new()
$summaryById = @{}
foreach ($item in $items) {
    $record = [pscustomobject][ordered]@{
        id = [string]$item.id
        sourceKind = [string]$item.sourceKind
        sourcePhase = [string]$item.sourcePhase
        classification = [string]$item.classification
        queueState = [string]$item.state
        status = if ($item.state -eq 'PASS_INTEGRATED') { 'PASS' } else { 'MISSING' }
        action = if ($item.state -eq 'PASS_INTEGRATED') { 'ALREADY_INTEGRATED' } else { 'NOT_RUN' }
        evidencePath = [string]$item.expectedEvidencePath
        evidenceReused = $false
        verifiedPrerequisites = @()
        delegatedPrerequisites = @()
        message = if ($item.state -eq 'PASS_INTEGRATED') { 'Genuine owner evidence is already reconciled in canonical history.' } else { 'Genuine owner execution has not been run by this invocation.' }
    }
    $itemSummaries.Add($record)
    if ($summaryById.ContainsKey($record.id)) {
        throw "Duplicate owner queue id '$($record.id)'."
    }
    $summaryById[$record.id] = $record
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

Push-Location $resolvedRoot
try {
    $gitRoot = (& git rev-parse --show-toplevel 2>&1 | Out-String).Trim()
    Assert-LastExitCode 'Repository identity check'
    if ([IO.Path]::GetFullPath($gitRoot).TrimEnd('\') -ne [IO.Path]::GetFullPath($resolvedRoot).TrimEnd('\')) {
        throw "Final owner acceptance resolved the wrong repository. Expected '$resolvedRoot', got '$gitRoot'."
    }

    Assert-ExactCandidate $normalizedCandidate

    $allowedEvidenceRoots = [System.Collections.Generic.List[string]]::new()
    $summaryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $fullSummaryPath)).TrimEnd('\')
    $allowedEvidenceRoots.Add($summaryRoot)
    foreach ($item in @($items | Where-Object state -eq 'QUEUED')) {
        $contract = Assert-QueueItemContract $item $resolvedRoot
        $allowedEvidenceRoots.Add([IO.Path]::GetFullPath((Split-Path -Parent $contract.EvidencePath)).TrimEnd('\'))
    }
    $allowedRootArray = @($allowedEvidenceRoots | Select-Object -Unique)
    Assert-CleanCandidateInputs $resolvedRoot $allowedRootArray

    # PASS_INTEGRATED entries are included in the machine summary and rechecked for durable evidence.
    foreach ($item in @($items | Where-Object state -eq 'PASS_INTEGRATED')) {
        $record = $summaryById[[string]$item.id]
        try {
            [void](Assert-QueueItemContract $item $resolvedRoot)
            if ($item.PSObject.Properties.Name -notcontains 'integratedEvidence' -or [string]::IsNullOrWhiteSpace([string]$item.integratedEvidence)) {
                throw "PASS_INTEGRATED owner item '$($item.id)' is missing integratedEvidence."
            }
            $integratedPath = Resolve-RepositoryPath $resolvedRoot ([string]$item.integratedEvidence) "Integrated evidence for $($item.id)"
            [void](Read-EvidenceText $integratedPath $item.id)
            $record.status = 'PASS'
            $record.action = 'ALREADY_INTEGRATED'
            $record.message = 'PASS_INTEGRATED evidence remains present and sanitized; no owner rerun was performed.'
        }
        catch {
            $record.status = 'FAIL'
            $record.action = 'INTEGRITY_CHECK_FAILED'
            $record.message = Protect-Text (Bound-Text $_.Exception.Message)
        }
    }

    if ($SelfTestOnly) {
        Invoke-SelfTest $normalizedCandidate
    }

    foreach ($item in @($items | Where-Object state -eq 'QUEUED')) {
        $record = $summaryById[[string]$item.id]
        try {
            Assert-ExactCandidate $normalizedCandidate
            $contract = Assert-QueueItemContract $item $resolvedRoot
            $prerequisiteAssessment = Get-DeclaredPrerequisiteAssessment `
                -Item $item `
                -ExpectedSha $normalizedCandidate `
                -Root $resolvedRoot `
                -AllowedRoots $allowedRootArray
            $record.verifiedPrerequisites = @($prerequisiteAssessment.Verified)
            $record.delegatedPrerequisites = @($prerequisiteAssessment.DelegatedToTrackedRunner)

            if ($SelfTestOnly) {
                $record.status = 'MISSING'
                $record.action = 'SELF_TEST_ONLY'
                $record.message = 'SELF_TEST_ONLY validated orchestration mechanics; genuine owner/environment execution was intentionally not performed.'
                continue
            }

            if ($Resume) {
                $existing = Get-ExistingEvidenceState $contract.EvidencePath $item $normalizedCandidate
                if ($existing -eq 'VALID_PASS') {
                    $record.status = 'PASS'
                    $record.action = 'REUSED_EXACT_MACHINE_EVIDENCE'
                    $record.evidenceReused = $true
                    $record.message = 'Existing sanitized machine-readable PASS evidence matches the exact candidate; safe resume skipped rerun.'
                    Write-Host "Owner item resume PASS: $($item.id). Existing exact-candidate evidence reused; queue state remains QUEUED until reconciliation."
                    continue
                }
            }

            Write-Host "Running queued owner item: $($item.id) [$($item.classification)]"
            $commandOutput = @(& pwsh -NoProfile -File $contract.CommandPath -RepositoryRoot $resolvedRoot 2>&1)
            $commandExitCode = $LASTEXITCODE
            $safeOutput = Protect-Text (($commandOutput | Out-String).TrimEnd())
            if (-not [string]::IsNullOrWhiteSpace($safeOutput)) {
                Write-Host $safeOutput
            }
            if ($commandExitCode -ne 0) {
                throw "Tracked owner runner failed for '$($item.id)' with exit code $commandExitCode."
            }

            Assert-ExactCandidate $normalizedCandidate
            Assert-CleanCandidateInputs $resolvedRoot $allowedRootArray
            $rawEvidence = Read-EvidenceText $contract.EvidencePath $item.id
            if ([IO.Path]::GetExtension($contract.EvidencePath).Equals('.json', [StringComparison]::OrdinalIgnoreCase)) {
                try {
                    $evidence = $rawEvidence | ConvertFrom-Json -Depth 40
                }
                catch {
                    throw "Owner acceptance evidence JSON is invalid for '$($item.id)'."
                }
                Assert-MachineEvidenceObject $evidence $item $normalizedCandidate
            }

            $record.status = 'PASS'
            $record.action = 'EXECUTED'
            $record.evidenceReused = $false
            $record.message = 'Genuine tracked owner command returned PASS evidence for the exact candidate. Evidence still requires canonical review/integration.'
            Write-Host "Owner item execution PASS: $($item.id). Evidence still requires review/integration; queue state remains QUEUED."
        }
        catch {
            $record.status = if ($SelfTestOnly) { 'MISSING' } else { 'FAIL' }
            $record.action = if ($SelfTestOnly) { 'SELF_TEST_ONLY' } else { 'FAILED_CLOSED' }
            $record.message = Protect-Text (Bound-Text $_.Exception.Message)
        }
        finally {
            Write-FinalSummary `
                -Path $fullSummaryPath `
                -ExpectedSha $normalizedCandidate `
                -QueueRelativePath $QueuePath `
                -Items @($itemSummaries) `
                -Mode $mode `
                -SelfTestStatus $(if ($SelfTestOnly) { 'PASS' } else { 'NOT_RUN' })
        }
    }

    Assert-ExactCandidate $normalizedCandidate
    Assert-CleanCandidateInputs $resolvedRoot $allowedRootArray

    $selfTestStatus = if ($SelfTestOnly) { 'PASS' } else { 'NOT_RUN' }
    Write-FinalSummary `
        -Path $fullSummaryPath `
        -ExpectedSha $normalizedCandidate `
        -QueueRelativePath $QueuePath `
        -Items @($itemSummaries) `
        -Mode $mode `
        -SelfTestStatus $selfTestStatus

    $overallStatus = Get-OverallStatus @($itemSummaries)
    if ($SelfTestOnly) {
        if (@($itemSummaries | Where-Object status -eq 'FAIL').Count -gt 0) {
            throw "Final owner pack SELF_TEST_ONLY integrity validation failed. See '$SummaryPath'."
        }
        Write-Host 'FINAL_OWNER_PACK_SELF_TEST_PASS'
        Write-Host "Self-test summary: $fullSummaryPath"
        exit 0
    }

    if ($overallStatus -ne 'PASS') {
        throw "Final owner acceptance failed closed with overallStatus=$overallStatus. See '$SummaryPath'."
    }

    Write-Host 'FINAL_OWNER_EXECUTION_COMPLETE_RECONCILIATION_REQUIRED'
    Write-Host "Machine-readable summary: $fullSummaryPath"
    Write-Host 'All executed owner evidence still requires review/integration; queue state remains QUEUED until canonical reconciliation.'
}
finally {
    Pop-Location
}
