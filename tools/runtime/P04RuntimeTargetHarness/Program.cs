using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FCCCodeDesktop.Fcc;
using FCCCodeDesktop.Runtime;

internal static class Program
{
    private static readonly TimeSpan ScenarioTimeout = TimeSpan.FromMinutes(2);

    private static async Task<int> Main(string[] args)
    {
        var options = HarnessOptions.Parse(args);
        var capturedAtUtc = DateTimeOffset.UtcNow;
        FccExecutableDiscovery discovery;
        string loopbackHealth;

        if (options.FccClaudePath is not null)
        {
            discovery = new FccExecutableDiscovery(
                "fcc-claude",
                Path.GetFullPath(options.FccClaudePath),
                options.Classification == "SELF_TEST_ONLY" ? "fixture" : null,
                ParsedVersion: null,
                ProbeFailure: null);
            loopbackHealth = "NOT_PROBED_SELF_TEST";
        }
        else
        {
            var snapshot = await new FccEnvironmentDiscoveryService().DiscoverAsync(CancellationToken.None);
            discovery = snapshot.FccClaude;
            loopbackHealth = snapshot.LoopbackHealth.State.ToString().ToUpperInvariant();
        }

        if (!discovery.IsFound || string.IsNullOrWhiteSpace(discovery.ExecutablePath))
        {
            return await WriteBlockedEvidenceAsync(
                options,
                capturedAtUtc,
                loopbackHealth,
                "FCC_CLAUDE_NOT_FOUND");
        }

        var executablePath = discovery.ExecutablePath;
        var version = discovery.ParsedVersion?.ToString()
            ?? (string.IsNullOrWhiteSpace(discovery.VersionText) ? null : discovery.VersionText.Trim());
        var workspace = Path.Combine(
            Path.GetTempPath(),
            "fccd-p04-runtime-target مساحة " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        var scenarios = new List<ScenarioEvidence>();
        string? sessionId = null;
        try
        {
            var success = await CaptureScenarioAsync(
                "structured_success_stream_session",
                async () =>
                {
                    var runtime = new FccStructuredAgentRuntime(executablePath, version);
                    Require(runtime.Descriptor.Transport == AgentRuntimeTransport.StructuredProcess, "structured transport");
                    Require(runtime.Descriptor.Capabilities.SupportsStreaming, "structured streaming capability");
                    Require(runtime.Descriptor.Capabilities.SupportsResume, "structured resume capability");

                    await using var execution = await runtime.StartAsync(
                        new AgentRuntimeRequest(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            "Reply with exactly FCCD_P04_SUCCESS.",
                            workspace),
                        CancellationToken.None);
                    var events = await CollectAsync(execution.Events);
                    var result = await execution.Completion.WaitAsync(ScenarioTimeout);
                    Require(result.State == AgentRuntimeTerminalState.Succeeded, "structured success terminal state");
                    Require(!string.IsNullOrWhiteSpace(result.SessionId), "structured session identity");
                    Require(events.Count > 0, "structured event stream");
                    Require(events.Any(static item => item.Kind == AgentRuntimeEventKind.SessionIdentified), "session event");
                    RequireMonotonic(events);
                    sessionId = result.SessionId;
                    return Observation(
                        result,
                        events,
                        sessionHash: HashSession(result.SessionId));
                });
            scenarios.Add(success);

            scenarios.Add(await CaptureScenarioAsync(
                "structured_resume",
                async () =>
                {
                    Require(!string.IsNullOrWhiteSpace(sessionId), "prior session required for resume");
                    var runtime = new FccStructuredAgentRuntime(executablePath, version);
                    await using var execution = await runtime.StartAsync(
                        new AgentRuntimeRequest(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            "Reply with exactly FCCD_P04_RESUME.",
                            workspace,
                            sessionId),
                        CancellationToken.None);
                    var events = await CollectAsync(execution.Events);
                    var result = await execution.Completion.WaitAsync(ScenarioTimeout);
                    Require(result.State == AgentRuntimeTerminalState.Succeeded, "resume terminal state");
                    Require(!string.IsNullOrWhiteSpace(result.SessionId), "resume session identity");
                    Require(events.Count > 0, "resume event stream");
                    RequireMonotonic(events);
                    return Observation(
                        result,
                        events,
                        sessionHash: HashSession(result.SessionId));
                }));

            scenarios.Add(await CaptureScenarioAsync(
                "structured_invalid_session_failure",
                async () =>
                {
                    var runtime = new FccStructuredAgentRuntime(executablePath, version);
                    await using var execution = await runtime.StartAsync(
                        new AgentRuntimeRequest(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            "Reply with exactly FCCD_P04_INVALID_SESSION.",
                            workspace,
                            "fccd-invalid-session-" + Guid.NewGuid().ToString("N")),
                        CancellationToken.None);
                    var events = await CollectAsync(execution.Events);
                    var result = await execution.Completion.WaitAsync(ScenarioTimeout);
                    Require(result.State == AgentRuntimeTerminalState.Failed, "invalid session must fail");
                    Require(result.Failure is not null, "invalid session failure classification");
                    RequireMonotonic(events);
                    return Observation(result, events);
                }));

            scenarios.Add(await CaptureScenarioAsync(
                "structured_cancellation",
                async () =>
                {
                    var runtime = new FccStructuredAgentRuntime(executablePath, version);
                    await using var execution = await runtime.StartAsync(
                        new AgentRuntimeRequest(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            "Produce 500 numbered one-line observations, starting immediately and without summarizing.",
                            workspace),
                        CancellationToken.None);
                    await using var enumerator = execution.Events.GetAsyncEnumerator(CancellationToken.None);
                    var firstObserved = await enumerator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(45));
                    Require(firstObserved, "cancellation probe first runtime event");
                    await execution.CancelAsync(CancellationToken.None);
                    var result = await execution.Completion.WaitAsync(TimeSpan.FromSeconds(30));
                    Require(result.State == AgentRuntimeTerminalState.Cancelled, "cancelled terminal state");
                    return Observation(result, [enumerator.Current]);
                }));

            scenarios.Add(await CaptureScenarioAsync(
                "fallback_after_structured_failure",
                async () =>
                {
                    var runtime = new FccCliFallbackAgentRuntime(executablePath, version);
                    Require(runtime.Descriptor.Transport == AgentRuntimeTransport.CliFallback, "fallback transport");
                    Require(!runtime.Descriptor.Capabilities.SupportsResume, "fallback resume capability must be false");
                    await using var execution = await runtime.StartAsync(
                        new AgentRuntimeRequest(
                            Guid.NewGuid(),
                            Guid.NewGuid(),
                            "Reply with exactly FCCD_P04_FALLBACK.",
                            workspace),
                        CancellationToken.None);
                    var events = await CollectAsync(execution.Events);
                    var result = await execution.Completion.WaitAsync(ScenarioTimeout);
                    Require(result.State == AgentRuntimeTerminalState.Succeeded, "fallback terminal state");
                    Require(events.Count == 1, "fallback emits one bounded result event");
                    RequireMonotonic(events);
                    return Observation(result, events);
                }));
        }
        finally
        {
            try
            {
                Directory.Delete(workspace, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        var overallStatus = scenarios.All(static item => item.Status == "PASS") ? "PASS" : "FAIL";
        var evidence = new RuntimeContractEvidence(
            SchemaVersion: 1,
            Task: "FCCD-P04-008",
            EvidenceClassification: options.Classification,
            TestedRepoSha: options.ExpectedSha,
            CapturedAtUtc: capturedAtUtc,
            OverallStatus: overallStatus,
            ExecutableName: Path.GetFileName(executablePath),
            RuntimeVersion: version,
            LoopbackHealth: loopbackHealth,
            RateLimitObservation: "NOT_INDUCED",
            Scenarios: scenarios.AsReadOnly());
        await WriteEvidenceAsync(options.EvidencePath, evidence);
        return overallStatus == "PASS" ? 0 : 1;
    }

    private static async Task<int> WriteBlockedEvidenceAsync(
        HarnessOptions options,
        DateTimeOffset capturedAtUtc,
        string loopbackHealth,
        string reason)
    {
        var evidence = new RuntimeContractEvidence(
            1,
            "FCCD-P04-008",
            options.Classification,
            options.ExpectedSha,
            capturedAtUtc,
            "BLOCKED",
            ExecutableName: null,
            RuntimeVersion: null,
            loopbackHealth,
            "NOT_INDUCED",
            [new ScenarioEvidence("environment_discovery", "BLOCKED", reason)]);
        await WriteEvidenceAsync(options.EvidencePath, evidence);
        return 2;
    }

    private static async Task<ScenarioEvidence> CaptureScenarioAsync(
        string name,
        Func<Task<string>> action)
    {
        try
        {
            var observation = await action().WaitAsync(ScenarioTimeout);
            return new ScenarioEvidence(name, "PASS", observation);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return new ScenarioEvidence(name, "FAIL", exception.GetType().Name);
        }
    }

    private static string Observation(
        AgentRuntimeResult result,
        IReadOnlyCollection<AgentRuntimeEvent> events,
        string? sessionHash = null)
    {
        var eventKinds = events
            .GroupBy(static item => item.Kind)
            .OrderBy(static group => group.Key)
            .Select(static group => $"{group.Key}:{group.Count()}");
        return string.Join(
            ";",
            new[]
            {
                $"state={result.State}",
                $"failure={result.Failure?.Kind.ToString() ?? "none"}",
                $"events={events.Count}",
                $"kinds={string.Join(',', eventKinds)}",
                $"sessionHash={sessionHash ?? "none"}"
            });
    }

    private static async Task<List<AgentRuntimeEvent>> CollectAsync(IAsyncEnumerable<AgentRuntimeEvent> source)
    {
        using var timeout = new CancellationTokenSource(ScenarioTimeout);
        var events = new List<AgentRuntimeEvent>();
        await foreach (var item in source.WithCancellation(timeout.Token))
        {
            events.Add(item);
        }

        return events;
    }

    private static void RequireMonotonic(IReadOnlyList<AgentRuntimeEvent> events)
    {
        for (var index = 0; index < events.Count; index++)
        {
            Require(events[index].Sequence == index, $"event sequence {index}");
        }
    }

    private static string? HashSession(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sessionId));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    private static void Require(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"P04 runtime contract assertion failed: {label}.");
        }
    }

    private static async Task WriteEvidenceAsync(string path, RuntimeContractEvidence evidence)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var json = JsonSerializer.Serialize(
            evidence,
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(fullPath, json + Environment.NewLine, Encoding.UTF8);
    }

    private sealed record ScenarioEvidence(string Name, string Status, string Observation);

    private sealed record RuntimeContractEvidence(
        int SchemaVersion,
        string Task,
        string EvidenceClassification,
        string TestedRepoSha,
        DateTimeOffset CapturedAtUtc,
        string OverallStatus,
        string? ExecutableName,
        string? RuntimeVersion,
        string LoopbackHealth,
        string RateLimitObservation,
        IReadOnlyList<ScenarioEvidence> Scenarios);

    private sealed record HarnessOptions(
        string EvidencePath,
        string Classification,
        string ExpectedSha,
        string? FccClaudePath)
    {
        public static HarnessOptions Parse(string[] args)
        {
            string? evidencePath = null;
            string? classification = null;
            string? expectedSha = null;
            string? fccClaudePath = null;

            for (var index = 0; index < args.Length; index++)
            {
                var value = args[index];
                if (value == "--evidence" && index + 1 < args.Length)
                {
                    evidencePath = args[++index];
                }
                else if (value == "--classification" && index + 1 < args.Length)
                {
                    classification = args[++index];
                }
                else if (value == "--expected-sha" && index + 1 < args.Length)
                {
                    expectedSha = args[++index];
                }
                else if (value == "--fcc-claude" && index + 1 < args.Length)
                {
                    fccClaudePath = args[++index];
                }
                else
                {
                    throw new ArgumentException($"Unsupported or incomplete harness argument: '{value}'.");
                }
            }

            if (string.IsNullOrWhiteSpace(evidencePath))
            {
                throw new ArgumentException("--evidence is required.");
            }
            if (classification is not ("REAL_TARGET" or "SELF_TEST_ONLY"))
            {
                throw new ArgumentException("--classification must be REAL_TARGET or SELF_TEST_ONLY.");
            }
            if (string.IsNullOrWhiteSpace(expectedSha))
            {
                throw new ArgumentException("--expected-sha is required.");
            }
            if (!string.IsNullOrWhiteSpace(fccClaudePath) && !File.Exists(fccClaudePath))
            {
                throw new FileNotFoundException("Explicit fcc-claude path does not exist.", fccClaudePath);
            }

            return new HarnessOptions(
                evidencePath,
                classification,
                expectedSha,
                string.IsNullOrWhiteSpace(fccClaudePath) ? null : fccClaudePath);
        }
    }
}
