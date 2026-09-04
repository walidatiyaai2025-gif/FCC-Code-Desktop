[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RunFixtures,
    [switch]$RequireRuntime
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ContainsLiteral {
    param([string]$Text, [string]$Literal, [string]$Label)
    if (-not $Text.Contains($Literal)) {
        throw "$Label is missing required text: $Literal"
    }
}

function Assert-ContractSuite {
    param(
        [string]$HarnessText,
        [string]$TargetRunnerText,
        [string]$DocumentationText
    )

    foreach ($literal in @(
        'structured_success_stream_session',
        'structured_resume',
        'structured_invalid_session_failure',
        'structured_cancellation',
        'fallback_after_structured_failure',
        'FccStructuredAgentRuntime',
        'FccCliFallbackAgentRuntime',
        'AgentRuntimeTerminalState.Cancelled',
        'AgentRuntimeTerminalState.Failed',
        'SessionIdentified',
        'REAL_TARGET',
        'SELF_TEST_ONLY',
        'RateLimitObservation',
        'NOT_INDUCED'
    )) {
        Assert-ContainsLiteral $HarnessText $literal 'P04RuntimeTargetHarness/Program.cs'
    }

    foreach ($literal in @(
        'git status --porcelain',
        'evidence/phases/P04/runtime-contract',
        '--classification',
        'REAL_TARGET',
        '--expected-sha',
        'overallStatus',
        'evidenceClassification',
        'testedRepoSha'
    )) {
        Assert-ContainsLiteral $TargetRunnerText $literal 'run-p04-runtime-target-validation.ps1'
    }

    foreach ($literal in @(
        'FCCD-P04-008',
        'SELF_TEST_ONLY',
        'REAL_TARGET',
        'structured success',
        'resume',
        'invalid-session',
        'cancellation',
        'fallback',
        'does not manufacture provider 429',
        'P04 exit gate',
        'P05 remains prohibited'
    )) {
        Assert-ContainsLiteral $DocumentationText $literal 'P04_RUNTIME_CONTRACT_SUITE.md'
    }
}

function Assert-ContractRejects {
    param([scriptblock]$Action, [string]$Label)
    try {
        & $Action
    }
    catch {
        Write-Host "Negative runtime-contract-suite fixture rejected as expected: $Label"
        return
    }

    throw "Negative runtime-contract-suite fixture was not rejected: $Label"
}

function Assert-LastExitCode {
    param([string]$Stage)
    if ($LASTEXITCODE -ne 0) {
        throw "$Stage failed with exit code $LASTEXITCODE."
    }
}

function Invoke-ContractFixture {
    param([string]$HarnessProjectPath)

    if (-not $IsWindows) {
        throw 'P04 runtime contract executable fixture requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the P04 runtime contract executable fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "P04 runtime contract fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-p04-contract-suite-' + [Guid]::NewGuid().ToString('N'))
    $fakeRoot = Join-Path $fixtureRoot 'fake fcc مساحة'
    $evidencePath = Join-Path $fixtureRoot 'synthetic-evidence.json'
    [void](New-Item -ItemType Directory -Path $fakeRoot -Force)

    try {
        $fakeProjectPath = Join-Path $fakeRoot 'FakeP04Fcc.csproj'
        $fakeProgramPath = Join-Path $fakeRoot 'Program.cs'
        Set-Content -LiteralPath $fakeProjectPath -Encoding utf8NoBOM -Value @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
'@
        Set-Content -LiteralPath $fakeProgramPath -Encoding utf8NoBOM -Value @'
using System.Text.Json;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var structured = Array.IndexOf(args, "--output-format") >= 0;
        var prompt = args.Length == 0 ? string.Empty : args[^1];
        if (!structured)
        {
            Console.WriteLine("FCCD_P04_FALLBACK");
            return 0;
        }

        var resumeIndex = Array.IndexOf(args, "--resume");
        var sessionId = resumeIndex >= 0 && resumeIndex + 1 < args.Length
            ? args[resumeIndex + 1]
            : "fixture-p04-session";

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = "system",
            subtype = "init",
            session_id = sessionId,
            uuid = "p04-init"
        }));
        Console.Out.Flush();

        if (sessionId.StartsWith("fccd-invalid-session-", StringComparison.Ordinal))
        {
            return 9;
        }

        if (prompt.Contains("500 numbered", StringComparison.Ordinal))
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            return 0;
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = "future_contract_event",
            text = prompt.Contains("RESUME", StringComparison.Ordinal) ? "FCCD_P04_RESUME" : "FCCD_P04_SUCCESS",
            uuid = "p04-result"
        }));
        return 0;
    }
}
'@

        & dotnet build $fakeProjectPath -c Release --nologo
        Assert-LastExitCode 'P04 fake FCC build'
        $fakeExecutable = Join-Path $fakeRoot 'bin\Release\net10.0-windows\FakeP04Fcc.exe'
        if (-not (Test-Path -LiteralPath $fakeExecutable)) {
            throw "P04 fake FCC executable was not produced: $fakeExecutable"
        }

        & dotnet run --project $HarnessProjectPath -c Release -- `
            --evidence $evidencePath `
            --classification SELF_TEST_ONLY `
            --expected-sha SELF_TEST_ONLY `
            --fcc-claude $fakeExecutable
        Assert-LastExitCode 'P04 runtime contract synthetic harness'

        if (-not (Test-Path -LiteralPath $evidencePath)) {
            throw 'P04 runtime contract synthetic harness did not produce evidence.'
        }

        $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
        if ($evidence.task -ne 'FCCD-P04-008') {
            throw "Unexpected P04 runtime contract task marker: $($evidence.task)"
        }
        if ($evidence.evidenceClassification -ne 'SELF_TEST_ONLY') {
            throw 'Synthetic P04 evidence was not clearly classified SELF_TEST_ONLY.'
        }
        if ($evidence.testedRepoSha -ne 'SELF_TEST_ONLY') {
            throw 'Synthetic P04 evidence cannot claim a real tested repository SHA.'
        }
        if ($evidence.overallStatus -ne 'PASS') {
            throw "Synthetic P04 runtime contract suite did not pass: $($evidence.overallStatus)"
        }
        if (@($evidence.scenarios).Count -ne 5) {
            throw 'Synthetic P04 runtime contract suite must exercise exactly five cross-path scenarios.'
        }
        if (@($evidence.scenarios | Where-Object status -ne 'PASS').Count -ne 0) {
            throw 'Synthetic P04 runtime contract suite contains a non-PASS scenario.'
        }
        if ($evidence.rateLimitObservation -ne 'NOT_INDUCED') {
            throw 'P04 runtime contract suite must not fabricate or induce rate-limit evidence.'
        }

        Write-Host 'P04 aggregate runtime contract synthetic happy/negative/cancel/resume/fallback fixture: PASS.'
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$harnessPath = Join-Path $RepositoryRoot 'tools\runtime\P04RuntimeTargetHarness\Program.cs'
$harnessProjectPath = Join-Path $RepositoryRoot 'tools\runtime\P04RuntimeTargetHarness\P04RuntimeTargetHarness.csproj'
$targetRunnerPath = Join-Path $RepositoryRoot 'tools\runtime\run-p04-runtime-target-validation.ps1'
$documentationPath = Join-Path $RepositoryRoot 'docs\runtime\P04_RUNTIME_CONTRACT_SUITE.md'

foreach ($path in @($harnessPath, $harnessProjectPath, $targetRunnerPath, $documentationPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "P04 runtime contract suite path is missing: $path"
    }
}

$harnessText = Get-Content -LiteralPath $harnessPath -Raw
$targetRunnerText = Get-Content -LiteralPath $targetRunnerPath -Raw
$documentationText = Get-Content -LiteralPath $documentationPath -Raw
Assert-ContractSuite $harnessText $targetRunnerText $documentationText

Assert-ContractRejects {
    Assert-ContractSuite ($harnessText.Replace('structured_resume', 'structured_resume_removed')) $targetRunnerText $documentationText
} 'resume scenario removed'
Assert-ContractRejects {
    Assert-ContractSuite ($harnessText.Replace('NOT_INDUCED', 'OBSERVED_429')) $targetRunnerText $documentationText
} 'rate-limit safety classification removed'
Assert-ContractRejects {
    Assert-ContractSuite $harnessText ($targetRunnerText.Replace('git status --porcelain', 'git status')) $documentationText
} 'target exact-worktree guard weakened'
Assert-ContractRejects {
    Assert-ContractSuite $harnessText $targetRunnerText ($documentationText.Replace('P05 remains prohibited', 'P05 may start'))
} 'phase lock weakened'

Write-Host 'Static P04 aggregate runtime contract-suite validation: PASS.'
Write-Host 'Negative fixtures verify resume, rate-limit safety, exact-worktree provenance, and phase-lock enforcement.'

if ($RunFixtures -or $RequireRuntime) {
    Invoke-ContractFixture -HarnessProjectPath $harnessProjectPath
}
