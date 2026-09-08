using System.Text;
using FCCCodeDesktop.Tools;
using FCCCodeDesktop.Tools.Unity;

return await UnityPlayModeTestFixture.RunAsync().ConfigureAwait(false);

internal static class UnityPlayModeTestFixture
{
    private const string PlayModeOperation = "unity.run-tests.playmode";
    private static readonly ToolIdentity UnityTool = new("unity", "Unity");
    private static readonly ToolProcessOutputSummary EmptyOutput = new(
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

    public static async Task<int> RunAsync()
    {
        var failures = new List<string>();
        var root = Path.Combine(Path.GetTempPath(), "fcc p10-008 playmode fixture عربي " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        void Check(bool condition, string message)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }

        try
        {
            VerifyTypedPlayModeCommand(root, Check);
            await VerifyPassingRunAsync(root, Check).ConfigureAwait(false);
            await VerifyFailedTestsAsync(root, Check).ConfigureAwait(false);
            await VerifyCancellationAsync(root, Check).ConfigureAwait(false);
            await VerifyStaleResultAsync(root, Check).ConfigureAwait(false);
            await VerifyModeIsolationAsync(root, failures).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("P10-008 Unity PlayMode test integration fixture: PASS");
            return 0;
        }

        Console.Error.WriteLine("P10-008 Unity PlayMode test integration fixture: FAIL");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(" - " + failure);
        }

        return 1;
    }

    private static void VerifyTypedPlayModeCommand(string root, Action<bool, string> check)
    {
        var projectRoot = Path.Combine(root, "project عربي");
        var editorPath = Path.Combine(root, "Editor", "Unity.exe");
        var logPath = Path.Combine(root, "logs", "playmode.log");
        var resultsPath = Path.Combine(root, "results", "playmode.xml");
        var project = new ProjectContext(Guid.NewGuid(), projectRoot);
        var request = new UnityCliCommandRequest(
            project,
            editorPath,
            logPath,
            new UnityRunTestsAction(UnityTestPlatform.PlayMode, resultsPath));

        var processRequest = new UnityCliCommandBuilder().Build(UnityTool, request);
        var arguments = processRequest.Invocation.Arguments.ToArray();
        var platformIndex = Array.IndexOf(arguments, "-testPlatform");
        var resultsIndex = Array.IndexOf(arguments, "-testResults");

        check(string.Equals(processRequest.Invocation.Operation, PlayModeOperation, StringComparison.Ordinal),
            "The typed Unity CLI builder must emit the PlayMode operation identity.");
        check(platformIndex >= 0 && platformIndex + 1 < arguments.Length &&
              string.Equals(arguments[platformIndex + 1], "PlayMode", StringComparison.Ordinal),
            "The typed Unity CLI builder must preserve '-testPlatform PlayMode' as discrete argv values.");
        check(resultsIndex >= 0 && resultsIndex + 1 < arguments.Length &&
              string.Equals(arguments[resultsIndex + 1], Path.GetFullPath(resultsPath), StringComparison.Ordinal),
            "The typed Unity CLI builder must preserve the exact fully-qualified PlayMode result path.");
        check(arguments.Contains("-runTests", StringComparer.Ordinal),
            "The typed Unity PlayMode command must include -runTests.");
    }

    private static async Task VerifyPassingRunAsync(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "passing عربي.xml");
        var validator = new UnityPlayModeTestValidator();
        var baseline = await validator.CaptureBaselineAsync(path).ConfigureAwait(false);
        check(!baseline.Existed, "A missing pre-run PlayMode result file must produce a non-existing baseline.");

        await File.WriteAllTextAsync(
            path,
            TestRunXml(
                result: "Passed",
                total: 2,
                passed: 1,
                failed: 0,
                skipped: 1,
                inconclusive: 0,
                body: "<test-suite type=\"Assembly\" name=\"FCC.PlayMode.Tests\" result=\"Passed\"><test-case name=\"Arabic\" fullname=\"FCC.PlayMode.Tests.اختبارΩ\" result=\"Passed\" /><test-case name=\"Skip\" fullname=\"FCC.PlayMode.Tests.Skip\" result=\"Skipped\" /></test-suite>"),
            Encoding.UTF8).ConfigureAwait(false);

        var result = await validator.ValidateAsync(SuccessfulProcess(), path, baseline).ConfigureAwait(false);
        check(result.Status == UnityPlayModeTestValidationStatus.Succeeded &&
              result.FailureKind == UnityPlayModeTestFailureKind.None,
            "A successful PlayMode process plus fresh passing NUnit evidence must succeed.");
        check(result.Total == 2 && result.Passed == 1 && result.Skipped == 1 && result.Failed == 0,
            "PlayMode NUnit counters must be retained exactly.");
        check(result.ResultFileBytes > 0 && result.ResultFileSha256?.Length == 64,
            "A passing PlayMode result must expose bounded artifact byte/hash provenance.");
        check(result.Summary.Contains("PlayMode", StringComparison.Ordinal) &&
              !result.Summary.Contains("EditMode", StringComparison.Ordinal),
            "PlayMode terminal summaries must not expose the shared EditMode implementation detail.");
    }

    private static async Task VerifyFailedTestsAsync(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "failed.xml");
        var validator = new UnityPlayModeTestValidator();
        var baseline = await validator.CaptureBaselineAsync(path).ConfigureAwait(false);
        var message = "فشل PlayMode متوقع Ω";
        await File.WriteAllTextAsync(
            path,
            TestRunXml(
                result: "Failed",
                total: 1,
                passed: 0,
                failed: 1,
                skipped: 0,
                inconclusive: 0,
                body: $"<test-suite type=\"Assembly\" name=\"FCC.PlayMode.Tests\" result=\"Failed\"><test-case name=\"Broken\" fullname=\"FCC.PlayMode.Tests.Broken\" result=\"Failed\"><failure><message><![CDATA[{message}]]></message><stack-trace><![CDATA[at FCC.PlayMode.Tests.Broken()]]></stack-trace></failure></test-case></test-suite>"),
            Encoding.UTF8).ConfigureAwait(false);

        var result = await validator.ValidateAsync(FailedTestProcess(), path, baseline).ConfigureAwait(false);
        check(result.Status == UnityPlayModeTestValidationStatus.Failed &&
              result.FailureKind == UnityPlayModeTestFailureKind.TestsFailed,
            "Failed PlayMode NUnit evidence must remain TestsFailed even when Unity returns a non-zero test-run exit code.");
        check(result.Failed == 1 && result.Failures.Count >= 1,
            "Failed PlayMode test accounting must retain structured failure evidence.");
        check(result.Failures.Any(failure => failure.Message?.Contains(message, StringComparison.Ordinal) == true),
            "Unicode/Arabic PlayMode failure messages must be preserved.");
    }

    private static async Task VerifyCancellationAsync(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "cancelled.xml");
        var validator = new UnityPlayModeTestValidator();
        var baseline = await validator.CaptureBaselineAsync(path).ConfigureAwait(false);
        var result = await validator.ValidateAsync(CancelledProcess(), path, baseline).ConfigureAwait(false);

        check(result.Status == UnityPlayModeTestValidationStatus.Cancelled &&
              result.FailureKind == UnityPlayModeTestFailureKind.Cancellation,
            "PlayMode cancellation must remain a distinct terminal outcome without fabricated result evidence.");
    }

    private static async Task VerifyStaleResultAsync(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "stale.xml");
        await File.WriteAllTextAsync(path, PassingXml(), Encoding.UTF8).ConfigureAwait(false);
        var validator = new UnityPlayModeTestValidator();
        var baseline = await validator.CaptureBaselineAsync(path).ConfigureAwait(false);
        var result = await validator.ValidateAsync(SuccessfulProcess(), path, baseline).ConfigureAwait(false);

        check(result.Status == UnityPlayModeTestValidationStatus.Indeterminate &&
              result.FailureKind == UnityPlayModeTestFailureKind.StaleResultArtifact,
            "Byte-identical pre-run PlayMode evidence must never be reused as a passing result.");
    }

    private static async Task VerifyModeIsolationAsync(string root, List<string> failures)
    {
        var path = Path.Combine(root, "mode-isolation.xml");
        var validator = new UnityPlayModeTestValidator();
        var baseline = await validator.CaptureBaselineAsync(path).ConfigureAwait(false);

        try
        {
            await validator.ValidateAsync(
                SuccessfulProcess(operation: "unity.run-tests.editmode"),
                path,
                baseline).ConfigureAwait(false);
            failures.Add("PlayMode validation must reject EditMode process evidence.");
        }
        catch (ArgumentException)
        {
        }
    }

    private static string PassingXml() =>
        TestRunXml(
            "Passed",
            total: 1,
            passed: 1,
            failed: 0,
            skipped: 0,
            inconclusive: 0,
            "<test-suite type=\"Assembly\" name=\"FCC.PlayMode.Tests\" result=\"Passed\"><test-case name=\"Pass\" fullname=\"FCC.PlayMode.Tests.Pass\" result=\"Passed\" /></test-suite>");

    private static string TestRunXml(
        string result,
        int total,
        int passed,
        int failed,
        int skipped,
        int inconclusive,
        string body) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><test-run result=\"{result}\" total=\"{total}\" passed=\"{passed}\" failed=\"{failed}\" skipped=\"{skipped}\" inconclusive=\"{inconclusive}\">{body}</test-run>";

    private static ToolProcessResult SuccessfulProcess(string operation = PlayModeOperation) =>
        new(
            UnityTool,
            operation,
            ToolResultStatus.Succeeded,
            ToolProcessLaunchStatus.Started,
            exitCode: 0,
            forcedTerminationRequested: false,
            EmptyOutput,
            "Unity PlayMode process completed successfully.");

    private static ToolProcessResult FailedTestProcess() =>
        new(
            UnityTool,
            PlayModeOperation,
            ToolResultStatus.Failed,
            ToolProcessLaunchStatus.Started,
            exitCode: 2,
            forcedTerminationRequested: false,
            EmptyOutput,
            "Unity PlayMode process reported failed tests.");

    private static ToolProcessResult CancelledProcess() =>
        new(
            UnityTool,
            PlayModeOperation,
            ToolResultStatus.Cancelled,
            ToolProcessLaunchStatus.Started,
            exitCode: -1,
            forcedTerminationRequested: true,
            EmptyOutput,
            "Unity PlayMode process was cancelled.");
}
