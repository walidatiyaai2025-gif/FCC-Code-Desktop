using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using FCCCodeDesktop.Runtime;
using FccStructuredPayloadSanitizer = FCCCodeDesktop.Fcc.FccStructuredAgentRuntime.FccStructuredPayloadSanitizer;

namespace FCCCodeDesktop.Fcc;

public sealed record FccToolPermissionRequest(string ToolName, string InputJson, string WorkingDirectory);

/// <summary>Bidirectional CLI transport. Each approval authorizes only the exact requested tool input.</summary>
public sealed class FccInteractiveAgentRuntime(FccExecutableDiscovery discovery, FccStructuredAgentRuntimeOptions options) : IAgentRuntime
{
    private readonly FccExecutableDiscovery _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
    private readonly FccStructuredAgentRuntimeOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    public AgentRuntimeDescriptor Descriptor { get; } = new("fcc.interactive", "FCC coding agent", AgentRuntimeTransport.StructuredProcess,
        new AgentRuntimeCapabilities(true, true, true, true, true));

    public Task<IAgentRuntimeExecution> StartAsync(AgentRuntimeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _options.Validate();
        if (!Directory.Exists(request.WorkingDirectory)) { throw new DirectoryNotFoundException("The selected repository is unavailable."); }
        if (!_discovery.IsFound) { throw new InvalidOperationException("fcc-claude was not found."); }
        return Task.FromResult<IAgentRuntimeExecution>(new Execution(_discovery.ExecutablePath!, request, _options));
    }

    private sealed class Execution : IAgentRuntimeExecution
    {
        private readonly Process _process;
        private readonly OwnedProcessTreeLease? _tree;
        private readonly FccStructuredAgentRuntimeOptions _options;
        private readonly AgentRuntimeRequest _request;
        private readonly Channel<AgentRuntimeEvent> _events = Channel.CreateBounded<AgentRuntimeEvent>(512);
        private readonly TaskCompletionSource<AgentRuntimeResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _pump;
        private int _cancelled;
        private int _disposed;

        public Execution(string executable, AgentRuntimeRequest request, FccStructuredAgentRuntimeOptions options)
        {
            _request = request;
            _options = options;
            var start = new ProcessStartInfo(executable)
            {
                WorkingDirectory = request.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (var argument in new[] { "--print", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose",
                         "--permission-prompt-tool", "stdio", "--permission-mode", options.AcceptFileEdits ? "acceptEdits" : "manual" })
            { start.ArgumentList.Add(argument); }
            if (options.Model is not null) { start.ArgumentList.Add("--model"); start.ArgumentList.Add(options.Model); }
            if (options.AppendSystemPrompt is not null) { start.ArgumentList.Add("--append-system-prompt"); start.ArgumentList.Add(options.AppendSystemPrompt); }
            if (request.ResumeSessionId is not null) { start.ArgumentList.Add("--resume=" + request.ResumeSessionId); }
            _process = new Process { StartInfo = start };
            try
            {
                if (!_process.Start()) { throw new InvalidOperationException("FCC coding process could not start."); }
                _tree = new OwnedProcessTreeLease(_process);
                _pump = PumpAsync();
            }
            catch
            {
                KillOwnedProcess();
                _tree?.Dispose();
                _process.Dispose();
                _stop.Dispose();
                throw;
            }
        }

        public Guid TaskId => _request.TaskId;
        public Guid RunId => _request.RunId;
        public IAsyncEnumerable<AgentRuntimeEvent> Events => _events.Reader.ReadAllAsync();
        public Task<AgentRuntimeResult> Completion => _completion.Task;

        public async ValueTask CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Exchange(ref _cancelled, 1);
            await _stop.CancelAsync().ConfigureAwait(false);
            KillOwnedProcess();
            await _pump.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
            try
            {
                if (!_completion.Task.IsCompleted) { await CancelAsync().ConfigureAwait(false); }
                await _pump.ConfigureAwait(false);
            }
            finally
            {
                _tree?.Dispose();
                _stop.Dispose();
                _process.Dispose();
            }
        }

        private async Task WriteAsync(object message, CancellationToken cancellationToken)
        {
            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task DrainErrorsAsync()
        {
            var buffer = new char[4096];
            try
            {
                while (await _process.StandardError.ReadAsync(buffer.AsMemory(), _stop.Token).ConfigureAwait(false) != 0) { }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }

        private async Task PumpAsync()
        {
            var stderr = DrainErrorsAsync();
            long sequence = 0;
            string? session = null;
            var initialized = false;
            var terminalSeen = false;
            var succeeded = false;
            var failureMessage = "FCC did not return a successful terminal result.";
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            try
            {
                await WriteAsync(new { type = "control_request", request_id = "fcc-init", request = new { subtype = "initialize" } }, deadline.Token).ConfigureAwait(false);
                while (await _process.StandardOutput.ReadLineAsync(deadline.Token).ConfigureAwait(false) is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line)) { continue; }
                    if (line.Length > 1024 * 1024) { throw new InvalidOperationException("FCC returned an oversized protocol frame."); }
                    using var json = JsonDocument.Parse(line);
                    var root = json.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) { throw new InvalidOperationException("FCC returned an invalid protocol frame."); }
                    var type = String(root, "type") ?? "json/unknown";
                    if (type == "control_response")
                    {
                        if (!initialized && root.TryGetProperty("response", out var response) && String(response, "request_id") == "fcc-init")
                        {
                            if (String(response, "subtype") != "success") { throw new InvalidOperationException("FCC rejected interactive initialization."); }
                            initialized = true;
                            deadline.CancelAfter(_options.FirstResponseTimeout);
                            await WriteAsync(new { type = "user", message = new { role = "user", content = _request.Prompt } }, deadline.Token).ConfigureAwait(false);
                        }
                        continue;
                    }
                    if (type == "control_request")
                    {
                        if (initialized && root.TryGetProperty("request", out var controlRequest)
                            && String(controlRequest, "subtype") == "can_use_tool")
                        { deadline.CancelAfter(Timeout.InfiniteTimeSpan); }
                        await AnswerPermissionAsync(root, deadline.Token).ConfigureAwait(false);
                        continue;
                    }
                    if (type == "control_cancel_request") { continue; }
                    if (initialized && type is "assistant" or "user" or "result")
                    {
                        deadline.CancelAfter(Timeout.InfiniteTimeSpan);
                    }
                    session = String(root, "session_id") ?? session;
                    var source = type == "system" && String(root, "subtype") == "init" ? "system/init" : type;
                    var payload = FccStructuredPayloadSanitizer.Sanitize(root, _options.MaximumPayloadCharacters);
                    foreach (var projection in FccRuntimeEventNormalizer.Normalize(root, source, session, String(root, "uuid"), _options.MaximumPayloadCharacters))
                    {
                        await _events.Writer.WriteAsync(new AgentRuntimeEvent(sequence++, DateTimeOffset.UtcNow, projection.Kind,
                            projection.Text, session, projection.CorrelationId, projection.SourceType, payload), _stop.Token).ConfigureAwait(false);
                    }
                    if (type == "result")
                    {
                        terminalSeen = true;
                        succeeded = String(root, "subtype") == "success" && root.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.False;
                        _process.StandardInput.Close();
                        break;
                    }
                }
                using var exitDeadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                exitDeadline.CancelAfter(TimeSpan.FromSeconds(10));
                await _process.WaitForExitAsync(exitDeadline.Token).ConfigureAwait(false);
                _tree?.Terminate();
                if (_tree is not null) { await _tree.WaitForExitAsync(exitDeadline.Token).ConfigureAwait(false); }
                await _stop.CancelAsync().ConfigureAwait(false);
                await stderr.ConfigureAwait(false);
                if (!initialized) { failureMessage = "FCC did not initialize its interactive protocol. Check the installed Claude Code version."; }
                var success = initialized && terminalSeen && succeeded && _process.ExitCode == 0;
                _completion.TrySetResult(new AgentRuntimeResult(TaskId, RunId,
                    success ? AgentRuntimeTerminalState.Succeeded : AgentRuntimeTerminalState.Failed, session,
                    success ? null : new AgentRuntimeFailure(AgentRuntimeFailureKind.UnknownFailure, failureMessage)));
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or OperationCanceledException or Win32Exception)
            {
                await _stop.CancelAsync().ConfigureAwait(false);
                KillOwnedProcess();
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
                if (_tree is not null) { await _tree.WaitForExitAsync(cleanup.Token).ConfigureAwait(false); }
                await stderr.ConfigureAwait(false);
                var cancelled = Volatile.Read(ref _cancelled) != 0;
                var timedOut = !cancelled && exception is OperationCanceledException;
                _completion.TrySetResult(new AgentRuntimeResult(TaskId, RunId,
                    cancelled ? AgentRuntimeTerminalState.Cancelled : AgentRuntimeTerminalState.Failed, session,
                    cancelled ? null : new AgentRuntimeFailure(timedOut ? AgentRuntimeFailureKind.Timeout : AgentRuntimeFailureKind.UnknownFailure,
                        timedOut && initialized ? "FCC model did not respond before the deadline. Choose another model or retry."
                        : initialized ? "FCC interactive execution failed. Check the server/model and retry." : "FCC interactive startup failed or timed out.")));
            }
            finally
            {
                _completion.TrySetResult(new AgentRuntimeResult(TaskId, RunId, AgentRuntimeTerminalState.Failed, session,
                    new AgentRuntimeFailure(AgentRuntimeFailureKind.ProcessCrash, "FCC execution ended unexpectedly; inspect the runtime and retry.")));
                _events.Writer.TryComplete();
            }
        }

        private async Task AnswerPermissionAsync(JsonElement root, CancellationToken cancellationToken)
        {
            var id = String(root, "request_id") ?? throw new InvalidOperationException("FCC approval request has no identity.");
            if (id.Length > 256 || id.Any(char.IsControl)) { throw new InvalidOperationException("Invalid FCC approval identity."); }
            if (!root.TryGetProperty("request", out var request) || String(request, "subtype") != "can_use_tool"
                || !request.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object)
            {
                await WriteAsync(new { type = "control_response", response = new { subtype = "error", request_id = id, error = "Unsupported FCC control request." } }, cancellationToken).ConfigureAwait(false);
                return;
            }
            var name = String(request, "tool_name") ?? "Unknown tool";
            var reviewable = input.GetRawText().Length <= _options.MaximumPayloadCharacters && name.Length <= 256 && !name.Any(char.IsControl);
            var approved = reviewable && _options.PermissionHandler is not null && await _options.PermissionHandler(
                new FccToolPermissionRequest(name, FccStructuredPayloadSanitizer.Sanitize(input, _options.MaximumPayloadCharacters), _request.WorkingDirectory),
                cancellationToken).ConfigureAwait(false);
            object decision = approved ? new { behavior = "allow", updatedInput = input.Clone() }
                : new { behavior = "deny", message = "The user did not approve this operation." };
            await WriteAsync(new { type = "control_response", response = new { subtype = "success", request_id = id, response = decision } }, cancellationToken).ConfigureAwait(false);
        }

        private void KillOwnedProcess()
        {
            try { if (!_process.HasExited) { _process.Kill(entireProcessTree: true); } }
            catch (InvalidOperationException) { }
            _tree?.Terminate();
        }

        private static string? String(JsonElement value, string name) => value.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }
}
