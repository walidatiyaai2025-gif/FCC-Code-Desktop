using System.Reflection;
using FCCCodeDesktop.Tools.Unity;

var operationId = Guid.Parse("6f5a907e-28fa-47b2-a0e6-197d52dd360f");

var warning = new UnityLogEntry(
    sequence: 7,
    UnityLogSeverity.Warning,
    "تحذير Unity — asset import took longer than expected",
    isTruncated: false,
    truncatedUtf8Bytes: 0,
    hadEncodingErrors: false);
var warningEvent = UnityStructuredUiEventProjector.FromLog(operationId, warning);
Expect(warningEvent.OperationId == operationId, "log correlation preserved");
Expect(warningEvent.Sequence == 7, "log sequence preserved");
Expect(warningEvent.Kind == UnityUiEventKind.Diagnostic, "log maps to diagnostic");
Expect(warningEvent.Severity == UnityUiEventSeverity.Warning, "warning severity preserved");
Expect(warningEvent.Code == "unity.log.warning", "warning code stable");
Expect(warningEvent.Message.Contains("تحذير", StringComparison.Ordinal), "Unicode message preserved");

var replacementEvent = new UnityStructuredUiEvent(
    operationId,
    8,
    UnityUiEventKind.Diagnostic,
    UnityUiEventSeverity.Info,
    "unity.test.nul",
    "before\0after");
Expect(!replacementEvent.Message.Contains('\0'), "NUL never reaches UI message");
Expect(replacementEvent.Message.Contains('\uFFFD'), "NUL replaced safely");

var longMessage = new string('x', UnityStructuredUiEvent.MaxMessageCharacters + 25);
var bounded = new UnityStructuredUiEvent(
    operationId,
    9,
    UnityUiEventKind.Diagnostic,
    UnityUiEventSeverity.Info,
    "unity.test.bound",
    longMessage);
Expect(bounded.Message.Length == UnityStructuredUiEvent.MaxMessageCharacters, "message bounded");
Expect(bounded.MessageWasTruncated, "truncation disclosed");

var progress = UnityStructuredUiEventProjector.Progress(operationId, 10, "Asset Import", 42.5, "Importing assets");
Expect(progress.Kind == UnityUiEventKind.Progress, "progress kind");
Expect(progress.Code == "unity.progress.asset-import", "progress stage normalized");
Expect(progress.ProgressPercent == 42.5, "progress value preserved");
ExpectThrows<ArgumentOutOfRangeException>(() =>
    UnityStructuredUiEventProjector.Progress(operationId, 11, "compile", 100.1, "bad"),
    "progress over 100 rejected");
ExpectThrows<ArgumentException>(() =>
    new UnityStructuredUiEvent(operationId, 12, UnityUiEventKind.Diagnostic, UnityUiEventSeverity.Info, "unity bad code", "bad"),
    "unsafe event code rejected");

var automationResult = CreateAutomationResult(
    UnityEditorAutomationValidationStatus.Cancelled,
    UnityEditorAutomationFailureKind.Cancellation,
    operationId,
    "Company.Editor.Run",
    message: "Cancelled by user");
var automationEvent = UnityStructuredUiEventProjector.FromAutomation(automationResult, 13);
Expect(automationEvent.Kind == UnityUiEventKind.Cancelled, "automation cancellation kind");
Expect(automationEvent.Severity == UnityUiEventSeverity.Warning, "automation cancellation severity");
Expect(automationEvent.Code == "unity.automation.cancellation", "automation cancellation code");

var buildResult = CreateBuildResult(
    UnityBuildTargetValidationStatus.Succeeded,
    UnityBuildTargetFailureKind.None,
    UnityBuildTarget.StandaloneWindows64,
    operationId,
    outputBytes: 12345,
    outputSha256: new string('a', 64));
var buildEvent = UnityStructuredUiEventProjector.FromBuild(buildResult, 14);
Expect(buildEvent.Kind == UnityUiEventKind.Completed, "build success completion kind");
Expect(buildEvent.Severity == UnityUiEventSeverity.Info, "build success severity");
Expect(buildEvent.Code.Contains("succeeded", StringComparison.Ordinal), "build success code");
Expect(buildEvent.ArtifactBytes == 12345, "build artifact bytes projected");
Expect(buildEvent.ArtifactSha256 == new string('a', 64), "build artifact hash projected");
Expect(!buildEvent.Message.Contains("C:\\", StringComparison.OrdinalIgnoreCase), "no local path introduced");

Console.WriteLine("P10-011 Unity structured UI event fixture PASS");
return 0;

static UnityEditorAutomationValidationResult CreateAutomationResult(
    UnityEditorAutomationValidationStatus status,
    UnityEditorAutomationFailureKind failureKind,
    Guid operationId,
    string methodName,
    string? message)
{
    var constructor = typeof(UnityEditorAutomationValidationResult)
        .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single();
    return (UnityEditorAutomationValidationResult)constructor.Invoke(new object?[]
    {
        status,
        failureKind,
        operationId,
        methodName,
        null,
        message,
        false,
        null,
        null,
        message ?? "automation summary",
    });
}

static UnityBuildTargetValidationResult CreateBuildResult(
    UnityBuildTargetValidationStatus status,
    UnityBuildTargetFailureKind failureKind,
    UnityBuildTarget target,
    Guid operationId,
    long? outputBytes,
    string? outputSha256)
{
    var constructor = typeof(UnityBuildTargetValidationResult)
        .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single();
    return (UnityBuildTargetValidationResult)constructor.Invoke(new object?[]
    {
        status,
        failureKind,
        target,
        operationId,
        "Company.Editor.Build",
        outputBytes,
        outputSha256,
        "Build completed",
    });
}

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"P10-011 assertion failed: {message}");
    }
}

static void ExpectThrows<TException>(Action action, string message) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"P10-011 assertion failed: {message}");
}
