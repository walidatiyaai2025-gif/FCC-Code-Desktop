[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$RunFixtures,
    [switch]$RequireRuntime
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ContainsLiteral {
    param([string]$Text, [string]$Literal, [string]$Label)

    if (-not $Text.Contains($Literal, [StringComparison]::Ordinal)) {
        throw "$Label is missing required text: $Literal"
    }
}

function Assert-DoesNotContainLiteral {
    param([string]$Text, [string]$Literal, [string]$Label)

    if ($Text.Contains($Literal, [StringComparison]::Ordinal)) {
        throw "$Label contains forbidden text: $Literal"
    }
}

function Assert-ProcessOutputContract {
    param([hashtable]$Text)

    foreach ($literal in @(
        'public enum ProcessOutputSource',
        'StandardOutput = 0',
        'StandardError = 1',
        'public enum ProcessOutputStreamState',
        'ReadFailed = 2',
        'public sealed class ProcessOutputCorrelation',
        'public sealed class ProcessOutputOptions',
        'public sealed record ProcessLogEntry',
        'long Sequence',
        'DateTimeOffset TimestampUtc',
        'int RetainedUtf8Bytes',
        'long TruncatedCharacters',
        'long DroppedDeliveryEntries',
        'long DroppedDeliveryUtf8Bytes',
        'public interface IProcessOutput',
        'Task<ProcessOutputStatistics> Completion',
        'IAsyncEnumerable<ProcessLogEntry> ReadEntriesAsync',
        'public sealed class ProcessOutputPolicy',
        'DefaultMaximumRetainedEntries = 4_096',
        'MaximumSupportedRetainedEntries = 100_000',
        'DefaultMaximumRetainedUtf8Bytes = 4 * 1024 * 1024',
        'MaximumSupportedRetainedUtf8Bytes = 128 * 1024 * 1024',
        'DefaultMaximumEntryCharacters = 16 * 1024',
        'MaximumSupportedEntryCharacters = 1024 * 1024',
        'DefaultMaximumEntryUtf8Bytes = 64 * 1024',
        'MaximumSupportedEntryUtf8Bytes = 4 * 1024 * 1024',
        'DefaultMaximumPartialLineCharacters = 16 * 1024',
        'MaximumSupportedPartialLineCharacters = 1024 * 1024',
        'DefaultMaximumPendingDeliveryEntries = 512',
        'MaximumSupportedPendingDeliveryEntries = 16 * 1024',
        'DefaultReadBufferCharacters = 4 * 1024',
        'MaximumSupportedReadBufferCharacters = 64 * 1024',
        'MaximumEntryUtf8Bytes > MaximumRetainedUtf8Bytes',
        'MaximumEntryCharacters > MaximumPartialLineCharacters'
    )) {
        Assert-ContainsLiteral $Text.Contracts $literal 'ProcessOutputContracts.cs'
    }

    foreach ($literal in @(
        'public sealed class BoundedProcessOutputPipeline',
        'private readonly Queue<ProcessLogEntry> _history',
        'Channel.CreateBounded<ProcessLogEntry>',
        'BoundedChannelFullMode.Wait',
        'SingleReader = true',
        'SingleWriter = false',
        'new char[maximumCharacters]',
        'Policy.MaximumPartialLineCharacters',
        'Policy.MaximumPendingDeliveryEntries',
        'Policy.MaximumRetainedEntries',
        'Policy.MaximumRetainedUtf8Bytes',
        'Policy.MaximumEntryCharacters',
        'Policy.MaximumEntryUtf8Bytes',
        '_delivery.Writer.TryWrite(entry)',
        '_droppedDeliveryEntries = SaturatingIncrement',
        '_droppedDeliveryUtf8Bytes = SaturatingAdd',
        '_evictedEntries = SaturatingIncrement',
        '_truncatedEntries = SaturatingIncrement',
        'Rune.DecodeFromUtf16',
        'EmitFinalPartialLine',
        'PendingCarriageReturn',
        'NextSequence()',
        '_timeProvider.GetUtcNow()',
        'CompleteSource(ProcessOutputSource source, bool readFailed = false)',
        'ProcessOutputStreamState.ReadFailed'
    )) {
        Assert-ContainsLiteral $Text.Pipeline $literal 'BoundedProcessOutputPipeline.cs'
    }

    foreach ($literal in @(
        'ProcessOutputOptions? Output = null',
        'IProcessOutput Output { get; }'
    )) {
        Assert-ContainsLiteral $Text.SupervisionContracts $literal 'ProcessSupervisionContracts.cs'
    }

    foreach ($literal in @(
        'RedirectStandardError = true',
        'RedirectStandardOutput = true',
        'throwOnInvalidBytes: false',
        'ArrayPool<char>.Shared.Rent',
        'await Task.Yield()',
        '.ReadAsync(buffer.AsMemory(0, readBufferCharacters), CancellationToken.None)',
        'ProcessOutputSource.StandardOutput',
        'ProcessOutputSource.StandardError',
        'Task.WhenAll(_standardOutputPump, _standardErrorPump)',
        'await _output.Completion.ConfigureAwait(false)',
        'public IProcessOutput Output => _output'
    )) {
        Assert-ContainsLiteral $Text.Supervisor $literal 'ProcessSupervisor.cs'
    }

    foreach ($forbidden in @(
        'ReadToEnd',
        'BeginOutputReadLine',
        'OutputDataReceived',
        'StringBuilder',
        'Dispatcher',
        'Channel.CreateUnbounded',
        'BoundedChannelFullMode.DropNewest',
        'BoundedChannelFullMode.DropOldest',
        'BoundedChannelFullMode.DropWrite'
    )) {
        Assert-DoesNotContainLiteral $Text.Pipeline $forbidden 'BoundedProcessOutputPipeline.cs'
        Assert-DoesNotContainLiteral $Text.Supervisor $forbidden 'ProcessSupervisor.cs output integration'
    }

    foreach ($literal in @(
        'DefaultsAreFiniteAndInternallyConsistent',
        'RejectsEveryInvalidOrContradictoryBound',
        'FramesSplitAndMultipleLinesAcrossBothSourcesWithDeterministicSequences',
        'HandlesCrlfLfLoneCrFinalPartialUnicodeArabicAndEmoji',
        'ConcurrentWritersPreserveSourceOrderAndUniqueGlobalSequence',
        'RetainedEntryAndByteBoundsKeepLatestWithExactEvictionAccounting',
        'VeryLongLineUsesFixedPartialBufferAndReportsExactCharacterTruncation',
        'FullDeliveryQueueDropsOnlyNotificationsAndReportsExactLoss',
        'CancelledWriteDoesNotMutateAndPipelineRecovers',
        'ReadFailureIsTypedAndStillFlushesFinalPartialLine'
    )) {
        Assert-ContainsLiteral ($Text.PolicyTests + $Text.PipelineTests) $literal 'P08-003 unit tests'
    }

    foreach ($literal in @(
        'ProcessCompletionDrainsBothUnicodeStreamsAndFinalPartialLines',
        'MalformedUtf8UsesReplacementFallbackWithoutReadFailure',
        'EmptyFastNonzeroProcessCompletesBothReadersWithoutHanging',
        'GracefulCancellationDrainsOutputWrittenAfterStopSignal',
        'ForcedTerminationCompletesActiveReadersAndRetainsAcceptedOutput',
        'SupervisorDisposalTerminatesTreeAndCompletesOutputDrain',
        'output fixture with spaces'
    )) {
        Assert-ContainsLiteral $Text.IntegrationTests $literal 'ProcessOutputIntegrationTests.cs'
    }

    foreach ($literal in @(
        'HighVolumeSingleSourceCompletesWithExactCountAndBoundedLatestHistory',
        'AlternatingHighVolumeBothStreamsCannotDeadlockOnStoppedConsumer',
        'ForcedStopDuringUnthrottledOutputRemainsBoundedAndCompletesReaders',
        'RepeatedStartsAndStopsLeaveNoOwnedProcessOrReaderLeak',
        'SingleSourceLineCount = 3_000',
        'DualSourceLineCount = 5_000'
    )) {
        Assert-ContainsLiteral $Text.StressTests $literal 'ProcessOutputStressTests.cs'
    }

    foreach ($literal in @(
        '# Bounded Process Output Pipeline',
        '4,096',
        '4 MiB',
        'Pending live-delivery entries',
        'single-consumer best-effort notification stream',
        'ProcessOutputStreamState.ReadFailed',
        'Malformed UTF-8 bytes are not a stream-read failure'
    )) {
        Assert-ContainsLiteral $Text.Docs $literal 'BOUNDED_PROCESS_OUTPUT.md'
    }

    Assert-ContainsLiteral `
        $Text.CanonicalWorkflow `
        '.\tools\runtime\validate-bounded-process-output.ps1 -RunFixtures -RequireRuntime' `
        'Canonical Windows CI workflow'
    Assert-ContainsLiteral `
        $Text.FocusedWorkflow `
        '.\tools\runtime\validate-bounded-process-output.ps1 -Configuration Release -RunFixtures -RequireRuntime' `
        'Focused P08-003 Windows workflow'
}

function Assert-ContractRejects {
    param([scriptblock]$Action, [string]$Label)

    try {
        & $Action
    }
    catch {
        Write-Host "Negative fixture rejected as expected: $Label"
        return
    }

    throw "Negative P08-003 fixture was not rejected: $Label"
}

$paths = @{
    Contracts = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Runtime\ProcessOutputContracts.cs'
    Pipeline = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Runtime\BoundedProcessOutputPipeline.cs'
    SupervisionContracts = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Runtime\ProcessSupervisionContracts.cs'
    Supervisor = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Runtime\ProcessSupervisor.cs'
    PolicyTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\ProcessOutputPolicyTests.cs'
    PipelineTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\BoundedProcessOutputPipelineTests.cs'
    IntegrationTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\ProcessOutputIntegrationTests.cs'
    StressTests = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\ProcessOutputStressTests.cs'
    Docs = Join-Path $RepositoryRoot 'docs\runtime\BOUNDED_PROCESS_OUTPUT.md'
    CanonicalWorkflow = Join-Path $RepositoryRoot '.github\workflows\windows-ci.yml'
    FocusedWorkflow = Join-Path $RepositoryRoot '.github\workflows\p08-003-bounded-process-output.yml'
}

foreach ($path in $paths.Values) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P08-003 path is missing: $path"
    }
}

$text = @{}
foreach ($key in $paths.Keys) {
    $text[$key] = Get-Content -LiteralPath $paths[$key] -Raw
}

Assert-ProcessOutputContract $text
Write-Host 'Static P08-003 bounded process output validation: PASS.'

if ($RunFixtures) {
    $mutated = $text.Clone()
    $mutated.Pipeline = $text.Pipeline.Replace(
        'Channel.CreateBounded<ProcessLogEntry>',
        'Channel.CreateUnbounded<ProcessLogEntry>')
    Assert-ContractRejects { Assert-ProcessOutputContract $mutated } 'bounded delivery channel removed'

    $mutated = $text.Clone()
    $mutated.Pipeline = $text.Pipeline.Replace(
        '_delivery.Writer.TryWrite(entry)',
        '_delivery.Writer.WriteAsync(entry).AsTask().GetAwaiter().GetResult()')
    Assert-ContractRejects { Assert-ProcessOutputContract $mutated } 'non-blocking process drain removed'

    $mutated = $text.Clone()
    $mutated.Contracts = $text.Contracts.Replace(
        'MaximumSupportedRetainedEntries = 100_000',
        'MaximumSupportedRetainedEntries = int.MaxValue')
    Assert-ContractRejects { Assert-ProcessOutputContract $mutated } 'retained-entry hard maximum removed'

    $mutated = $text.Clone()
    $mutated.Supervisor = $text.Supervisor.Replace(
        'RedirectStandardError = true',
        'RedirectStandardError = false')
    Assert-ContractRejects { Assert-ProcessOutputContract $mutated } 'stderr drain disabled'

    $mutated = $text.Clone()
    $mutated.Supervisor = $text.Supervisor.Replace(
        'Task.WhenAll(_standardOutputPump, _standardErrorPump)',
        'Task.CompletedTask')
    Assert-ContractRejects { Assert-ProcessOutputContract $mutated } 'completion drain barrier removed'

    Write-Host 'P08-003 negative fixtures: PASS.'
}

if ($RequireRuntime) {
    if (-not $IsWindows) {
        throw 'Executable P08-003 validation requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for executable P08-003 validation.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "P08-003 validation requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $solution = Join-Path $RepositoryRoot 'FCCCodeDesktop.sln'
    $testProject = Join-Path $RepositoryRoot 'tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj'
    & dotnet restore $solution --locked-mode --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'P08-003 locked solution restore failed.'
    }

    & dotnet build $testProject -c $Configuration --no-restore --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'P08-003 focused test-project build failed.'
    }

    $filter = 'FullyQualifiedName~ProcessOutputPolicyTests|FullyQualifiedName~BoundedProcessOutputPipelineTests|FullyQualifiedName~ProcessOutputIntegrationTests|FullyQualifiedName~ProcessOutputStressTests|FullyQualifiedName~ProcessSupervisorTests|FullyQualifiedName~ProcessCancellationEscalatorTests'
    & dotnet test $testProject -c $Configuration --no-restore --no-build --nologo --filter $filter
    if ($LASTEXITCODE -ne 0) {
        throw 'Executable P08-003 process output and P08 ownership/cancellation regression tests failed.'
    }

    Write-Host 'Executable P08-003 bounded process output validation: PASS.'
}
