using FCCCodeDesktop.Tools;
using FCCCodeDesktop.Tools.Unity;

return UnityCompileValidationFixture.Run();

internal static class UnityCompileValidationFixture
{
    private const string CompileOperation = "unity.open-project";
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

    public static int Run()
    {
        var failures = new List<string>();

        void Check(bool condition, string message)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }

        VerifySuccessfulCompile(Check);
        VerifyCompilerErrors(Check);
        VerifyFailureMarkers(Check);
        VerifyProcessOutcomes(Check);
        VerifyEvidenceCompleteness(Check);
        VerifyDiagnosticBounds(Check);
        VerifyGuardsAndSnapshot(failures, Check);

        if (failures.Count == 0)
        {
            Console.WriteLine("P10-006 Unity compile validation fixture: PASS");
            return 0;
        }

        Console.Error.WriteLine("P10-006 Unity compile validation fixture: FAIL");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(" - " + failure);
        }

        return 1;
    }

    private static void VerifySuccessfulCompile(Action<bool, string> check)
    {
        var validator = new UnityCompileValidator();
        var result = validator.Validate(
            SuccessfulProcess(),
            Evidence(
                Entry(1, UnityLogSeverity.Info, "Using project path C:\\repo\\game"),
                Entry(2, UnityLogSeverity.Warning, "Assets/Test.cs(4,2): warning CS0219: variable is assigned"),
                Entry(3, UnityLogSeverity.Error, "Error: cache server unavailable; continuing locally"),
                Entry(4, UnityLogSeverity.Info, "Refreshing native plugins complete")));

        check(result.Status == UnityCompileValidationStatus.Succeeded,
            "A zero-exit controlled process with complete compiler-clean log evidence must succeed.");
        check(result.FailureKind == UnityCompileFailureKind.None,
            "A successful compile must not report a failure kind.");
        check(result.CompilerErrorCount == 0 && result.FailureMarkerCount == 0,
            "Warnings and unrelated Unity errors must not be misreported as script compiler failures.");
        check(result.Diagnostics.Count == 0 && !result.DiagnosticsTruncated,
            "A successful compile must not retain compiler-failure diagnostics.");
    }

    private static void VerifyCompilerErrors(Action<bool, string> check)
    {
        var validator = new UnityCompileValidator();
        var result = validator.Validate(
            SuccessfulProcess(),
            Evidence(
                Entry(10, UnityLogSeverity.Info, "Starting script compilation"),
                Entry(11, UnityLogSeverity.Error, "Assets/Player.cs(14,9): error CS1002: ; expected"),
                Entry(12, UnityLogSeverity.Warning, "Assets/Other.cs(3,1): warning CS0168: unused variable")));

        check(result.Status == UnityCompileValidationStatus.Failed,
            "A compiler error must fail validation even when Unity exits zero.");
        check(result.FailureKind == UnityCompileFailureKind.CompilerError,
            "C# compiler diagnostics must use the CompilerError failure kind.");
        check(result.CompilerErrorCount == 1 && result.FailureMarkerCount == 0,
            "Compiler-error accounting must be exact and warning-safe.");
        check(result.Diagnostics.Count == 1 && result.Diagnostics[0].CompilerCode == "CS1002",
            "Compiler diagnostics must retain the structured CS code.");
        check(result.Diagnostics[0].Sequence == 11,
            "Compiler diagnostics must retain source log sequence correlation.");
    }

    private static void VerifyFailureMarkers(Action<bool, string> check)
    {
        var validator = new UnityCompileValidator();
        var result = validator.Validate(
            SuccessfulProcess(),
            Evidence(
                Entry(1, UnityLogSeverity.Info, "Reloading assemblies"),
                Entry(2, UnityLogSeverity.Error, "Scripts have compiler errors.")));

        check(result.Status == UnityCompileValidationStatus.Failed,
            "Explicit Unity compiler-failure markers must fail validation with exit code zero.");
        check(result.FailureKind == UnityCompileFailureKind.CompilationFailureMarker,
            "Marker-only failure must be typed separately from a parsed CS diagnostic.");
        check(result.CompilerErrorCount == 0 && result.FailureMarkerCount == 1,
            "Marker-only failure accounting must be exact.");
        check(result.Diagnostics.Count == 1 && result.Diagnostics[0].CompilerCode is null,
            "Marker-only diagnostics must not invent a compiler code.");
    }

    private static void VerifyProcessOutcomes(Action<bool, string> check)
    {
        var validator = new UnityCompileValidator();
        var evidence = Evidence(Entry(1, UnityLogSeverity.Info, "Editor initialized"));

        var failed = validator.Validate(FailedProcess(), evidence);
        check(failed.Status == UnityCompileValidationStatus.Failed &&
              failed.FailureKind == UnityCompileFailureKind.ProcessFailure,
            "A non-zero Unity process result must fail compile validation.");

        var launchFailed = validator.Validate(LaunchFailedProcess(), evidence);
        check(launchFailed.Status == UnityCompileValidationStatus.Failed &&
              launchFailed.FailureKind == UnityCompileFailureKind.ProcessFailure,
            "A Unity launch failure must fail compile validation without relying on log content.");

        var cancelled = validator.Validate(CancelledProcess(), evidence);
        check(cancelled.Status == UnityCompileValidationStatus.Cancelled &&
              cancelled.FailureKind == UnityCompileFailureKind.Cancellation,
            "A cancelled Unity process must remain distinctly cancelled.");

        var forced = validator.Validate(ForcedTerminationSuccessProcess(), evidence);
        check(forced.Status == UnityCompileValidationStatus.Failed &&
              forced.FailureKind == UnityCompileFailureKind.ProcessFailure,
            "Forced termination must never be promoted to compile success even with exit code zero.");
    }

    private static void VerifyEvidenceCompleteness(Action<bool, string> check)
    {
        var validator = new UnityCompileValidator();
        var process = SuccessfulProcess();

        var missing = validator.Validate(
            process,
            new UnityCompileLogEvidence(
                Array.Empty<UnityLogEntry>(),
                fileObserved: false,
                captureFinalized: true,
                coverageComplete: true));
        check(missing.Status == UnityCompileValidationStatus.Indeterminate &&
              missing.FailureKind == UnityCompileFailureKind.LogUnavailable,
            "Exit code zero without an observed invocation log must remain indeterminate.");

        var empty = validator.Validate(
            process,
            new UnityCompileLogEvidence(
                Array.Empty<UnityLogEntry>(),
                fileObserved: true,
                captureFinalized: true,
                coverageComplete: true));
        check(empty.Status == UnityCompileValidationStatus.Indeterminate &&
              empty.FailureKind == UnityCompileFailureKind.LogUnavailable,
            "An empty observed log must not turn process success into compile success.");

        var unfinished = validator.Validate(
            process,
            new UnityCompileLogEvidence(
                new[] { Entry(1, UnityLogSeverity.Info, "Compiling") },
                fileObserved: true,
                captureFinalized: false,
                coverageComplete: true));
        check(unfinished.Status == UnityCompileValidationStatus.Indeterminate &&
              unfinished.FailureKind == UnityCompileFailureKind.LogIncomplete,
            "A non-finalized log capture must remain indeterminate.");

        var coverageGap = validator.Validate(
            process,
            new UnityCompileLogEvidence(
                new[] { Entry(1, UnityLogSeverity.Info, "Compiling") },
                fileObserved: true,
                captureFinalized: true,
                coverageComplete: false));
        check(coverageGap.Status == UnityCompileValidationStatus.Indeterminate &&
              coverageGap.FailureKind == UnityCompileFailureKind.LogIncomplete,
            "A declared log coverage gap must remain indeterminate.");

        var reset = validator.Validate(
            process,
            new UnityCompileLogEvidence(
                new[] { Entry(1, UnityLogSeverity.Info, "Compiling") },
                fileObserved: true,
                captureFinalized: true,
                coverageComplete: true,
                generationResetObserved: true));
        check(reset.Status == UnityCompileValidationStatus.Indeterminate &&
              reset.FailureKind == UnityCompileFailureKind.LogIncomplete,
            "A log generation reset must block success because earlier compiler output may have been lost.");

        var nonContiguous = validator.Validate(
            process,
            new UnityCompileLogEvidence(
                new[]
                {
                    Entry(4, UnityLogSeverity.Info, "first retained"),
                    Entry(6, UnityLogSeverity.Info, "gap after first retained"),
                },
                fileObserved: true,
                captureFinalized: true,
                coverageComplete: true));
        check(nonContiguous.Status == UnityCompileValidationStatus.Indeterminate &&
              nonContiguous.FailureKind == UnityCompileFailureKind.LogIncomplete,
            "Non-contiguous log sequences must block compile success.");

        var truncated = validator.Validate(
            process,
            new UnityCompileLogEvidence(
                new[] { Entry(1, UnityLogSeverity.Info, "bounded line", isTruncated: true) },
                fileObserved: true,
                captureFinalized: true,
                coverageComplete: true));
        check(truncated.Status == UnityCompileValidationStatus.Indeterminate &&
              truncated.FailureKind == UnityCompileFailureKind.LogCorrupted,
            "Truncated log evidence must block compile success.");

        var encoding = validator.Validate(
            process,
            new UnityCompileLogEvidence(
                new[] { Entry(1, UnityLogSeverity.Info, "replacement text", hadEncodingErrors: true) },
                fileObserved: true,
                captureFinalized: true,
                coverageComplete: true));
        check(encoding.Status == UnityCompileValidationStatus.Indeterminate &&
              encoding.FailureKind == UnityCompileFailureKind.LogCorrupted,
            "Malformed log encoding must block compile success.");

        var explicitErrorDespiteCorruption = validator.Validate(
            process,
            new UnityCompileLogEvidence(
                new[] { Entry(1, UnityLogSeverity.Error, "error CS2001: source file missing", isTruncated: true) },
                fileObserved: true,
                captureFinalized: true,
                coverageComplete: false));
        check(explicitErrorDespiteCorruption.Status == UnityCompileValidationStatus.Failed &&
              explicitErrorDespiteCorruption.FailureKind == UnityCompileFailureKind.CompilerError,
            "An explicit compiler error must fail even when other log evidence is incomplete.");
    }

    private static void VerifyDiagnosticBounds(Action<bool, string> check)
    {
        var validator = new UnityCompileValidator();
        var entries = Enumerable.Range(1, 205)
            .Select(index => Entry(
                index,
                UnityLogSeverity.Error,
                $"Assets/Test{index}.cs(1,1): error CS1002: {new string('x', 5000)}"))
            .ToArray();

        var result = validator.Validate(SuccessfulProcess(), Evidence(entries));
        check(result.Status == UnityCompileValidationStatus.Failed && result.CompilerErrorCount == 205,
            "Compiler-error counting must retain the full count even when diagnostic retention is bounded.");
        check(result.Diagnostics.Count == 200 && result.DiagnosticsTruncated,
            "Retained compile diagnostics must be bounded and explicitly report truncation.");
        check(result.Diagnostics[0].Message.Length == 4096 && result.Diagnostics[0].MessageWasTruncated,
            "Each retained diagnostic message must also be bounded independently.");
    }

    private static void VerifyGuardsAndSnapshot(List<string> failures, Action<bool, string> check)
    {
        var entries = new List<UnityLogEntry>
        {
            Entry(1, UnityLogSeverity.Info, "original"),
        };
        var evidence = new UnityCompileLogEvidence(
            entries,
            fileObserved: true,
            captureFinalized: true,
            coverageComplete: true);
        entries.Clear();
        check(evidence.Entries.Count == 1 && evidence.Entries[0].Text == "original",
            "Compile log evidence must snapshot the caller collection immutably.");

        ExpectThrows<ArgumentException>(
            () => _ = new UnityCompileLogEvidence(
                new[] { Entry(1, UnityLogSeverity.Info, "impossible") },
                fileObserved: false,
                captureFinalized: true,
                coverageComplete: true),
            failures,
            "Log entries must be rejected when the log file was not observed.");

        var validator = new UnityCompileValidator();
        ExpectThrows<ArgumentException>(
            () => _ = validator.Validate(
                SuccessfulProcess(operation: "unity.execute-method"),
                evidence),
            failures,
            "Compile validation must reject a non-compile Unity operation instead of reusing unrelated process evidence.");
    }

    private static UnityCompileLogEvidence Evidence(params UnityLogEntry[] entries) =>
        new(
            entries,
            fileObserved: true,
            captureFinalized: true,
            coverageComplete: true);

    private static UnityLogEntry Entry(
        long sequence,
        UnityLogSeverity severity,
        string text,
        bool isTruncated = false,
        bool hadEncodingErrors = false) =>
        new(
            sequence,
            severity,
            text,
            isTruncated,
            truncatedUtf8Bytes: isTruncated ? 1 : 0,
            hadEncodingErrors);

    private static ToolProcessResult SuccessfulProcess(string operation = CompileOperation) =>
        new(
            UnityTool,
            operation,
            ToolResultStatus.Succeeded,
            ToolProcessLaunchStatus.Started,
            exitCode: 0,
            forcedTerminationRequested: false,
            EmptyOutput,
            "Unity process completed successfully.");

    private static ToolProcessResult FailedProcess() =>
        new(
            UnityTool,
            CompileOperation,
            ToolResultStatus.Failed,
            ToolProcessLaunchStatus.Started,
            exitCode: 1,
            forcedTerminationRequested: false,
            EmptyOutput,
            "Unity process failed.");

    private static ToolProcessResult LaunchFailedProcess() =>
        new(
            UnityTool,
            CompileOperation,
            ToolResultStatus.Failed,
            ToolProcessLaunchStatus.ExecutableNotFound,
            exitCode: null,
            forcedTerminationRequested: false,
            EmptyOutput,
            "Unity executable was not available.");

    private static ToolProcessResult CancelledProcess() =>
        new(
            UnityTool,
            CompileOperation,
            ToolResultStatus.Cancelled,
            ToolProcessLaunchStatus.Started,
            exitCode: -1,
            forcedTerminationRequested: true,
            EmptyOutput,
            "Unity process was cancelled.");

    private static ToolProcessResult ForcedTerminationSuccessProcess() =>
        new(
            UnityTool,
            CompileOperation,
            ToolResultStatus.Succeeded,
            ToolProcessLaunchStatus.Started,
            exitCode: 0,
            forcedTerminationRequested: true,
            EmptyOutput,
            "Unity process exited after forced termination was requested.");

    private static void ExpectThrows<TException>(
        Action action,
        List<string> failures,
        string message)
        where TException : Exception
    {
        try
        {
            action();
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
