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
    param(
        [string]$RuntimeText,
        [string]$OptionsText,
        [string]$DocumentationText
    )

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

    foreach ($literal in @(
        'MaximumPayloadCharacters',
        '64 * 1024',
        '1024 * 1024'
    )) {
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

    try {
        & $Action
    }
    catch {
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

    if (-not $IsWindows) {
        throw 'Structured FCC runtime fixture requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the structured FCC runtime fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Structured FCC runtime fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-p04-structured-fixture-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $fakeRoot = Join-Path $fixtureRoot 'fake runtime'
        $harnessRoot = Join-Path $fixtureRoot 'harness مساحة'
        [void](New-Item -ItemType Directory -Path $fakeRoot -Force)
        [void](New-Item -ItemType Directory -Path $harnessRoot -Force)

        $fakeProjectPath = Join-Path $fakeRoot 'FakeFccRuntime.csproj'
        $fakeProgramPath = Join-Path $fakeRoot 'Program.cs'
        $fakeProject = @'
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
        $fakeProgram = @'
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

        Console.WriteLine(
            JsonSerializer.Serialize(
                new
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
        Console.WriteLine(
            JsonSerializer.Serialize(
                new
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
        Set-Content -LiteralPath $fakeProjectPath -Value $fakeProject -Encoding utf8NoBOM
        Set-Content -LiteralPath $fakeProgramPath -Value $fakeProgram -Encoding utf8NoBOM

        & dotnet build $fakeProjectPath -c Release --nologo
        Assert-LastExitCode 'Fake FCC runtime build'
        $fakeExecutable = Join-Path $fakeRoot 'bin\Release\net10.0-windows\FakeFccRuntime.exe'
        if (-not (Test-Path -LiteralPath $fakeExecutable)) {
            throw "Fake FCC runtime executable was not produced: $fakeExecutable"
        }

        $projectReference = [Security.SecurityElement]::Escape($FccProjectPath)
        $harnessProjectPath = Join-Path $harnessRoot 'StructuredRuntimeFixture.csproj'
        $harnessProgramPath = Join-Path $harnessRoot 'Program.cs'
        $harnessProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$projectReference" />
  </ItemGroup>
</Project>
"@
        $harnessProgram = @'
using System.Text.Json;
using FCCCodeDesktop.Fcc;
using FCCCodeDesktop.Runtime;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Assert(args.Length == 1, "fake runtime path supplied");
        var fakeExecutable = args[0];
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "fccd structured work مساحة " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            await VerifyNewRunAsync(fakeExecutable, workingDirectory);
            await VerifyResumeAsync(fakeExecutable, workingDirectory);
            await VerifyMalformedAsync(fakeExecutable, workingDirectory);
            await VerifyNonZeroAsync(fakeExecutable, workingDirectory);
            await VerifyMissingRuntimeAsync(workingDirectory);
            await VerifyCancellationAsync(fakeExecutable, workingDirectory);
            await VerifyPayloadBoundAsync(fakeExecutable, workingDirectory);
            await VerifyMissingWorkingDirectoryAsync(fakeExecutable, workingDirectory);
            Console.WriteLine("Runtime FCC structured adapter happy/negative/recovery fixture: PASS.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static async Task VerifyNewRunAsync(string fakeExecutable, string workingDirectory)
    {
        const string prompt = "  structured prompt مساحة  ";
        var runtime = new FccStructuredAgentRuntime(fakeExecutable, "2.1.251");
        Assert(runtime.Descriptor.Transport == AgentRuntimeTransport.StructuredProcess, "structured descriptor");
        Assert(runtime.Descriptor.Capabilities.SupportsStreaming, "streaming capability");
        Assert(runtime.Descriptor.Capabilities.SupportsResume, "resume capability");

        var request = new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), prompt, workingDirectory);
        await using var execution = await runtime.StartAsync(request, CancellationToken.None);
        var events = await CollectAsync(execution.Events);
        var result = await execution.Completion;

        Assert(result.State == AgentRuntimeTerminalState.Succeeded, "new run succeeds");
        Assert(result.SessionId == "fixture-session", "session id propagated");
        Assert(events.Count == 2, "two structured frames emitted");
        Assert(events[0].Kind == AgentRuntimeEventKind.SessionIdentified, "init frame identifies session");
        Assert(events[0].SourceType == "system/init", "system subtype combined");
        Assert(events[0].PayloadJson?.Contains("fixture-secret", StringComparison.Ordinal) == false, "api key redacted");
        Assert(events[0].PayloadJson?.Contains("[REDACTED]", StringComparison.Ordinal) == true, "redaction marker persisted");
        Assert(events[1].Kind == AgentRuntimeEventKind.Unknown, "future frame remains unknown");
        Assert(events[1].SourceType == "future_event", "future source type preserved");
        Assert(events[1].PayloadJson?.Contains("fixture-token", StringComparison.Ordinal) == false, "token redacted");
        Assert(events[1].PayloadJson?.Contains("مرحبا", StringComparison.Ordinal) == true, "Unicode payload preserved");
        AssertArguments(events[1], ["--print", "--output-format", "stream-json", "--verbose", prompt]);
    }

    private static async Task VerifyResumeAsync(string fakeExecutable, string workingDirectory)
    {
        const string prompt = "resume prompt مساحة";
        const string sessionId = "resume-session-123";
        var runtime = new FccStructuredAgentRuntime(fakeExecutable);
        var request = new AgentRuntimeRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            prompt,
            workingDirectory,
            sessionId);
        await using var execution = await runtime.StartAsync(request, CancellationToken.None);
        var events = await CollectAsync(execution.Events);
        var result = await execution.Completion;

        Assert(result.State == AgentRuntimeTerminalState.Succeeded, "resume run succeeds");
        Assert(result.SessionId == sessionId, "resume session identity retained");
        AssertArguments(
            events[1],
            ["--print", "--output-format", "stream-json", "--verbose", "--resume", sessionId, prompt]);
    }

    private static async Task VerifyMalformedAsync(string fakeExecutable, string workingDirectory)
    {
        var runtime = new FccStructuredAgentRuntime(fakeExecutable);
        var request = new AgentRuntimeRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FCC_FIXTURE_MALFORMED",
            workingDirectory);
        await using var execution = await runtime.StartAsync(request, CancellationToken.None);
        var events = await CollectAsync(execution.Events);
        var result = await execution.Completion;

        Assert(result.State == AgentRuntimeTerminalState.Failed, "malformed stream fails");
        Assert(result.Failure?.Kind == AgentRuntimeFailureKind.MalformedStream, "malformed stream classification");
        Assert(events.Count == 1 && events[0].Kind == AgentRuntimeEventKind.Error, "malformed frame surfaced");
    }

    private static async Task VerifyNonZeroAsync(string fakeExecutable, string workingDirectory)
    {
        var runtime = new FccStructuredAgentRuntime(fakeExecutable);
        var request = new AgentRuntimeRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FCC_FIXTURE_NONZERO",
            workingDirectory);
        await using var execution = await runtime.StartAsync(request, CancellationToken.None);
        _ = await CollectAsync(execution.Events);
        var result = await execution.Completion;

        Assert(result.State == AgentRuntimeTerminalState.Failed, "nonzero exit fails");
        Assert(result.Failure?.Kind == AgentRuntimeFailureKind.NonZeroExit, "nonzero exit classification");
        Assert(result.Failure?.Message.Contains("7", StringComparison.Ordinal) == true, "exit code retained");
    }

    private static async Task VerifyMissingRuntimeAsync(string workingDirectory)
    {
        var missingPath = Path.Combine(workingDirectory, "missing-fcc-claude.exe");
        var runtime = new FccStructuredAgentRuntime(missingPath);
        var request = new AgentRuntimeRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "missing runtime",
            workingDirectory);
        await using var execution = await runtime.StartAsync(request, CancellationToken.None);
        var result = await execution.Completion;

        Assert(result.State == AgentRuntimeTerminalState.Failed, "missing runtime fails");
        Assert(result.Failure?.Kind == AgentRuntimeFailureKind.RuntimeNotFound, "missing runtime classification");
    }

    private static async Task VerifyCancellationAsync(string fakeExecutable, string workingDirectory)
    {
        var runtime = new FccStructuredAgentRuntime(fakeExecutable);
        var request = new AgentRuntimeRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FCC_FIXTURE_CANCEL",
            workingDirectory);
        await using var execution = await runtime.StartAsync(request, CancellationToken.None);
        await using var enumerator = execution.Events.GetAsyncEnumerator(CancellationToken.None);
        Assert(await enumerator.MoveNextAsync(), "cancellation fixture emitted init frame");
        Assert(enumerator.Current.Kind == AgentRuntimeEventKind.SessionIdentified, "cancellation fixture session frame");

        await execution.CancelAsync(CancellationToken.None);
        var result = await execution.Completion;
        Assert(result.State == AgentRuntimeTerminalState.Cancelled, "owned process cancellation classified");
    }

    private static async Task VerifyPayloadBoundAsync(string fakeExecutable, string workingDirectory)
    {
        var runtime = new FccStructuredAgentRuntime(
            fakeExecutable,
            options: new FccStructuredAgentRuntimeOptions { MaximumPayloadCharacters = 1024 });
        var request = new AgentRuntimeRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FCC_FIXTURE_TRUNCATE",
            workingDirectory);
        await using var execution = await runtime.StartAsync(request, CancellationToken.None);
        var events = await CollectAsync(execution.Events);
        var result = await execution.Completion;

        Assert(result.State == AgentRuntimeTerminalState.Succeeded, "truncated payload run succeeds");
        Assert(events[1].PayloadJson?.Contains("fccdTruncated", StringComparison.Ordinal) == true, "payload truncation marker");
        Assert(events[1].PayloadJson!.Length < 1024, "truncated envelope stays bounded");
    }

    private static async Task VerifyMissingWorkingDirectoryAsync(string fakeExecutable, string workingDirectory)
    {
        var runtime = new FccStructuredAgentRuntime(fakeExecutable);
        var request = new AgentRuntimeRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "missing cwd",
            Path.Combine(workingDirectory, "not-created"));
        await AssertThrowsAsync<DirectoryNotFoundException>(
            () => runtime.StartAsync(request, CancellationToken.None),
            "missing working directory rejected");
    }

    private static void AssertArguments(AgentRuntimeEvent runtimeEvent, string[] expected)
    {
        using var document = JsonDocument.Parse(runtimeEvent.PayloadJson!);
        var actual = document.RootElement.GetProperty("args")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .ToArray();
        Assert(actual.Length == expected.Length, "argument count");
        for (var index = 0; index < expected.Length; index++)
        {
            Assert(actual[index] == expected[index], $"argument {index}");
        }
    }

    private static async Task<List<AgentRuntimeEvent>> CollectAsync(IAsyncEnumerable<AgentRuntimeEvent> source)
    {
        var events = new List<AgentRuntimeEvent>();
        await foreach (var runtimeEvent in source)
        {
            events.Add(runtimeEvent);
        }

        return events;
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string label)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected rejection: {label}");
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Structured FCC runtime assertion failed: {label}");
        }
    }
}
'@
        Set-Content -LiteralPath $harnessProjectPath -Value $harnessProject -Encoding utf8NoBOM
        Set-Content -LiteralPath $harnessProgramPath -Value $harnessProgram -Encoding utf8NoBOM

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
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required structured FCC runtime path is missing: $path"
    }
}

$runtimeText = Get-Content -LiteralPath $runtimePath -Raw
$optionsText = Get-Content -LiteralPath $optionsPath -Raw
$documentationText = Get-Content -LiteralPath $documentationPath -Raw

Assert-StructuredRuntimeContract $runtimeText $optionsText $documentationText
Write-Host 'Static FCC structured-runtime validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-StructuredRuntimeContract ($runtimeText.Replace('startInfo.ArgumentList.Add("stream-json")', 'startInfo.ArgumentList.Add("json")')) $optionsText $documentationText
    } 'stream-json primary transport removed'

    Assert-ContractRejects {
        Assert-StructuredRuntimeContract ($runtimeText.Replace('UseShellExecute = false', 'UseShellExecute = true')) $optionsText $documentationText
    } 'shell execution enabled'

    Assert-ContractRejects {
        Assert-StructuredRuntimeContract ($runtimeText.Replace('startInfo.ArgumentList.Add("--resume")', 'startInfo.ArgumentList.Add("--continue")')) $optionsText $documentationText
    } 'observed resume surface removed'

    Assert-ContractRejects {
        Assert-StructuredRuntimeContract ($runtimeText.Replace('process.Kill(entireProcessTree: true)', 'process.Kill()')) $optionsText $documentationText
    } 'owned process-tree cancellation removed'

    Assert-ContractRejects {
        Assert-StructuredRuntimeContract ($runtimeText.Replace('"[REDACTED]"', '"UNSAFE"')) $optionsText $documentationText
    } 'payload secret redaction removed'

    Assert-StructuredRuntimeContract $runtimeText $optionsText $documentationText
    Write-Host 'FCC structured-runtime recovery fixture: PASS.'
    Write-Host 'Deterministic FCC structured-runtime negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-StructuredRuntimeFixture $fccProjectPath
}
