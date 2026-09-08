using System.Text.Json;
using FCCCodeDesktop.Tools;
using FCCCodeDesktop.Tools.Unity;

var failures = new List<string>();
await RunAsync("Typed Editor automation argv and correlation", TypedInvocationContractAsync, failures);
await RunAsync("Automation-owned switch injection rejected", OwnedSwitchInjectionRejectedAsync, failures);
await RunAsync("Fresh matching structured result succeeds", FreshMatchingResultSucceedsAsync, failures);
await RunAsync("Stale result artifact rejected", StaleResultRejectedAsync, failures);
await RunAsync("Missing result artifact rejected", MissingResultRejectedAsync, failures);
await RunAsync("Malformed and oversized result artifacts rejected", MalformedAndOversizedResultsRejectedAsync, failures);
await RunAsync("Operation contract mismatch rejected", ContractMismatchRejectedAsync, failures);
await RunAsync("Automation-reported failure overrides zero exit", ReportedFailureOverridesZeroExitAsync, failures);
await RunAsync("Process failure overrides reported success", ProcessFailureOverridesReportedSuccessAsync, failures);
await RunAsync("Cancellation remains distinct", CancellationRemainsDistinctAsync, failures);
await RunAsync("Invalid result paths rejected", InvalidResultPathsRejectedAsync, failures);

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Unity Editor automation fixture failed: {failures.Count} case(s).");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine("P10-009 Unity Editor automation fixture: PASS.");
return 0;

static Task TypedInvocationContractAsync()
{
    using var scope = new FixtureScope("Typed Invocation عربي");
    var operationId = Guid.NewGuid();
    var taskId = Guid.NewGuid();
    var extras = new List<string>
    {
        "-fixtureValue",
        "value with spaces",
        "quote-\"literal\"",
        "semi;colon&pipe|literal",
        "عربي-✓",
    };
    var correlation = new ToolProcessCorrelation(taskId: taskId, operationId: operationId);
    var plan = CreatePlan(scope, operationId, extras, correlation);

    var invocation = RequireUnityInvocation(plan.ProcessRequest);
    Equal("unity.execute-method", invocation.Operation, "operation identity");
    Equal(operationId, plan.OperationId, "plan operation identity");
    Equal(MethodName, plan.MethodName, "plan method name");
    Equal(Path.GetFullPath(scope.ResultPath), plan.ResultFilePath, "normalized result path");
    Equal(taskId, plan.ProcessRequest.Correlation?.TaskId, "task correlation");
    Equal(operationId, plan.ProcessRequest.Correlation?.OperationId, "operation correlation");

    var executeIndex = IndexOf(invocation.Arguments, "-executeMethod");
    Equal(MethodName, invocation.Arguments[executeIndex + 1], "executeMethod value");
    Equal(UnityEditorAutomationCommandBuilder.OperationIdSwitch, invocation.Arguments[executeIndex + 2], "operation switch");
    Equal(operationId.ToString("D"), invocation.Arguments[executeIndex + 3], "operation switch value");
    Equal(UnityEditorAutomationCommandBuilder.ResultFileSwitch, invocation.Arguments[executeIndex + 4], "result switch");
    Equal(Path.GetFullPath(scope.ResultPath), invocation.Arguments[executeIndex + 5], "result switch value");
    SequenceEqual(extras, invocation.Arguments.Skip(executeIndex + 6).Take(extras.Count), "caller argv");
    True(invocation.Arguments[^1] == "-quit", "quit must remain the final builder-owned switch");

    extras[1] = "mutated";
    extras.Add("unexpected");
    True(invocation.Arguments.Contains("value with spaces", StringComparer.Ordinal), "snapshotted argument missing");
    True(!invocation.Arguments.Contains("mutated", StringComparer.Ordinal), "mutable caller argument leaked into invocation");

    return Task.CompletedTask;
}

static Task OwnedSwitchInjectionRejectedAsync()
{
    using var scope = new FixtureScope("Owned Switches");
    foreach (var argument in new[]
    {
        UnityEditorAutomationCommandBuilder.OperationIdSwitch,
        "-FCCAUTOMATIONOPERATIONID=00000000-0000-0000-0000-000000000001",
        UnityEditorAutomationCommandBuilder.ResultFileSwitch,
        "-fccautomationresult=C:\\other.json",
    })
    {
        Throws<ArgumentException>(
            () => _ = CreatePlan(scope, Guid.NewGuid(), new[] { argument }),
            $"product-owned switch '{argument}'");
    }

    var operationId = Guid.NewGuid();
    Throws<ArgumentException>(
        () => _ = CreatePlan(
            scope,
            operationId,
            correlation: new ToolProcessCorrelation(operationId: Guid.NewGuid())),
        "mismatched operation correlation");

    return Task.CompletedTask;
}

static async Task FreshMatchingResultSucceedsAsync()
{
    using var scope = new FixtureScope("Fresh Success");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId);
    var validator = new UnityEditorAutomationValidator();
    var baseline = await validator.CaptureBaselineAsync(scope.ResultPath);
    True(!baseline.Existed, "success baseline should start without a result artifact");

    await WriteResultAsync(scope.ResultPath, operationId, MethodName, "succeeded", "automation complete");
    var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);

    Equal(UnityEditorAutomationValidationStatus.Succeeded, result.Status, "validation status");
    Equal(UnityEditorAutomationFailureKind.None, result.FailureKind, "failure kind");
    Equal(UnityEditorAutomationReportedStatus.Succeeded, result.ReportedStatus, "reported status");
    Equal(operationId, result.OperationId, "validated operation id");
    Equal(MethodName, result.MethodName, "validated method name");
    Equal("automation complete", result.Message, "structured message");
    True(result.ResultFileBytes > 0, "result artifact byte length missing");
    True(result.ResultFileSha256?.Length == 64, "result artifact SHA-256 missing");
}

static async Task StaleResultRejectedAsync()
{
    using var scope = new FixtureScope("Stale Result");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId);
    var validator = new UnityEditorAutomationValidator();

    await WriteResultAsync(scope.ResultPath, operationId, MethodName, "succeeded", "old evidence");
    var baseline = await validator.CaptureBaselineAsync(scope.ResultPath);
    True(baseline.Existed, "stale fixture baseline should exist");

    var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
    Equal(UnityEditorAutomationValidationStatus.Indeterminate, result.Status, "stale validation status");
    Equal(UnityEditorAutomationFailureKind.StaleResultArtifact, result.FailureKind, "stale failure kind");
}

static async Task MissingResultRejectedAsync()
{
    using var scope = new FixtureScope("Missing Result");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId);
    var validator = new UnityEditorAutomationValidator();
    var baseline = await validator.CaptureBaselineAsync(scope.ResultPath);

    var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
    Equal(UnityEditorAutomationValidationStatus.Indeterminate, result.Status, "missing result status");
    Equal(UnityEditorAutomationFailureKind.ResultArtifactUnavailable, result.FailureKind, "missing result failure kind");
}

static async Task MalformedAndOversizedResultsRejectedAsync()
{
    using (var scope = new FixtureScope("Malformed Result"))
    {
        var operationId = Guid.NewGuid();
        var plan = CreatePlan(scope, operationId);
        var validator = new UnityEditorAutomationValidator();
        var baseline = await validator.CaptureBaselineAsync(scope.ResultPath);
        await File.WriteAllTextAsync(scope.ResultPath, "{ not-json");

        var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
        Equal(UnityEditorAutomationValidationStatus.Indeterminate, result.Status, "malformed result status");
        Equal(UnityEditorAutomationFailureKind.ResultMalformed, result.FailureKind, "malformed result failure kind");
    }

    using (var scope = new FixtureScope("Oversized Result"))
    {
        var operationId = Guid.NewGuid();
        var plan = CreatePlan(scope, operationId);
        var validator = new UnityEditorAutomationValidator();
        var baseline = await validator.CaptureBaselineAsync(scope.ResultPath);
        await File.WriteAllTextAsync(
            scope.ResultPath,
            new string('x', checked((int)UnityEditorAutomationValidator.MaxResultFileBytes + 1)));

        var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
        Equal(UnityEditorAutomationValidationStatus.Indeterminate, result.Status, "oversized result status");
        Equal(UnityEditorAutomationFailureKind.ResultArtifactTooLarge, result.FailureKind, "oversized result failure kind");
    }
}

static async Task ContractMismatchRejectedAsync()
{
    using var scope = new FixtureScope("Contract Mismatch");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId);
    var validator = new UnityEditorAutomationValidator();
    var baseline = await validator.CaptureBaselineAsync(scope.ResultPath);

    await WriteResultAsync(scope.ResultPath, Guid.NewGuid(), MethodName, "succeeded", "wrong operation");
    var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
    Equal(UnityEditorAutomationValidationStatus.Indeterminate, result.Status, "contract mismatch status");
    Equal(UnityEditorAutomationFailureKind.ContractMismatch, result.FailureKind, "contract mismatch failure kind");
}

static async Task ReportedFailureOverridesZeroExitAsync()
{
    using var scope = new FixtureScope("Reported Failure");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId);
    var validator = new UnityEditorAutomationValidator();
    var baseline = await validator.CaptureBaselineAsync(scope.ResultPath);

    await WriteResultAsync(scope.ResultPath, operationId, MethodName, "failed", "known fixture failure");
    var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
    Equal(UnityEditorAutomationValidationStatus.Failed, result.Status, "reported failure status");
    Equal(UnityEditorAutomationFailureKind.AutomationReportedFailure, result.FailureKind, "reported failure kind");
    Equal(UnityEditorAutomationReportedStatus.Failed, result.ReportedStatus, "reported automation status");
}

static async Task ProcessFailureOverridesReportedSuccessAsync()
{
    using var scope = new FixtureScope("Process Failure");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId);
    var validator = new UnityEditorAutomationValidator();
    var baseline = await validator.CaptureBaselineAsync(scope.ResultPath);

    await WriteResultAsync(scope.ResultPath, operationId, MethodName, "succeeded", "artifact says success");
    var result = await validator.ValidateAsync(FailedProcess(), plan, baseline);
    Equal(UnityEditorAutomationValidationStatus.Failed, result.Status, "process failure status");
    Equal(UnityEditorAutomationFailureKind.ProcessFailure, result.FailureKind, "process failure kind");
    Equal(UnityEditorAutomationReportedStatus.Succeeded, result.ReportedStatus, "parsed artifact status");
}

static async Task CancellationRemainsDistinctAsync()
{
    using var scope = new FixtureScope("Cancelled");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId);
    var validator = new UnityEditorAutomationValidator();
    var baseline = await validator.CaptureBaselineAsync(scope.ResultPath);

    var result = await validator.ValidateAsync(CancelledProcess(), plan, baseline);
    Equal(UnityEditorAutomationValidationStatus.Cancelled, result.Status, "cancelled validation status");
    Equal(UnityEditorAutomationFailureKind.Cancellation, result.FailureKind, "cancelled failure kind");
}

static Task InvalidResultPathsRejectedAsync()
{
    using var scope = new FixtureScope("Path Validation");
    Throws<ArgumentException>(
        () => _ = new UnityEditorAutomationCommandBuilder().Build(
            new ToolIdentity("unity", "Unity Editor"),
            scope.Project,
            scope.EditorPath,
            scope.LogPath,
            MethodName,
            Guid.NewGuid(),
            "result.json"),
        "relative result path");

    Throws<ArgumentException>(
        () => _ = new UnityEditorAutomationCommandBuilder().Build(
            new ToolIdentity("unity", "Unity Editor"),
            scope.Project,
            scope.EditorPath,
            scope.LogPath,
            MethodName,
            Guid.NewGuid(),
            Path.Combine(scope.RootPath, "result.xml")),
        "non-JSON result path");

    Throws<ArgumentException>(
        () => _ = new UnityEditorAutomationCommandBuilder().Build(
            new ToolIdentity("unity", "Unity Editor"),
            scope.Project,
            scope.EditorPath,
            scope.LogPath,
            MethodName,
            Guid.Empty,
            scope.ResultPath),
        "empty operation id");

    return Task.CompletedTask;
}

static UnityEditorAutomationInvocationPlan CreatePlan(
    FixtureScope scope,
    Guid operationId,
    IEnumerable<string>? additionalArguments = null,
    ToolProcessCorrelation? correlation = null) =>
    new UnityEditorAutomationCommandBuilder().Build(
        new ToolIdentity("unity", "Unity Editor"),
        scope.Project,
        scope.EditorPath,
        scope.LogPath,
        MethodName,
        operationId,
        scope.ResultPath,
        additionalArguments,
        environment: new[]
        {
            new KeyValuePair<string, string>("UNITY_AUTOMATION_FIXTURE", "P10-009"),
        },
        correlation);

static async Task WriteResultAsync(
    string path,
    Guid operationId,
    string methodName,
    string status,
    string? message)
{
    var payload = JsonSerializer.Serialize(
        new
        {
            schemaVersion = 1,
            operationId = operationId.ToString("D"),
            methodName,
            status,
            message,
        });
    await File.WriteAllTextAsync(path, payload);
}

static ToolProcessResult SucceededProcess() =>
    new(
        new ToolIdentity("unity", "Unity Editor"),
        "unity.execute-method",
        ToolResultStatus.Succeeded,
        ToolProcessLaunchStatus.Started,
        exitCode: 0,
        forcedTerminationRequested: false,
        CompletedOutput(),
        "fixture process succeeded");

static ToolProcessResult FailedProcess() =>
    new(
        new ToolIdentity("unity", "Unity Editor"),
        "unity.execute-method",
        ToolResultStatus.Failed,
        ToolProcessLaunchStatus.Started,
        exitCode: 1,
        forcedTerminationRequested: false,
        CompletedOutput(),
        "fixture process failed");

static ToolProcessResult CancelledProcess() =>
    new(
        new ToolIdentity("unity", "Unity Editor"),
        "unity.execute-method",
        ToolResultStatus.Cancelled,
        ToolProcessLaunchStatus.Started,
        exitCode: 130,
        forcedTerminationRequested: false,
        CompletedOutput(),
        "fixture process cancelled");

static ToolProcessOutputSummary CompletedOutput() =>
    new(
        AcceptedEntries: 0,
        AcceptedUtf8Bytes: 0,
        RetainedEntries: 0,
        RetainedUtf8Bytes: 0,
        EvictedEntries: 0,
        EvictedUtf8Bytes: 0,
        TruncatedEntries: 0,
        TruncatedCharacters: 0,
        DroppedDeliveryEntries: 0,
        DroppedDeliveryUtf8Bytes: 0,
        IsCompleted: true);

static UnityCliInvocation RequireUnityInvocation(ToolProcessRequest request)
{
    if (request.Invocation is not UnityCliInvocation invocation)
    {
        throw new InvalidOperationException("Expected UnityCliInvocation.");
    }

    return invocation;
}

static int IndexOf(IReadOnlyList<string> values, string expected)
{
    for (var index = 0; index < values.Count; index++)
    {
        if (string.Equals(values[index], expected, StringComparison.Ordinal))
        {
            return index;
        }
    }

    throw new InvalidOperationException($"Expected argv value '{expected}' was not found.");
}

static void SequenceEqual(
    IEnumerable<string> expected,
    IEnumerable<string> actual,
    string description)
{
    var expectedArray = expected.ToArray();
    var actualArray = actual.ToArray();
    if (!expectedArray.SequenceEqual(actualArray, StringComparer.Ordinal))
    {
        throw new InvalidOperationException(
            $"{description}: expected [{string.Join(" | ", expectedArray)}], actual [{string.Join(" | ", actualArray)}].");
    }
}

static void Equal<T>(T expected, T actual, string description)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{description}: expected '{expected}', actual '{actual}'.");
    }
}

static void True(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException(description);
    }
}

static void Throws<TException>(Action action, string description)
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

    throw new InvalidOperationException($"Expected {typeof(TException).Name}: {description}.");
}

static async Task RunAsync(
    string name,
    Func<Task> test,
    ICollection<string> failures)
{
    try
    {
        await test();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL: {name}: {exception.GetType().Name}: {exception.Message}");
    }
}

const string MethodName = "Company.Tools.Automation.Run";

sealed class FixtureScope : IDisposable
{
    public FixtureScope(string suffix)
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"FCC P10-009 {suffix} {Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
        Project = new ProjectContext(Guid.NewGuid(), RootPath);
        EditorPath = Path.Combine(Path.GetTempPath(), "Unity Hub", "6000.5.8f1", "Editor", "Unity.exe");
        LogPath = Path.Combine(RootPath, "Logs", "editor automation.log");
        ResultPath = Path.Combine(RootPath, "Artifacts", "automation result.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath)!);
    }

    public string RootPath { get; }

    public ProjectContext Project { get; }

    public string EditorPath { get; }

    public string LogPath { get; }

    public string ResultPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
