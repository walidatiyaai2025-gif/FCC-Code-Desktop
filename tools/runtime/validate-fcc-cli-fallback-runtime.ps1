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

function Assert-FallbackRuntimeContract {
    param([string]$RuntimeText, [string]$OptionsText, [string]$DocumentationText)

    foreach ($literal in @(
        'startInfo.ArgumentList.Add("--print")',
        'startInfo.ArgumentList.Add(request.Prompt)',
        'UseShellExecute = false',
        'RedirectStandardOutput = true',
        'RedirectStandardError = true',
        'AgentRuntimeTransport.CliFallback',
        'supportsStreaming: false',
        'supportsSessions: false',
        'supportsResume: false',
        'supportsCancellation: true',
        'supportsToolActivity: false',
        'request.ResumeSessionId is not null',
        'throw new NotSupportedException',
        'process.Kill(entireProcessTree: true)',
        'AgentRuntimeFailureKind.RuntimeNotFound',
        'AgentRuntimeFailureKind.NonZeroExit',
        'AgentRuntimeFailureKind.UnknownFailure',
        'AgentRuntimeFailureKind.ProcessCrash',
        'cli-fallback/json',
        'cli-fallback/stdout',
        '"[REDACTED]"',
        'CaptureAsync(process.StandardOutput',
        'CaptureAsync(process.StandardError'
    )) {
        Assert-ContainsLiteral $RuntimeText $literal 'FccCliFallbackAgentRuntime.cs'
    }

    if ($RuntimeText.Contains('stream-json') -or $RuntimeText.Contains('startInfo.ArgumentList.Add("--resume")')) {
        throw 'P04-004 fallback runtime must remain the plain --print compatibility transport.'
    }
    if ($RuntimeText.Contains('Task.Delay') -or $RuntimeText.Contains('retry_delay_ms')) {
        throw 'P04-004 crossed into P04-007 retry/backoff supervision.'
    }

    foreach ($literal in @('MaximumOutputCharacters', '64 * 1024', '1024 * 1024')) {
        Assert-ContainsLiteral $OptionsText $literal 'FccCliFallbackAgentRuntimeOptions.cs'
    }

    foreach ($literal in @(
        'FCCD-P04-004',
        'fcc-claude --print <prompt>',
        'AgentRuntimeTransport.CliFallback',
        'does not claim provider execution',
        'FCCD-P04-005',
        'FCCD-P04-007',
        'FCCD-P04-008'
    )) {
        Assert-ContainsLiteral $DocumentationText $literal 'FCC_CLI_FALLBACK_RUNTIME.md'
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
    throw "Negative CLI fallback fixture was not rejected: $Label"
}

function Assert-LastExitCode {
    param([string]$Stage)
    if ($LASTEXITCODE -ne 0) {
        throw "$Stage failed with exit code $LASTEXITCODE."
    }
}

function Invoke-FallbackRuntimeFixture {
    param([string]$FccProjectPath)

    if (-not $IsWindows) {
        throw 'CLI fallback FCC runtime fixture requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the CLI fallback FCC runtime fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "CLI fallback FCC runtime fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-p04-fallback-fixture-' + [Guid]::NewGuid().ToString('N'))
    $fakeRoot = Join-Path $fixtureRoot 'fake fallback runtime'
    $harnessRoot = Join-Path $fixtureRoot 'harness مساحة'
    [void](New-Item -ItemType Directory -Path $fakeRoot -Force)
    [void](New-Item -ItemType Directory -Path $harnessRoot -Force)

    try {
        $fakeProjectPath = Join-Path $fakeRoot 'FakeFallbackRuntime.csproj'
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
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var prompt = args.Length == 0 ? string.Empty : args[^1];
        if (prompt.Contains("FCC_FIXTURE_CANCEL", StringComparison.Ordinal))
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            return 0;
        }
        if (prompt.Contains("FCC_FIXTURE_NONZERO", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("token=fixture-stderr-secret");
            return 7;
        }
        if (prompt.Contains("FCC_FIXTURE_EMPTY", StringComparison.Ordinal))
        {
            return 0;
        }
        if (prompt.Contains("FCC_FIXTURE_LARGE", StringComparison.Ordinal))
        {
            Console.WriteLine(new string('x', 20_000));
            return 0;
        }
        if (prompt.Contains("FCC_FIXTURE_TEXT", StringComparison.Ordinal))
        {
            Console.WriteLine("مرحبا من fallback token=fixture-plain-secret");
            return 0;
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            message = "مرحبا من fallback JSON",
            token = "fixture-json-secret",
            args
        }));
        return 0;
    }
}
'@

        & dotnet build $fakeProjectPath -c Release --nologo
        Assert-LastExitCode 'Fake fallback FCC runtime build'
        $fakeExecutable = Join-Path $fakeRoot 'bin\Release\net10.0-windows\FakeFallbackRuntime.exe'
        if (-not (Test-Path -LiteralPath $fakeExecutable)) {
            throw "Fake fallback runtime executable was not produced: $fakeExecutable"
        }

        $projectReference = [Security.SecurityElement]::Escape($FccProjectPath)
        $harnessProjectPath = Join-Path $harnessRoot 'FallbackRuntimeFixture.csproj'
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
        var cwd = Path.Combine(Path.GetTempPath(), "fccd fallback work مساحة " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cwd);
        try
        {
            await VerifyJsonAsync(fakeExecutable, cwd);
            await VerifyTextAsync(fakeExecutable, cwd);
            await VerifyFailureAsync(fakeExecutable, cwd, "FCC_FIXTURE_NONZERO", AgentRuntimeFailureKind.NonZeroExit);
            await VerifyFailureAsync(fakeExecutable, cwd, "FCC_FIXTURE_EMPTY", AgentRuntimeFailureKind.UnknownFailure);
            await VerifyMissingAsync(cwd);
            await VerifyCancelAsync(fakeExecutable, cwd);
            await VerifyBoundAsync(fakeExecutable, cwd);
            await VerifyResumeRejectedAsync(fakeExecutable, cwd);
            await VerifyMissingCwdAsync(fakeExecutable, cwd);
            Console.WriteLine("Runtime FCC CLI fallback happy/negative/recovery fixture: PASS.");
        }
        finally
        {
            Directory.Delete(cwd, recursive: true);
        }
    }

    private static async Task VerifyJsonAsync(string executable, string cwd)
    {
        const string prompt = "  fallback prompt مساحة  ";
        var runtime = new FccCliFallbackAgentRuntime(executable, "2.1.251");
        Assert(runtime.Descriptor.Transport == AgentRuntimeTransport.CliFallback, "fallback descriptor");
        Assert(!runtime.Descriptor.Capabilities.SupportsStreaming, "streaming not advertised");
        Assert(!runtime.Descriptor.Capabilities.SupportsSessions, "sessions not advertised");
        Assert(!runtime.Descriptor.Capabilities.SupportsResume, "resume not advertised");
        Assert(runtime.Descriptor.Capabilities.SupportsCancellation, "cancellation advertised");
        Assert(!runtime.Descriptor.Capabilities.SupportsToolActivity, "tool activity not advertised");

        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), prompt, cwd),
            CancellationToken.None);
        var events = await CollectAsync(execution.Events);
        var result = await execution.Completion;
        Assert(result.State == AgentRuntimeTerminalState.Succeeded, "JSON fallback succeeds");
        Assert(result.SessionId is null, "fallback does not invent session id");
        Assert(events.Count == 1, "one JSON compatibility event emitted");
        var runtimeEvent = events[0];
        Assert(runtimeEvent.Kind == AgentRuntimeEventKind.Unknown, "compatibility event remains unknown");
        Assert(runtimeEvent.SourceType == "cli-fallback/json", "JSON source retained");
        using var document = JsonDocument.Parse(runtimeEvent.PayloadJson!);
        Assert(document.RootElement.GetProperty("token").GetString() == "[REDACTED]", "JSON token redacted");
        Assert(document.RootElement.GetProperty("message").GetString() == "مرحبا من fallback JSON", "Unicode JSON retained");
        var actual = document.RootElement.GetProperty("args").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert(actual.Length == 2, "fallback argument count");
        Assert(actual[0] == "--print", "fallback uses --print");
        Assert(actual[1] == prompt, "prompt preserved as one argument");
    }

    private static async Task VerifyTextAsync(string executable, string cwd)
    {
        var runtime = new FccCliFallbackAgentRuntime(executable);
        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "FCC_FIXTURE_TEXT", cwd),
            CancellationToken.None);
        var events = await CollectAsync(execution.Events);
        Assert((await execution.Completion).State == AgentRuntimeTerminalState.Succeeded, "text fallback succeeds");
        Assert(events.Count == 1, "one text compatibility event emitted");
        var runtimeEvent = events[0];
        Assert(runtimeEvent.SourceType == "cli-fallback/stdout", "text source retained");
        Assert(runtimeEvent.Text?.Contains("مرحبا من fallback", StringComparison.Ordinal) == true, "Unicode text retained");
        Assert(runtimeEvent.Text?.Contains("[REDACTED]", StringComparison.Ordinal) == true, "plain secret assignment redacted");
        Assert(runtimeEvent.Text?.Contains("fixture-plain-secret", StringComparison.Ordinal) == false, "plain secret absent");
    }

    private static async Task VerifyFailureAsync(
        string executable,
        string cwd,
        string prompt,
        AgentRuntimeFailureKind expected)
    {
        var runtime = new FccCliFallbackAgentRuntime(executable);
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
        var runtime = new FccCliFallbackAgentRuntime(Path.Combine(cwd, "missing-fcc-claude.exe"));
        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "missing", cwd),
            CancellationToken.None);
        Assert((await execution.Completion).Failure?.Kind == AgentRuntimeFailureKind.RuntimeNotFound, "missing runtime classification");
    }

    private static async Task VerifyCancelAsync(string executable, string cwd)
    {
        var runtime = new FccCliFallbackAgentRuntime(executable);
        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "FCC_FIXTURE_CANCEL", cwd),
            CancellationToken.None);
        await Task.Delay(250);
        await execution.CancelAsync(CancellationToken.None);
        Assert((await execution.Completion).State == AgentRuntimeTerminalState.Cancelled, "owned fallback process cancelled");
    }

    private static async Task VerifyBoundAsync(string executable, string cwd)
    {
        var runtime = new FccCliFallbackAgentRuntime(
            executable,
            options: new FccCliFallbackAgentRuntimeOptions { MaximumOutputCharacters = 1024 });
        await using var execution = await runtime.StartAsync(
            new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "FCC_FIXTURE_LARGE", cwd),
            CancellationToken.None);
        var events = await CollectAsync(execution.Events);
        Assert((await execution.Completion).State == AgentRuntimeTerminalState.Succeeded, "bounded fallback succeeds");
        Assert(events.Count == 1, "one bounded compatibility event emitted");
        var runtimeEvent = events[0];
        Assert(runtimeEvent.Text?.Length <= 1024, "stdout retention bounded");
        using var metadata = JsonDocument.Parse(runtimeEvent.PayloadJson!);
        Assert(metadata.RootElement.GetProperty("fccdTruncated").GetBoolean(), "truncation marker set");
        Assert(metadata.RootElement.GetProperty("originalCharacters").GetInt64() > 1024, "original character count retained");
    }

    private static async Task VerifyResumeRejectedAsync(string executable, string cwd)
    {
        var runtime = new FccCliFallbackAgentRuntime(executable);
        var request = new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "resume", cwd, "session-id");
        try
        {
            _ = await runtime.StartAsync(request, CancellationToken.None);
        }
        catch (NotSupportedException)
        {
            return;
        }
        throw new InvalidOperationException("Fallback resume request was not rejected.");
    }

    private static async Task VerifyMissingCwdAsync(string executable, string cwd)
    {
        var runtime = new FccCliFallbackAgentRuntime(executable);
        var request = new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "missing cwd", Path.Combine(cwd, "not-created"));
        try
        {
            _ = await runtime.StartAsync(request, CancellationToken.None);
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        throw new InvalidOperationException("Missing fallback working directory was not rejected.");
    }

    private static async Task<List<AgentRuntimeEvent>> CollectAsync(IAsyncEnumerable<AgentRuntimeEvent> source)
    {
        var events = new List<AgentRuntimeEvent>();
        await foreach (var item in source)
        {
            events.Add(item);
        }
        return events;
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"CLI fallback runtime assertion failed: {label}");
        }
    }
}
'@

        & dotnet run --project $harnessProjectPath -c Release -- $fakeExecutable
        Assert-LastExitCode 'CLI fallback FCC runtime executable fixture'
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$runtimePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FccCliFallbackAgentRuntime.cs'
$optionsPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FccCliFallbackAgentRuntimeOptions.cs'
$documentationPath = Join-Path $RepositoryRoot 'docs\runtime\FCC_CLI_FALLBACK_RUNTIME.md'
foreach ($requiredPath in @($runtimePath, $optionsPath, $documentationPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required CLI fallback runtime path is missing: $requiredPath"
    }
}

$runtimeText = Get-Content -LiteralPath $runtimePath -Raw
$optionsText = Get-Content -LiteralPath $optionsPath -Raw
$documentationText = Get-Content -LiteralPath $documentationPath -Raw
Assert-FallbackRuntimeContract $runtimeText $optionsText $documentationText
Write-Host 'Static FCC CLI fallback runtime validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-FallbackRuntimeContract ($runtimeText.Replace('startInfo.ArgumentList.Add("--print")', '')) $optionsText $documentationText
    } 'plain --print invocation removed'
    Assert-ContractRejects {
        Assert-FallbackRuntimeContract ($runtimeText.Replace('UseShellExecute = false', 'UseShellExecute = true')) $optionsText $documentationText
    } 'shell execution enabled'
    Assert-ContractRejects {
        Assert-FallbackRuntimeContract ($runtimeText.Replace('supportsStreaming: false', 'supportsStreaming: true')) $optionsText $documentationText
    } 'streaming capability overstated'
    Assert-ContractRejects {
        Assert-FallbackRuntimeContract ($runtimeText.Replace('throw new NotSupportedException', 'throw new InvalidOperationException')) $optionsText $documentationText
    } 'unsupported resume rejection weakened'
    Assert-ContractRejects {
        Assert-FallbackRuntimeContract ($runtimeText.Replace('process.Kill(entireProcessTree: true)', 'process.Kill()')) $optionsText $documentationText
    } 'owned process-tree cancellation removed'
    Assert-ContractRejects {
        Assert-FallbackRuntimeContract ($runtimeText.Replace('"[REDACTED]"', 'property.Value.GetRawText()')) $optionsText $documentationText
    } 'JSON secret redaction removed'
    Write-Host 'FCC CLI fallback recovery fixture: PASS.'
    Write-Host 'Deterministic FCC CLI fallback negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    $fccProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FCCCodeDesktop.Fcc.csproj'
    Invoke-FallbackRuntimeFixture -FccProjectPath $fccProjectPath
}