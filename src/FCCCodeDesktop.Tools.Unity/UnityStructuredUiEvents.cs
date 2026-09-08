using System.Globalization;

namespace FCCCodeDesktop.Tools.Unity;

public enum UnityUiEventKind
{
    Diagnostic = 1,
    Progress = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    Indeterminate = 6,
}

public enum UnityUiEventSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>
/// Bounded, provider-neutral event intended for presentation/activity surfaces.
/// It deliberately contains no raw command line, environment, stdout/stderr, or local path.
/// </summary>
public sealed record UnityStructuredUiEvent
{
    public const int MaxCodeCharacters = 128;
    public const int MaxMessageCharacters = 4096;

    public UnityStructuredUiEvent(
        Guid operationId,
        long sequence,
        UnityUiEventKind kind,
        UnityUiEventSeverity severity,
        string code,
        string message,
        bool messageWasTruncated = false,
        double? progressPercent = null,
        long? artifactBytes = null,
        string? artifactSha256 = null)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Unity UI event operation identity cannot be empty.", nameof(operationId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }

        code = NormalizeCode(code);
        message = NormalizeMessage(message, out var normalizedTruncated);
        if (progressPercent is < 0 or > 100 || double.IsNaN(progressPercent ?? 0) || double.IsInfinity(progressPercent ?? 0))
        {
            throw new ArgumentOutOfRangeException(nameof(progressPercent));
        }

        if (artifactBytes is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(artifactBytes));
        }

        if (artifactSha256 is not null && !IsSha256(artifactSha256))
        {
            throw new ArgumentException("Artifact SHA-256 must be 64 hexadecimal characters.", nameof(artifactSha256));
        }

        OperationId = operationId;
        Sequence = sequence;
        Kind = kind;
        Severity = severity;
        Code = code;
        Message = message;
        MessageWasTruncated = messageWasTruncated || normalizedTruncated;
        ProgressPercent = progressPercent;
        ArtifactBytes = artifactBytes;
        ArtifactSha256 = artifactSha256?.ToLowerInvariant();
    }

    public Guid OperationId { get; }
    public long Sequence { get; }
    public UnityUiEventKind Kind { get; }
    public UnityUiEventSeverity Severity { get; }
    public string Code { get; }
    public string Message { get; }
    public bool MessageWasTruncated { get; }
    public double? ProgressPercent { get; }
    public long? ArtifactBytes { get; }
    public string? ArtifactSha256 { get; }

    private static string NormalizeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        code = code.Trim();
        if (code.Length > MaxCodeCharacters)
        {
            throw new ArgumentOutOfRangeException(nameof(code), "Unity UI event code is too long.");
        }

        foreach (var character in code)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'))
            {
                throw new ArgumentException("Unity UI event code contains an unsupported character.", nameof(code));
            }
        }

        return code;
    }

    private static string NormalizeMessage(string message, out bool wasTruncated)
    {
        ArgumentNullException.ThrowIfNull(message);
        message = message.Replace('\0', '\uFFFD');
        wasTruncated = message.Length > MaxMessageCharacters;
        return wasTruncated ? message[..MaxMessageCharacters] : message;
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Converts P10 Unity log/automation/build outcomes into bounded UI events without exposing execution internals.
/// </summary>
public static class UnityStructuredUiEventProjector
{
    public static UnityStructuredUiEvent FromLog(Guid operationId, UnityLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var (severity, code) = entry.Severity switch
        {
            UnityLogSeverity.Warning => (UnityUiEventSeverity.Warning, "unity.log.warning"),
            UnityLogSeverity.Error => (UnityUiEventSeverity.Error, "unity.log.error"),
            UnityLogSeverity.Exception => (UnityUiEventSeverity.Error, "unity.log.exception"),
            UnityLogSeverity.Assert => (UnityUiEventSeverity.Error, "unity.log.assert"),
            _ => (UnityUiEventSeverity.Info, "unity.log.info"),
        };

        return new UnityStructuredUiEvent(
            operationId,
            entry.Sequence,
            UnityUiEventKind.Diagnostic,
            severity,
            code,
            entry.Text,
            entry.IsTruncated || entry.HadEncodingErrors);
    }

    public static UnityStructuredUiEvent Progress(
        Guid operationId,
        long sequence,
        string stage,
        double progressPercent,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        var normalizedStage = string.Concat(stage.Trim().Select(static c =>
            char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(normalizedStage))
        {
            throw new ArgumentException("Unity progress stage must contain an ASCII letter or digit.", nameof(stage));
        }

        return new UnityStructuredUiEvent(
            operationId,
            sequence,
            UnityUiEventKind.Progress,
            UnityUiEventSeverity.Info,
            $"unity.progress.{normalizedStage}",
            message,
            progressPercent: progressPercent);
    }

    public static UnityStructuredUiEvent FromAutomation(UnityEditorAutomationValidationResult result, long sequence)
    {
        ArgumentNullException.ThrowIfNull(result);
        var kind = result.Status switch
        {
            UnityEditorAutomationValidationStatus.Succeeded => UnityUiEventKind.Completed,
            UnityEditorAutomationValidationStatus.Failed => UnityUiEventKind.Failed,
            UnityEditorAutomationValidationStatus.Cancelled => UnityUiEventKind.Cancelled,
            _ => UnityUiEventKind.Indeterminate,
        };
        var severity = result.Status == UnityEditorAutomationValidationStatus.Succeeded
            ? UnityUiEventSeverity.Info
            : result.Status == UnityEditorAutomationValidationStatus.Cancelled
                ? UnityUiEventSeverity.Warning
                : UnityUiEventSeverity.Error;
        var code = result.FailureKind == UnityEditorAutomationFailureKind.None
            ? "unity.automation.succeeded"
            : $"unity.automation.{ToCodeToken(result.FailureKind)}";

        return new UnityStructuredUiEvent(
            result.OperationId,
            sequence,
            kind,
            severity,
            code,
            result.Message ?? result.Summary,
            result.MessageWasTruncated);
    }

    public static UnityStructuredUiEvent FromBuild(UnityBuildTargetValidationResult result, long sequence)
    {
        ArgumentNullException.ThrowIfNull(result);
        var kind = result.Status switch
        {
            UnityBuildTargetValidationStatus.Succeeded => UnityUiEventKind.Completed,
            UnityBuildTargetValidationStatus.Failed => UnityUiEventKind.Failed,
            UnityBuildTargetValidationStatus.Cancelled => UnityUiEventKind.Cancelled,
            _ => UnityUiEventKind.Indeterminate,
        };
        var severity = result.Status == UnityBuildTargetValidationStatus.Succeeded
            ? UnityUiEventSeverity.Info
            : result.Status == UnityBuildTargetValidationStatus.Cancelled
                ? UnityUiEventSeverity.Warning
                : UnityUiEventSeverity.Error;
        var code = result.FailureKind == UnityBuildTargetFailureKind.None
            ? $"unity.build.{ToCodeToken(result.Target)}.succeeded"
            : $"unity.build.{ToCodeToken(result.Target)}.{ToCodeToken(result.FailureKind)}";

        return new UnityStructuredUiEvent(
            result.OperationId,
            sequence,
            kind,
            severity,
            code,
            result.Summary,
            artifactBytes: result.OutputBytes,
            artifactSha256: result.OutputSha256);
    }

    private static string ToCodeToken<T>(T value) where T : struct, Enum
    {
        var text = value.ToString();
        var builder = new System.Text.StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var current = text[i];
            if (char.IsUpper(current) && i > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
