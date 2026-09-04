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

function Assert-StructuredRuntimeContract {
    param([string]$RuntimeText, [string]$OptionsText, [string]$DocumentationText)

    foreach ($literal in @(
        'startInfo.ArgumentList.Add("--print")',
        'startInfo.ArgumentList.Add("--output-format")',
        'startInfo.ArgumentList.Add("stream-json")',
        'startInfo.ArgumentList.Add("--verbose")',
        'startInfo.ArgumentList.Add("--resume")',
        'UseShellExecute = false',
        'RedirectStandardOutput = true',
        'RedirectStandardError = true',
        'JsonDocument.Parse(line)',
        'AgentRuntimeEventKind.SessionIdentified',
        'AgentRuntimeFailureKind.MalformedStream',
        'AgentRuntimeFailureKind.NonZeroExit',
        'AgentRuntimeFailureKind.RuntimeNotFound',
        'process.Kill(entireProcessTree: true)',
        'FccStructuredPayloadSanitizer.Sanitize',
        '"[REDACTED]"',
        '"system/init"',
        '"json/unknown"'
    )) {
        Assert-ContainsLiteral $RuntimeText $literal 'FccStructuredAgentRuntime.cs'
    }

    foreach ($literal in @('MaximumPayloadCharacters', '64 * 1024', '1024 * 1024')) {
        Assert-ContainsLiteral $OptionsText $literal 'FccStructuredAgentRuntimeOptions.cs'
    }

    foreach ($literal in @(
        'FCCD-P04-003',
        '--print --output-format stream-json --verbose',
        '--resume <session-id>',
        'fixture-only',
        'does not claim provider execution',
        'P04-005',
        'P04-007'
    )) {
        Assert-ContainsLiteral $DocumentationText $literal 'FCC_STRUCTURED_RUNTIME.md'
    }

    if ($RuntimeText.Contains('AgentRuntimeTransport.CliFallback')) {
        throw 'P04-003 crossed into the P04-004 CLI fallback adapter.'
    }
    if ($RuntimeText.Contains('Task.Delay') -or $RuntimeText.Contains('retry_delay_ms')) {
        throw 'P04-003 crossed into P04-007 retry/backoff supervision.'
    }
}

function Assert-ContractRejects {
    param([scriptblock]$Action, [string]$Label)
    try { & $Action } catch {
        Write-Host "Negative fixture rejected as expected: $Label"
        return
    }
    throw "Negative structured-runtime fixture was not rejected: $Label"
}

function Assert-LastExitCode {
    param([string]$Stage)
    if ($LASTEXITCODE -ne 0) {
        throw "$Stage failed with exit code $LASTEXITCODE."
    }
}

function Invoke-StructuredRuntimeFixture {
    param([string]$FccProjectPath)

    if (-not $IsWindows) { throw 'Structured FCC runtime fixture requires Windows.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the structured FCC runtime fixture.'
    }
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Structured FCC runtime fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-p04-structured-fixture-' + [Guid]::NewGuid().ToString('N'))
    $fakeRoot = Join-Path $fixtureRoot 'fake runtime'
    $harnessRoot = Join-Path $fixtureRoot 'harness مساحة'
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
using System.Text.Json;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var prompt = args.Length == 0 ? string.Empty : args[^1];
        var resumeIndex = Array.IndexOf(args, "--resume");
        var sessionId = resumeIndex >= 0 && resumeIndex + 1 < args.Length
            ? args[resumeIndex + 1]
            : "fixture-session";

        if (prompt.Contains("FCC_FIXTURE_MALFORMED", StringComparison.Ordinal))
        {
            Console.WriteLine("{not-json}");
            return 0;
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = "system",
            subtype = "init",
            session_id = sessionId,
            uuid = "fixture-init",
            api_key = "fixture-secret"
        }));
        Console.Out.Flush();

        if (prompt.Contains("FCC_FIXTURE_CANCEL", StringComparison.Ordinal))
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            return 0;
        }
        if (prompt.Contains("FCC_FIXTURE_NONZERO", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("fixture stderr must be drained");
            return 7;
        }

        var large = prompt.Contains("FCC_FIXTURE_TRUNCATE", StringComparison.Ordinal)
            ? new string('x', 20_000)
            : "small";
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            type = "future_event",
            text = "مرحبا من fixture",
            token = "fixture-token",
            args,
            large
        }));
        return 0;
    }
}
'@

        & dotnet build $fakeProjectPath -c Release --nologo
        Assert-LastExitCode 'Fake FCC runtime build'
        $fakeExecutable = Join-Path $fakeRoot 'bin\Release\net10.0-windows\FakeFccRuntime.exe'
        if (-not (Test-Path -LiteralPath $fakeExecutable)) {
            throw "Fake FCC runtime executable was not produced: $fakeExecutable"
        }

        $projectReference = [Security.SecurityElement]::Escape($FccProjectPath)
        $harnessProjectPath = Join-Path $harnessRoot 'StructuredRuntimeFixture.csproj'
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
        var fakeExecutable = args[0];
        var cwd = Path.Combine(Path.GetTempPath(), "fccd structured work مساحة " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cwd);
        try
        {
            await VerifyNewAsync(fakeExecutable, cwd);
            await VerifyResumeAsync(fakeExecutable, cwd);
            await VerifyFailureAsync(fakeExecutable, cwd, "FCC_FIXTURE_MALFORMED", AgentRuntimeFailureKind.MalformedStream);
            await VerifyFailureAsync(fakeExecutable, cwd, "FCC_FIXTURE_NONZERO", AgentRuntimeFailureKind.NonZeroExit);
            await VerifyMissingAsync(cwd);
            await VerifyCancelAsync(fakeExecutable, cwd);
            await VerifyBoundAsync(fakeExecutable, cwd);
            await VerifyMissingCwdAsync(fakeExecutable, cwd);
            Console.WriteLine("Runtime FCC structured adapter happy/negative/recovery fixture: PASS.");
        }
        finally { Directory.Delete(cwd, recursive: true); }
    }

    private static async Task VerifyNewAsync(string executable, string cwd)
    {
        const string prompt = "  structured prompt مساحة  ";
        var runtime = new FccStructuredAgentRuntime(executable, "2.1.251");
        Assert(runtime.Descriptor.Transport == AgentRuntimeTransport.StructuredProcess, "structured descriptor");
        Assert(runtime.Descriptor.Capabilities.SupportsStreaming, "streaming capability");
        Assert(runtime.Descriptor.Capabilities.SupportsResume, "resume capability");

        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), prompt, cwd),
            CancellationToken.None);
        var events = await CollectAsync(execution.Events);
        var result = await execution.Completion;
        Assert(result.State == AgentRuntimeTerminalState.Succeeded, "new run succeeds");
        Assert(result.SessionId == "fixture-session", "session id propagated");
        Assert(events.Count == 2, "two structured frames emitted");
        Assert(events[0].Kind == AgentRuntimeEventKind.SessionIdentified, "init frame identifies session");
        Assert(events[0].SourceType == "system/init", "system subtype combined");
        using (var init = JsonDocument.Parse(events[0].PayloadJson!))
        {
            Assert(init.RootElement.GetProperty("api_key").GetString() == "[REDACTED]", "api key redacted");
        }
        Assert(events[1].Kind == AgentRuntimeEventKind.Unknown, "future frame stays unknown");
        Assert(events[1].SourceType == "future_event", "future source preserved");
        using (var future = JsonDocument.Parse(events[1].PayloadJson!))
        {
            Assert(future.RootElement.GetProperty("token").GetString() == "[REDACTED]", "token redacted");
            Assert(future.RootElement.GetProperty("text").GetString() == "مرحبا من fixture", "Unicode payload preserved semantically");
        }
        AssertArguments(events[1], ["--print", "--output-format", "stream-json", "--verbose", prompt]);
    }

    private static async Task VerifyResumeAsync(string executable, string cwd)
    {
        const string prompt = "resume prompt مساحة";
        const string sessionId = "resume-session-123";
        var runtime = new FccStructuredAgentRuntime(executable);
        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), prompt, cwd, sessionId),
            CancellationToken.None);
        var events = await CollectAsync(execution.Events);
        var result = await execution.Completion;
        Assert(result.State == AgentRuntimeTerminalState.Succeeded, "resume succeeds");
        Assert(result.SessionId == sessionId, "resume session retained");
        AssertArguments(events[1], ["--print", "--output-format", "stream-json", "--verbose", "--resume", sessionId, prompt]);
    }

    private static async Task VerifyFailureAsync(string executable, string cwd, string prompt, AgentRuntimeFailureKind expected)
    {
        var runtime = new FccStructuredAgentRuntime(executable);
        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), prompt, cwd),
            CancellationToken.None);
        _ = await CollectAsync(execution.Events);
        var result = await execution.Completion;
        Assert(result.State == AgentRuntimeTerminalState.Failed, $"{expected} fails");
        Assert(result.Failure?.Kind == expected, $"{expected} classification");
    }

    private static async Task VerifyMissingAsync(string cwd)
    {
        var runtime = new FccStructuredAgentRuntime(Path.Combine(cwd, "missing-fcc-claude.exe"));
        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "missing", cwd),
            CancellationToken.None);
        var result = await execution.Completion;
        Assert(result.Failure?.Kind == AgentRuntimeFailureKind.RuntimeNotFound, "missing runtime classification");
    }

    private static async Task VerifyCancelAsync(string executable, string cwd)
    {
        var runtime = new FccStructuredAgentRuntime(executable);
        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "FCC_FIXTURE_CANCEL", cwd),
            CancellationToken.None);
        await using var enumerator = execution.Events.GetAsyncEnumerator(CancellationToken.None);
        Assert(await enumerator.MoveNextAsync(), "cancel fixture init emitted");
        await execution.CancelAsync(CancellationToken.None);
        Assert((await execution.Completion).State == AgentRuntimeTerminalState.Cancelled, "owned process cancelled");
    }

    private static async Task VerifyBoundAsync(string executable, string cwd)
    {
        var runtime = new FccStructuredAgentRuntime(
            executable,
            options: new FccStructuredAgentRuntimeOptions { MaximumPayloadCharacters = 1024 });
        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "FCC_FIXTURE_TRUNCATE", cwd),
            CancellationToken.None);
        var events = await CollectAsync(execution.Events);
        Assert((await execution.Completion).State == AgentRuntimeTerminalState.Succeeded, "bounded payload succeeds");
        Assert(events[1].PayloadJson?.Contains("fccdTruncated", StringComparison.Ordinal) == true, "truncation marker");
        Assert(events[1].PayloadJson!.Length < 1024, "truncation envelope bounded");
    }

    private static async Task VerifyMissingCwdAsync(string executable, string cwd)
    {
        var runtime = new FccStructuredAgentRuntime(executable);
        var request = new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "missing cwd", Path.Combine(cwd, "not-created"));
        try
        {
            _ = await runtime.StartAsync(request, CancellationToken.None);
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        throw new InvalidOperationException("Missing working directory was not rejected.");
    }

    private static void AssertArguments(AgentRuntimeEvent runtimeEvent, string[] expected)
    {
        using var document = JsonDocument.Parse(runtimeEvent.PayloadJson!);
        var actual = document.RootElement.GetProperty("args").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert(actual.Length == expected.Length, "argument count");
        for (var index = 0; index < expected.Length; index++)
        {
            Assert(actual[index] == expected[index], $"argument {index}");
        }
    }

    private static async Task<List<AgentRuntimeEvent>> CollectAsync(IAsyncEnumerable<AgentRuntimeEvent> source)
    {
        var events = new List<AgentRuntimeEvent>();
        await foreach (var item in source) { events.Add(item); }
        return events;
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition) { throw new InvalidOperationException($"Structured FCC runtime assertion failed: {label}"); }
    }
}
'@

        & dotnet run --project $harnessProjectPath -c Release -- $fakeExecutable
        Assert-LastExitCode 'Structured FCC runtime executable fixture'
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$runtimePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FccStructuredAgentRuntime.cs'
$optionsPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FccStructuredAgentRuntimeOptions.cs'
$documentationPath = Join-Path $RepositoryRoot 'docs\runtime\FCC_STRUCTURED_RUNTIME.md'
$fccProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FCCCodeDesktop.Fcc.csproj'
foreach ($path in @($runtimePath, $optionsPath, $documentationPath, $fccProjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required structured FCC runtime path is missing: $path" }
}

$runtimeText = Get-Content -LiteralPath $runtimePath -Raw
$optionsText = Get-Content -LiteralPath $optionsPath -Raw
$documentationText = Get-Content -LiteralPath $documentationPath -Raw
Assert-StructuredRuntimeContract $runtimeText $optionsText $documentationText
Write-Host 'Static FCC structured-runtime validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects { Assert-StructuredRuntimeContract ($runtimeText.Replace('startInfo.ArgumentList.Add("stream-json")', 'startInfo.ArgumentList.Add("json")')) $optionsText $documentationText } 'stream-json primary transport removed'
    Assert-ContractRejects { Assert-StructuredRuntimeContract ($runtimeText.Replace('UseShellExecute = false', 'UseShellExecute = true')) $optionsText $documentationText } 'shell execution enabled'
    Assert-ContractRejects { Assert-StructuredRuntimeContract ($runtimeText.Replace('startInfo.ArgumentList.Add("--resume")', 'startInfo.ArgumentList.Add("--continue")')) $optionsText $documentationText } 'observed resume surface removed'
    Assert-ContractRejects { Assert-StructuredRuntimeContract ($runtimeText.Replace('process.Kill(entireProcessTree: true)', 'process.Kill()')) $optionsText $documentationText } 'owned process-tree cancellation removed'
    Assert-ContractRejects { Assert-StructuredRuntimeContract ($runtimeText.Replace('"[REDACTED]"', '"UNSAFE"')) $optionsText $documentationText } 'payload secret redaction removed'
    Assert-StructuredRuntimeContract $runtimeText $optionsText $documentationText
    Write-Host 'FCC structured-runtime recovery fixture: PASS.'
    Write-Host 'Deterministic FCC structured-runtime negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) { Invoke-StructuredRuntimeFixture $fccProjectPath }
