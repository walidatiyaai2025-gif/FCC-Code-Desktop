using System.Text;
using FCCCodeDesktop.Tools.Unity;

return await UnityLogCaptureFixture.RunAsync();

internal static class UnityLogCaptureFixture
{
    public static async Task<int> RunAsync()
    {
        var failures = new List<string>();
        var root = Path.Combine(Path.GetTempPath(), "FCC P10-005", Guid.NewGuid().ToString("N"));
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
            VerifyParserClassification(Check);
            VerifyOptionAndPathGuards(failures, root);
            await VerifyIncrementalCaptureAsync(Check, root);
            await VerifyBatchLimitAsync(Check, root);
            await VerifyOversizedLineAsync(Check, root);
            await VerifyInvalidUtf8Async(Check, root);
            await VerifyFinalLineFlushAsync(Check, root);
            await VerifyCancellationAsync(failures, root);
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
            Console.WriteLine("P10-005 Unity log capture/parser fixture: PASS");
            return 0;
        }

        Console.Error.WriteLine("P10-005 Unity log capture/parser fixture: FAIL");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(" - " + failure);
        }

        return 1;
    }

    private static void VerifyParserClassification(Action<bool, string> check)
    {
        check(UnityLogParser.Classify("normal editor message") == UnityLogSeverity.Info,
            "Ordinary Unity log text must classify as Info.");
        check(UnityLogParser.Classify("Warning: fallback renderer") == UnityLogSeverity.Warning,
            "Unity warning prefixes must classify as Warning.");
        check(UnityLogParser.Classify("Assets/Test.cs(4,2): warning CS0219: variable is assigned") == UnityLogSeverity.Warning,
            "Compiler warning lines must classify as Warning without claiming test/compile outcome.");
        check(UnityLogParser.Classify("Assets/Test.cs(4,2): error CS1002: ; expected") == UnityLogSeverity.Error,
            "Compiler error lines must classify as Error.");
        check(UnityLogParser.Classify("NullReferenceException: Object reference not set") == UnityLogSeverity.Exception,
            "Typed exception lines must classify as Exception.");
        check(UnityLogParser.Classify("Unhandled exception while importing") == UnityLogSeverity.Exception,
            "Unhandled exception lines must classify as Exception.");
        check(UnityLogParser.Classify("Assertion failed on expression: 'value'") == UnityLogSeverity.Assert,
            "Assertion failures must classify as Assert.");
        check(UnityLogParser.Classify("[2026-09-08 14:01:02.123] Warning: timestamped") == UnityLogSeverity.Warning,
            "Documented timestamp prefixes must not hide severity.");
        check(UnityLogParser.Classify("[Licensing::Client] ErrorCode: 0") == UnityLogSeverity.Info,
            "Non-timestamp Unity bracket prefixes must not be stripped heuristically.");
    }

    private static void VerifyOptionAndPathGuards(List<string> failures, string root)
    {
        ExpectThrows<ArgumentOutOfRangeException>(
            () => _ = new UnityLogCaptureOptions(maxLineUtf8Bytes: 1),
            failures,
            "Unbounded/tiny Unity log line configuration must be rejected.");
        ExpectThrows<ArgumentOutOfRangeException>(
            () => _ = new UnityLogCaptureOptions(maxEntriesPerRead: 0),
            failures,
            "A zero-entry Unity log batch must be rejected.");

        var reader = new UnityLogCaptureReader();
        ExpectThrowsAsync<ArgumentException>(
            () => reader.ReadNewAsync("relative.log").AsTask(),
            failures,
            "Relative Unity log paths must be rejected.").GetAwaiter().GetResult();

        var missingPath = Path.Combine(root, "missing.log");
        var missing = reader.ReadNewAsync(missingPath).AsTask().GetAwaiter().GetResult();
        if (missing.FileExists || missing.Entries.Count != 0 || missing.Cursor.ByteOffset != 0)
        {
            failures.Add("A missing Unity log file must produce an empty, non-advancing capture result.");
        }
    }

    private static async Task VerifyIncrementalCaptureAsync(Action<bool, string> check, string root)
    {
        var path = Path.Combine(root, "incremental.log");
        var reader = new UnityLogCaptureReader();
        var prefix = Encoding.UTF8.GetBytes("Info first\r\nمرحبا");
        await File.WriteAllBytesAsync(path, prefix);

        var first = await reader.ReadNewAsync(path);
        check(first.FileExists, "Existing Unity log must be reported as present.");
        check(first.Entries.Count == 1 && first.Entries[0].Text == "Info first",
            "Only complete lines may be emitted while a UTF-8 line is partial.");
        check(first.Cursor.HasPendingLine,
            "Partial Unity log line bytes must survive the polling boundary.");
        check(first.Entries[0].Sequence == 1,
            "Unity log sequence numbering must start at one.");

        await AppendUtf8Async(
            path,
            " بالعالم\nWarning: تحذير\nAssets/Test.cs(4,2): error CS1002: ; expected\nNullReferenceException: boom\nAssertion failed on expression: 'x'\n");

        var second = await reader.ReadNewAsync(path, first.Cursor);
        check(second.Entries.Count == 5,
            "Incremental capture must emit every newly completed Unity log line exactly once.");
        check(second.Entries[0].Text == "مرحبا بالعالم",
            "UTF-8/Arabic content split across polls must round-trip without corruption.");
        check(second.Entries[0].Severity == UnityLogSeverity.Info,
            "Arabic informational text must remain Info.");
        check(second.Entries[1].Severity == UnityLogSeverity.Warning,
            "Warning capture must preserve parser classification.");
        check(second.Entries[2].Severity == UnityLogSeverity.Error,
            "Compiler error capture must preserve parser classification.");
        check(second.Entries[3].Severity == UnityLogSeverity.Exception,
            "Exception capture must preserve parser classification.");
        check(second.Entries[4].Severity == UnityLogSeverity.Assert,
            "Assertion capture must preserve parser classification.");
        check(second.Entries[0].Sequence == 2 && second.Entries[4].Sequence == 6,
            "Sequence numbers must remain monotonic across incremental reads.");
        check(!second.Cursor.HasPendingLine,
            "Complete incremental lines must leave no pending bytes.");

        await File.WriteAllTextAsync(path, "reset line\n", Encoding.UTF8);
        var reset = await reader.ReadNewAsync(path, second.Cursor);
        check(reset.WasReset,
            "A Unity log truncated below the cursor must reset capture instead of seeking beyond EOF.");
        check(reset.Cursor.Generation == second.Cursor.Generation + 1,
            "Log truncation must advance the capture generation.");
        check(reset.Entries.Count == 1 && reset.Entries[0].Text == "reset line",
            "A truncated/recreated Unity log must be read from its new beginning.");
        check(reset.Entries[0].Sequence == 7,
            "Sequence numbers must remain monotonic across log reset generations.");
    }

    private static async Task VerifyBatchLimitAsync(Action<bool, string> check, string root)
    {
        var path = Path.Combine(root, "batch.log");
        await File.WriteAllTextAsync(path, "one\ntwo\nthree\n", Encoding.UTF8);
        var reader = new UnityLogCaptureReader(
            new UnityLogCaptureOptions(maxEntriesPerRead: 2));

        var first = await reader.ReadNewAsync(path);
        check(first.Entries.Count == 2 && first.ReachedEntryLimit,
            "Unity log capture must stop at the configured batch entry bound.");
        check(first.Entries[0].Text == "one" && first.Entries[1].Text == "two",
            "Bounded reads must retain deterministic line ordering.");

        var second = await reader.ReadNewAsync(path, first.Cursor);
        check(second.Entries.Count == 1 && second.Entries[0].Text == "three",
            "The cursor must resume exactly after a bounded batch without duplicate/lost lines.");
        check(!second.ReachedEntryLimit,
            "A final under-limit batch must not report an artificial limit hit.");
    }

    private static async Task VerifyOversizedLineAsync(Action<bool, string> check, string root)
    {
        var path = Path.Combine(root, "oversized.log");
        await File.WriteAllTextAsync(path, new string('x', 300) + "\n", Encoding.UTF8);
        var reader = new UnityLogCaptureReader(
            new UnityLogCaptureOptions(maxLineUtf8Bytes: 256));

        var result = await reader.ReadNewAsync(path);
        check(result.Entries.Count == 1,
            "Oversized Unity log input must still produce one bounded structured entry.");
        check(result.Entries[0].IsTruncated && result.Entries[0].Text.Length == 256,
            "Oversized Unity log lines must be bounded and explicitly marked truncated.");
        check(result.Entries[0].TruncatedUtf8Bytes == 44,
            "Unity log truncation must report the exact dropped UTF-8 byte count.");
    }

    private static async Task VerifyInvalidUtf8Async(Action<bool, string> check, string root)
    {
        var path = Path.Combine(root, "invalid-utf8.log");
        await File.WriteAllBytesAsync(path, new byte[] { 0xC3, 0x28, (byte)'\n' });
        var reader = new UnityLogCaptureReader();

        var result = await reader.ReadNewAsync(path);
        check(result.Entries.Count == 1 && result.Entries[0].HadEncodingErrors,
            "Malformed UTF-8 must be surfaced structurally rather than crashing log capture.");
        check(result.Entries[0].Text.Length > 0,
            "Malformed UTF-8 capture must retain replacement-decoded diagnostic text.");
    }

    private static async Task VerifyFinalLineFlushAsync(Action<bool, string> check, string root)
    {
        var path = Path.Combine(root, "final.log");
        await File.WriteAllTextAsync(path, "final line without newline", Encoding.UTF8);
        var reader = new UnityLogCaptureReader();

        var pending = await reader.ReadNewAsync(path);
        check(pending.Entries.Count == 0 && pending.Cursor.HasPendingLine,
            "An active partial final line must remain pending by default.");

        var flushed = await reader.ReadNewAsync(path, pending.Cursor, flushFinalLine: true);
        check(flushed.Entries.Count == 1 && flushed.Entries[0].Text == "final line without newline",
            "The caller must be able to flush a final partial line after Unity terminates.");
        check(!flushed.Cursor.HasPendingLine,
            "Final-line flush must clear pending decoder state.");
    }

    private static async Task VerifyCancellationAsync(List<string> failures, string root)
    {
        var path = Path.Combine(root, "cancel.log");
        await File.WriteAllTextAsync(path, "line\n", Encoding.UTF8);
        var reader = new UnityLogCaptureReader();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await reader.ReadNewAsync(path, cancellationToken: cancellation.Token);
            failures.Add("Pre-cancelled Unity log capture must propagate cancellation.");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task AppendUtf8Async(string path, string text)
    {
        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var bytes = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

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
    }

    private static async Task ExpectThrowsAsync<TException>(
        Func<Task> action,
        List<string> failures,
        string message)
        where TException : Exception
    {
        try
        {
            await action();
            failures.Add(message);
        }
        catch (TException)
        {
        }
    }
}
