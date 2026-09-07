[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$solution = Join-Path $repositoryRoot 'FCCCodeDesktop.sln'
$runtimeProject = Join-Path $repositoryRoot 'src\FCCCodeDesktop.Runtime\FCCCodeDesktop.Runtime.csproj'
$unitProject = Join-Path $repositoryRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'
$contractsPath = Join-Path $repositoryRoot 'src\FCCCodeDesktop.Runtime\ProcessOutputContracts.cs'
$pipelinePath = Join-Path $repositoryRoot 'src\FCCCodeDesktop.Runtime\BoundedProcessOutputPipeline.cs'
$supervisorPath = Join-Path $repositoryRoot 'src\FCCCodeDesktop.Runtime\ProcessSupervisor.cs'
$supervisionContractsPath = Join-Path $repositoryRoot 'src\FCCCodeDesktop.Runtime\ProcessSupervisionContracts.cs'
$documentationPath = Join-Path $repositoryRoot 'docs\runtime\BOUNDED_PROCESS_OUTPUT.md'

foreach ($path in @(
    $solution,
    $runtimeProject,
    $unitProject,
    $contractsPath,
    $pipelinePath,
    $supervisorPath,
    $supervisionContractsPath,
    $documentationPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required P08-003 path is missing: $path"
    }
}

$contracts = Get-Content -LiteralPath $contractsPath -Raw
$pipeline = Get-Content -LiteralPath $pipelinePath -Raw
$supervisor = Get-Content -LiteralPath $supervisorPath -Raw
$supervisionContracts = Get-Content -LiteralPath $supervisionContractsPath -Raw
$combined = $contracts + "`n" + $pipeline + "`n" + $supervisor + "`n" + $supervisionContracts

$requiredTokens = @(
    'ProcessOutputPolicy',
    'MaximumRetainedEntries',
    'MaximumRetainedUtf8Bytes',
    'MaximumPendingDeliveryEntries',
    'ProcessOutputStatistics',
    'DroppedDeliveryEntries',
    'EvictedEntries',
    'TruncatedEntries',
    'Channel.CreateBounded<ProcessLogEntry>',
    'BoundedChannelFullMode.Wait',
    '_delivery.Writer.TryWrite',
    'RedirectStandardOutput = true',
    'RedirectStandardError = true',
    'StandardOutputEncoding = ProcessOutputEncoding',
    'StandardErrorEncoding = ProcessOutputEncoding',
    'Task.WhenAll(_standardOutputPump, _standardErrorPump)',
    'await _output.Completion.ConfigureAwait(false)',
    'ProcessOutputStreamState.ReadFailed'
)
foreach ($token in $requiredTokens) {
    if (-not $combined.Contains($token, [StringComparison]::Ordinal)) {
        throw "P08-003 implementation is missing required bounded-output token '$token'."
    }
}

$forbiddenTokens = @(
    'Channel.CreateUnbounded',
    '.ReadToEnd()',
    '.ReadToEndAsync(',
    'new List<ProcessLogEntry>',
    'ConcurrentBag<ProcessLogEntry>',
    'ConcurrentQueue<ProcessLogEntry>'
)
foreach ($token in $forbiddenTokens) {
    if ($combined.Contains($token, [StringComparison]::Ordinal)) {
        throw "P08-003 implementation contains forbidden unbounded-output token '$token'."
    }
}

if (-not $contracts.Contains('MaximumSupportedRetainedEntries', [StringComparison]::Ordinal) -or
    -not $contracts.Contains('MaximumSupportedRetainedUtf8Bytes', [StringComparison]::Ordinal) -or
    -not $contracts.Contains('MaximumSupportedPendingDeliveryEntries', [StringComparison]::Ordinal)) {
    throw 'P08-003 output policy must expose finite hard maxima for history and live delivery.'
}

if (-not $pipeline.Contains('SaturatingIncrement', [StringComparison]::Ordinal) -or
    -not $pipeline.Contains('SaturatingAdd', [StringComparison]::Ordinal)) {
    throw 'P08-003 loss/accounting counters must remain overflow-safe.'
}

Write-Host 'P08-003 static bounded-output contract: PASS.'

& dotnet restore $solution --locked-mode --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Locked solution restore failed with exit code $LASTEXITCODE."
}

& dotnet build $runtimeProject --configuration $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Runtime project build failed with exit code $LASTEXITCODE."
}

$filter = 'FullyQualifiedName~ProcessOutput|FullyQualifiedName~ProcessCancellationEscalatorTests'
& dotnet test $unitProject `
    --configuration $Configuration `
    --no-restore `
    --nologo `
    --filter $filter `
    --logger 'console;verbosity=minimal'
if ($LASTEXITCODE -ne 0) {
    throw "P08-003 focused bounded-output/runtime recovery tests failed with exit code $LASTEXITCODE."
}

Write-Host 'P08-003 bounded streaming log pipeline validation: PASS.'
