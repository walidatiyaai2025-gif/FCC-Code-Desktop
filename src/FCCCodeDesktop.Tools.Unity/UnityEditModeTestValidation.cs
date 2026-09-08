using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using FCCCodeDesktop.Tools;

namespace FCCCodeDesktop.Tools.Unity;

public enum UnityEditModeTestValidationStatus
{
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
    Indeterminate = 4,
}

public enum UnityEditModeTestFailureKind
{
    None = 0,
    Cancellation = 1,
    ProcessFailure = 2,
    ResultArtifactUnavailable = 3,
    ResultArtifactInvalid = 4,
    ResultArtifactTooLarge = 5,
    StaleResultArtifact = 6,
    ResultMalformed = 7,
    NoTestsDiscovered = 8,
    TestsFailed = 9,
    NonPassingResult = 10,
}

/// <summary>
/// Immutable pre-run fingerprint used to prove that a post-run Unity test-results artifact is fresh.
/// The normalized path remains internal so UI/diagnostics do not need to surface local filesystem paths.
/// </summary>
public sealed record UnityTestResultArtifactBaseline
{
    internal UnityTestResultArtifactBaseline(
        string fullPath,
        bool existed,
        long? length,
        string? sha256)
    {
        FullPath = fullPath;
        Existed = existed;
        Length = length;
        Sha256 = sha256;
    }

    internal string FullPath { get; }

    public bool Existed { get; }

    public long? Length { get; }

    public string? Sha256 { get; }
}

/// <summary>
/// One bounded failed NUnit node retained for structured UI/diagnostics.
/// </summary>
public sealed record UnityTestFailureDetail
{
    internal UnityTestFailureDetail(
        string name,
        string? fullName,
        string? message,
        string? stackTrace,
        bool messageTruncated,
        bool stackTraceTruncated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        FullName = fullName;
        Message = message;
        StackTrace = stackTrace;
        MessageTruncated = messageTruncated;
        StackTraceTruncated = stackTraceTruncated;
    }

    public string Name { get; }

    public string? FullName { get; }

    public string? Message { get; }

    public string? StackTrace { get; }

    public bool MessageTruncated { get; }

    public bool StackTraceTruncated { get; }
}

/// <summary>
/// Structured terminal result for one Unity EditMode invocation.
/// Success requires a successful controlled process and a fresh, well-formed, non-empty NUnit result artifact.
/// </summary>
public sealed record UnityEditModeTestValidationResult
{
    private readonly ReadOnlyCollection<UnityTestFailureDetail> _failures;

    internal UnityEditModeTestValidationResult(
        UnityEditModeTestValidationStatus status,
        UnityEditModeTestFailureKind failureKind,
        int total,
        int passed,
        int failed,
        int skipped,
        int inconclusive,
        IEnumerable<UnityTestFailureDetail> failures,
        bool failuresTruncated,
        string? nunitResult,
        long? resultFileBytes,
        string? resultFileSha256,
        string summary)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "A concrete EditMode validation status is required.");
        }

        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "A concrete EditMode failure kind is required.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(total);
        ArgumentOutOfRangeException.ThrowIfNegative(passed);
        ArgumentOutOfRangeException.ThrowIfNegative(failed);
        ArgumentOutOfRangeException.ThrowIfNegative(skipped);
        ArgumentOutOfRangeException.ThrowIfNegative(inconclusive);
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        Status = status;
        FailureKind = failureKind;
        Total = total;
        Passed = passed;
        Failed = failed;
        Skipped = skipped;
        Inconclusive = inconclusive;
        _failures = Array.AsReadOnly(failures.ToArray());
        FailuresTruncated = failuresTruncated;
        NUnitResult = nunitResult;
        ResultFileBytes = resultFileBytes;
        ResultFileSha256 = resultFileSha256;
        Summary = summary;
    }

    public UnityEditModeTestValidationStatus Status { get; }

    public UnityEditModeTestFailureKind FailureKind { get; }

    public int Total { get; }

    public int Passed { get; }

    public int Failed { get; }

    public int Skipped { get; }

    public int Inconclusive { get; }

    public IReadOnlyList<UnityTestFailureDetail> Failures => _failures;

    public bool FailuresTruncated { get; }

    public string? NUnitResult { get; }

    public long? ResultFileBytes { get; }

    public string? ResultFileSha256 { get; }

    public string Summary { get; }
}

public interface IUnityEditModeTestValidator
{
    Task<UnityTestResultArtifactBaseline> CaptureBaselineAsync(
        string testResultsPath,
        CancellationToken cancellationToken = default);

    Task<UnityEditModeTestValidationResult> ValidateAsync(
        ToolProcessResult processResult,
        string testResultsPath,
        UnityTestResultArtifactBaseline baseline,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates Unity Test Framework EditMode execution from the process lifecycle and the invocation-specific NUnit XML artifact.
/// It reuses the P09 safe artifact validator, rejects stale result reuse, parses XML with DTDs disabled, bounds retained diagnostics,
/// and never treats process exit code alone as test success.
/// </summary>
public sealed class UnityEditModeTestValidator : IUnityEditModeTestValidator
{
    public const long MaxResultFileBytes = 16L * 1024L * 1024L;

    private const string EditModeOperation = "unity.run-tests.editmode";
    private const int MaxRetainedFailures = 100;
    private const int MaxFailureMessageCharacters = 4096;
    private const int MaxFailureStackCharacters = 8192;
    private const string ArtifactId = "unity-editmode-test-results";

    private readonly IToolArtifactValidator _artifactValidator;

    public UnityEditModeTestValidator()
        : this(new ToolArtifactValidator())
    {
    }

    public UnityEditModeTestValidator(IToolArtifactValidator artifactValidator)
    {
        _artifactValidator = artifactValidator ?? throw new ArgumentNullException(nameof(artifactValidator));
    }

    public async Task<UnityTestResultArtifactBaseline> CaptureBaselineAsync(
        string testResultsPath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = NormalizeResultsPath(testResultsPath);
        var report = await _artifactValidator
            .ValidateAsync(CreateManifest(fullPath, requireNonEmpty: false), cancellationToken)
            .ConfigureAwait(false);
        var entry = report.Entries.Single();

        if (entry.Status == ToolArtifactValidationStatus.Missing)
        {
            return new UnityTestResultArtifactBaseline(fullPath, existed: false, length: null, sha256: null);
        }

        if (entry.Status != ToolArtifactValidationStatus.Valid || entry.Length is null || entry.Sha256 is null)
        {
            throw new InvalidOperationException(
                $"A safe EditMode test-results baseline could not be established ({entry.Status}).");
        }

        return new UnityTestResultArtifactBaseline(fullPath, existed: true, entry.Length, entry.Sha256);
    }

    public async Task<UnityEditModeTestValidationResult> ValidateAsync(
        ToolProcessResult processResult,
        string testResultsPath,
        UnityTestResultArtifactBaseline baseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processResult);
        ArgumentNullException.ThrowIfNull(baseline);

        if (!string.Equals(processResult.Operation, EditModeOperation, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unity EditMode validation requires operation '{EditModeOperation}'.",
                nameof(processResult));
        }

        var fullPath = NormalizeResultsPath(testResultsPath);
        if (!string.Equals(fullPath, baseline.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The EditMode result baseline must have been captured for the same normalized result path.",
                nameof(baseline));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (processResult.Status == ToolResultStatus.Cancelled)
        {
            return CreateTerminalResult(
                UnityEditModeTestValidationStatus.Cancelled,
                UnityEditModeTestFailureKind.Cancellation,
                "Unity EditMode test execution was cancelled before a successful terminal result was established.");
        }

        if (processResult.LaunchStatus != ToolProcessLaunchStatus.Started || processResult.ForcedTerminationRequested)
        {
            return CreateTerminalResult(
                UnityEditModeTestValidationStatus.Failed,
                UnityEditModeTestFailureKind.ProcessFailure,
                "Unity EditMode test execution failed because the controlled Unity process did not start normally or required forced termination.");
        }

        var processSucceeded =
            processResult.Status == ToolResultStatus.Succeeded &&
            processResult.ExitCode == 0;

        var report = await _artifactValidator
            .ValidateAsync(CreateManifest(fullPath, requireNonEmpty: true), cancellationToken)
            .ConfigureAwait(false);
        var entry = report.Entries.Single();

        if (entry.Status != ToolArtifactValidationStatus.Valid || entry.Length is null || entry.Sha256 is null)
        {
            if (!processSucceeded)
            {
                return CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Failed,
                    UnityEditModeTestFailureKind.ProcessFailure,
                    "Unity EditMode execution failed and did not leave a safe, readable, non-empty test-results artifact.");
            }

            var kind = entry.Status is ToolArtifactValidationStatus.Missing or ToolArtifactValidationStatus.Empty
                ? UnityEditModeTestFailureKind.ResultArtifactUnavailable
                : UnityEditModeTestFailureKind.ResultArtifactInvalid;
            return CreateTerminalResult(
                UnityEditModeTestValidationStatus.Indeterminate,
                kind,
                "Unity EditMode execution cannot pass because its result artifact is missing, empty, unsafe, or unreadable.");
        }

        if (entry.Length > MaxResultFileBytes)
        {
            return processSucceeded
                ? CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Indeterminate,
                    UnityEditModeTestFailureKind.ResultArtifactTooLarge,
                    "Unity EditMode execution cannot pass because its NUnit result artifact exceeds the bounded parser limit.")
                : CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Failed,
                    UnityEditModeTestFailureKind.ProcessFailure,
                    "Unity EditMode execution failed and its NUnit result artifact exceeds the bounded parser limit.");
        }

        byte[] bytes;
        string currentSha256;
        long currentLength;
        try
        {
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            currentLength = stream.Length;
            if (currentLength > MaxResultFileBytes)
            {
                return processSucceeded
                    ? CreateTerminalResult(
                        UnityEditModeTestValidationStatus.Indeterminate,
                        UnityEditModeTestFailureKind.ResultArtifactTooLarge,
                        "Unity EditMode execution cannot pass because its NUnit result artifact grew beyond the bounded parser limit.")
                    : CreateTerminalResult(
                        UnityEditModeTestValidationStatus.Failed,
                        UnityEditModeTestFailureKind.ProcessFailure,
                        "Unity EditMode execution failed and its NUnit result artifact grew beyond the bounded parser limit.");
            }

            bytes = new byte[checked((int)currentLength)];
            await stream.ReadExactlyAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (stream.Length != currentLength)
            {
                return processSucceeded
                    ? CreateTerminalResult(
                        UnityEditModeTestValidationStatus.Indeterminate,
                        UnityEditModeTestFailureKind.ResultArtifactInvalid,
                        "Unity EditMode execution cannot pass because its result artifact changed while it was being validated.")
                    : CreateTerminalResult(
                        UnityEditModeTestValidationStatus.Failed,
                        UnityEditModeTestFailureKind.ProcessFailure,
                        "Unity EditMode execution failed and its result artifact changed while it was being validated.");
            }

            currentSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return processSucceeded
                ? CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Indeterminate,
                    UnityEditModeTestFailureKind.ResultArtifactInvalid,
                    "Unity EditMode execution cannot pass because its result artifact could not be read safely.")
                : CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Failed,
                    UnityEditModeTestFailureKind.ProcessFailure,
                    "Unity EditMode execution failed and its result artifact could not be read safely.");
        }

        if (!string.Equals(entry.Sha256, currentSha256, StringComparison.OrdinalIgnoreCase) ||
            entry.Length != currentLength)
        {
            return processSucceeded
                ? CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Indeterminate,
                    UnityEditModeTestFailureKind.ResultArtifactInvalid,
                    "Unity EditMode execution cannot pass because the result artifact changed between safe artifact validation and parsing.")
                : CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Failed,
                    UnityEditModeTestFailureKind.ProcessFailure,
                    "Unity EditMode execution failed and the result artifact changed during validation.");
        }

        if (baseline.Existed && string.Equals(baseline.Sha256, currentSha256, StringComparison.OrdinalIgnoreCase))
        {
            return processSucceeded
                ? CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Indeterminate,
                    UnityEditModeTestFailureKind.StaleResultArtifact,
                    "Unity EditMode execution cannot pass because the result artifact is byte-identical to the pre-run artifact.")
                : CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Failed,
                    UnityEditModeTestFailureKind.ProcessFailure,
                    "Unity EditMode execution failed and did not produce demonstrably fresh result evidence.");
        }

        ParsedTestRun parsed;
        try
        {
            parsed = ParseTestRun(bytes);
        }
        catch (Exception exception) when (exception is XmlException or InvalidDataException)
        {
            return processSucceeded
                ? CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Indeterminate,
                    UnityEditModeTestFailureKind.ResultMalformed,
                    "Unity EditMode execution cannot pass because its NUnit result artifact is malformed or violates the supported contract.",
                    resultFileBytes: currentLength,
                    resultFileSha256: currentSha256)
                : CreateTerminalResult(
                    UnityEditModeTestValidationStatus.Failed,
                    UnityEditModeTestFailureKind.ProcessFailure,
                    "Unity EditMode execution failed and its NUnit result artifact is malformed or violates the supported contract.",
                    resultFileBytes: currentLength,
                    resultFileSha256: currentSha256);
        }

        if (parsed.Total == 0)
        {
            return processSucceeded
                ? CreateParsedResult(
                    UnityEditModeTestValidationStatus.Indeterminate,
                    UnityEditModeTestFailureKind.NoTestsDiscovered,
                    parsed,
                    currentLength,
                    currentSha256,
                    "Unity EditMode execution cannot pass because the NUnit artifact contains zero discovered tests.")
                : CreateParsedResult(
                    UnityEditModeTestValidationStatus.Failed,
                    UnityEditModeTestFailureKind.ProcessFailure,
                    parsed,
                    currentLength,
                    currentSha256,
                    "Unity EditMode execution failed and the NUnit artifact contains zero discovered tests.");
        }

        if (parsed.Failed > 0 || string.Equals(parsed.Result, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return CreateParsedResult(
                UnityEditModeTestValidationStatus.Failed,
                UnityEditModeTestFailureKind.TestsFailed,
                parsed,
                currentLength,
                currentSha256,
                "Unity EditMode test execution completed with one or more failed NUnit tests or suites.");
        }

        if (!string.Equals(parsed.Result, "Passed", StringComparison.OrdinalIgnoreCase))
        {
            return CreateParsedResult(
                UnityEditModeTestValidationStatus.Failed,
                UnityEditModeTestFailureKind.NonPassingResult,
                parsed,
                currentLength,
                currentSha256,
                "Unity EditMode test execution produced a non-passing NUnit result.");
        }

        if (!processSucceeded)
        {
            return CreateParsedResult(
                UnityEditModeTestValidationStatus.Failed,
                UnityEditModeTestFailureKind.ProcessFailure,
                parsed,
                currentLength,
                currentSha256,
                "Unity EditMode NUnit results passed, but the controlled Unity process did not complete successfully.");
        }

        return CreateParsedResult(
            UnityEditModeTestValidationStatus.Succeeded,
            UnityEditModeTestFailureKind.None,
            parsed,
            currentLength,
            currentSha256,
            "Unity EditMode tests succeeded from a successful controlled process and fresh, valid NUnit result evidence.");
    }

    private static UnityEditModeTestValidationResult CreateTerminalResult(
        UnityEditModeTestValidationStatus status,
        UnityEditModeTestFailureKind failureKind,
        string summary,
        long? resultFileBytes = null,
        string? resultFileSha256 = null) =>
        new(
            status,
            failureKind,
            total: 0,
            passed: 0,
            failed: 0,
            skipped: 0,
            inconclusive: 0,
            Array.Empty<UnityTestFailureDetail>(),
            failuresTruncated: false,
            nunitResult: null,
            resultFileBytes,
            resultFileSha256,
            summary);

    private static UnityEditModeTestValidationResult CreateParsedResult(
        UnityEditModeTestValidationStatus status,
        UnityEditModeTestFailureKind failureKind,
        ParsedTestRun parsed,
        long resultFileBytes,
        string resultFileSha256,
        string summary) =>
        new(
            status,
            failureKind,
            parsed.Total,
            parsed.Passed,
            parsed.Failed,
            parsed.Skipped,
            parsed.Inconclusive,
            parsed.Failures,
            parsed.FailuresTruncated,
            parsed.Result,
            resultFileBytes,
            resultFileSha256,
            summary);

    private static ToolArtifactManifest CreateManifest(string fullPath, bool requireNonEmpty)
    {
        var root = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Unity EditMode result path must have a filesystem parent directory.");
        }

        return new ToolArtifactManifest(
            root,
            [new ToolArtifactDefinition(
                ArtifactId,
                Path.GetFileName(fullPath),
                ToolArtifactKind.File,
                requireNonEmpty)]);
    }

    private static string NormalizeResultsPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!string.Equals(path, path.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unity EditMode result path must not contain leading or trailing whitespace.",
                nameof(path));
        }

        if (path.Contains('\0'))
        {
            throw new ArgumentException("Unity EditMode result path must not contain NUL characters.", nameof(path));
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Unity EditMode result path must be fully qualified.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), ".xml", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Unity EditMode result path must use an .xml extension.", nameof(path));
        }

        return fullPath;
    }

    private static ParsedTestRun ParseTestRun(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            MaxCharactersInDocument = MaxResultFileBytes,
        };

        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("The NUnit result document has no root element.");
        if (!string.Equals(root.Name.LocalName, "test-run", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unity EditMode results must use the verified NUnit3 test-run root.");
        }

        var result = root.Attribute("result")?.Value;
        if (string.IsNullOrWhiteSpace(result) ||
            !(string.Equals(result, "Passed", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(result, "Failed", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(result, "Skipped", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(result, "Inconclusive", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("The NUnit test-run result attribute is missing or unsupported.");
        }

        var total = ReadNonNegativeCount(root, "total");
        var passed = ReadNonNegativeCount(root, "passed");
        var failed = ReadNonNegativeCount(root, "failed");
        var skipped = ReadNonNegativeCount(root, "skipped");
        var inconclusive = ReadNonNegativeCount(root, "inconclusive");
        var accounted = (long)passed + failed + skipped + inconclusive;
        if (accounted != total)
        {
            throw new InvalidDataException("The NUnit test-run counters are internally inconsistent.");
        }

        if (string.Equals(result, "Passed", StringComparison.OrdinalIgnoreCase) && failed != 0)
        {
            throw new InvalidDataException("The NUnit test-run cannot report Passed while failed tests are nonzero.");
        }

        var failures = new List<UnityTestFailureDetail>();
        var failuresTruncated = false;
        foreach (var node in root.Descendants().Where(IsFailedTestNode))
        {
            if (failures.Count >= MaxRetainedFailures)
            {
                failuresTruncated = true;
                break;
            }

            failures.Add(CreateFailureDetail(node));
        }

        if (failed > failures.Count)
        {
            failuresTruncated = failuresTruncated || failures.Count >= MaxRetainedFailures;
        }

        return new ParsedTestRun(
            result,
            total,
            passed,
            failed,
            skipped,
            inconclusive,
            failures,
            failuresTruncated);
    }

    private static bool IsFailedTestNode(XElement element)
    {
        var localName = element.Name.LocalName;
        if (!string.Equals(localName, "test-case", StringComparison.Ordinal) &&
            !string.Equals(localName, "test-suite", StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(element.Attribute("result")?.Value, "Failed", StringComparison.OrdinalIgnoreCase);
    }

    private static UnityTestFailureDetail CreateFailureDetail(XElement element)
    {
        var name = element.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "(unnamed Unity test node)";
        }

        var fullName = NormalizeOptionalText(element.Attribute("fullname")?.Value);
        var failure = element.Elements()
            .FirstOrDefault(static child => string.Equals(child.Name.LocalName, "failure", StringComparison.Ordinal));
        var message = failure?.Elements()
            .FirstOrDefault(static child => string.Equals(child.Name.LocalName, "message", StringComparison.Ordinal))?.Value;
        var stackTrace = failure?.Elements()
            .FirstOrDefault(static child => string.Equals(child.Name.LocalName, "stack-trace", StringComparison.Ordinal))?.Value;

        var boundedMessage = BoundOptionalText(message, MaxFailureMessageCharacters, out var messageTruncated);
        var boundedStack = BoundOptionalText(stackTrace, MaxFailureStackCharacters, out var stackTruncated);
        return new UnityTestFailureDetail(
            name,
            fullName,
            boundedMessage,
            boundedStack,
            messageTruncated,
            stackTruncated);
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? BoundOptionalText(string? value, int maxCharacters, out bool truncated)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            truncated = false;
            return null;
        }

        if (value.Length <= maxCharacters)
        {
            truncated = false;
            return value;
        }

        truncated = true;
        return value[..maxCharacters];
    }

    private static int ReadNonNegativeCount(XElement root, string attributeName)
    {
        var text = root.Attribute(attributeName)?.Value;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            throw new InvalidDataException($"The NUnit test-run '{attributeName}' count is missing or invalid.");
        }

        return value;
    }

    private sealed record ParsedTestRun(
        string Result,
        int Total,
        int Passed,
        int Failed,
        int Skipped,
        int Inconclusive,
        IReadOnlyList<UnityTestFailureDetail> Failures,
        bool FailuresTruncated);
}
