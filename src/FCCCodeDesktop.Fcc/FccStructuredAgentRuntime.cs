using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.Fcc;

/// <summary>
/// Primary FCC runtime adapter backed by the target-observed non-interactive
/// <c>fcc-claude --print --output-format stream-json --verbose</c> process contract.
/// </summary>
public sealed class FccStructuredAgentRuntime : IAgentRuntime
{
    private readonly string _executablePath;
    private readonly FccStructuredAgentRuntimeOptions _options;

    public FccStructuredAgentRuntime(
        FccExecutableDiscovery discovery,
        FccStructuredAgentRuntimeOptions? options = null)
        : this(RequireExecutablePath(discovery), ResolveVersion(discovery), options)
    {
    }

    public FccStructuredAgentRuntime(
        string executablePath,
        string? version = null,
        FccStructuredAgentRuntimeOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("fcc-claude executable path is required.", nameof(executablePath));
        }

        _options = options ?? new FccStructuredAgentRuntimeOptions();
        _options.Validate();
        _executablePath = executablePath.Trim();

        Descriptor = new AgentRuntimeDescriptor(
            "fcc.structured",
            "FCC structured runtime",
            AgentRuntimeTransport.StructuredProcess,
            new AgentRuntimeCapabilities(
                supportsStreaming: true,
                supportsSessions: true,
                supportsResume: true,
                supportsCancellation: true,
                supportsToolActivity: true),
            version);
    }

    public AgentRuntimeDescriptor Descriptor { get; }

    public Task<IAgentRuntimeExecution> StartAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(request.WorkingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Structured runtime working directory does not exist: '{request.WorkingDirectory}'.");
        }

        var startInfo = CreateStartInfo(request);
        return Task.FromResult<IAgentRuntimeExecution>(
            new StructuredExecution(request.TaskId, request.RunId, startInfo, _options.MaximumPayloadCharacters));
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
        startInfo.ArgumentList.Add("--output-format");
        startInfo.ArgumentList.Add("stream-json");
        startInfo.ArgumentList.Add("--verbose");

        if (request.ResumeSessionId is not null)
        {
            startInfo.ArgumentList.Add("--resume");
            startInfo.ArgumentList.Add(request.ResumeSessionId);
        }

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

    private sealed class StructuredExecution : IAgentRuntimeExecution
    {
        private readonly Channel<AgentRuntimeEvent> _events;
        private readonly TaskCompletionSource<AgentRuntimeResult> _completion;
        private readonly int _maximumPayloadCharacters;
        private readonly Process? _process;
        private readonly Task _pumpTask;
        private int _cancellationRequested;

        public StructuredExecution(
            Guid taskId,
            Guid runId,
            ProcessStartInfo startInfo,
            int maximumPayloadCharacters)
        {
            TaskId = taskId;
            RunId = runId;
            _maximumPayloadCharacters = maximumPayloadCharacters;
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
                    CompleteLaunchFailure("fcc-claude process did not start.", "Process.Start");
                    _pumpTask = Task.CompletedTask;
                    return;
                }
            }
            catch (Win32Exception)
            {
                process.Dispose();
                CompleteLaunchFailure("fcc-claude executable could not be started.", "Process.Start");
                _pumpTask = Task.CompletedTask;
                return;
            }
            catch (FileNotFoundException)
            {
                process.Dispose();
                CompleteLaunchFailure("fcc-claude executable could not be found.", "Process.Start");
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
            var stderrDrainTask = DrainAsync(process.StandardError);
            long sequence = 0;
            var validFrameCount = 0;
            var malformedFrameObserved = false;
            string? sessionId = null;

            try
            {
                while (true)
                {
                    var line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    if (line is null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        using var document = JsonDocument.Parse(line);
                        if (document.RootElement.ValueKind != JsonValueKind.Object)
                        {
                            malformedFrameObserved = true;
                            WriteMalformedEvent(ref sequence);
                            continue;
                        }

                        validFrameCount++;
                        var root = document.RootElement;
                        var sourceType = ResolveSourceType(root);
                        var frameSessionId = TryGetString(root, "session_id") ?? TryGetString(root, "sessionId");
                        if (!string.IsNullOrWhiteSpace(frameSessionId))
                        {
                            sessionId = frameSessionId.Trim();
                        }

                        var normalizedSourceType = IsSessionInitialization(sourceType, root, frameSessionId)
                            ? "system/init"
                            : sourceType;
                        var correlationId = TryGetString(root, "uuid") ?? TryGetString(root, "id");
                        var payloadJson = FccStructuredPayloadSanitizer.Sanitize(
                            root,
                            _maximumPayloadCharacters);
                        var projections = FccRuntimeEventNormalizer.Normalize(
                            root,
                            normalizedSourceType,
                            frameSessionId,
                            correlationId,
                            _maximumPayloadCharacters);
                        // P04-003 invariant retained after P04-005 extraction: init frames normalize to AgentRuntimeEventKind.SessionIdentified.
                        var occurredUtc = DateTimeOffset.UtcNow;
                        var effectiveSessionId = frameSessionId ?? sessionId;

                        foreach (var projection in projections)
                        {
                            _events.Writer.TryWrite(
                                new AgentRuntimeEvent(
                                    sequence++,
                                    occurredUtc,
                                    projection.Kind,
                                    text: projection.Text,
                                    sessionId: effectiveSessionId,
                                    correlationId: projection.CorrelationId,
                                    sourceType: projection.SourceType,
                                    payloadJson: payloadJson));
                        }
                    }
                    catch (JsonException)
                    {
                        malformedFrameObserved = true;
                        WriteMalformedEvent(ref sequence);
                    }
                }

                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                await stderrDrainTask.ConfigureAwait(false);

                AgentRuntimeResult result;
                if (Volatile.Read(ref _cancellationRequested) != 0)
                {
                    result = new AgentRuntimeResult(
                        TaskId,
                        RunId,
                        AgentRuntimeTerminalState.Cancelled,
                        sessionId);
                }
                else if (malformedFrameObserved || validFrameCount == 0)
                {
                    result = CreateFailedResult(
                        AgentRuntimeFailureKind.MalformedStream,
                        "fcc-claude structured output was missing or contained malformed JSON frames.",
                        sessionId,
                        "stream-json");
                }
                else if (process.ExitCode != 0)
                {
                    result = CreateFailedResult(
                        AgentRuntimeFailureKind.NonZeroExit,
                        $"fcc-claude exited with code {process.ExitCode}.",
                        sessionId,
                        "process-exit");
                }
                else
                {
                    result = new AgentRuntimeResult(
                        TaskId,
                        RunId,
                        AgentRuntimeTerminalState.Succeeded,
                        sessionId);
                }

                _completion.TrySetResult(result);
            }
            catch (IOException)
            {
                CompleteProcessFailure(sessionId);
            }
            catch (InvalidOperationException)
            {
                CompleteProcessFailure(sessionId);
            }
            finally
            {
                _events.Writer.TryComplete();
            }
        }

        private void WriteMalformedEvent(ref long sequence)
        {
            _events.Writer.TryWrite(
                new AgentRuntimeEvent(
                    sequence++,
                    DateTimeOffset.UtcNow,
                    AgentRuntimeEventKind.Error,
                    text: "Malformed structured runtime frame.",
                    sourceType: "malformed-json"));
        }

        private void CompleteLaunchFailure(string message, string source)
        {
            _events.Writer.TryComplete();
            _completion.TrySetResult(
                CreateFailedResult(
                    AgentRuntimeFailureKind.RuntimeNotFound,
                    message,
                    sessionId: null,
                    source));
        }

        private void CompleteProcessFailure(string? sessionId)
        {
            var state = Volatile.Read(ref _cancellationRequested) != 0
                ? AgentRuntimeTerminalState.Cancelled
                : AgentRuntimeTerminalState.Failed;

            if (state == AgentRuntimeTerminalState.Cancelled)
            {
                _completion.TrySetResult(
                    new AgentRuntimeResult(TaskId, RunId, state, sessionId));
                return;
            }

            _completion.TrySetResult(
                CreateFailedResult(
                    AgentRuntimeFailureKind.ProcessCrash,
                    "fcc-claude structured process ended unexpectedly.",
                    sessionId,
                    "process-stream"));
        }

        private AgentRuntimeResult CreateFailedResult(
            AgentRuntimeFailureKind failureKind,
            string message,
            string? sessionId,
            string source) =>
            new(
                TaskId,
                RunId,
                AgentRuntimeTerminalState.Failed,
                sessionId,
                new AgentRuntimeFailure(failureKind, message, source: source));

        private static async Task DrainAsync(StreamReader reader)
        {
            var buffer = new char[4096];
            while (await reader.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false) > 0)
            {
            }
        }

        private static string ResolveSourceType(JsonElement root)
        {
            var primary =
                TryGetString(root, "type")
                ?? TryGetString(root, "event")
                ?? TryGetString(root, "kind")
                ?? TryGetString(root, "name");

            if (string.IsNullOrWhiteSpace(primary))
            {
                return "json/unknown";
            }

            var subtype = TryGetString(root, "subtype");
            if (!string.IsNullOrWhiteSpace(subtype) && !primary.Contains('/', StringComparison.Ordinal))
            {
                return $"{primary.Trim()}/{subtype.Trim()}";
            }

            return primary.Trim();
        }

        private static bool IsSessionInitialization(
            string sourceType,
            JsonElement root,
            string? sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            if (string.Equals(sourceType, "system/init", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(TryGetString(root, "type"), "system", StringComparison.OrdinalIgnoreCase)
                && string.Equals(TryGetString(root, "subtype"), "init", StringComparison.OrdinalIgnoreCase);
        }

        private static string? TryGetString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
        }
    }

    private static class FccStructuredPayloadSanitizer
    {
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

        public static string Sanitize(JsonElement root, int maximumPayloadCharacters)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteElement(writer, root);
            }

            var sanitized = Encoding.UTF8.GetString(stream.ToArray());
            if (sanitized.Length <= maximumPayloadCharacters)
            {
                return sanitized;
            }

            var previewLength = Math.Min(
                sanitized.Length,
                Math.Max(64, Math.Min(1024, maximumPayloadCharacters / 8)));
            return JsonSerializer.Serialize(
                new
                {
                    fccdTruncated = true,
                    originalCharacters = sanitized.Length,
                    preview = sanitized[..previewLength]
                });
        }

        private static void WriteElement(Utf8JsonWriter writer, JsonElement element)
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
                            WriteElement(writer, property.Value);
                        }
                    }

                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                    {
                        WriteElement(writer, item);
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
    }
}
