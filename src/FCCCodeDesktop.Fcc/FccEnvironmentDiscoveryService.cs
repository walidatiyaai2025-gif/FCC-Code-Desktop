using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;

namespace FCCCodeDesktop.Fcc;

public sealed class FccEnvironmentDiscoveryService
{
    private const int DefaultFccServerPort = 8082;
    private const int MaximumVersionTextLength = 4096;
    private const string PowerShellVersionWrapper =
        "$exe=[Environment]::GetEnvironmentVariable('FCCD_DISCOVERY_EXECUTABLE','Process');" +
        "$arg=[Environment]::GetEnvironmentVariable('FCCD_DISCOVERY_ARGUMENT','Process');" +
        "if([string]::IsNullOrWhiteSpace($exe)){exit 64};& $exe $arg;exit $LASTEXITCODE";

    private static readonly string EncodedPowerShellVersionWrapper =
        Convert.ToBase64String(Encoding.Unicode.GetBytes(PowerShellVersionWrapper));

    private static readonly string[] VersionArguments = ["--version", "version", "-V"];

    private readonly FccEnvironmentDiscoveryOptions _options;

    public FccEnvironmentDiscoveryService(FccEnvironmentDiscoveryOptions? options = null)
    {
        _options = options ?? new FccEnvironmentDiscoveryOptions();
        ValidateOptions(_options);
    }

    public async Task<FccEnvironmentSnapshot> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        var fccClaudePath = ResolveExecutable("fcc-claude", _options.FccClaudeExecutablePath);
        var fccServerPath = ResolveExecutable("fcc-server", _options.FccServerExecutablePath);

        var fccClaude = await ProbeFccClaudeAsync(fccClaudePath, cancellationToken).ConfigureAwait(false);
        var fccServer = new FccExecutableDiscovery(
            "fcc-server",
            fccServerPath,
            VersionText: null,
            ParsedVersion: null,
            ProbeFailure: null);
        var loopbackHealth = await ProbeLoopbackHealthAsync(cancellationToken).ConfigureAwait(false);

        return new FccEnvironmentSnapshot(fccClaude, fccServer, loopbackHealth);
    }

    private async Task<FccExecutableDiscovery> ProbeFccClaudeAsync(
        string? executablePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new FccExecutableDiscovery(
                "fcc-claude",
                ExecutablePath: null,
                VersionText: null,
                ParsedVersion: null,
                ProbeFailure: "fcc-claude was not found on the configured explicit path or PATH.");
        }

        string? lastFailure = null;
        foreach (var argument in VersionArguments)
        {
            var probe = await RunVersionProbeAsync(executablePath, argument, cancellationToken)
                .ConfigureAwait(false);
            if (probe.TimedOut)
            {
                lastFailure = $"Version probe '{argument}' timed out.";
                continue;
            }

            if (probe.ExitCode != 0)
            {
                lastFailure = probe.Failure ??
                    $"Version probe '{argument}' exited with code {probe.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}.";
                continue;
            }

            var versionText = FirstNonEmpty(probe.StandardOutput, probe.StandardError);
            if (string.IsNullOrWhiteSpace(versionText))
            {
                lastFailure = $"Version probe '{argument}' returned no version text.";
                continue;
            }

            var boundedVersionText = Bound(versionText.Trim(), MaximumVersionTextLength);
            var parsedVersion = TryParseVersion(boundedVersionText);
            return new FccExecutableDiscovery(
                "fcc-claude",
                executablePath,
                boundedVersionText,
                parsedVersion,
                parsedVersion is null ? "Version text did not contain a parseable numeric version." : null);
        }

        return new FccExecutableDiscovery(
            "fcc-claude",
            executablePath,
            VersionText: null,
            ParsedVersion: null,
            ProbeFailure: lastFailure ?? "No supported version probe completed successfully.");
    }

    private async Task<ProcessProbeResult> RunVersionProbeAsync(
        string executablePath,
        string argument,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = CreateVersionProbeStartInfo(executablePath, argument),
            EnableRaisingEvents = true
        };

        try
        {
            if (!process.Start())
            {
                return ProcessProbeResult.Failed("Version probe process did not start.");
            }
        }
        catch (Win32Exception exception)
        {
            return ProcessProbeResult.Failed($"Version probe launch failed: {exception.NativeErrorCode}.");
        }
        catch (InvalidOperationException)
        {
            return ProcessProbeResult.Failed("Version probe launch failed because the process state was invalid.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_options.ProcessTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillOwnedProcess(process);
            await WaitForTerminationAfterKillAsync(process).ConfigureAwait(false);
            return ProcessProbeResult.Timeout(
                Bound(await standardOutputTask.ConfigureAwait(false), MaximumVersionTextLength),
                Bound(await standardErrorTask.ConfigureAwait(false), MaximumVersionTextLength));
        }
        catch (OperationCanceledException)
        {
            TryKillOwnedProcess(process);
            await WaitForTerminationAfterKillAsync(process).ConfigureAwait(false);
            throw;
        }

        return new ProcessProbeResult(
            process.ExitCode,
            Bound(await standardOutputTask.ConfigureAwait(false), MaximumVersionTextLength),
            Bound(await standardErrorTask.ConfigureAwait(false), MaximumVersionTextLength),
            TimedOut: false,
            Failure: null);
    }

    private ProcessStartInfo CreateVersionProbeStartInfo(string executablePath, string argument)
    {
        var extension = Path.GetExtension(executablePath);
        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            var powerShellPath = GetWindowsPowerShellPath();
            var startInfo = CreateBaseStartInfo(powerShellPath);
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-EncodedCommand");
            startInfo.ArgumentList.Add(EncodedPowerShellVersionWrapper);
            startInfo.Environment["FCCD_DISCOVERY_EXECUTABLE"] = executablePath;
            startInfo.Environment["FCCD_DISCOVERY_ARGUMENT"] = argument;
            return startInfo;
        }

        var directStartInfo = CreateBaseStartInfo(executablePath);
        directStartInfo.ArgumentList.Add(argument);
        return directStartInfo;
    }

    private static ProcessStartInfo CreateBaseStartInfo(string executablePath) =>
        new()
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

    private async Task<FccLoopbackHealth> ProbeLoopbackHealthAsync(CancellationToken cancellationToken)
    {
        var endpoint = ResolveHealthUri();
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        };
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_options.HealthTimeout);

        try
        {
            using var response = await client.GetAsync(
                    endpoint,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token)
                .ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? new FccLoopbackHealth(endpoint, FccLoopbackHealthState.Healthy, statusCode, Failure: null)
                : new FccLoopbackHealth(
                    endpoint,
                    FccLoopbackHealthState.Unhealthy,
                    statusCode,
                    $"FCC loopback health returned HTTP {statusCode.ToString(CultureInfo.InvariantCulture)}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new FccLoopbackHealth(
                endpoint,
                FccLoopbackHealthState.Unreachable,
                HttpStatusCode: null,
                Failure: "FCC loopback health probe timed out.");
        }
        catch (HttpRequestException)
        {
            return new FccLoopbackHealth(
                endpoint,
                FccLoopbackHealthState.Unreachable,
                HttpStatusCode: null,
                Failure: "FCC loopback health endpoint was unreachable.");
        }
    }

    private string? ResolveExecutable(string logicalName, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var explicitCandidate = NormalizeCandidatePath(explicitPath);
            return File.Exists(explicitCandidate) ? explicitCandidate : null;
        }

        var pathValue = _options.PathValue ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathExtensions = _options.PathExtensions ??
            Environment.GetEnvironmentVariable("PATHEXT") ??
            ".EXE;.CMD;.BAT;.COM";
        var candidateNames = BuildCandidateNames(logicalName, pathExtensions);

        foreach (var rawDirectory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = Environment.ExpandEnvironmentVariables(rawDirectory.Trim().Trim('"'));
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (var candidateName in candidateNames)
            {
                var candidatePath = NormalizeCandidatePath(Path.Combine(directory, candidateName));
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> BuildCandidateNames(string logicalName, string pathExtensions)
    {
        if (Path.HasExtension(logicalName))
        {
            return [logicalName];
        }

        var names = new List<string> { logicalName };
        foreach (var extension in pathExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalizedExtension = extension.StartsWith('.', StringComparison.Ordinal) ? extension : $".{extension}";
            var candidate = logicalName + normalizedExtension;
            if (!names.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(candidate);
            }
        }

        return names.AsReadOnly();
    }

    private Uri ResolveHealthUri()
    {
        if (_options.HealthUri is not null)
        {
            return _options.HealthUri;
        }

        var port = _options.FccServerPort ?? ResolveEnvironmentFccPort() ?? DefaultFccServerPort;
        return new Uri($"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/health", UriKind.Absolute);
    }

    private static int? ResolveEnvironmentFccPort()
    {
        var rawPort = Environment.GetEnvironmentVariable("FCC_PORT");
        return int.TryParse(rawPort, NumberStyles.None, CultureInfo.InvariantCulture, out var port) &&
               port is >= IPEndPoint.MinPort and <= IPEndPoint.MaxPort
            ? port
            : null;
    }

    private static void ValidateOptions(FccEnvironmentDiscoveryOptions options)
    {
        if (options.ProcessTimeout <= TimeSpan.Zero || options.ProcessTimeout > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "ProcessTimeout must be greater than zero and no more than one minute.");
        }

        if (options.HealthTimeout <= TimeSpan.Zero || options.HealthTimeout > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "HealthTimeout must be greater than zero and no more than one minute.");
        }

        if (options.FccServerPort is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "FccServerPort must be between 0 and 65535.");
        }

        if (options.HealthUri is not null)
        {
            if (!options.HealthUri.IsAbsoluteUri ||
                !options.HealthUri.IsLoopback ||
                (!options.HealthUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                 !options.HealthUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    "HealthUri must be an absolute HTTP(S) loopback URI.",
                    nameof(options));
            }
        }
    }

    private static string NormalizeCandidatePath(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));

    private static string GetWindowsPowerShellPath()
    {
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windowsDirectory))
        {
            windowsDirectory = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
        }

        return Path.Combine(
            windowsDirectory,
            "System32",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
    }

    private static Version? TryParseVersion(string text)
    {
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = token.Trim('v', 'V', '(', ')', '[', ']', ',', ';');
            if (Version.TryParse(candidate, out var version))
            {
                return version;
            }
        }

        return null;
    }

    private static string FirstNonEmpty(string first, string second) =>
        !string.IsNullOrWhiteSpace(first) ? first : second;

    private static string Bound(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private static void TryKillOwnedProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process already exited between the state check and the kill request.
        }
        catch (Win32Exception)
        {
            // Discovery reports the timeout/cancellation; later runtime supervision owns richer cleanup evidence.
        }
    }

    private static async Task WaitForTerminationAfterKillAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // A process that never started has no termination wait to perform.
        }
    }

    private sealed record ProcessProbeResult(
        int? ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut,
        string? Failure)
    {
        public static ProcessProbeResult Failed(string failure) =>
            new(null, string.Empty, string.Empty, TimedOut: false, failure);

        public static ProcessProbeResult Timeout(string standardOutput, string standardError) =>
            new(null, standardOutput, standardError, TimedOut: true, Failure: null);
    }
}
