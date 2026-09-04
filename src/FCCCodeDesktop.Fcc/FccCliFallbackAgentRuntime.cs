using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.Fcc;

/// <summary>
/// Compatibility FCC runtime backed by the target-observed plain
/// <c>fcc-claude --print &lt;prompt&gt;</c> contract.
/// </summary>
public sealed class FccCliFallbackAgentRuntime : IAgentRuntime
{
    private readonly string _executablePath;
    private readonly FccCliFallbackAgentRuntimeOptions _options;

    public FccCliFallbackAgentRuntime(
        FccExecutableDiscovery discovery,
        FccCliFallbackAgentRuntimeOptions? options = null)
        : this(RequireExecutablePath(discovery), ResolveVersion(discovery), options)
    {
    }

    public FccCliFallbackAgentRuntime(
        string executablePath,
        string? version = null,
        FccCliFallbackAgentRuntimeOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("fcc-claude executable path is required.", nameof(executablePath));
        }

        _options = options ?? new FccCliFallbackAgentRuntimeOptions();
        _options.Validate();
        _executablePath = executablePath.Trim();

        Descriptor = new AgentRuntimeDescriptor(
            "fcc.cli-fallback",
            "FCC CLI fallback runtime",
            AgentRuntimeTransport.CliFallback,
            new AgentRuntimeCapabilities(
                supportsStreaming: false,
                supportsSessions: false,
                supportsResume: false,
                supportsCancellation: true,
                supportsToolActivity: false),
            version);
    }

    public AgentRuntimeDescriptor Descriptor { get; }

    public Task<IAgentRuntimeExecution> StartAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ResumeSessionId is not null)
        {
            throw new NotSupportedException(
                "The plain CLI fallback contract does not advertise session resume support.");
        }

        if (!Directory.Exists(request.WorkingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"CLI fallback working directory does not exist: '{request.WorkingDirectory}'.");
        }

        var startInfo = CreateStartInfo(request);
        return Task.FromResult<IAgentRuntimeExecution>(
            new CliFallbackExecution(
                request.TaskId,
                request.RunId,
                startInfo,
                _options.MaximumOutputCharacters));
    }

    private ProcessStartInfo CreateStartInfo(AgentRuntimeRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("--print");
        startInfo.ArgumentList.Add(request.Prompt);
        return startInfo;
    }

    private static string RequireExecutablePath(FccExecutableDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        if (!discovery.IsFound || string.IsNullOrWhiteSpace(discovery.ExecutablePath))
        {
            throw new ArgumentException(
                "The FCC environment snapshot does not contain a discovered fcc-claude executable.",
                nameof(discovery));
        }

        return discovery.ExecutablePath;
    }

    private static string? ResolveVersion(FccExecutableDiscovery discovery) =>
        discovery.ParsedVersion?.ToString()
        ?? (string.IsNullOrWhiteSpace(discovery.VersionText) ? null : discovery.VersionText.Trim());

    private sealed class CliFallbackExecution : IAgentRuntimeExecution
    {
        private static readonly Regex SensitiveTextAssignment = new(
            @"(?im)\b(api[_-]?key|token|password|authorization|secret|credential)\b(\s*[:=]\s*)([^\r\n,;]+)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        private static readonly string[] SensitivePropertyFragments =
        [
            "token",
            "secret",
            "password",
            "authorization",
            "apikey",
            "api_key",
            "credential",
            "cookie"
        ];

        private readonly Channel<AgentRuntimeEvent> _events;
        private readonly TaskCompletionSource<AgentRuntimeResult> _completion;
        private readonly int _maximumOutputCharacters;
        private readonly Process? _process;
        private readonly Task _pumpTask;
        private int _cancellationRequested;

        public CliFallbackExecution(
            Guid taskId,
            Guid runId,
            ProcessStartInfo startInfo,
            int maximumOutputCharacters)
        {
            TaskId = taskId;
            RunId = runId;
            _maximumOutputCharacters = maximumOutputCharacters;
            _events = Channel.CreateUnbounded<AgentRuntimeEvent>(
                new UnboundedChannelOptions
                {
                    AllowSynchronousContinuations = false,
                    SingleReader = false,
                    SingleWriter = true
                });
            _completion = new TaskCompletionSource<AgentRuntimeResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var process = new Process { StartInfo = startInfo };
            try
            {
                if (!process.Start())
                {
                    process.Dispose();
                    CompleteLaunchFailure("fcc-claude fallback process did not start.", "Process.Start");
                    _pumpTask = Task.CompletedTask;
                    return;
                }
            }
            catch (Win32Exception)
            {
                process.Dispose();
                CompleteLaunchFailure("fcc-claude fallback executable could not be started.", "Process.Start");
                _pumpTask = Task.CompletedTask;
                return;
            }
            catch (FileNotFoundException)
            {
                process.Dispose();
                CompleteLaunchFailure("fcc-claude fallback executable could not be found.", "Process.Start");
                _pumpTask = Task.CompletedTask;
                return;
            }

            _process = process;
            _pumpTask = PumpAsync(process);
        }

        public Guid TaskId { get; }

        public Guid RunId { get; }

        public IAsyncEnumerable<AgentRuntimeEvent> Events => _events.Reader.ReadAllAsync();

        public Task<AgentRuntimeResult> Completion => _completion.Task;

        public async ValueTask CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_completion.Task.IsCompleted)
            {
                return;
            }

            Interlocked.Exchange(ref _cancellationRequested, 1);
            var process = _process;
            if (process is not null)
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
                    // The owned process exited between the state check and the kill request.
                }
            }

            await _pumpTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completion.Task.IsCompleted)
            {
                await CancelAsync(CancellationToken.None).ConfigureAwait(false);
            }

            _process?.Dispose();
        }

        private async Task PumpAsync(Process process)
        {
            var stdoutTask = CaptureAsync(process.StandardOutput, _maximumOutputCharacters);
            var stderrTask = CaptureAsync(process.StandardError, _maximumOutputCharacters);

            try
            {
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                var stdout = await stdoutTask.ConfigureAwait(false);
                _ = await stderrTask.ConfigureAwait(false);

                AgentRuntimeResult result;
                if (Volatile.Read(ref _cancellationRequested) != 0)
                {
                    result = new AgentRuntimeResult(
                        TaskId,
                        RunId,
                        AgentRuntimeTerminalState.Cancelled);
                }
                else if (process.ExitCode != 0)
                {
                    result = CreateFailedResult(
                        AgentRuntimeFailureKind.NonZeroExit,
                        $"fcc-claude fallback exited with code {process.ExitCode}.",
                        "process-exit");
                }
                else if (string.IsNullOrWhiteSpace(stdout.Text))
                {
                    result = CreateFailedResult(
                        AgentRuntimeFailureKind.UnknownFailure,
                        "fcc-claude fallback completed without a usable stdout result.",
                        "cli-fallback/stdout");
                }
                else
                {
                    WriteOutputEvent(stdout);
                    result = new AgentRuntimeResult(
                        TaskId,
                        RunId,
                        AgentRuntimeTerminalState.Succeeded);
                }

                _completion.TrySetResult(result);
            }
            catch (IOException)
            {
                CompleteProcessFailure();
            }
            catch (InvalidOperationException)
            {
                CompleteProcessFailure();
            }
            finally
            {
                _events.Writer.TryComplete();
            }
        }

        private void WriteOutputEvent(BoundedTextCapture stdout)
        {
            var trimmed = stdout.Text.TrimEnd('\r', '\n');
            if (!stdout.Truncated && TrySanitizeJson(trimmed, out var sanitizedJson))
            {
                _events.Writer.TryWrite(
                    new AgentRuntimeEvent(
                        0,
                        DateTimeOffset.UtcNow,
                        AgentRuntimeEventKind.Unknown,
                        sourceType: "cli-fallback/json",
                        payloadJson: sanitizedJson));
                return;
            }

            var sanitizedText = SanitizePlainText(trimmed);
            var metadataJson = JsonSerializer.Serialize(
                new
                {
                    fccdTruncated = stdout.Truncated,
                    originalCharacters = stdout.TotalCharacters
                });
            _events.Writer.TryWrite(
                new AgentRuntimeEvent(
                    0,
                    DateTimeOffset.UtcNow,
                    AgentRuntimeEventKind.Unknown,
                    text: sanitizedText,
                    sourceType: "cli-fallback/stdout",
                    payloadJson: metadataJson));
        }

        private static async Task<BoundedTextCapture> CaptureAsync(
            StreamReader reader,
            int maximumCharacters)
        {
            var builder = new StringBuilder(Math.Min(maximumCharacters, 4096));
            var buffer = new char[4096];
            long totalCharacters = 0;

            while (true)
            {
                var count = await reader.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                totalCharacters += count;
                var remaining = maximumCharacters - builder.Length;
                if (remaining > 0)
                {
                    builder.Append(buffer, 0, Math.Min(count, remaining));
                }
            }

            return new BoundedTextCapture(
                builder.ToString(),
                totalCharacters > maximumCharacters,
                totalCharacters);
        }

        private static bool TrySanitizeJson(string text, out string? sanitizedJson)
        {
            sanitizedJson = null;
            try
            {
                using var document = JsonDocument.Parse(text);
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    WriteSanitizedJson(writer, document.RootElement);
                }

                sanitizedJson = Encoding.UTF8.GetString(stream.ToArray());
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static void WriteSanitizedJson(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);
                        if (IsSensitive(property.Name))
                        {
                            writer.WriteStringValue("[REDACTED]");
                        }
                        else
                        {
                            WriteSanitizedJson(writer, property.Value);
                        }
                    }

                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                    {
                        WriteSanitizedJson(writer, item);
                    }

                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Number:
                    writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    writer.WriteNullValue();
                    break;
                default:
                    writer.WriteNullValue();
                    break;
            }
        }

        private static string SanitizePlainText(string text) =>
            SensitiveTextAssignment.Replace(text, "$1$2[REDACTED]");

        private static bool IsSensitive(string propertyName)
        {
            foreach (var fragment in SensitivePropertyFragments)
            {
                if (propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void CompleteLaunchFailure(string message, string source)
        {
            _events.Writer.TryComplete();
            _completion.TrySetResult(
                CreateFailedResult(
                    AgentRuntimeFailureKind.RuntimeNotFound,
                    message,
                    source));
        }

        private void CompleteProcessFailure()
        {
            if (Volatile.Read(ref _cancellationRequested) != 0)
            {
                _completion.TrySetResult(
                    new AgentRuntimeResult(
                        TaskId,
                        RunId,
                        AgentRuntimeTerminalState.Cancelled));
                return;
            }

            _completion.TrySetResult(
                CreateFailedResult(
                    AgentRuntimeFailureKind.ProcessCrash,
                    "fcc-claude fallback process ended unexpectedly.",
                    "process-stream"));
        }

        private AgentRuntimeResult CreateFailedResult(
            AgentRuntimeFailureKind failureKind,
            string message,
            string source) =>
            new(
                TaskId,
                RunId,
                AgentRuntimeTerminalState.Failed,
                failure: new AgentRuntimeFailure(failureKind, message, source: source));

        private sealed record BoundedTextCapture(
            string Text,
            bool Truncated,
            long TotalCharacters);
    }
}
