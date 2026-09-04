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

function Assert-NormalizationContract {
    param(
        [string]$NormalizerText,
        [string]$RuntimeText,
        [string]$DocumentationText
    )

    foreach ($literal in @(
        'AgentRuntimeEventKind.SessionIdentified',
        'AgentRuntimeEventKind.Retry',
        'AgentRuntimeEventKind.AssistantTextDelta',
        'AgentRuntimeEventKind.ToolStarted',
        'AgentRuntimeEventKind.ToolProgress',
        'AgentRuntimeEventKind.ToolResult',
        'AgentRuntimeEventKind.Usage',
        'AgentRuntimeEventKind.Error',
        'AgentRuntimeEventKind.Completion',
        'AgentRuntimeEventKind.Unknown',
        '"system/api_retry"',
        '"content_block_delta"',
        '"input_json_delta"',
        '"tool_use"',
        '"tool_result"',
        'SensitiveTextAssignment.Replace',
        '"[REDACTED]"'
    )) {
        Assert-ContainsLiteral $NormalizerText $literal 'FccRuntimeEventNormalizer.cs'
    }

    foreach ($literal in @(
        'FccRuntimeEventNormalizer.Normalize',
        'projection.Kind',
        'projection.Text',
        'projection.CorrelationId',
        'projection.SourceType',
        'payloadJson: payloadJson',
        'sequence++'
    )) {
        Assert-ContainsLiteral $RuntimeText $literal 'FccStructuredAgentRuntime.cs'
    }

    foreach ($literal in @(
        'FCCD-P04-005',
        'TARGET_OBSERVED',
        'COMPATIBILITY',
        'system/api_retry',
        'Unknown',
        'FCCD-P04-008',
        'P04-007',
        'does **not claim provider execution**',
        'makes no new provider/FCC target-execution claim'
    )) {
        Assert-ContainsLiteral $DocumentationText $literal 'FCC_RUNTIME_EVENT_NORMALIZATION.md'
    }

    if ($NormalizerText.Contains('Task.Delay') -or $NormalizerText.Contains('StartAsync(')) {
        throw 'P04-005 crossed into P04-007 retry/start supervision.'
    }
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

    throw "Negative runtime-normalization fixture was not rejected: $Label"
}

function Assert-LastExitCode {
    param([string]$Stage)
    if ($LASTEXITCODE -ne 0) {
        throw "$Stage failed with exit code $LASTEXITCODE."
    }
}

function Invoke-NormalizationFixture {
    param([string]$FccProjectPath)

    if (-not $IsWindows) {
        throw 'FCC runtime event-normalization fixture requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the FCC runtime event-normalization fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "FCC runtime event-normalization fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-p04-normalization-' + [Guid]::NewGuid().ToString('N'))
    $fakeRoot = Join-Path $fixtureRoot 'fake fcc runtime'
    $harnessRoot = Join-Path $fixtureRoot 'normalization harness مساحة'
    [void](New-Item -ItemType Directory -Path $fakeRoot -Force)
    [void](New-Item -ItemType Directory -Path $harnessRoot -Force)

    try {
        $fakeProjectPath = Join-Path $fakeRoot 'FakeFccRuntime.csproj'
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
using System.Text;
using System.Text.Json;

internal static class Program
{
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Write(new
        {
            type = "system",
            subtype = "init",
            session_id = "fixture-session",
            uuid = "init-1",
            api_key = "fixture-secret"
        });
        Write(new
        {
            type = "system",
            subtype = "api_retry",
            attempt = 2,
            max_retries = 5,
            retry_delay_ms = 1250,
            error_status = 503,
            error = "provider unavailable",
            session_id = "fixture-session",
            uuid = "retry-1",
            token = "fixture-token"
        });
        Write(new
        {
            type = "assistant",
            session_id = "fixture-session",
            uuid = "assistant-1",
            message = new
            {
                content = new object[]
                {
                    new { type = "text", text = "normalized مرحبا token=fixture-secret" },
                    new { type = "tool_use", id = "tool-1", name = "ReadFile", input = new { path = "x", api_key = "fixture-key" } },
                    new { type = "future_content", id = "future-block", value = 17 }
                },
                usage = new { input_tokens = 3, output_tokens = 4 }
            }
        });
        Write(new
        {
            type = "user",
            session_id = "fixture-session",
            message = new
            {
                content = new object[]
                {
                    new { type = "tool_result", tool_use_id = "tool-1", content = "done" }
                }
            }
        });
        Write(new
        {
            type = "stream_event",
            session_id = "fixture-session",
            uuid = "stream-text",
            @event = new
            {
                type = "content_block_delta",
                delta = new { type = "text_delta", text = " delta " }
            }
        });
        Write(new
        {
            type = "stream_event",
            session_id = "fixture-session",
            uuid = "stream-tool",
            @event = new
            {
                type = "content_block_delta",
                delta = new { type = "input_json_delta", partial_json = "{\"path\":\"x\"}" }
            }
        });
        Write(new
        {
            type = "error",
            session_id = "fixture-session",
            uuid = "error-1",
            message = "fixture error"
        });
        Write(new
        {
            type = "status",
            session_id = "fixture-session",
            uuid = "status-1",
            status = "ready"
        });
        Write(new
        {
            type = "result",
            session_id = "fixture-session",
            uuid = "result-1",
            result = "finished",
            usage = new { input_tokens = 5, output_tokens = 6 }
        });
        Write(new
        {
            type = "future_event",
            session_id = "fixture-session",
            uuid = "future-1",
            token = "future-secret",
            value = "preserve me"
        });
        return 0;
    }

    private static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value));
}
'@

        & dotnet build $fakeProjectPath -c Release --nologo
        Assert-LastExitCode 'Fake FCC normalization runtime build'
        $fakeExecutable = Join-Path $fakeRoot 'bin\Release\net10.0-windows\FakeFccRuntime.exe'
        if (-not (Test-Path -LiteralPath $fakeExecutable)) {
            throw "Fake FCC normalization runtime executable was not produced: $fakeExecutable"
        }

        $projectReference = [Security.SecurityElement]::Escape($FccProjectPath)
        $harnessProjectPath = Join-Path $harnessRoot 'NormalizationFixture.csproj'
        $harnessProgramPath = Join-Path $harnessRoot 'Program.cs'
        Set-Content -LiteralPath $harnessProjectPath -Encoding utf8NoBOM -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="$projectReference" /></ItemGroup>
</Project>
"@
        Set-Content -LiteralPath $harnessProgramPath -Encoding utf8NoBOM -Value @'
using System.Text.Json;
using FCCCodeDesktop.Fcc;
using FCCCodeDesktop.Runtime;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Assert(args.Length == 1, "fake runtime path supplied");
        var cwd = Path.Combine(Path.GetTempPath(), "fccd normalization work مساحة " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cwd);
        try
        {
            var runtime = new FccStructuredAgentRuntime(args[0], "2.1.251");
            await using var execution = await runtime.StartAsync(
                new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "normalization fixture", cwd),
                CancellationToken.None);
            var events = new List<AgentRuntimeEvent>();
            await foreach (var item in execution.Events) { events.Add(item); }
            var result = await execution.Completion;

            Assert(result.State == AgentRuntimeTerminalState.Succeeded, "fixture run succeeds");
            Assert(result.SessionId == "fixture-session", "session propagated to terminal result");
            Assert(events.Count >= 14, "expected normalized projections emitted");
            for (var index = 0; index < events.Count; index++)
            {
                Assert(events[index].Sequence == index, $"sequence {index} is contiguous");
                Assert(events[index].SessionId == "fixture-session", $"session retained on event {index}");
                Assert(!string.IsNullOrWhiteSpace(events[index].PayloadJson), $"payload retained on event {index}");
            }

            var session = Single(events, AgentRuntimeEventKind.SessionIdentified, "session event");
            Assert(session.SourceType == "system/init", "observed init source retained");
            using (var payload = JsonDocument.Parse(session.PayloadJson!))
            {
                Assert(payload.RootElement.GetProperty("api_key").GetString() == "[REDACTED]", "init API key redacted");
            }

            var retry = Single(events, AgentRuntimeEventKind.Retry, "retry event");
            Assert(retry.SourceType == "system/api_retry", "observed retry source retained");
            Assert(retry.CorrelationId == "retry-1", "retry correlation retained");
            using (var payload = JsonDocument.Parse(retry.PayloadJson!))
            {
                Assert(payload.RootElement.GetProperty("retry_delay_ms").GetInt32() == 1250, "retry delay evidence retained");
                Assert(payload.RootElement.GetProperty("error_status").GetInt32() == 503, "retry status evidence retained");
                Assert(payload.RootElement.GetProperty("token").GetString() == "[REDACTED]", "retry token redacted");
            }

            var assistant = events.First(item => item.Kind == AgentRuntimeEventKind.AssistantTextDelta && item.SourceType == "assistant/content/text");
            Assert(assistant.Text?.Contains("مرحبا", StringComparison.Ordinal) == true, "Unicode assistant text retained");
            Assert(assistant.Text?.Contains("[REDACTED]", StringComparison.Ordinal) == true, "assistant credential assignment redacted");
            Assert(assistant.Text?.Contains("fixture-secret", StringComparison.Ordinal) != true, "assistant secret absent from projected text");

            var toolStarted = Single(events, AgentRuntimeEventKind.ToolStarted, "tool started");
            Assert(toolStarted.CorrelationId == "tool-1", "tool start correlation retained");
            Assert(toolStarted.Text == "ReadFile", "tool name retained");
            using (var payload = JsonDocument.Parse(toolStarted.PayloadJson!))
            {
                var tool = payload.RootElement.GetProperty("message").GetProperty("content")[1];
                Assert(tool.GetProperty("input").GetProperty("api_key").GetString() == "[REDACTED]", "nested tool secret redacted");
            }

            var toolResult = Single(events, AgentRuntimeEventKind.ToolResult, "tool result");
            Assert(toolResult.CorrelationId == "tool-1", "tool result correlation retained");
            Assert(toolResult.Text == "done", "tool result text retained");

            var toolProgress = Single(events, AgentRuntimeEventKind.ToolProgress, "tool progress");
            Assert(toolProgress.Text?.Contains("path", StringComparison.Ordinal) == true, "tool JSON delta retained");

            Assert(events.Count(item => item.Kind == AgentRuntimeEventKind.Usage) >= 2, "usage projections emitted");
            Assert(events.Any(item => item.Kind == AgentRuntimeEventKind.Error && item.Text == "fixture error"), "error projection emitted");
            Assert(events.Any(item => item.Kind == AgentRuntimeEventKind.RuntimeStatus && item.Text == "ready"), "status projection emitted");
            Assert(events.Any(item => item.Kind == AgentRuntimeEventKind.Completion && item.Text == "finished"), "completion projection emitted");
            Assert(events.Any(item => item.Kind == AgentRuntimeEventKind.Unknown && item.SourceType == "assistant/content/future_content"), "future nested block preserved");
            var future = events.Single(item => item.Kind == AgentRuntimeEventKind.Unknown && item.SourceType == "future_event");
            using (var payload = JsonDocument.Parse(future.PayloadJson!))
            {
                Assert(payload.RootElement.GetProperty("token").GetString() == "[REDACTED]", "future-frame token redacted");
                Assert(payload.RootElement.GetProperty("value").GetString() == "preserve me", "future-frame payload preserved");
            }

            Console.WriteLine("FCC runtime event-normalization happy/negative/recovery fixture: PASS.");
        }
        finally
        {
            Directory.Delete(cwd, recursive: true);
        }
    }

    private static AgentRuntimeEvent Single(List<AgentRuntimeEvent> events, AgentRuntimeEventKind kind, string label)
    {
        var matches = events.Where(item => item.Kind == kind).ToArray();
        Assert(matches.Length == 1, $"{label} count");
        return matches[0];
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition) { throw new InvalidOperationException($"FCC normalization assertion failed: {label}"); }
    }
}
'@

        & dotnet run --project $harnessProjectPath -c Release -- $fakeExecutable
        Assert-LastExitCode 'FCC runtime event-normalization executable fixture'
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$normalizerPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FccRuntimeEventNormalizer.cs'
$runtimePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FccStructuredAgentRuntime.cs'
$documentationPath = Join-Path $RepositoryRoot 'docs\runtime\FCC_RUNTIME_EVENT_NORMALIZATION.md'
$fccProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FCCCodeDesktop.Fcc.csproj'

foreach ($requiredPath in @($normalizerPath, $runtimePath, $documentationPath, $fccProjectPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required P04-005 path is missing: $requiredPath"
    }
}

$normalizerText = Get-Content -LiteralPath $normalizerPath -Raw
$runtimeText = Get-Content -LiteralPath $runtimePath -Raw
$documentationText = Get-Content -LiteralPath $documentationPath -Raw
Assert-NormalizationContract $normalizerText $runtimeText $documentationText
Write-Host 'Static FCC runtime event-normalization validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-NormalizationContract ($normalizerText.Replace('"system/api_retry"', '"system/future_retry"')) $runtimeText $documentationText
    } 'observed system/api_retry mapping removed'
    Assert-ContractRejects {
        Assert-NormalizationContract ($normalizerText.Replace('AgentRuntimeEventKind.Unknown', 'AgentRuntimeEventKind.RuntimeStatus')) $runtimeText $documentationText
    } 'unknown-event preservation removed'
    Assert-ContractRejects {
        Assert-NormalizationContract ($normalizerText.Replace('AgentRuntimeEventKind.ToolResult', 'AgentRuntimeEventKind.ToolProgress')) $runtimeText $documentationText
    } 'tool-result mapping removed'
    Assert-ContractRejects {
        Assert-NormalizationContract ($normalizerText.Replace('SensitiveTextAssignment.Replace', 'SensitiveTextAssignment.Match')) $runtimeText $documentationText
    } 'projected-text secret redaction removed'
    Assert-ContractRejects {
        Assert-NormalizationContract $normalizerText ($runtimeText.Replace('FccRuntimeEventNormalizer.Normalize', 'FccRuntimeEventNormalizer.Disabled')) $documentationText
    } 'structured adapter normalization integration removed'

    Assert-NormalizationContract $normalizerText $runtimeText $documentationText
    Write-Host 'Deterministic FCC runtime event-normalization negative/recovery fixtures: PASS.'
}

if ($RequireRuntime -and -not $RunFixtures) {
    throw '-RequireRuntime requires -RunFixtures so the executable normalization fixture actually runs.'
}

if ($RequireRuntime) {
    Invoke-NormalizationFixture -FccProjectPath $fccProjectPath
}
