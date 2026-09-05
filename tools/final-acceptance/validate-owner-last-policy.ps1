[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RunNegativeFixtures
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$validator = Join-Path $PSScriptRoot 'owner-last-policy-validator.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
    throw "Owner-last validator implementation is missing: $validator"
}

$arguments = @('-NoProfile', '-File', $validator, '-RepositoryRoot', $RepositoryRoot)
if ($RunNegativeFixtures) {
    $arguments += '-RunNegativeFixtures'
}

& pwsh @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Owner-last execution governance validation failed with exit code $LASTEXITCODE."
}
