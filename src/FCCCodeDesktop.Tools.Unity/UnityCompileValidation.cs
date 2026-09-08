using System.Collections.ObjectModel;
using FCCCodeDesktop.Tools;

namespace FCCCodeDesktop.Tools.Unity;

public enum UnityCompileValidationStatus
{
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
    Indeterminate = 4,
}

public enum UnityCompileFailureKind
{
    None = 0,
    Cancellation = 1,
    ProcessFailure = 2,
    CompilerError = 3,
    CompilationFailureMarker = 4,
    LogUnavailable = 5,
    LogIncomplete = 6,
    LogCorrupted = 7,
}

/// <summary>
/// One bounded compiler diagnostic retained from the Unity log evidence.
/// </summary>
public sealed record UnityCompileDiagnostic
{
    public UnityCompileDiagnostic(
        long sequence,
        UnityLogSeverity severity,
        string? compilerCode,
        string message,
        bool sourceWasTruncated,
        bool sourceHadEncodingErrors,
        bool messageWasTruncated)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "A concrete Unity log severity is required.");
        }

        if (compilerCode is not null && !IsCompilerCode(compilerCode))
        {
            throw new ArgumentException("Compiler codes must use the CS#### form.", nameof(compilerCode));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Sequence = sequence;
        Severity = severity;
        CompilerCode = compilerCode;
        Message = message;
        SourceWasTruncated = sourceWasTruncated;
        SourceHadEncodingErrors = sourceHadEncodingErrors;
        MessageWasTruncated = messageWasTruncated;
    }

    public long Sequence { get; }

    public UnityLogSeverity Severity { get; }

    public string? CompilerCode { get; }

    public string Message { get; }

    public bool SourceWasTruncated { get; }

    public bool SourceHadEncodingErrors { get; }

    public bool MessageWasTruncated { get; }

    private static bool IsCompilerCode(string value)
    {
        if (value.Length != 6 || value[0] != 'C' || value[1] != 'S')
        {
            return false;
        }

        for (var index = 2; index < value.Length; index++)
        {
            if (!char.IsAsciiDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Complete invocation-scoped Unity log evidence supplied to compile validation after process termination.
/// </summary>
public sealed record UnityCompileLogEvidence
{
    private readonly ReadOnlyCollection<UnityLogEntry> _entries;

    public UnityCompileLogEvidence(
        IEnumerable<UnityLogEntry> entries,
        bool fileObserved,
        bool captureFinalized,
        bool coverageComplete,
        bool generationResetObserved = false)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var snapshot = entries.ToArray();
        if (!fileObserved && snapshot.Length != 0)
        {
            throw new ArgumentException("Log entries cannot be supplied when the Unity log file was not observed.", nameof(entries));
        }

        _entries = Array.AsReadOnly(snapshot);
        FileObserved = fileObserved;
        CaptureFinalized = captureFinalized;
        CoverageComplete = coverageComplete;
        GenerationResetObserved = generationResetObserved;
    }

    public IReadOnlyList<UnityLogEntry> Entries => _entries;

    public bool FileObserved { get; }

    public bool CaptureFinalized { get; }

    public bool CoverageComplete { get; }

    public bool GenerationResetObserved { get; }
}

/// <summary>
/// Terminal result for Unity script compile validation. Success requires both a successful process result and complete log evidence.
/// </summary>
public sealed record UnityCompileValidationResult
{
    private readonly ReadOnlyCollection<UnityCompileDiagnostic> _diagnostics;

    internal UnityCompileValidationResult(
        UnityCompileValidationStatus status,
        UnityCompileFailureKind failureKind,
        int compilerErrorCount,
        int failureMarkerCount,
        IEnumerable<UnityCompileDiagnostic> diagnostics,
        bool diagnosticsTruncated,
        string summary)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "A concrete compile validation status is required.");
        }

        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "A concrete compile failure kind is required.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(compilerErrorCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failureMarkerCount);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        _diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Status = status;
        FailureKind = failureKind;
        CompilerErrorCount = compilerErrorCount;
        FailureMarkerCount = failureMarkerCount;
        DiagnosticsTruncated = diagnosticsTruncated;
        Summary = summary;
    }

    public UnityCompileValidationStatus Status { get; }

    public UnityCompileFailureKind FailureKind { get; }

    public int CompilerErrorCount { get; }

    public int FailureMarkerCount { get; }

    public IReadOnlyList<UnityCompileDiagnostic> Diagnostics => _diagnostics;

    public bool DiagnosticsTruncated { get; }

    public string Summary { get; }
}

public interface IUnityCompileValidator
{
    UnityCompileValidationResult Validate(
        ToolProcessResult processResult,
        UnityCompileLogEvidence logEvidence);
}

/// <summary>
/// Validates Unity script compilation from the terminal process result plus complete project-owned -logFile evidence.
/// A zero exit code alone can never produce success.
/// </summary>
public sealed class UnityCompileValidator : IUnityCompileValidator
{
    private const string CompileOperation = "unity.open-project";
    private const int MaxRetainedDiagnostics = 200;
    private const int MaxDiagnosticCharacters = 4096;

    private static readonly string[] CompilationFailureMarkers =
    {
        "scripts have compiler errors",
        "compiler errors have to be fixed",
        "script compilation failed",
        "compilation pipeline failed",
        "compilation failed",
        "failed to compile scripts",
    };

    public UnityCompileValidationResult Validate(
        ToolProcessResult processResult,
        UnityCompileLogEvidence logEvidence)
    {
        ArgumentNullException.ThrowIfNull(processResult);
        ArgumentNullException.ThrowIfNull(logEvidence);

        if (!string.Equals(processResult.Operation, CompileOperation, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unity compile validation requires operation '{CompileOperation}'.",
                nameof(processResult));
        }

        if (processResult.Status == ToolResultStatus.Cancelled)
        {
            return CreateTerminalResult(
                UnityCompileValidationStatus.Cancelled,
                UnityCompileFailureKind.Cancellation,
                "Unity compile validation was cancelled before a successful terminal compile result was established.");
        }

        if (processResult.Status != ToolResultStatus.Succeeded ||
            processResult.LaunchStatus != ToolProcessLaunchStatus.Started ||
            processResult.ExitCode != 0 ||
            processResult.ForcedTerminationRequested)
        {
            return CreateTerminalResult(
                UnityCompileValidationStatus.Failed,
                UnityCompileFailureKind.ProcessFailure,
                "Unity compile validation failed because the controlled Unity process did not complete successfully.");
        }

        var diagnostics = new List<UnityCompileDiagnostic>();
        var compilerErrorCount = 0;
        var failureMarkerCount = 0;
        var diagnosticsTruncated = false;

        foreach (var entry in logEvidence.Entries)
        {
            var compilerCode = TryExtractCompilerErrorCode(entry.Text);
            var isCompilerError = compilerCode is not null;
            var isFailureMarker = !isCompilerError && IsCompilationFailureMarker(entry.Text);
            if (!isCompilerError && !isFailureMarker)
            {
                continue;
            }

            if (isCompilerError)
            {
                compilerErrorCount = checked(compilerErrorCount + 1);
            }
            else
            {
                failureMarkerCount = checked(failureMarkerCount + 1);
            }

            if (diagnostics.Count < MaxRetainedDiagnostics)
            {
                diagnostics.Add(CreateDiagnostic(entry, compilerCode));
            }
            else
            {
                diagnosticsTruncated = true;
            }
        }

        if (compilerErrorCount > 0 || failureMarkerCount > 0)
        {
            var failureKind = compilerErrorCount > 0
                ? UnityCompileFailureKind.CompilerError
                : UnityCompileFailureKind.CompilationFailureMarker;
            return new UnityCompileValidationResult(
                UnityCompileValidationStatus.Failed,
                failureKind,
                compilerErrorCount,
                failureMarkerCount,
                diagnostics,
                diagnosticsTruncated,
                compilerErrorCount > 0
                    ? "Unity compile validation found one or more C# compiler errors in the invocation-scoped Unity log."
                    : "Unity compile validation found an explicit Unity compilation-failure marker in the invocation-scoped log.");
        }

        if (!logEvidence.FileObserved || logEvidence.Entries.Count == 0)
        {
            return CreateTerminalResult(
                UnityCompileValidationStatus.Indeterminate,
                UnityCompileFailureKind.LogUnavailable,
                "Unity compile validation cannot pass because no invocation-scoped Unity log evidence was observed.");
        }

        if (!logEvidence.CaptureFinalized ||
            !logEvidence.CoverageComplete ||
            logEvidence.GenerationResetObserved ||
            !HasContiguousSequences(logEvidence.Entries))
        {
            return CreateTerminalResult(
                UnityCompileValidationStatus.Indeterminate,
                UnityCompileFailureKind.LogIncomplete,
                "Unity compile validation cannot pass because the Unity log evidence is incomplete or contains a capture gap/reset.");
        }

        if (logEvidence.Entries.Any(static entry => entry.IsTruncated || entry.HadEncodingErrors))
        {
            return CreateTerminalResult(
                UnityCompileValidationStatus.Indeterminate,
                UnityCompileFailureKind.LogCorrupted,
                "Unity compile validation cannot pass because at least one log entry was truncated or had decoding errors.");
        }

        return CreateTerminalResult(
            UnityCompileValidationStatus.Succeeded,
            UnityCompileFailureKind.None,
            "Unity compile validation succeeded from a successful controlled process and complete Unity log evidence with no compiler failures.");
    }

    private static UnityCompileValidationResult CreateTerminalResult(
        UnityCompileValidationStatus status,
        UnityCompileFailureKind failureKind,
        string summary) =>
        new(
            status,
            failureKind,
            compilerErrorCount: 0,
            failureMarkerCount: 0,
            Array.Empty<UnityCompileDiagnostic>(),
            diagnosticsTruncated: false,
            summary);

    private static UnityCompileDiagnostic CreateDiagnostic(UnityLogEntry entry, string? compilerCode)
    {
        var message = entry.Text;
        var messageWasTruncated = false;
        if (message.Length > MaxDiagnosticCharacters)
        {
            message = message[..MaxDiagnosticCharacters];
            messageWasTruncated = true;
        }

        return new UnityCompileDiagnostic(
            entry.Sequence,
            entry.Severity,
            compilerCode,
            message,
            entry.IsTruncated,
            entry.HadEncodingErrors,
            messageWasTruncated);
    }

    private static bool HasContiguousSequences(IReadOnlyList<UnityLogEntry> entries)
    {
        for (var index = 1; index < entries.Count; index++)
        {
            if (entries[index].Sequence != checked(entries[index - 1].Sequence + 1))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCompilationFailureMarker(string text) =>
        CompilationFailureMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static string? TryExtractCompilerErrorCode(string text)
    {
        const string token = "error CS";
        var searchIndex = 0;
        while (searchIndex < text.Length)
        {
            var markerIndex = text.IndexOf(token, searchIndex, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return null;
            }

            var digitsStart = markerIndex + token.Length;
            if (digitsStart + 4 <= text.Length &&
                char.IsAsciiDigit(text[digitsStart]) &&
                char.IsAsciiDigit(text[digitsStart + 1]) &&
                char.IsAsciiDigit(text[digitsStart + 2]) &&
                char.IsAsciiDigit(text[digitsStart + 3]) &&
                (digitsStart + 4 == text.Length || !char.IsAsciiDigit(text[digitsStart + 4])))
            {
                return "CS" + text.Substring(digitsStart, 4);
            }

            searchIndex = markerIndex + token.Length;
        }

        return null;
    }
}
