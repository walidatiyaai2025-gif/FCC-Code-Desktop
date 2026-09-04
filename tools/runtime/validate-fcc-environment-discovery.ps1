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

function Assert-DiscoveryContract {
    param(
        [string]$ServiceText,
        [string]$OptionsText,
        [string]$SnapshotText,
        [string]$DocumentationText
    )

    foreach ($literal in @(
        'ResolveExecutable("fcc-claude"',
        'ResolveExecutable("fcc-server"',
        'VersionArguments = ["--version", "version", "-V"]',
        'FCCD_DISCOVERY_EXECUTABLE',
        'FCCD_DISCOVERY_ARGUMENT',
        'ArgumentList.Add',
        'UseShellExecute = false',
        'AllowAutoRedirect = false',
        'UseProxy = false',
        'HealthUri.IsLoopback',
        'Environment.GetEnvironmentVariable("FCC_PORT")',
        'http://127.0.0.1:',
        'process.Kill(entireProcessTree: true)'
    )) {
        Assert-ContainsLiteral $ServiceText $literal 'FccEnvironmentDiscoveryService.cs'
    }

    foreach ($literal in @(
        'ProcessTimeout',
        'HealthTimeout',
        'FccClaudeExecutablePath',
        'FccServerExecutablePath',
        'FccServerPort',
        'HealthUri'
    )) {
        Assert-ContainsLiteral $OptionsText $literal 'FccEnvironmentDiscoveryOptions.cs'
    }

    foreach ($literal in @(
        'FccExecutableDiscovery',
        'FccLoopbackHealth',
        'FccLoopbackHealthState.Healthy',
        'IsFccClaudeAvailable'
    )) {
        Assert-ContainsLiteral $SnapshotText $literal 'FccEnvironmentSnapshot.cs'
    }

    foreach ($forbidden in @(
        '--print',
        '--output-format',
        '--resume',
        '--session-id'
    )) {
        if ($ServiceText.Contains($forbidden)) {
            throw "P04-001 crossed into prompt/runtime execution scope: $forbidden"
        }
    }

    foreach ($literal in @(
        'FCCD-P04-001',
        'does not establish provider readiness',
        'Redirect following and proxy use are disabled',
        'does **not** implement',
        'without contacting a provider'
    )) {
        Assert-ContainsLiteral $DocumentationText $literal 'FCC_ENVIRONMENT_DISCOVERY.md'
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

    throw "Negative FCC environment-discovery fixture was not rejected: $Label"
}

function Invoke-DiscoveryRuntimeFixture {
    param([string]$FccProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime FCC environment-discovery fixture requires Windows.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime FCC environment-discovery fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime FCC environment-discovery fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-p04-discovery-fixture-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'FccDiscoveryFixture.csproj'
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
        var root = Path.Combine(
            Path.GetTempPath(),
            "fccd p04 discovery مساحة " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var fakeBin = Path.Combine(root, "fake bin");
            var emptyBin = Path.Combine(root, "empty");
            Directory.CreateDirectory(fakeBin);
            Directory.CreateDirectory(emptyBin);

            var claudePath = Path.Combine(fakeBin, "fcc-claude.cmd");
            var serverPath = Path.Combine(fakeBin, "fcc-server.cmd");
            await File.WriteAllTextAsync(
                claudePath,
                "@echo off\r\nif /I \"%~1\"==\"--version\" goto version\r\nexit /b 7\r\n:version\r\necho 2.1.251 ^(Claude Code^)\r\nexit /b 0\r\n");
            await File.WriteAllTextAsync(serverPath, "@echo off\r\nexit /b 0\r\n");

            using (var listener = new TcpListener(IPAddress.Loopback, 0))
            {
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var responseTask = ServeHealthyResponseAsync(listener);

                var service = new FccEnvironmentDiscoveryService(
                    new FccEnvironmentDiscoveryOptions
                    {
                        PathValue = fakeBin,
                        PathExtensions = ".CMD;.EXE",
                        HealthUri = new Uri($"http://127.0.0.1:{port}/health"),
                        ProcessTimeout = TimeSpan.FromSeconds(5),
                        HealthTimeout = TimeSpan.FromSeconds(2)
                    });

                var snapshot = await service.DiscoverAsync(CancellationToken.None);
                await responseTask;

                Assert(snapshot.FccClaude.IsFound, "PATH fcc-claude discovery");
                Assert(snapshot.FccClaude.IsVersionKnown, "version parsed");
                Assert(snapshot.FccClaude.ParsedVersion == new Version(2, 1, 251), "version value");
                Assert(snapshot.FccClaude.VersionText?.Contains("Claude Code", StringComparison.Ordinal) == true, "version text");
                Assert(snapshot.FccServer.IsFound, "PATH fcc-server discovery");
                Assert(snapshot.LoopbackHealth.State == FccLoopbackHealthState.Healthy, "healthy loopback classification");
                Assert(snapshot.LoopbackHealth.HttpStatusCode == 200, "healthy loopback status");
            }

            var closedPort = GetClosedLoopbackPort();
            var missingService = new FccEnvironmentDiscoveryService(
                new FccEnvironmentDiscoveryOptions
                {
                    PathValue = emptyBin,
                    PathExtensions = ".CMD;.EXE",
                    HealthUri = new Uri($"http://127.0.0.1:{closedPort}/health"),
                    HealthTimeout = TimeSpan.FromMilliseconds(400)
                });
            var missing = await missingService.DiscoverAsync(CancellationToken.None);
            Assert(!missing.FccClaude.IsFound, "missing fcc-claude stays missing");
            Assert(!missing.FccServer.IsFound, "missing fcc-server stays missing");
            Assert(missing.LoopbackHealth.State == FccLoopbackHealthState.Unreachable, "unreachable loopback classification");

            using (var listener = new TcpListener(IPAddress.Loopback, 0))
            {
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var responseTask = ServeHealthyResponseAsync(listener);

                var explicitService = new FccEnvironmentDiscoveryService(
                    new FccEnvironmentDiscoveryOptions
                    {
                        FccClaudeExecutablePath = claudePath,
                        FccServerExecutablePath = serverPath,
                        PathValue = emptyBin,
                        HealthUri = new Uri($"http://127.0.0.1:{port}/health")
                    });
                var explicitSnapshot = await explicitService.DiscoverAsync(CancellationToken.None);
                await responseTask;
                Assert(explicitSnapshot.FccClaude.IsFound, "explicit fcc-claude path override");
                Assert(explicitSnapshot.FccServer.IsFound, "explicit fcc-server path override");
            }

            AssertThrows<ArgumentException>(
                () => _ = new FccEnvironmentDiscoveryService(
                    new FccEnvironmentDiscoveryOptions
                    {
                        HealthUri = new Uri("https://example.com/health")
                    }),
                "external health URI rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => _ = new FccEnvironmentDiscoveryService(
                    new FccEnvironmentDiscoveryOptions
                    {
                        FccServerPort = 0
                    }),
                "invalid port rejected");

            Console.WriteLine("Runtime FCC environment-discovery happy/negative/recovery fixture: PASS.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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

    private static int GetClosedLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"FCC discovery assertion failed: {label}");
        }
    }

    private static void AssertThrows<TException>(Action action, string label)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected rejection: {label}");
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime FCC environment-discovery fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$servicePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FccEnvironmentDiscoveryService.cs'
$optionsPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FccEnvironmentDiscoveryOptions.cs'
$snapshotPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FccEnvironmentSnapshot.cs'
$documentationPath = Join-Path $RepositoryRoot 'docs\runtime\FCC_ENVIRONMENT_DISCOVERY.md'
$fccProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Fcc\FCCCodeDesktop.Fcc.csproj'

foreach ($path in @($servicePath, $optionsPath, $snapshotPath, $documentationPath, $fccProjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required FCC environment-discovery path is missing: $path"
    }
}

$serviceText = Get-Content -LiteralPath $servicePath -Raw
$optionsText = Get-Content -LiteralPath $optionsPath -Raw
$snapshotText = Get-Content -LiteralPath $snapshotPath -Raw
$documentationText = Get-Content -LiteralPath $documentationPath -Raw

Assert-DiscoveryContract $serviceText $optionsText $snapshotText $documentationText
Write-Host 'Static FCC environment-discovery validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-DiscoveryContract ($serviceText.Replace('AllowAutoRedirect = false', 'AllowAutoRedirect = true')) $optionsText $snapshotText $documentationText
    } 'loopback redirect protection removed'

    Assert-ContractRejects {
        Assert-DiscoveryContract ($serviceText.Replace('HealthUri.IsLoopback', 'RemovedLoopbackValidation')) $optionsText $snapshotText $documentationText
    } 'loopback URI validation removed'

    Assert-ContractRejects {
        Assert-DiscoveryContract ($serviceText.Replace('VersionArguments = ["--version", "version", "-V"]', 'VersionArguments = ["--version"]')) $optionsText $snapshotText $documentationText
    } 'version fallback probes removed'

    Assert-ContractRejects {
        Assert-DiscoveryContract ($serviceText + "`n// --print") $optionsText $snapshotText $documentationText
    } 'prompt execution leaked into P04-001'

    Assert-DiscoveryContract $serviceText $optionsText $snapshotText $documentationText
    Write-Host 'FCC environment-discovery recovery fixture: PASS.'
    Write-Host 'Deterministic FCC environment-discovery negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-DiscoveryRuntimeFixture $fccProjectPath
}
