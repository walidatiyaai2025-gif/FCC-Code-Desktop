using System.Text.Json;
using FCCCodeDesktop.Tools;
using FCCCodeDesktop.Tools.Unity;

const string MethodName = "Company.Tools.Build.Run";

var failures = new List<string>();
await RunAsync("Typed build argv and correlation", TypedBuildInvocationAsync, failures);
await RunAsync("Build-owned switch injection rejected", OwnedSwitchInjectionRejectedAsync, failures);
await RunAsync("Target artifact shape is enforced", TargetArtifactShapeEnforcedAsync, failures);
await RunAsync("Fresh Windows build output succeeds", FreshWindowsBuildSucceedsAsync, failures);
await RunAsync("Missing and empty output fail closed", MissingAndEmptyOutputRejectedAsync, failures);
await RunAsync("Stale file output rejected", StaleFileOutputRejectedAsync, failures);
await RunAsync("Pre-existing directory output remains indeterminate", PreExistingDirectoryRejectedAsync, failures);
await RunAsync("Build-reported failure overrides artifact", ReportedFailureOverridesArtifactAsync, failures);
await RunAsync("Process failure overrides artifact", ProcessFailureOverridesArtifactAsync, failures);
await RunAsync("Cancellation remains distinct", CancellationRemainsDistinctAsync, failures);

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Unity build target fixture failed: {failures.Count} case(s).");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine("P10-010 Unity build target fixture: PASS.");
return 0;

static Task TypedBuildInvocationAsync()
{
    using var scope = new FixtureScope("Typed Build");
    var operationId = Guid.NewGuid();
    var taskId = Guid.NewGuid();
    var extras = new List<string>
    {
        "-fixtureValue",
        "value with spaces",
        "عربي-✓",
    };
    var plan = CreatePlan(
        scope,
        operationId,
        UnityBuildTarget.StandaloneWindows64,
        scope.WindowsOutputPath,
        ToolArtifactKind.File,
        extras,
        new ToolProcessCorrelation(taskId: taskId, operationId: operationId));
    var invocation = RequireUnityInvocation(plan.ProcessRequest);

    Equal("unity.execute-method", invocation.Operation, "underlying typed operation");
    Equal(operationId, plan.OperationId, "operation id");
    Equal(MethodName, plan.MethodName, "method name");
    Equal(UnityBuildTarget.StandaloneWindows64, plan.Target, "target");
    Equal(Path.GetFullPath(scope.WindowsOutputPath), plan.OutputArtifactPath, "output path");
    Equal(ToolArtifactKind.File, plan.OutputArtifactKind, "output kind");
    Equal(taskId, plan.ProcessRequest.Correlation?.TaskId, "task correlation");
    Equal(operationId, plan.ProcessRequest.Correlation?.OperationId, "operation correlation");

    var executeIndex = IndexOf(invocation.Arguments, "-executeMethod");
    Equal(MethodName, invocation.Arguments[executeIndex + 1], "executeMethod value");
    Equal(UnityEditorAutomationCommandBuilder.OperationIdSwitch, invocation.Arguments[executeIndex + 2], "operation switch");
    Equal(operationId.ToString("D"), invocation.Arguments[executeIndex + 3], "operation value");
    Equal(UnityEditorAutomationCommandBuilder.ResultFileSwitch, invocation.Arguments[executeIndex + 4], "result switch");
    Equal(Path.GetFullPath(scope.ResultPath), invocation.Arguments[executeIndex + 5], "result value");
    Equal(UnityBuildTargetCommandBuilder.BuildTargetSwitch, invocation.Arguments[executeIndex + 6], "build target switch");
    Equal("StandaloneWindows64", invocation.Arguments[executeIndex + 7], "build target value");
    Equal(UnityBuildTargetCommandBuilder.BuildOutputSwitch, invocation.Arguments[executeIndex + 8], "build output switch");
    Equal(Path.GetFullPath(scope.WindowsOutputPath), invocation.Arguments[executeIndex + 9], "build output value");
    Equal(UnityBuildTargetCommandBuilder.BuildArtifactKindSwitch, invocation.Arguments[executeIndex + 10], "artifact kind switch");
    Equal("file", invocation.Arguments[executeIndex + 11], "artifact kind value");
    SequenceEqual(extras, invocation.Arguments.Skip(executeIndex + 12).Take(extras.Count), "caller argv");
    Equal("-quit", invocation.Arguments[^1], "quit switch");

    extras[1] = "mutated";
    extras.Add("unexpected");
    True(invocation.Arguments.Contains("value with spaces", StringComparer.Ordinal), "snapshotted argument missing");
    True(!invocation.Arguments.Contains("mutated", StringComparer.Ordinal), "caller mutation leaked into argv");
    return Task.CompletedTask;
}

static Task OwnedSwitchInjectionRejectedAsync()
{
    using var scope = new FixtureScope("Owned Switches");
    foreach (var argument in new[]
    {
        UnityBuildTargetCommandBuilder.BuildTargetSwitch,
        "-BUILDTARGET=Android",
        UnityBuildTargetCommandBuilder.BuildOutputSwitch,
        "-fccbuildoutput=C:\\other.exe",
        UnityBuildTargetCommandBuilder.BuildArtifactKindSwitch,
        "-fccbuildartifactkind=directory",
        UnityEditorAutomationCommandBuilder.OperationIdSwitch,
        UnityEditorAutomationCommandBuilder.ResultFileSwitch,
    })
    {
        Throws<ArgumentException>(
            () => _ = CreatePlan(
                scope,
                Guid.NewGuid(),
                UnityBuildTarget.StandaloneWindows64,
                scope.WindowsOutputPath,
                ToolArtifactKind.File,
                new[] { argument }),
            $"owned switch '{argument}'");
    }

    return Task.CompletedTask;
}

static Task TargetArtifactShapeEnforcedAsync()
{
    using var scope = new FixtureScope("Target Shape");
    Throws<ArgumentException>(
        () => _ = CreatePlan(
            scope,
            Guid.NewGuid(),
            UnityBuildTarget.StandaloneWindows64,
            Path.Combine(scope.OutputRoot, "game.bin"),
            ToolArtifactKind.File),
        "Windows output extension");
    Throws<ArgumentException>(
        () => _ = CreatePlan(
            scope,
            Guid.NewGuid(),
            UnityBuildTarget.WebGL,
            Path.Combine(scope.OutputRoot, "WebGL"),
            ToolArtifactKind.File),
        "WebGL directory kind");
    Throws<ArgumentException>(
        () => _ = CreatePlan(
            scope,
            Guid.NewGuid(),
            UnityBuildTarget.Android,
            Path.Combine(scope.OutputRoot, "game.zip"),
            ToolArtifactKind.File),
        "Android extension");
    return Task.CompletedTask;
}

static async Task FreshWindowsBuildSucceedsAsync()
{
    using var scope = new FixtureScope("Fresh Windows");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(
        scope,
        operationId,
        UnityBuildTarget.StandaloneWindows64,
        scope.WindowsOutputPath,
        ToolArtifactKind.File);
    var validator = new UnityBuildTargetValidator();
    var baseline = await validator.CaptureBaselineAsync(plan);
    True(!baseline.OutputExisted, "output should not exist before fresh build");

    await WriteAutomationResultAsync(scope.ResultPath, operationId, "succeeded", "build completed");
    Directory.CreateDirectory(scope.OutputRoot);
    await File.WriteAllBytesAsync(scope.WindowsOutputPath, new byte[] { 0x4d, 0x5a, 1, 2, 3, 4 });

    var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
    Equal(UnityBuildTargetValidationStatus.Succeeded, result.Status, "validation status");
    Equal(UnityBuildTargetFailureKind.None, result.FailureKind, "failure kind");
    Equal(UnityBuildTarget.StandaloneWindows64, result.Target, "validated target");
    True(result.OutputBytes > 0, "output byte count missing");
    True(result.OutputSha256?.Length == 64, "output SHA-256 missing");
}

static async Task MissingAndEmptyOutputRejectedAsync()
{
    using (var scope = new FixtureScope("Missing Output"))
    {
        var operationId = Guid.NewGuid();
        var plan = CreatePlan(scope, operationId, UnityBuildTarget.StandaloneWindows64, scope.WindowsOutputPath, ToolArtifactKind.File);
        var validator = new UnityBuildTargetValidator();
        var baseline = await validator.CaptureBaselineAsync(plan);
        await WriteAutomationResultAsync(scope.ResultPath, operationId, "succeeded", "claims success");
        var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
        Equal(UnityBuildTargetValidationStatus.Failed, result.Status, "missing status");
        Equal(UnityBuildTargetFailureKind.OutputArtifactUnavailable, result.FailureKind, "missing kind");
    }

    using (var scope = new FixtureScope("Empty Output"))
    {
        var operationId = Guid.NewGuid();
        var plan = CreatePlan(scope, operationId, UnityBuildTarget.StandaloneWindows64, scope.WindowsOutputPath, ToolArtifactKind.File);
        var validator = new UnityBuildTargetValidator();
        var baseline = await validator.CaptureBaselineAsync(plan);
        await WriteAutomationResultAsync(scope.ResultPath, operationId, "succeeded", "claims success");
        Directory.CreateDirectory(scope.OutputRoot);
        await File.WriteAllBytesAsync(scope.WindowsOutputPath, Array.Empty<byte>());
        var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
        Equal(UnityBuildTargetValidationStatus.Failed, result.Status, "empty status");
        Equal(UnityBuildTargetFailureKind.OutputArtifactUnavailable, result.FailureKind, "empty kind");
    }
}

static async Task StaleFileOutputRejectedAsync()
{
    using var scope = new FixtureScope("Stale File");
    Directory.CreateDirectory(scope.OutputRoot);
    await File.WriteAllBytesAsync(scope.WindowsOutputPath, new byte[] { 1, 2, 3, 4 });
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId, UnityBuildTarget.StandaloneWindows64, scope.WindowsOutputPath, ToolArtifactKind.File);
    var validator = new UnityBuildTargetValidator();
    var baseline = await validator.CaptureBaselineAsync(plan);
    True(baseline.OutputExisted, "stale baseline should observe output");
    await WriteAutomationResultAsync(scope.ResultPath, operationId, "succeeded", "claims new build");

    var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
    Equal(UnityBuildTargetValidationStatus.Indeterminate, result.Status, "stale status");
    Equal(UnityBuildTargetFailureKind.StaleOutputArtifact, result.FailureKind, "stale kind");
}

static async Task PreExistingDirectoryRejectedAsync()
{
    using var scope = new FixtureScope("Directory Freshness");
    var output = Path.Combine(scope.OutputRoot, "WebGL");
    Directory.CreateDirectory(output);
    await File.WriteAllTextAsync(Path.Combine(output, "old.txt"), "old");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId, UnityBuildTarget.WebGL, output, ToolArtifactKind.Directory);
    var validator = new UnityBuildTargetValidator();
    var baseline = await validator.CaptureBaselineAsync(plan);
    True(baseline.OutputExisted, "directory baseline should observe output");
    await WriteAutomationResultAsync(scope.ResultPath, operationId, "succeeded", "claims new build");
    await File.WriteAllTextAsync(Path.Combine(output, "index.html"), "new");

    var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
    Equal(UnityBuildTargetValidationStatus.Indeterminate, result.Status, "directory freshness status");
    Equal(UnityBuildTargetFailureKind.StaleOutputArtifact, result.FailureKind, "directory freshness kind");
}

static async Task ReportedFailureOverridesArtifactAsync()
{
    using var scope = new FixtureScope("Reported Failure");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId, UnityBuildTarget.StandaloneWindows64, scope.WindowsOutputPath, ToolArtifactKind.File);
    var validator = new UnityBuildTargetValidator();
    var baseline = await validator.CaptureBaselineAsync(plan);
    await WriteAutomationResultAsync(scope.ResultPath, operationId, "failed", "BuildPipeline reported failure");
    Directory.CreateDirectory(scope.OutputRoot);
    await File.WriteAllBytesAsync(scope.WindowsOutputPath, new byte[] { 1, 2, 3 });

    var result = await validator.ValidateAsync(SucceededProcess(), plan, baseline);
    Equal(UnityBuildTargetValidationStatus.Failed, result.Status, "reported failure status");
    Equal(UnityBuildTargetFailureKind.ProcessOrBuildResultFailure, result.FailureKind, "reported failure kind");
}

static async Task ProcessFailureOverridesArtifactAsync()
{
    using var scope = new FixtureScope("Process Failure");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId, UnityBuildTarget.StandaloneWindows64, scope.WindowsOutputPath, ToolArtifactKind.File);
    var validator = new UnityBuildTargetValidator();
    var baseline = await validator.CaptureBaselineAsync(plan);
    await WriteAutomationResultAsync(scope.ResultPath, operationId, "succeeded", "artifact says success");
    Directory.CreateDirectory(scope.OutputRoot);
    await File.WriteAllBytesAsync(scope.WindowsOutputPath, new byte[] { 1, 2, 3 });

    var result = await validator.ValidateAsync(FailedProcess(), plan, baseline);
    Equal(UnityBuildTargetValidationStatus.Failed, result.Status, "process failure status");
    Equal(UnityBuildTargetFailureKind.ProcessOrBuildResultFailure, result.FailureKind, "process failure kind");
}

static async Task CancellationRemainsDistinctAsync()
{
    using var scope = new FixtureScope("Cancelled");
    var operationId = Guid.NewGuid();
    var plan = CreatePlan(scope, operationId, UnityBuildTarget.StandaloneWindows64, scope.WindowsOutputPath, ToolArtifactKind.File);
    var validator = new UnityBuildTargetValidator();
    var baseline = await validator.CaptureBaselineAsync(plan);
    var result = await validator.ValidateAsync(CancelledProcess(), plan, baseline);
    Equal(UnityBuildTargetValidationStatus.Cancelled, result.Status, "cancelled status");
    Equal(UnityBuildTargetFailureKind.Cancellation, result.FailureKind, "cancelled kind");
}

static UnityBuildTargetInvocationPlan CreatePlan(
    FixtureScope scope,
    Guid operationId,
    UnityBuildTarget target,
    string outputPath,
    ToolArtifactKind outputKind,
    IEnumerable<string>? additionalArguments = null,
    ToolProcessCorrelation? correlation = null) =>
    new UnityBuildTargetCommandBuilder().Build(
        new ToolIdentity("unity", "Unity Editor"),
        scope.Project,
        scope.EditorPath,
        scope.LogPath,
        MethodName,
        operationId,
        scope.ResultPath,
        target,
        outputPath,
        outputKind,
        additionalArguments,
        environment: new[] { new KeyValuePair<string, string>("UNITY_BUILD_FIXTURE", "P10-010") },
        correlation);

static async Task WriteAutomationResultAsync(string path, Guid operationId, string status, string? message)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var payload = JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        operationId = operationId.ToString("D"),
        methodName = MethodName,
        status,
        message,
    });
    await File.WriteAllTextAsync(path, payload);
}

static ToolProcessResult SucceededProcess() => CreateProcess(ToolResultStatus.Succeeded, 0, "fixture build process succeeded");
static ToolProcessResult FailedProcess() => CreateProcess(ToolResultStatus.Failed, 1, "fixture build process failed");
static ToolProcessResult CancelledProcess() => CreateProcess(ToolResultStatus.Cancelled, 130, "fixture build process cancelled");

static ToolProcessResult CreateProcess(ToolResultStatus status, int exitCode, string summary) =>
    new(
        new ToolIdentity("unity", "Unity Editor"),
        "unity.execute-method",
        status,
        ToolProcessLaunchStatus.Started,
        exitCode,
        forcedTerminationRequested: false,
        new ToolProcessOutputSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, IsCompleted: true),
        summary);

static UnityCliInvocation RequireUnityInvocation(ToolProcessRequest request) =>
    request.Invocation as UnityCliInvocation ?? throw new InvalidOperationException("Expected UnityCliInvocation.");

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

static void SequenceEqual(IEnumerable<string> expected, IEnumerable<string> actual, string description)
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

static async Task RunAsync(string name, Func<Task> test, ICollection<string> failures)
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

sealed class FixtureScope : IDisposable
{
    public FixtureScope(string suffix)
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"FCC P10-010 {suffix} {Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
        Project = new ProjectContext(Guid.NewGuid(), RootPath);
        EditorPath = Path.Combine(Path.GetTempPath(), "Unity Hub", "6000.5.8f1", "Editor", "Unity.exe");
        LogPath = Path.Combine(RootPath, "Logs", "build.log");
        ResultPath = Path.Combine(RootPath, "Artifacts", "build result.json");
        OutputRoot = Path.Combine(RootPath, "Builds");
        WindowsOutputPath = Path.Combine(OutputRoot, "FCC Fixture.exe");
    }

    public string RootPath { get; }
    public ProjectContext Project { get; }
    public string EditorPath { get; }
    public string LogPath { get; }
    public string ResultPath { get; }
    public string OutputRoot { get; }
    public string WindowsOutputPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
