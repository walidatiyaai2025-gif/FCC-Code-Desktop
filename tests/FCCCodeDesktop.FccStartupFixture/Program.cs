using System.IO;
using System.Net.Http;
using System.Text.Json;
using FCCCodeDesktop.Fcc;
using FCCCodeDesktop.Runtime;

if (args.Contains("--hold-probe-pipes", StringComparer.Ordinal))
{
    await Task.Delay(TimeSpan.FromSeconds(2));
    return;
}
if (args.Contains("--hold-tool-pipes", StringComparer.Ordinal))
{
    await Task.Delay(TimeSpan.FromSeconds(30));
    return;
}
if (Environment.GetEnvironmentVariable("FCCD_TEST_VERSION_PIPES") == "hold"
    && args.Any(argument => argument is "--version" or "version" or "-V"))
{
    using var descendant = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
        Path.Combine(AppContext.BaseDirectory, "FCCCodeDesktop.FccStartupFixture.exe"))
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true,
        ArgumentList = { "--hold-probe-pipes" }
    });
    return;
}

if (args.Contains("--print", StringComparer.Ordinal)
    && Environment.GetEnvironmentVariable("FCCD_TEST_INTERACTIVE_MODE") is { } fixtureMode)
{
    var initialize = await Console.In.ReadLineAsync();
    using var initialization = JsonDocument.Parse(initialize!);
    Console.WriteLine(JsonSerializer.Serialize(new { type = "control_response", response = new { subtype = "success", request_id = "fcc-init", response = new { } } }));
    var user = await Console.In.ReadLineAsync();
    using var userMessage = JsonDocument.Parse(user!);
    Require(userMessage.RootElement.GetProperty("message").GetProperty("content").GetString() == "fixture prompt", "Prompt was not delivered through stdin.");
    Console.WriteLine("""{"type":"system","subtype":"init","session_id":"fixture-session"}""");
    if (fixtureMode == "silent") { await Task.Delay(Timeout.InfiniteTimeSpan); return; }
    if (fixtureMode == "orphan")
    {
        using var descendant = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            Path.Combine(AppContext.BaseDirectory, "FCCCodeDesktop.FccStartupFixture.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            ArgumentList = { "--hold-tool-pipes" }
        });
        Console.WriteLine(JsonSerializer.Serialize(new { type = "system", subtype = "fixture_child", child_pid = descendant!.Id }));
        return;
    }
    if (fixtureMode == "missing-result") { return; }
    if (fixtureMode == "malformed") { Console.WriteLine("not json"); return; }
    Console.WriteLine("""{"type":"control_request","request_id":"approval-1","request":{"subtype":"can_use_tool","tool_name":"Bash","input":{"command":"fixture-command","api_key":"fixture-sensitive-value"}}}""");
    var reply = await Console.In.ReadLineAsync();
    if (reply is null) { return; }
    using var decision = JsonDocument.Parse(reply);
    var response = decision.RootElement.GetProperty("response");
    Require(response.GetProperty("request_id").GetString() == "approval-1", "Approval identity mismatch.");
    var approved = response.GetProperty("response").GetProperty("behavior").GetString() == "allow";
    Require(approved == (fixtureMode == "allow"), "Approval decision mismatch.");
    if (approved)
    {
        Require(response.GetProperty("response").GetProperty("updatedInput").GetProperty("api_key").GetString() == "fixture-sensitive-value", "Approved input was modified.");
    }
    Console.WriteLine("""{"type":"result","subtype":"success","is_error":false,"session_id":"fixture-session"}""");
    Require(await Console.In.ReadLineAsync() is null, "Runtime did not close input after the terminal result.");
    return;
}

if (args.Contains("--real-startup", StringComparer.Ordinal))
{
    await using var real = new FccServerLifecycle();
    real.StatusChanged += (_, _) => Console.Error.WriteLine($"FCC_STARTUP_STATE: {real.Status.State}");
    using var startupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    var snapshot = await real.EnsureReadyAsync(startupDeadline.Token);
    Console.WriteLine($"REAL_LOCAL_STARTUP: {real.Status.State}; owned={real.Status.OwnsServer}; health={snapshot.LoopbackHealth.HttpStatusCode}");
    Console.Out.Flush();
    if (args.Length >= 3 && args[1] == "--task-repository")
    {
        using var taskTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var selectedModel = args.Length >= 5 && args[3] == "--model" ? args[4] : null;
        var runtime = new FccManagedAgentRuntime(real, () => selectedModel, (permission, _) =>
        {
            using var input = JsonDocument.Parse(permission.InputJson);
            var command = input.RootElement.TryGetProperty("command", out var value) ? value.GetString() : null;
            var approved = permission.ToolName == "Bash" && command == "py -3 -c \"from calculator import add; assert add(2, 3) == 5; print('PASS')\"";
            Console.WriteLine($"FIXTURE_TOOL_APPROVAL: {permission.ToolName}; allowed={approved}");
            return Task.FromResult(approved);
        });
        await using var execution = await runtime.StartAsync(new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(),
            "Read TASKS.md and implement its requested coding task in this repository using file editing tools. Do not merely describe the solution.",
            Path.GetFullPath(args[2])), taskTimeout.Token);
        var eventCount = 0;
        try
        {
            await foreach (var item in execution.Events.WithCancellation(taskTimeout.Token))
            {
                eventCount++;
                Console.WriteLine($"EVENT: {item.Kind}; source={item.SourceType}; text={item.Text}");
            }
        }
        catch (OperationCanceledException)
        {
            await execution.CancelAsync();
            throw;
        }
        var result = await execution.Completion;
        Console.WriteLine($"REAL_REPOSITORY_TASK: state={result.State}; events={eventCount}; failure={result.Failure?.Kind}; reason={result.Failure?.Message}");
        Require(result.State == AgentRuntimeTerminalState.Succeeded, "Real repository coding task did not succeed.");
        return;
    }
    using var help = new System.Diagnostics.Process
    {
        StartInfo = new System.Diagnostics.ProcessStartInfo(snapshot.FccClaude.ExecutablePath!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "--help" }
        }
    };
    help.Start();
    var outputTask = help.StandardOutput.ReadToEndAsync();
    var errorTask = help.StandardError.ReadToEndAsync();
    using var helpTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try { await help.WaitForExitAsync(helpTimeout.Token); }
    catch (OperationCanceledException) { help.Kill(entireProcessTree: true); throw; }
    var output = await outputTask;
    _ = await errorTask;
    foreach (var line in output.Split('\n').Where(line => line.Contains("permission-mode", StringComparison.Ordinal)
        || line.Contains("allowedTools", StringComparison.Ordinal) || line.Contains("system-prompt", StringComparison.Ordinal)))
    {
        Console.WriteLine(line.Trim());
    }
    return;
}

static void Require(bool value, string message)
{
    if (!value) { throw new InvalidOperationException(message); }
}

static async Task RejectAsync(Func<Task> operation)
{
    try { await operation(); }
    catch (InvalidOperationException) { return; }
    throw new InvalidOperationException("Expected startup to be rejected.");
}

static async Task<IReadOnlyList<string>> CatalogAsync(string body, int status = 200)
{
    var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    probe.Start();
    var port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
    probe.Stop();
    using var listener = new System.Net.HttpListener();
    var prefix = $"http://127.0.0.1:{port}/";
    listener.Prefixes.Add(prefix);
    listener.Start();
    var responseTask = Task.Run(async () =>
    {
        var context = await listener.GetContextAsync();
        Require(context.Request.RawUrl == "/v1/models", "Unexpected model discovery path.");
        context.Response.StatusCode = status;
        if (status == 302) { context.Response.RedirectLocation = "https://example.com/"; }
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    });
    try { return await FccModelCatalog.ListAsync(new Uri(prefix + "health")); }
    finally { await responseTask; }
}

var catalog = await CatalogAsync("""{"data":[{"id":"fixture/coding-model"},{"id":"fixture/coding-model"},{"id":""},{"id":42}]}""");
Require(catalog.Count == 1 && catalog[0] == "fixture/coding-model", "Catalog identity validation/deduplication failed.");
await RejectAsync(() => CatalogAsync("""{"data":[]}"""));
await RejectAsync(() => CatalogAsync(new string(' ', 256 * 1024 + 1)));
try { await CatalogAsync("{}", 302); throw new InvalidOperationException("Model redirect accepted."); }
catch (HttpRequestException) { }
try { await FccModelCatalog.ListAsync(new Uri("https://example.com/health")); throw new InvalidOperationException("Remote catalog accepted."); }
catch (ArgumentException) { }

foreach (var mode in new[] { "allow", "deny", "missing-result", "malformed", "cancel", "orphan", "silent" })
{
    var previousMode = Environment.GetEnvironmentVariable("FCCD_TEST_INTERACTIVE_MODE");
    Environment.SetEnvironmentVariable("FCCD_TEST_INTERACTIVE_MODE", mode);
    try
    {
        var permissionReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int? ownedDescendantPid = null;
        var options = new FccStructuredAgentRuntimeOptions
        {
            FirstResponseTimeout = mode == "silent" ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(15),
            PermissionHandler = async (permission, token) =>
            {
                Require(!permission.InputJson.Contains("fixture-sensitive-value", StringComparison.Ordinal), "Approval UI leaked a credential.");
                Require(permission.ToolName == "Bash", "Wrong permission tool identity.");
                permissionReached.TrySetResult();
                if (mode == "cancel") { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                return mode == "allow";
            }
        };
        var runtime = new FccInteractiveAgentRuntime(new FccExecutableDiscovery("fcc-claude",
            Path.Combine(AppContext.BaseDirectory, "FCCCodeDesktop.FccStartupFixture.exe"), null, null, null), options);
        await using var execution = await runtime.StartAsync(new AgentRuntimeRequest(Guid.NewGuid(), Guid.NewGuid(), "fixture prompt", Path.GetTempPath()));
        var drain = Task.Run(async () =>
        {
            await foreach (var item in execution.Events)
            {
                if (mode == "orphan" && item.PayloadJson is not null)
                {
                    using var payload = JsonDocument.Parse(item.PayloadJson);
                    if (payload.RootElement.TryGetProperty("child_pid", out var pid))
                    {
                        ownedDescendantPid = pid.GetInt32();
                        permissionReached.TrySetResult();
                    }
                }
            }
        });
        if (mode is "cancel" or "orphan")
        {
            await permissionReached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (mode == "orphan") { await Task.Delay(250); }
            await execution.CancelAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
        }
        var result = await execution.Completion.WaitAsync(TimeSpan.FromSeconds(15));
        await drain;
        var expected = mode is "cancel" or "orphan" ? AgentRuntimeTerminalState.Cancelled
            : mode is "missing-result" or "malformed" or "silent" ? AgentRuntimeTerminalState.Failed : AgentRuntimeTerminalState.Succeeded;
        Require(result.State == expected, $"Interactive fixture {mode} returned {result.State}.");
        if (mode == "silent")
        {
            Require(result.Failure?.Kind == AgentRuntimeFailureKind.Timeout, "Silent provider was not classified as a timeout.");
            Require(result.Failure?.Message.Contains("another model", StringComparison.Ordinal) == true, "Silent provider failure has no actionable model recovery.");
        }
        if (ownedDescendantPid is { } childPid)
        {
            try
            {
                using var descendant = System.Diagnostics.Process.GetProcessById(childPid);
                Require(descendant.HasExited, "Owned descendant survived cancellation after its launcher exited.");
            }
            catch (ArgumentException) { }
        }
    }
    finally { Environment.SetEnvironmentVariable("FCCD_TEST_INTERACTIVE_MODE", previousMode); }
}

var previousProbeMode = Environment.GetEnvironmentVariable("FCCD_TEST_VERSION_PIPES");
Environment.SetEnvironmentVariable("FCCD_TEST_VERSION_PIPES", "hold");
try
{
    var elapsed = System.Diagnostics.Stopwatch.StartNew();
    var probe = await new FccEnvironmentDiscoveryService(new FccEnvironmentDiscoveryOptions
    {
        FccClaudeExecutablePath = Path.Combine(AppContext.BaseDirectory, "FCCCodeDesktop.FccStartupFixture.exe"),
        PathValue = string.Empty,
        ProcessTimeout = TimeSpan.FromMilliseconds(250),
        HealthTimeout = TimeSpan.FromMilliseconds(50),
        HealthUri = new Uri("http://127.0.0.1:1/health")
    }).DiscoverAsync().WaitAsync(TimeSpan.FromSeconds(5));
    Require(elapsed.Elapsed < TimeSpan.FromSeconds(4) && probe.FccClaude.ProbeFailure?.Contains("timed out", StringComparison.Ordinal) == true,
        "Version discovery waited for inherited pipe EOF beyond its deadline.");
}
finally
{
    Environment.SetEnvironmentVariable("FCCD_TEST_VERSION_PIPES", previousProbeMode);
    await Task.Delay(TimeSpan.FromSeconds(2));
}

// An already running service belongs to its original owner, including after application exit.
var existing = new FakeDiscovery { Healthy = true };
var externalSupervisor = new FakeSupervisor();
await using (var lifecycle = new FccServerLifecycle(existing, externalSupervisor))
{
    await lifecycle.EnsureReadyAsync();
    Require(lifecycle.Status.State == FccServerState.Ready && !lifecycle.Status.OwnsServer, "Existing server not reused.");
}
Require(externalSupervisor.Starts == 0 && externalSupervisor.Child.Disposals == 0, "External server was touched.");

var discovery = new FakeDiscovery();
var supervisor = new FakeSupervisor();
await using (var lifecycle = new FccServerLifecycle(discovery, supervisor))
{
    var first = lifecycle.EnsureReadyAsync();
    var second = lifecycle.EnsureReadyAsync();
    await Task.WhenAll(first, second);
    Require(supervisor.Starts == 1 && lifecycle.Status.OwnsServer, "Concurrent startup launched duplicate servers.");
    Require(supervisor.LastRequest!.Arguments.Count == 0, "Invented server arguments.");
    Require(supervisor.LastRequest.WorkingDirectory == Path.GetDirectoryName(discovery.ServerPath), "Wrong server working directory.");
}
Require(supervisor.Child.Disposals == 1, "Owned server was not cleaned up.");

foreach (var missingServer in new[] { false, true })
{
    var missing = new FakeDiscovery { MissingClaude = !missingServer, MissingServer = missingServer };
    var neverLaunch = new FakeSupervisor();
    await using var lifecycle = new FccServerLifecycle(missing, neverLaunch);
    await RejectAsync(() => lifecycle.EnsureReadyAsync());
    Require(lifecycle.Status.State == FccServerState.Failed && neverLaunch.Starts == 0, "Missing executable was not handled.");
}

var unhealthy = new FakeDiscovery { Unhealthy = true };
var noDuplicate = new FakeSupervisor();
await using (var lifecycle = new FccServerLifecycle(unhealthy, noDuplicate))
{
    await RejectAsync(() => lifecycle.EnsureReadyAsync());
    Require(noDuplicate.Starts == 0, "Launched over an unhealthy existing endpoint.");
}

var timeout = new FakeDiscovery { NeverHealthy = true };
var timeoutSupervisor = new FakeSupervisor();
await using (var lifecycle = new FccServerLifecycle(timeout, timeoutSupervisor, TimeSpan.FromMilliseconds(50)))
{
    await RejectAsync(() => lifecycle.EnsureReadyAsync());
    Require(timeoutSupervisor.Child.Disposals == 1 && lifecycle.Status.State == FccServerState.Failed, "Timeout leaked owned server.");
    timeout.NeverHealthy = false;
    await lifecycle.EnsureReadyAsync();
    Require(lifecycle.Status.State == FccServerState.Ready && timeoutSupervisor.Starts == 2, "Retry did not recover.");
}

var cancelledSupervisor = new FakeSupervisor();
await using (var lifecycle = new FccServerLifecycle(new FakeDiscovery { NeverHealthy = true }, cancelledSupervisor))
{
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));
    try { await lifecycle.EnsureReadyAsync(cancellation.Token); }
    catch (OperationCanceledException) { }
    Require(cancelledSupervisor.Child.Disposals == 1 && lifecycle.Status.State == FccServerState.Stopped, "Cancellation leaked server.");
}

var exitedSupervisor = new FakeSupervisor { ExitImmediately = true };
await using (var lifecycle = new FccServerLifecycle(new FakeDiscovery(), exitedSupervisor))
{
    await RejectAsync(() => lifecycle.EnsureReadyAsync());
    Require(exitedSupervisor.Child.Disposals == 1, "Early-exit cleanup missing.");
}

var failedSupervisor = new FakeSupervisor { FailLaunch = true };
await using (var lifecycle = new FccServerLifecycle(new FakeDiscovery(), failedSupervisor))
{
    await RejectAsync(() => lifecycle.EnsureReadyAsync());
    Require(lifecycle.Status.State == FccServerState.Failed, "Launch failure was not surfaced.");
}

var disposed = new FccServerLifecycle(new FakeDiscovery(), new FakeSupervisor());
await disposed.DisposeAsync();
await disposed.DisposeAsync();
try { await disposed.EnsureReadyAsync(); throw new InvalidOperationException("Disposed lifecycle restarted."); }
catch (OperationCanceledException) { }

await DesktopWorkflowFixture.RunAsync();
Console.WriteLine("PASS: server reuse/ownership/startup/retry/cleanup; bounded loopback model catalog; interactive allow/deny/cancel; exact approval identity and input; secret redaction; malformed and missing-terminal-result rejection; desktop approvals and session ownership.");

sealed class FakeDiscovery : IFccEnvironmentDiscovery
{
    public string ServerPath { get; } = Path.Combine(Path.GetTempPath(), "FCC server fixture", "fcc-server.exe");
    public bool Healthy { get; set; }
    public bool Unhealthy { get; init; }
    public bool MissingClaude { get; init; }
    public bool MissingServer { get; init; }
    public bool NeverHealthy { get; set; }
    public Task<FccEnvironmentSnapshot> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult(new FccEnvironmentSnapshot(
        new("fcc-claude", MissingClaude ? null : "fcc-claude.exe", null, null, null),
        new("fcc-server", MissingServer ? null : ServerPath, null, null, null), Health()));
    public Task<FccLoopbackHealth> ProbeLoopbackHealthAsync(CancellationToken cancellationToken = default)
    {
        Healthy = !NeverHealthy;
        return Task.FromResult(Health());
    }
    private FccLoopbackHealth Health() => new(new Uri("http://127.0.0.1:8082/health"),
        Healthy ? FccLoopbackHealthState.Healthy : Unhealthy ? FccLoopbackHealthState.Unhealthy : FccLoopbackHealthState.Unreachable,
        Healthy ? 200 : Unhealthy ? 503 : null, null);
}

sealed class FakeSupervisor : IProcessSupervisor
{
    public int Starts { get; private set; }
    public FakeProcess Child { get; private set; } = new();
    public ProcessLaunchRequest? LastRequest { get; private set; }
    public bool ExitImmediately { get; init; }
    public bool FailLaunch { get; init; }
    public IReadOnlyList<OwnedProcessSnapshot> GetActiveProcesses() => [];
    public Task<ProcessLaunchResult> StartAsync(ProcessLaunchRequest request, CancellationToken cancellationToken = default)
    {
        Starts++;
        LastRequest = request;
        Child = new FakeProcess();
        if (ExitImmediately) { Child.Exit(); }
        return Task.FromResult(FailLaunch ? new ProcessLaunchResult(ProcessLaunchStatus.StartFailed, null)
            : new ProcessLaunchResult(ProcessLaunchStatus.Started, Child));
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class FakeProcess : ISupervisedProcess
{
    private readonly TaskCompletionSource<OwnedProcessExit> _completion = new();
    public Guid OwnershipId { get; } = Guid.NewGuid();
    public int RootProcessId => 1;
    public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
    public IProcessOutput Output => throw new NotSupportedException("Lifecycle must never persist raw server output.");
    public Task<OwnedProcessExit> Completion => _completion.Task;
    public int Disposals { get; private set; }
    public void Exit() => _completion.TrySetResult(new(OwnershipId, RootProcessId, 1, StartedUtc, DateTimeOffset.UtcNow, false));
    public ValueTask TerminateOwnedTreeAsync(CancellationToken cancellationToken = default) { Exit(); return ValueTask.CompletedTask; }
    public ValueTask DisposeAsync() { Disposals++; Exit(); return ValueTask.CompletedTask; }
}
