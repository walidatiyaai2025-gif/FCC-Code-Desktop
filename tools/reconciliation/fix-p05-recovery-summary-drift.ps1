[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root
$path = Join-Path $root 'CURRENT_PHASE.md'
$text = [IO.File]::ReadAllText($path)
$old = 'The owner-last policy permits sequential cloud advancement despite those two earlier environment-bound obligations only because their cloud preparation is complete and they are represented one-to-one in the canonical release-blocking owner queue. All P04/P05 functional and acceptance requirements remain unchanged.'
$new = 'The owner-last policy continues to permit sequential cloud advancement despite the remaining P04 environment-bound obligation because its cloud preparation is complete and it remains represented one-to-one in the canonical release-blocking owner queue. The former P05 owner obligation has passed and is integrated; P04 requirements remain unchanged.'
if (-not $text.Contains($old)) { throw 'Expected stale current-summary paragraph was not found.' }
$text = $text.Replace($old, $new)
[IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
& .\tools\final-acceptance\validate-owner-last-policy.ps1 -RunNegativeFixtures
if ($LASTEXITCODE -ne 0) { throw 'Owner-last validator failed after summary repair.' }
& git diff --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed after summary repair.' }
Write-Host 'P05 recovery current-summary drift repair: PASS'
