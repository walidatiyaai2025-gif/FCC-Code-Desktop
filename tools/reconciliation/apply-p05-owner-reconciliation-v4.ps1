[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
& (Join-Path $PSScriptRoot 'apply-p05-owner-reconciliation-v2.ps1') -RepositoryRoot $root

# Normalize the owner-queue Markdown fence after the semantic transformation.
$queuePath = Join-Path $root 'docs\FINAL_OWNER_ACCEPTANCE_QUEUE.md'
$text = [IO.File]::ReadAllText($queuePath)
$begin = '<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->'
$end = '<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->'
$beginIndex = $text.IndexOf($begin, [StringComparison]::Ordinal)
$endIndex = $text.IndexOf($end, [StringComparison]::Ordinal)
if ($beginIndex -lt 0 -or $endIndex -le $beginIndex) { throw 'Owner queue markers are missing after semantic transformation.' }
$middleStart = $beginIndex + $begin.Length
$middle = $text.Substring($middleStart, $endIndex - $middleStart)
$firstBrace = $middle.IndexOf('{')
$lastBrace = $middle.LastIndexOf('}')
if ($firstBrace -lt 0 -or $lastBrace -le $firstBrace) { throw 'Owner queue JSON payload cannot be recovered after semantic transformation.' }
$jsonText = $middle.Substring($firstBrace, $lastBrace - $firstBrace + 1)
$parsed = $jsonText | ConvertFrom-Json -Depth 30
$p05 = @($parsed.items | Where-Object { $_.id -eq 'OWNER-P05-EXIT-REAL-TARGET' })
if ($p05.Count -ne 1 -or $p05[0].state -ne 'PASS_INTEGRATED') { throw 'Recovered owner queue does not contain the reconciled P05 PASS_INTEGRATED item.' }
$normalizedJson = $parsed | ConvertTo-Json -Depth 30
$template = @'
<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->
```json
{QUEUE_JSON}
```
<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->
'@
$block = $template.Replace('{QUEUE_JSON}', $normalizedJson, [StringComparison]::Ordinal).TrimEnd("`r", "`n")
$text = $text.Substring(0, $beginIndex) + $block + $text.Substring($endIndex + $end.Length)
[IO.File]::WriteAllText($queuePath, $text, [Text.UTF8Encoding]::new($false))

Write-Host 'P05 v4 reconciliation applied with canonical owner-queue fencing.'
