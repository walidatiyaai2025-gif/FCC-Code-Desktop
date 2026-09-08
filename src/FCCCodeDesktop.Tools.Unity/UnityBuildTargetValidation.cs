using FCCCodeDesktop.Tools;

namespace FCCCodeDesktop.Tools.Unity;

public enum UnityBuildTargetValidationStatus
{
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
    Indeterminate = 4,
}

public enum UnityBuildTargetFailureKind
{
    None = 0,
    Cancellation = 1,
    ProcessOrBuildResultFailure = 2,
    OutputArtifactUnavailable = 3,
    OutputArtifactInvalid = 4,
    StaleOutputArtifact = 5,
}

/// <summary>
/// Pre-run evidence used to reject stale build outputs while retaining the P10-009 structured-result baseline.
/// </summary>
public sealed record UnityBuildTargetBaseline
{
    internal UnityBuildTargetBaseline(
        UnityEditorAutomationArtifactBaseline automationBaseline,
        bool outputExisted,
        long? outputLength,
        string? outputSha256)
    {
        AutomationBaseline = automationBaseline ?? throw new ArgumentNullException(nameof(automationBaseline));
        OutputExisted = outputExisted;
        OutputLength = outputLength;
        OutputSha256 = outputSha256;
    }

    public UnityEditorAutomationArtifactBaseline AutomationBaseline { get; }

    public bool OutputExisted { get; }

    public long? OutputLength { get; }

    public string? OutputSha256 { get; }
}

/// <summary>
/// Structured terminal evidence for a first-class Unity build target operation.
/// A successful process/build-result artifact is necessary but insufficient: the expected output must also be safe,
/// non-empty and demonstrably fresh.
/// </summary>
public sealed record UnityBuildTargetValidationResult
{
    internal UnityBuildTargetValidationResult(
        UnityBuildTargetValidationStatus status,
        UnityBuildTargetFailureKind failureKind,
        UnityBuildTarget target,
        Guid operationId,
        string methodName,
        long? outputBytes,
        string? outputSha256,
        string summary)
    {
        Status = status;
        FailureKind = failureKind;
        Target = target;
        OperationId = operationId;
        MethodName = methodName;
        OutputBytes = outputBytes;
        OutputSha256 = outputSha256;
        Summary = summary;
    }

    public UnityBuildTargetValidationStatus Status { get; }

    public UnityBuildTargetFailureKind FailureKind { get; }

    public UnityBuildTarget Target { get; }

    public Guid OperationId { get; }

    public string MethodName { get; }

    public long? OutputBytes { get; }

    public string? OutputSha256 { get; }

    public string Summary { get; }
}

public interface IUnityBuildTargetValidator
{
    Task<UnityBuildTargetBaseline> CaptureBaselineAsync(
        UnityBuildTargetInvocationPlan plan,
        CancellationToken cancellationToken = default);

    Task<UnityBuildTargetValidationResult> ValidateAsync(
        ToolProcessResult processResult,
        UnityBuildTargetInvocationPlan plan,
        UnityBuildTargetBaseline baseline,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fail-closed Unity build validator. It composes the P10-009 structured Editor-automation validator with the
/// provider-neutral P09 artifact validator so process exit code or structured status alone can never establish build success.
/// </summary>
public sealed class UnityBuildTargetValidator : IUnityBuildTargetValidator
{
    private const string ArtifactId = "unity-build-output";

    private readonly IUnityEditorAutomationValidator _automationValidator;
    private readonly IToolArtifactValidator _artifactValidator;

    public UnityBuildTargetValidator()
        : this(new UnityEditorAutomationValidator(), new ToolArtifactValidator())
    {
    }

    public UnityBuildTargetValidator(
        IUnityEditorAutomationValidator automationValidator,
        IToolArtifactValidator artifactValidator)
    {
        _automationValidator = automationValidator ?? throw new ArgumentNullException(nameof(automationValidator));
        _artifactValidator = artifactValidator ?? throw new ArgumentNullException(nameof(artifactValidator));
    }

    public async Task<UnityBuildTargetBaseline> CaptureBaselineAsync(
        UnityBuildTargetInvocationPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var automationBaseline = await _automationValidator
            .CaptureBaselineAsync(plan.ResultFilePath, cancellationToken)
            .ConfigureAwait(false);

        var report = await _artifactValidator
            .ValidateAsync(CreateOutputManifest(plan, requireNonEmpty: false), cancellationToken)
            .ConfigureAwait(false);
        var entry = report.Entries.Single();
        if (entry.Status == ToolArtifactValidationStatus.Missing)
        {
            return new UnityBuildTargetBaseline(automationBaseline, false, null, null);
        }

        if (entry.Status != ToolArtifactValidationStatus.Valid)
        {
            throw new InvalidOperationException(
                $"A safe Unity build output baseline could not be established ({entry.Status}).");
        }

        return new UnityBuildTargetBaseline(
            automationBaseline,
            true,
            entry.Length,
            entry.Sha256);
    }

    public async Task<UnityBuildTargetValidationResult> ValidateAsync(
        ToolProcessResult processResult,
        UnityBuildTargetInvocationPlan plan,
        UnityBuildTargetBaseline baseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processResult);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(baseline);

        var automation = await _automationValidator
            .ValidateAsync(processResult, plan.AutomationPlan, baseline.AutomationBaseline, cancellationToken)
            .ConfigureAwait(false);

        if (automation.Status == UnityEditorAutomationValidationStatus.Cancelled)
        {
            return Create(
                plan,
                UnityBuildTargetValidationStatus.Cancelled,
                UnityBuildTargetFailureKind.Cancellation,
                "Unity build was cancelled before successful fresh build evidence was established.");
        }

        if (automation.Status != UnityEditorAutomationValidationStatus.Succeeded)
        {
            return Create(
                plan,
                automation.Status == UnityEditorAutomationValidationStatus.Indeterminate
                    ? UnityBuildTargetValidationStatus.Indeterminate
                    : UnityBuildTargetValidationStatus.Failed,
                UnityBuildTargetFailureKind.ProcessOrBuildResultFailure,
                "Unity build cannot pass because the controlled process and fresh structured build result did not both succeed.");
        }

        var report = await _artifactValidator
            .ValidateAsync(CreateOutputManifest(plan, requireNonEmpty: true), cancellationToken)
            .ConfigureAwait(false);
        var entry = report.Entries.Single();
        if (entry.Status != ToolArtifactValidationStatus.Valid)
        {
            var failureKind = entry.Status is ToolArtifactValidationStatus.Missing or ToolArtifactValidationStatus.Empty
                ? UnityBuildTargetFailureKind.OutputArtifactUnavailable
                : UnityBuildTargetFailureKind.OutputArtifactInvalid;
            return Create(
                plan,
                UnityBuildTargetValidationStatus.Failed,
                failureKind,
                "Unity build reported success but the expected output artifact is missing, empty, unsafe, unreadable, or has the wrong filesystem type.");
        }

        if (baseline.OutputExisted)
        {
            if (plan.OutputArtifactKind == ToolArtifactKind.Directory)
            {
                return Create(
                    plan,
                    UnityBuildTargetValidationStatus.Indeterminate,
                    UnityBuildTargetFailureKind.StaleOutputArtifact,
                    "Unity build output directory pre-existed the operation, so freshness cannot be proven. Use a fresh operation-owned output directory.");
            }

            if (entry.Length == baseline.OutputLength &&
                string.Equals(entry.Sha256, baseline.OutputSha256, StringComparison.OrdinalIgnoreCase))
            {
                return Create(
                    plan,
                    UnityBuildTargetValidationStatus.Indeterminate,
                    UnityBuildTargetFailureKind.StaleOutputArtifact,
                    "Unity build output is byte-identical to the pre-run artifact and cannot prove a fresh build.",
                    entry.Length,
                    entry.Sha256);
            }
        }

        return Create(
            plan,
            UnityBuildTargetValidationStatus.Succeeded,
            UnityBuildTargetFailureKind.None,
            "Unity build succeeded from a successful controlled process, fresh matching structured result evidence, and a safe non-empty fresh output artifact.",
            entry.Length,
            entry.Sha256);
    }

    private static ToolArtifactManifest CreateOutputManifest(
        UnityBuildTargetInvocationPlan plan,
        bool requireNonEmpty)
    {
        var normalized = Path.GetFullPath(plan.OutputArtifactPath);
        var trimmed = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(trimmed);
        var leaf = Path.GetFileName(trimmed);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(leaf))
        {
            throw new InvalidOperationException("Unity build output must identify an artifact beneath a fully-qualified parent directory.");
        }

        return new ToolArtifactManifest(
            parent,
            new[]
            {
                new ToolArtifactDefinition(
                    ArtifactId,
                    leaf,
                    plan.OutputArtifactKind,
                    requireNonEmpty),
            });
    }

    private static UnityBuildTargetValidationResult Create(
        UnityBuildTargetInvocationPlan plan,
        UnityBuildTargetValidationStatus status,
        UnityBuildTargetFailureKind failureKind,
        string summary,
        long? outputBytes = null,
        string? outputSha256 = null) =>
        new(
            status,
            failureKind,
            plan.Target,
            plan.OperationId,
            plan.MethodName,
            outputBytes,
            outputSha256,
            summary);
}
