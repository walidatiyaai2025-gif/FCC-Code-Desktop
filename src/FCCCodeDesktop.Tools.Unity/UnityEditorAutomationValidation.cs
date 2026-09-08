using System.Security.Cryptography;
using System.Text.Json;
using FCCCodeDesktop.Tools;

namespace FCCCodeDesktop.Tools.Unity;

public enum UnityEditorAutomationValidationStatus
{
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
    Indeterminate = 4,
}

public enum UnityEditorAutomationFailureKind
{
    None = 0,
    Cancellation = 1,
    ProcessFailure = 2,
    ResultArtifactUnavailable = 3,
    ResultArtifactInvalid = 4,
    ResultArtifactTooLarge = 5,
    StaleResultArtifact = 6,
    ResultMalformed = 7,
    ContractMismatch = 8,
    AutomationReportedFailure = 9,
}

public enum UnityEditorAutomationReportedStatus
{
    Succeeded = 1,
    Failed = 2,
}

/// <summary>
/// Pre-run fingerprint that prevents a successful-looking result from an earlier Editor invocation being reused.
/// The local path remains internal and is not intended for UI/diagnostic exposure.
/// </summary>
public sealed record UnityEditorAutomationArtifactBaseline
{
    internal UnityEditorAutomationArtifactBaseline(
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
/// Structured terminal evidence for a project-owned Unity Editor automation entry point.
/// Success requires both a successful controlled process and a fresh matching schema-v1 result artifact.
/// </summary>
public sealed record UnityEditorAutomationValidationResult
{
    internal UnityEditorAutomationValidationResult(
        UnityEditorAutomationValidationStatus status,
        UnityEditorAutomationFailureKind failureKind,
        Guid operationId,
        string methodName,
        UnityEditorAutomationReportedStatus? reportedStatus,
        string? message,
        bool messageWasTruncated,
        long? resultFileBytes,
        string? resultFileSha256,
        string summary)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "A concrete Unity automation validation status is required.");
        }

        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "A concrete Unity automation failure kind is required.");
        }

        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Unity automation operation identity cannot be empty.", nameof(operationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        if (reportedStatus is not null && !Enum.IsDefined(reportedStatus.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(reportedStatus), reportedStatus, "A valid reported automation status is required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        Status = status;
        FailureKind = failureKind;
        OperationId = operationId;
        MethodName = methodName;
        ReportedStatus = reportedStatus;
        Message = message;
        MessageWasTruncated = messageWasTruncated;
        ResultFileBytes = resultFileBytes;
        ResultFileSha256 = resultFileSha256;
        Summary = summary;
    }

    public UnityEditorAutomationValidationStatus Status { get; }

    public UnityEditorAutomationFailureKind FailureKind { get; }

    public Guid OperationId { get; }

    public string MethodName { get; }

    public UnityEditorAutomationReportedStatus? ReportedStatus { get; }

    public string? Message { get; }

    public bool MessageWasTruncated { get; }

    public long? ResultFileBytes { get; }

    public string? ResultFileSha256 { get; }

    public string Summary { get; }
}

public interface IUnityEditorAutomationValidator
{
    Task<UnityEditorAutomationArtifactBaseline> CaptureBaselineAsync(
        string resultFilePath,
        CancellationToken cancellationToken = default);

    Task<UnityEditorAutomationValidationResult> ValidateAsync(
        ToolProcessResult processResult,
        UnityEditorAutomationInvocationPlan plan,
        UnityEditorAutomationArtifactBaseline baseline,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fail-closed validator for project-owned Unity Editor automation. It reuses the P09 artifact validator,
/// rejects stale/mutating evidence, parses a bounded strict JSON contract, and never treats exit code zero alone as success.
/// </summary>
public sealed class UnityEditorAutomationValidator : IUnityEditorAutomationValidator
{
    public const long MaxResultFileBytes = 256L * 1024L;

    private const int ResultSchemaVersion = 1;
    private const int MaxMessageCharacters = 4096;
    private const string ExecuteMethodOperation = "unity.execute-method";
    private const string ArtifactId = "unity-editor-automation-result";

    private readonly IToolArtifactValidator _artifactValidator;

    public UnityEditorAutomationValidator()
        : this(new ToolArtifactValidator())
    {
    }

    public UnityEditorAutomationValidator(IToolArtifactValidator artifactValidator)
    {
        _artifactValidator = artifactValidator ?? throw new ArgumentNullException(nameof(artifactValidator));
    }

    public async Task<UnityEditorAutomationArtifactBaseline> CaptureBaselineAsync(
        string resultFilePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = NormalizeResultPath(resultFilePath);
        var report = await _artifactValidator
            .ValidateAsync(CreateManifest(fullPath, requireNonEmpty: false), cancellationToken)
            .ConfigureAwait(false);
        var entry = report.Entries.Single();

        if (entry.Status == ToolArtifactValidationStatus.Missing)
        {
            return new UnityEditorAutomationArtifactBaseline(fullPath, existed: false, length: null, sha256: null);
        }

        if (entry.Status != ToolArtifactValidationStatus.Valid || entry.Length is null || entry.Sha256 is null)
        {
            throw new InvalidOperationException(
                $"A safe Unity Editor automation result baseline could not be established ({entry.Status}).");
        }

        return new UnityEditorAutomationArtifactBaseline(fullPath, existed: true, entry.Length, entry.Sha256);
    }

    public async Task<UnityEditorAutomationValidationResult> ValidateAsync(
        ToolProcessResult processResult,
        UnityEditorAutomationInvocationPlan plan,
        UnityEditorAutomationArtifactBaseline baseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processResult);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(baseline);

        if (!string.Equals(processResult.Operation, ExecuteMethodOperation, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unity Editor automation validation requires operation '{ExecuteMethodOperation}'.",
                nameof(processResult));
        }

        var fullPath = NormalizeResultPath(plan.ResultFilePath);
        if (!string.Equals(fullPath, baseline.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The Unity Editor automation result baseline must have been captured for the plan result path.",
                nameof(baseline));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (processResult.Status == ToolResultStatus.Cancelled)
        {
            return CreateTerminalResult(
                plan,
                UnityEditorAutomationValidationStatus.Cancelled,
                UnityEditorAutomationFailureKind.Cancellation,
                "Unity Editor automation was cancelled before successful result evidence was established.");
        }

        if (processResult.LaunchStatus != ToolProcessLaunchStatus.Started || processResult.ForcedTerminationRequested)
        {
            return CreateTerminalResult(
                plan,
                UnityEditorAutomationValidationStatus.Failed,
                UnityEditorAutomationFailureKind.ProcessFailure,
                "Unity Editor automation failed because the controlled Unity process did not start normally or required forced termination.");
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
                    plan,
                    UnityEditorAutomationValidationStatus.Failed,
                    UnityEditorAutomationFailureKind.ProcessFailure,
                    "Unity Editor automation process failed and did not leave a safe, readable, non-empty structured result artifact.");
            }

            var kind = entry.Status is ToolArtifactValidationStatus.Missing or ToolArtifactValidationStatus.Empty
                ? UnityEditorAutomationFailureKind.ResultArtifactUnavailable
                : UnityEditorAutomationFailureKind.ResultArtifactInvalid;
            return CreateTerminalResult(
                plan,
                UnityEditorAutomationValidationStatus.Indeterminate,
                kind,
                "Unity Editor automation cannot pass because its structured result artifact is missing, empty, unsafe, or unreadable.");
        }

        if (entry.Length > MaxResultFileBytes)
        {
            return processSucceeded
                ? CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Indeterminate,
                    UnityEditorAutomationFailureKind.ResultArtifactTooLarge,
                    "Unity Editor automation cannot pass because its structured result artifact exceeds the bounded parser limit.")
                : CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Failed,
                    UnityEditorAutomationFailureKind.ProcessFailure,
                    "Unity Editor automation process failed and its result artifact exceeds the bounded parser limit.");
        }

        var read = await ReadStableArtifactAsync(fullPath, processSucceeded, plan, cancellationToken).ConfigureAwait(false);
        if (read.TerminalResult is not null)
        {
            return read.TerminalResult;
        }

        if (read.Bytes is null || read.Sha256 is null || read.Length is null)
        {
            throw new InvalidOperationException("Unity Editor automation stable artifact read returned incomplete internal evidence.");
        }

        if (!string.Equals(entry.Sha256, read.Sha256, StringComparison.OrdinalIgnoreCase) ||
            entry.Length != read.Length.Value)
        {
            return processSucceeded
                ? CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Indeterminate,
                    UnityEditorAutomationFailureKind.ResultArtifactInvalid,
                    "Unity Editor automation cannot pass because the result artifact changed between safe artifact validation and parsing.")
                : CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Failed,
                    UnityEditorAutomationFailureKind.ProcessFailure,
                    "Unity Editor automation process failed and its result artifact changed during validation.");
        }

        if (baseline.Existed && string.Equals(baseline.Sha256, read.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return processSucceeded
                ? CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Indeterminate,
                    UnityEditorAutomationFailureKind.StaleResultArtifact,
                    "Unity Editor automation cannot pass because its result artifact is byte-identical to the pre-run artifact.")
                : CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Failed,
                    UnityEditorAutomationFailureKind.ProcessFailure,
                    "Unity Editor automation process failed and did not produce demonstrably fresh structured evidence.");
        }

        ParsedAutomationResult parsed;
        try
        {
            parsed = ParseResult(read.Bytes, plan);
        }
        catch (JsonException)
        {
            return processSucceeded
                ? CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Indeterminate,
                    UnityEditorAutomationFailureKind.ResultMalformed,
                    "Unity Editor automation cannot pass because its structured result artifact is malformed JSON.",
                    read.Length,
                    read.Sha256)
                : CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Failed,
                    UnityEditorAutomationFailureKind.ProcessFailure,
                    "Unity Editor automation process failed and its structured result artifact is malformed JSON.",
                    read.Length,
                    read.Sha256);
        }
        catch (InvalidDataException)
        {
            return processSucceeded
                ? CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Indeterminate,
                    UnityEditorAutomationFailureKind.ContractMismatch,
                    "Unity Editor automation cannot pass because its result artifact does not match the requested operation contract.",
                    read.Length,
                    read.Sha256)
                : CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Failed,
                    UnityEditorAutomationFailureKind.ProcessFailure,
                    "Unity Editor automation process failed and its result artifact does not match the requested operation contract.",
                    read.Length,
                    read.Sha256);
        }

        if (parsed.Status == UnityEditorAutomationReportedStatus.Failed)
        {
            return CreateParsedResult(
                plan,
                UnityEditorAutomationValidationStatus.Failed,
                UnityEditorAutomationFailureKind.AutomationReportedFailure,
                parsed,
                read.Length.Value,
                read.Sha256,
                "The project-owned Unity Editor automation entry point reported failure.");
        }

        if (!processSucceeded)
        {
            return CreateParsedResult(
                plan,
                UnityEditorAutomationValidationStatus.Failed,
                UnityEditorAutomationFailureKind.ProcessFailure,
                parsed,
                read.Length.Value,
                read.Sha256,
                "The project-owned Unity Editor automation artifact reported success, but the controlled Unity process did not complete successfully.");
        }

        return CreateParsedResult(
            plan,
            UnityEditorAutomationValidationStatus.Succeeded,
            UnityEditorAutomationFailureKind.None,
            parsed,
            read.Length.Value,
            read.Sha256,
            "Unity Editor automation succeeded from a successful controlled process and fresh matching structured result evidence.");
    }

    private static async Task<StableArtifactRead> ReadStableArtifactAsync(
        string fullPath,
        bool processSucceeded,
        UnityEditorAutomationInvocationPlan plan,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var length = stream.Length;
            if (length > MaxResultFileBytes)
            {
                var result = processSucceeded
                    ? CreateTerminalResult(
                        plan,
                        UnityEditorAutomationValidationStatus.Indeterminate,
                        UnityEditorAutomationFailureKind.ResultArtifactTooLarge,
                        "Unity Editor automation cannot pass because its result artifact grew beyond the bounded parser limit.")
                    : CreateTerminalResult(
                        plan,
                        UnityEditorAutomationValidationStatus.Failed,
                        UnityEditorAutomationFailureKind.ProcessFailure,
                        "Unity Editor automation process failed and its result artifact grew beyond the bounded parser limit.");
                return new StableArtifactRead(null, null, null, result);
            }

            var bytes = new byte[checked((int)length)];
            await stream.ReadExactlyAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (stream.Length != length)
            {
                var result = processSucceeded
                    ? CreateTerminalResult(
                        plan,
                        UnityEditorAutomationValidationStatus.Indeterminate,
                        UnityEditorAutomationFailureKind.ResultArtifactInvalid,
                        "Unity Editor automation cannot pass because its result artifact changed while it was being validated.")
                    : CreateTerminalResult(
                        plan,
                        UnityEditorAutomationValidationStatus.Failed,
                        UnityEditorAutomationFailureKind.ProcessFailure,
                        "Unity Editor automation process failed and its result artifact changed while it was being validated.");
                return new StableArtifactRead(null, null, null, result);
            }

            var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            return new StableArtifactRead(bytes, length, sha256, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var result = processSucceeded
                ? CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Indeterminate,
                    UnityEditorAutomationFailureKind.ResultArtifactInvalid,
                    "Unity Editor automation cannot pass because its result artifact could not be read safely.")
                : CreateTerminalResult(
                    plan,
                    UnityEditorAutomationValidationStatus.Failed,
                    UnityEditorAutomationFailureKind.ProcessFailure,
                    "Unity Editor automation process failed and its result artifact could not be read safely.");
            return new StableArtifactRead(null, null, null, result);
        }
    }

    private static ParsedAutomationResult ParseResult(
        byte[] bytes,
        UnityEditorAutomationInvocationPlan plan)
    {
        using var document = JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16,
            });

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Unity Editor automation result root must be an object.");
        }

        JsonElement? schemaVersion = null;
        JsonElement? operationId = null;
        JsonElement? methodName = null;
        JsonElement? status = null;
        JsonElement? message = null;
        var observed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in root.EnumerateObject())
        {
            if (!observed.Add(property.Name))
            {
                throw new InvalidDataException($"Unity Editor automation result property '{property.Name}' is duplicated.");
            }

            switch (property.Name)
            {
                case "schemaVersion":
                    schemaVersion = property.Value;
                    break;
                case "operationId":
                    operationId = property.Value;
                    break;
                case "methodName":
                    methodName = property.Value;
                    break;
                case "status":
                    status = property.Value;
                    break;
                case "message":
                    message = property.Value;
                    break;
                default:
                    throw new InvalidDataException(
                        $"Unity Editor automation schema v{ResultSchemaVersion} does not define property '{property.Name}'.");
            }
        }

        if (schemaVersion is null ||
            schemaVersion.Value.ValueKind != JsonValueKind.Number ||
            !schemaVersion.Value.TryGetInt32(out var parsedSchemaVersion) ||
            parsedSchemaVersion != ResultSchemaVersion)
        {
            throw new InvalidDataException("Unity Editor automation result schemaVersion is missing or unsupported.");
        }

        if (operationId is null || operationId.Value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Unity Editor automation result operationId is missing or invalid.");
        }

        var operationText = operationId.Value.GetString();
        if (!Guid.TryParseExact(operationText, "D", out var parsedOperationId) || parsedOperationId != plan.OperationId)
        {
            throw new InvalidDataException("Unity Editor automation result operationId does not match the invocation.");
        }

        if (methodName is null || methodName.Value.ValueKind != JsonValueKind.String ||
            !string.Equals(methodName.Value.GetString(), plan.MethodName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unity Editor automation result methodName does not match the invocation.");
        }

        if (status is null || status.Value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Unity Editor automation result status is missing or invalid.");
        }

        var reportedStatus = status.Value.GetString() switch
        {
            "succeeded" => UnityEditorAutomationReportedStatus.Succeeded,
            "failed" => UnityEditorAutomationReportedStatus.Failed,
            _ => throw new InvalidDataException("Unity Editor automation result status is unsupported."),
        };

        string? parsedMessage = null;
        var messageWasTruncated = false;
        if (message is not null)
        {
            if (message.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("Unity Editor automation result message must be a string when supplied.");
            }

            parsedMessage = message.Value.GetString();
            if (parsedMessage is not null && parsedMessage.Contains('\0'))
            {
                throw new InvalidDataException("Unity Editor automation result message must not contain NUL characters.");
            }

            if (parsedMessage?.Length > MaxMessageCharacters)
            {
                parsedMessage = parsedMessage[..MaxMessageCharacters];
                messageWasTruncated = true;
            }
        }

        return new ParsedAutomationResult(reportedStatus, parsedMessage, messageWasTruncated);
    }

    private static ToolArtifactManifest CreateManifest(string fullPath, bool requireNonEmpty)
    {
        var root = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Unity Editor automation result path must have a filesystem parent directory.");
        }

        return new ToolArtifactManifest(
            root,
            [new ToolArtifactDefinition(
                ArtifactId,
                Path.GetFileName(fullPath),
                ToolArtifactKind.File,
                requireNonEmpty)]);
    }

    private static string NormalizeResultPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!string.Equals(path, path.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unity Editor automation result path must not contain leading or trailing whitespace.",
                nameof(path));
        }

        if (path.Contains('\0'))
        {
            throw new ArgumentException(
                "Unity Editor automation result path must not contain NUL characters.",
                nameof(path));
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                "Unity Editor automation result path must be fully qualified.",
                nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Unity Editor automation result path must use a .json extension.",
                nameof(path));
        }

        return fullPath;
    }

    private static UnityEditorAutomationValidationResult CreateTerminalResult(
        UnityEditorAutomationInvocationPlan plan,
        UnityEditorAutomationValidationStatus status,
        UnityEditorAutomationFailureKind failureKind,
        string summary,
        long? resultFileBytes = null,
        string? resultFileSha256 = null) =>
        new(
            status,
            failureKind,
            plan.OperationId,
            plan.MethodName,
            reportedStatus: null,
            message: null,
            messageWasTruncated: false,
            resultFileBytes,
            resultFileSha256,
            summary);

    private static UnityEditorAutomationValidationResult CreateParsedResult(
        UnityEditorAutomationInvocationPlan plan,
        UnityEditorAutomationValidationStatus status,
        UnityEditorAutomationFailureKind failureKind,
        ParsedAutomationResult parsed,
        long resultFileBytes,
        string resultFileSha256,
        string summary) =>
        new(
            status,
            failureKind,
            plan.OperationId,
            plan.MethodName,
            parsed.Status,
            parsed.Message,
            parsed.MessageWasTruncated,
            resultFileBytes,
            resultFileSha256,
            summary);

    private sealed record ParsedAutomationResult(
        UnityEditorAutomationReportedStatus Status,
        string? Message,
        bool MessageWasTruncated);

    private sealed record StableArtifactRead(
        byte[]? Bytes,
        long? Length,
        string? Sha256,
        UnityEditorAutomationValidationResult? TerminalResult);
}
