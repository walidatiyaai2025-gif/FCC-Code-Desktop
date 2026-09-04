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

function Assert-HealthCompatibilityContract {
    param(
        [string]$ServiceText,
        [string]$DocumentationText
    )

    foreach ($literal in @(
        'FccRuntimeHealthCompatibilityService',
        'TestedFccClaudeVersionText = "2.1.251"',
        'FccRuntimeVersionEvidenceState.TestedBaseline',
        'FccRuntimeVersionEvidenceState.DetectedUntestedVersion',
        'FccRuntimeVersionEvidenceState.UnverifiedVersion',
        'FccRuntimeVersionEvidenceState.RuntimeMissing',
        'RequiresCompatibilitySmokeCheck',
        'CanAttemptRuntime => Availability == FccRuntimeAvailabilityState.Available',
        'Environment.LoopbackHealth.IsHealthy',
        'InspectAsync',
        'DiscoverAsync',
        'Provider readiness is not'
    )) {
        Assert-ContainsLiteral $ServiceText $literal 'FccRuntimeHealthCompatibilityService.cs'
    }

    foreach ($literal in @(
        'FCCD-P04-006',
        'exact tested baseline',
        'does **not** establish provider readiness',
        'DetectedUntestedVersion',
        'UnverifiedVersion',
        'RequiresCompatibilitySmokeCheck',
        'P04-008',
        'does not manufacture 429 traffic'
    )) {
        Assert-ContainsLiteral $DocumentationText $literal 'FCC_RUNTIME_HEALTH_COMPATIBILITY.md'
    }

    if ($ServiceText.Contains('ProviderReady = true') -or
        $ServiceText.Contains('ProviderReadiness = true')) {
        throw 'P04-006 must not infer provider readiness from local health/version discovery.'
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

    throw "Negative FCC runtime health/compatibility fixture was not rejected: $Label"
}

function Invoke-HealthCompatibilityRuntimeFixture {
    param([string]$FccProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime FCC health/compatibility fixture requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime FCC health/compatibility fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime FCC health/compatibility fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-p04-health-compat-fixture-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'FccHealthCompatibilityFixture.csproj'
        $programPath = Join-Path $fixtureRoot 'Program.cs'
        $projectReference = [Security.SecurityElement]::Escape($FccProjectPath)

        $project = @"
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

        $program = @'
using System.Net;
using System.Net.Sockets;
using System.Text;
using FCCCodeDesktop.Fcc;

internal static class Program
{
    private static async Task Main()
    {
        var healthy = new FccLoopbackHealth(
            new Uri("http://127.0.0.1:8082/health"),
            FccLoopbackHealthState.Healthy,
            200,
            null);
        var unreachable = new FccLoopbackHealth(
            new Uri("http://127.0.0.1:8082/health"),
            FccLoopbackHealthState.Unreachable,
            null,
            "offline");
        var server = new FccExecutableDiscovery("fcc-server", @"C:\fake\fcc-server.cmd", null, null, null);
        var evaluator = new FccRuntimeHealthCompatibilityService(
            new FccEnvironmentDiscoveryService(
                new FccEnvironmentDiscoveryOptions
                {
                    PathValue = string.Empty,
                    HealthUri = new Uri("http://127.0.0.1:1/health")
                }));

        var tested = evaluator.Evaluate(
            new FccEnvironmentSnapshot(
                new FccExecutableDiscovery(
                    "fcc-claude",
                    @"C:\fake\fcc-claude.cmd",
                    "2.1.251 (Claude Code)",
                    new Version(2, 1, 251),
                    null),
                server,
                healthy));
        Assert(tested.Availability == FccRuntimeAvailabilityState.Available, "tested runtime available");
        Assert(tested.VersionEvidence == FccRuntimeVersionEvidenceState.TestedBaseline, "tested baseline classification");
        Assert(tested.TestedBaselineVersion == "2.1.251", "tested baseline value");
        Assert(tested.CanAttemptRuntime, "tested runtime launch attempt allowed");
        Assert(!tested.RequiresCompatibilitySmokeCheck, "tested baseline does not require smoke check");
        Assert(tested.IsLoopbackHealthy, "healthy loopback retained");
        Assert(tested.Summary.Contains("Provider readiness is not implied", StringComparison.Ordinal), "provider-readiness boundary retained");

        var changedVersion = evaluator.Evaluate(
            new FccEnvironmentSnapshot(
                new FccExecutableDiscovery(
                    "fcc-claude",
                    @"C:\fake\fcc-claude.cmd",
                    "2.1.252",
                    new Version(2, 1, 252),
                    null),
                server,
                healthy));
        Assert(changedVersion.VersionEvidence == FccRuntimeVersionEvidenceState.DetectedUntestedVersion, "changed version untested");
        Assert(changedVersion.RequiresCompatibilitySmokeCheck, "changed version requires smoke check");
        Assert(changedVersion.CanAttemptRuntime, "changed version remains launchable");

        var unknownVersion = evaluator.Evaluate(
            new FccEnvironmentSnapshot(
                new FccExecutableDiscovery(
                    "fcc-claude",
                    @"C:\fake\fcc-claude.cmd",
                    "Claude Code unknown",
                    null,
                    "Version text did not contain a parseable numeric version."),
                server,
                healthy));
        Assert(unknownVersion.VersionEvidence == FccRuntimeVersionEvidenceState.UnverifiedVersion, "unknown version unverified");
        Assert(unknownVersion.RequiresCompatibilitySmokeCheck, "unknown version requires smoke check");

        var missing = evaluator.Evaluate(
            new FccEnvironmentSnapshot(
                new FccExecutableDiscovery("fcc-claude", null, null, null, "missing"),
                server,
                healthy));
        Assert(missing.Availability == FccRuntimeAvailabilityState.Unavailable, "missing runtime unavailable");
        Assert(missing.VersionEvidence == FccRuntimeVersionEvidenceState.RuntimeMissing, "missing runtime version state");
        Assert(!missing.CanAttemptRuntime, "missing runtime cannot launch");
        Assert(!missing.RequiresCompatibilitySmokeCheck, "missing runtime is not a version smoke case");

        var degradedLoopback = evaluator.Evaluate(
            new FccEnvironmentSnapshot(
                new FccExecutableDiscovery(
                    "fcc-claude",
                    @"C:\fake\fcc-claude.cmd",
                    "2.1.251",
                    new Version(2, 1, 251),
                    null),
                server,
                unreachable));
        Assert(degradedLoopback.CanAttemptRuntime, "loopback failure does not rewrite executable availability");
        Assert(!degradedLoopback.IsLoopbackHealthy, "unreachable loopback retained separately");
        Assert(degradedLoopback.VersionEvidence == FccRuntimeVersionEvidenceState.TestedBaseline, "version evidence independent from loopback");

        var root = Path.Combine(Path.GetTempPath(), "fccd p04 health مساحة " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var fakeBin = Path.Combine(root, "fake bin");
            Directory.CreateDirectory(fakeBin);
            var claudePath = Path.Combine(fakeBin, "fcc-claude.cmd");
            var serverPath = Path.Combine(fakeBin, "fcc-server.cmd");
            await File.WriteAllTextAsync(
                claudePath,
                "@echo off\r\nif /I \"%~1\"==\"--version\" goto version\r\nexit /b 7\r\n:version\r\necho 2.1.251 ^(Claude Code^)\r\nexit /b 0\r\n");
            await File.WriteAllTextAsync(serverPath, "@echo off\r\nexit /b 0\r\n");

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var responseTask = ServeHealthyResponseAsync(listener);
            var discovery = new FccEnvironmentDiscoveryService(
                new FccEnvironmentDiscoveryOptions
                {
                    PathValue = fakeBin,
                    PathExtensions = ".CMD;.EXE",
                    HealthUri = new Uri($"http://127.0.0.1:{port}/health"),
                    ProcessTimeout = TimeSpan.FromSeconds(5),
                    HealthTimeout = TimeSpan.FromSeconds(2)
                });
            var integrated = await new FccRuntimeHealthCompatibilityService(discovery)
                .InspectAsync(CancellationToken.None);
            await responseTask;

            Assert(integrated.Availability == FccRuntimeAvailabilityState.Available, "InspectAsync discovery integration");
            Assert(integrated.VersionEvidence == FccRuntimeVersionEvidenceState.TestedBaseline, "InspectAsync version integration");
            Assert(integrated.IsLoopbackHealthy, "InspectAsync loopback integration");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        Console.WriteLine("Runtime FCC health/version compatibility happy/negative/recovery fixture: PASS.");
    }

    private static async Task ServeHealthyResponseAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync(CancellationToken.None);
        await using var stream = client.GetStream();
        var requestBuffer = new byte[2048];
        _ = await stream.ReadAsync(requestBuffer, CancellationToken.None);
        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK");
        await stream.WriteAsync(response, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"FCC health/compatibility assertion failed: {label}");
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime FCC health/compatibility fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$servicePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FccRuntimeHealthCompatibilityService.cs'
$documentationPath = Join-Path $RepositoryRoot 'docs\runtime\FCC_RUNTIME_HEALTH_COMPATIBILITY.md'
$fccProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FCCCodeDesktop.Fcc.csproj'

foreach ($path in @($servicePath, $documentationPath, $fccProjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required FCC health/compatibility path is missing: $path"
    }
}

$serviceText = Get-Content -LiteralPath $servicePath -Raw
$documentationText = Get-Content -LiteralPath $documentationPath -Raw
Assert-HealthCompatibilityContract $serviceText $documentationText
Write-Host 'Static FCC runtime health/version compatibility validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-HealthCompatibilityContract ($serviceText.Replace('TestedFccClaudeVersionText = "2.1.251"', 'TestedFccClaudeVersionText = "2.1.252"')) $documentationText
    } 'exact tested baseline changed'

    Assert-ContractRejects {
        Assert-HealthCompatibilityContract ($serviceText.Replace('RequiresCompatibilitySmokeCheck', 'RemovedCompatibilitySmokeCheck')) $documentationText
    } 'version-change smoke requirement removed'

    Assert-ContractRejects {
        Assert-HealthCompatibilityContract $serviceText ($documentationText.Replace('does **not** establish provider readiness', 'establishes provider readiness'))
    } 'provider-readiness boundary removed'

    Assert-HealthCompatibilityContract $serviceText $documentationText
    Write-Host 'FCC runtime health/version compatibility recovery fixture: PASS.'
    Write-Host 'Deterministic FCC runtime health/version compatibility negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-HealthCompatibilityRuntimeFixture $fccProjectPath
}
