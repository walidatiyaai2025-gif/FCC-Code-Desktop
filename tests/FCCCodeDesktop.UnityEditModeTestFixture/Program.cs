using System.Text;
using FCCCodeDesktop.Tools;
using FCCCodeDesktop.Tools.Unity;

return await UnityEditModeTestFixture.RunAsync().ConfigureAwait(false);

internal static class UnityEditModeTestFixture
{
    private const string EditModeOperation = "unity.run-tests.editmode";
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
        var root = Path.Combine(Path.GetTempPath(), "fcc p10-007 editmode fixture عربي " + Guid.NewGuid().ToString("N"));
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
            await VerifyPassingRunAsync(root, Check).ConfigureAwait(false);
            await VerifyFailedTestsAsync(root, Check).ConfigureAwait(false);
            await VerifyProcessOutcomesAsync(root, Check).ConfigureAwait(false);
            await VerifyArtifactGuardsAsync(root, failures, Check).ConfigureAwait(false);
            await VerifyMalformedAndNoTestsAsync(root, Check).ConfigureAwait(false);
            await VerifyFailureBoundsAsync(root, Check).ConfigureAwait(false);
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
            Console.WriteLine("P10-007 Unity EditMode test integration fixture: PASS");
            return 0;
        }

        Console.Error.WriteLine("P10-007 Unity EditMode test integration fixture: FAIL");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(" - " + failure);
        }

        return 1;
    }

    private static async Task VerifyPassingRunAsync(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "passing عربي.xml");
        var validator = new UnityEditModeTestValidator();
        var baseline = await validator.CaptureBaselineAsync(path).ConfigureAwait(false);
        check(!baseline.Existed, "A missing pre-run result file must produce a non-existing baseline.");

        await File.WriteAllTextAsync(
            path,
            TestRunXml(
                result: "Passed",
                total: 2,
                passed: 1,
                failed: 0,
                skipped: 1,
                inconclusive: 0,
                body: "<test-suite type=\"Assembly\" name=\"FCC.Tests\" result=\"Passed\"><test-case name=\"Arabic\" fullname=\"FCC.Tests.اختبارΩ\" result=\"Passed\" /><test-case name=\"Skip\" fullname=\"FCC.Tests.Skip\" result=\"Skipped\" /></test-suite>"),
            Encoding.UTF8).ConfigureAwait(false);

        var result = await validator.ValidateAsync(SuccessfulProcess(), path, baseline).ConfigureAwait(false);
        check(result.Status == UnityEditModeTestValidationStatus.Succeeded,
            "A successful process plus fresh passing NUnit EditMode evidence must succeed.");
        check(result.FailureKind == UnityEditModeTestFailureKind.None,
            "A passing EditMode result must not expose a failure kind.");
        check(result.Total == 2 && result.Passed == 1 && result.Skipped == 1 && result.Failed == 0,
            "EditMode NUnit counters must be retained exactly.");
        check(result.ResultFileBytes > 0 && result.ResultFileSha256?.Length == 64,
            "A passing result must expose bounded artifact byte/hash provenance.");
        check(result.Failures.Count == 0 && !result.FailuresTruncated,
            "A passing result must not retain failure details.");
    }

    private static async Task VerifyFailedTestsAsync(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "failed.xml");
        var validator = new UnityEditModeTestValidator();
        var baseline = await validator.CaptureBaselineAsync(path).ConfigureAwait(false);
        var message = "فشل متوقع Ω";
        await File.WriteAllTextAsync(
            path,
            TestRunXml(
                result: "Failed",
                total: 2,
                passed: 1,
                failed: 1,
                skipped: 0,
                inconclusive: 0,
                body: $"<test-suite type=\"Assembly\" name=\"FCC.Tests\" result=\"Failed\"><test-case name=\"Broken\" fullname=\"FCC.Tests.Broken\" result=\"Failed\"><failure><message><![CDATA[{message}]]></message><stack-trace><![CDATA[at FCC.Tests.Broken()]]></stack-trace></failure></test-case></test-suite>"),
            Encoding.UTF8).ConfigureAwait(false);

        var result = await validator.ValidateAsync(FailedTestProcess(), path, baseline).ConfigureAwait(false);
        check(result.Status == UnityEditModeTestValidationStatus.Failed &&
              result.FailureKind == UnityEditModeTestFailureKind.TestsFailed,
            "A failed NUnit result must be typed as TestsFailed even when Unity returns a non-zero test-run exit code.");
        check(result.Failed == 1 && result.Failures.Count >= 1,
            "Failed EditMode test accounting must retain structured failure evidence.");
        check(result.Failures.Any(failure => string.Equals(failure.FullName, "FCC.Tests.Broken", StringComparison.Ordinal)),
            "Failed test detail must retain the NUnit full name.");
        check(result.Failures.Any(failure => failure.Message?.Contains(message, StringComparison.Ordinal) == true),
            "Unicode/Arabic NUnit failure messages must be preserved.");
    }

    private static async Task VerifyProcessOutcomesAsync(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "process.xml");
        var validator = new UnityEditModeTestValidator();

        var cancelledBaseline = await validator.CaptureBaselineAsync(path).ConfigureAwait(false);
        var cancelled = await validator.ValidateAsync(CancelledProcess(), path, cancelledBaseline).ConfigureAwait(false);
        check(cancelled.Status == UnityEditModeTestValidationStatus.Cancelled &&
              cancelled.FailureKind == UnityEditModeTestFailureKind.Cancellation,
            "Cancellation must remain distinct and must not require a result artifact.");

        var launchBaseline = await validator.CaptureBaselineAsync(path).ConfigureAwait(false);
        var launchFailed = await validator.ValidateAsync(LaunchFailedProcess(), path, launchBaseline).ConfigureAwait(false);
        check(launchFailed.Status == UnityEditModeTestValidationStatus.Failed &&
              launchFailed.FailureKind == UnityEditModeTestFailureKind.ProcessFailure,
            "A launch failure must fail before result-artifact interpretation.");

        var passedPath = Path.Combine(root, "process-passed.xml");
        var passedBaseline = await validator.CaptureBaselineAsync(passedPath).ConfigureAwait(false);
        await File.WriteAllTextAsync(passedPath, PassingXml(), Encoding.UTF8).ConfigureAwait(false);
        var disagreement = await validator.ValidateAsync(FailedProcess(), passedPath, passedBaseline).ConfigureAwait(false);
        check(disagreement.Status == UnityEditModeTestValidationStatus.Failed &&
              disagreement.FailureKind == UnityEditModeTestFailureKind.ProcessFailure,
            "A passing NUnit artifact must not override a failed controlled Unity process.");
    }

    private static async Task VerifyArtifactGuardsAsync(
        string root,
        List<string> failures,
        Action<bool, string> check)
    {
        var validator = new UnityEditModeTestValidator();
        var missingPath = Path.Combine(root, "missing.xml");
        var missingBaseline = await validator.CaptureBaselineAsync(missingPath).ConfigureAwait(false);
        var missing = await validator.ValidateAsync(SuccessfulProcess(), missingPath, missingBaseline).ConfigureAwait(false);
        check(missing.Status == UnityEditModeTestValidationStatus.Indeterminate &&
              missing.FailureKind == UnityEditModeTestFailureKind.ResultArtifactUnavailable,
            "Process success without a result artifact must remain indeterminate.");

        var stalePath = Path.Combine(root, "stale.xml");
        await File.WriteAllTextAsync(stalePath, PassingXml(), Encoding.UTF8).ConfigureAwait(false);
        var staleBaseline = await validator.CaptureBaselineAsync(stalePath).ConfigureAwait(false);
        var stale = await validator.ValidateAsync(SuccessfulProcess(), stalePath, staleBaseline).ConfigureAwait(false);
        check(stale.Status == UnityEditModeTestValidationStatus.Indeterminate &&
              stale.FailureKind == UnityEditModeTestFailureKind.StaleResultArtifact,
            "Byte-identical pre-run NUnit evidence must never be reused as a new passing result.");

        var emptyPath = Path.Combine(root, "empty.xml");
        var emptyBaseline = await validator.CaptureBaselineAsync(emptyPath).ConfigureAwait(false);
        await File.WriteAllBytesAsync(emptyPath, Array.Empty<byte>()).ConfigureAwait(false);
        var empty = await validator.ValidateAsync(SuccessfulProcess(), emptyPath, emptyBaseline).ConfigureAwait(false);
        check(empty.Status == UnityEditModeTestValidationStatus.Indeterminate &&
              empty.FailureKind == UnityEditModeTestFailureKind.ResultArtifactUnavailable,
            "An empty EditMode results file must not pass.");

        var tooLargePath = Path.Combine(root, "too-large.xml");
        var tooLargeBaseline = await validator.CaptureBaselineAsync(tooLargePath).ConfigureAwait(false);
        await using (var stream = new FileStream(tooLargePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength(UnityEditModeTestValidator.MaxResultFileBytes + 1);
        }
        var tooLarge = await validator.ValidateAsync(SuccessfulProcess(), tooLargePath, tooLargeBaseline).ConfigureAwait(false);
        check(tooLarge.Status == UnityEditModeTestValidationStatus.Indeterminate &&
              tooLarge.FailureKind == UnityEditModeTestFailureKind.ResultArtifactTooLarge,
            "Oversized NUnit artifacts must fail closed before XML parsing.");

        var guardPath = Path.Combine(root, "guard.xml");
        var guardBaseline = await validator.CaptureBaselineAsync(guardPath).ConfigureAwait(false);
        ExpectThrowsAsync<ArgumentException>(
            () => validator.ValidateAsync(
                SuccessfulProcess(operation: "unity.run-tests.playmode"),
                guardPath,
                guardBaseline),
            failures,
            "EditMode validation must reject PlayMode process evidence.");

        var otherPath = Path.Combine(root, "other.xml");
        ExpectThrowsAsync<ArgumentException>(
            () => validator.ValidateAsync(SuccessfulProcess(), otherPath, guardBaseline),
            failures,
            "A result baseline must not be reused for another path.");
    }

    private static async Task VerifyMalformedAndNoTestsAsync(string root, Action<bool, string> check)
    {
        var validator = new UnityEditModeTestValidator();

        var malformedPath = Path.Combine(root, "malformed.xml");
        var malformedBaseline = await validator.CaptureBaselineAsync(malformedPath).ConfigureAwait(false);
        await File.WriteAllTextAsync(malformedPath, "<test-run result=\"Passed\">", Encoding.UTF8).ConfigureAwait(false);
        var malformed = await validator.ValidateAsync(SuccessfulProcess(), malformedPath, malformedBaseline).ConfigureAwait(false);
        check(malformed.Status == UnityEditModeTestValidationStatus.Indeterminate &&
              malformed.FailureKind == UnityEditModeTestFailureKind.ResultMalformed,
            "Malformed NUnit XML must remain indeterminate rather than passing from process exit code.");

        var dtdPath = Path.Combine(root, "dtd.xml");
        var dtdBaseline = await validator.CaptureBaselineAsync(dtdPath).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            dtdPath,
            "<!DOCTYPE test-run [<!ENTITY xxe SYSTEM \"file:///C:/Windows/win.ini\">]><test-run result=\"Passed\" total=\"1\" passed=\"1\" failed=\"0\" skipped=\"0\" inconclusive=\"0\"><test-suite name=\"x\" result=\"Passed\">&xxe;</test-suite></test-run>",
            Encoding.UTF8).ConfigureAwait(false);
        var dtd = await validator.ValidateAsync(SuccessfulProcess(), dtdPath, dtdBaseline).ConfigureAwait(false);
        check(dtd.Status == UnityEditModeTestValidationStatus.Indeterminate &&
              dtd.FailureKind == UnityEditModeTestFailureKind.ResultMalformed,
            "NUnit XML containing a DTD/entity must be rejected by the secure parser.");

        var zeroPath = Path.Combine(root, "zero.xml");
        var zeroBaseline = await validator.CaptureBaselineAsync(zeroPath).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            zeroPath,
            TestRunXml("Passed", 0, 0, 0, 0, 0, string.Empty),
            Encoding.UTF8).ConfigureAwait(false);
        var zero = await validator.ValidateAsync(SuccessfulProcess(), zeroPath, zeroBaseline).ConfigureAwait(false);
        check(zero.Status == UnityEditModeTestValidationStatus.Indeterminate &&
              zero.FailureKind == UnityEditModeTestFailureKind.NoTestsDiscovered,
            "A zero-test EditMode artifact must not count as a green suite.");

        var inconsistentPath = Path.Combine(root, "inconsistent.xml");
        var inconsistentBaseline = await validator.CaptureBaselineAsync(inconsistentPath).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            inconsistentPath,
            TestRunXml("Passed", 2, 1, 0, 0, 0, "<test-case name=\"one\" result=\"Passed\" />"),
            Encoding.UTF8).ConfigureAwait(false);
        var inconsistent = await validator.ValidateAsync(SuccessfulProcess(), inconsistentPath, inconsistentBaseline).ConfigureAwait(false);
        check(inconsistent.Status == UnityEditModeTestValidationStatus.Indeterminate &&
              inconsistent.FailureKind == UnityEditModeTestFailureKind.ResultMalformed,
            "Inconsistent NUnit counters must fail closed as malformed evidence.");
    }

    private static async Task VerifyFailureBoundsAsync(string root, Action<bool, string> check)
    {
        var path = Path.Combine(root, "bounded.xml");
        var validator = new UnityEditModeTestValidator();
        var baseline = await validator.CaptureBaselineAsync(path).ConfigureAwait(false);
        var longMessage = new string('م', 5000);
        var longStack = new string('s', 9000);
        var body = new StringBuilder();
        body.Append("<test-suite type=\"Assembly\" name=\"Many\" result=\"Failed\">");
        for (var index = 0; index < 105; index++)
        {
            body.Append("<test-case name=\"T").Append(index)
                .Append("\" fullname=\"FCC.Tests.T").Append(index)
                .Append("\" result=\"Failed\"><failure><message><![CDATA[")
                .Append(longMessage)
                .Append("]]></message><stack-trace><![CDATA[")
                .Append(longStack)
                .Append("]]></stack-trace></failure></test-case>");
        }
        body.Append("</test-suite>");

        await File.WriteAllTextAsync(
            path,
            TestRunXml("Failed", 105, 0, 105, 0, 0, body.ToString()),
            Encoding.UTF8).ConfigureAwait(false);
        var result = await validator.ValidateAsync(FailedTestProcess(), path, baseline).ConfigureAwait(false);
        check(result.Status == UnityEditModeTestValidationStatus.Failed && result.Failed == 105,
            "The full failed-test count must be preserved when retained details are bounded.");
        check(result.Failures.Count == 100 && result.FailuresTruncated,
            "Retained failed-test detail must be capped at 100 with explicit truncation metadata.");
        check(result.Failures.Any(failure => failure.Message?.Length == 4096 && failure.MessageTruncated),
            "Each retained failure message must be independently bounded without depending on XML node order.");
        check(result.Failures.Any(failure => failure.StackTrace?.Length == 8192 && failure.StackTraceTruncated),
            "Each retained failure stack trace must be independently bounded without depending on XML node order.");
    }

    private static string PassingXml() =>
        TestRunXml(
            result: "Passed",
            total: 1,
            passed: 1,
            failed: 0,
            skipped: 0,
            inconclusive: 0,
            body: "<test-suite type=\"Assembly\" name=\"FCC.Tests\" result=\"Passed\"><test-case name=\"Pass\" fullname=\"FCC.Tests.Pass\" result=\"Passed\" /></test-suite>");

    private static string TestRunXml(
        string result,
        int total,
        int passed,
        int failed,
        int skipped,
        int inconclusive,
        string body) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><test-run result=\"{result}\" total=\"{total}\" passed=\"{passed}\" failed=\"{failed}\" skipped=\"{skipped}\" inconclusive=\"{inconclusive}\">{body}</test-run>";

    private static ToolProcessResult SuccessfulProcess(string operation = EditModeOperation) =>
        new(
            UnityTool,
            operation,
            ToolResultStatus.Succeeded,
            ToolProcessLaunchStatus.Started,
            exitCode: 0,
            forcedTerminationRequested: false,
            EmptyOutput,
            "Unity EditMode process completed successfully.");

    private static ToolProcessResult FailedTestProcess() =>
        new(
            UnityTool,
            EditModeOperation,
            ToolResultStatus.Failed,
            ToolProcessLaunchStatus.Started,
            exitCode: 2,
            forcedTerminationRequested: false,
            EmptyOutput,
            "Unity EditMode process reported failed tests.");

    private static ToolProcessResult FailedProcess() =>
        new(
            UnityTool,
            EditModeOperation,
            ToolResultStatus.Failed,
            ToolProcessLaunchStatus.Started,
            exitCode: 1,
            forcedTerminationRequested: false,
            EmptyOutput,
            "Unity EditMode process failed.");

    private static ToolProcessResult LaunchFailedProcess() =>
        new(
            UnityTool,
            EditModeOperation,
            ToolResultStatus.Failed,
            ToolProcessLaunchStatus.ExecutableNotFound,
            exitCode: null,
            forcedTerminationRequested: false,
            EmptyOutput,
            "Unity executable was not available.");

    private static ToolProcessResult CancelledProcess() =>
        new(
            UnityTool,
            EditModeOperation,
            ToolResultStatus.Cancelled,
            ToolProcessLaunchStatus.Started,
            exitCode: -1,
            forcedTerminationRequested: true,
            EmptyOutput,
            "Unity EditMode process was cancelled.");

    private static void ExpectThrowsAsync<TException>(
        Func<Task> action,
        List<string> failures,
        string message)
        where TException : Exception
    {
        try
        {
            action().GetAwaiter().GetResult();
            failures.Add(message);
        }
        catch (TException)
        {
        }
        catch (Exception exception)
        {
            failures.Add($"{message} Expected {typeof(TException).Name}, got {exception.GetType().Name}.");
        }
    }
}
