using System.Collections.ObjectModel;
using FCCCodeDesktop.Tools;

namespace FCCCodeDesktop.Tools.Unity;

public enum UnityPlayModeTestValidationStatus
{
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
    Indeterminate = 4,
}

public enum UnityPlayModeTestFailureKind
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
/// Structured terminal result for one Unity PlayMode invocation.
/// Success requires a successful controlled process and fresh, valid NUnit3 evidence.
/// </summary>
public sealed record UnityPlayModeTestValidationResult
{
    private readonly ReadOnlyCollection<UnityTestFailureDetail> _failures;

    internal UnityPlayModeTestValidationResult(
        UnityPlayModeTestValidationStatus status,
        UnityPlayModeTestFailureKind failureKind,
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
            throw new ArgumentOutOfRangeException(nameof(status), status, "A concrete PlayMode validation status is required.");
        }

        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "A concrete PlayMode failure kind is required.");
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

    public UnityPlayModeTestValidationStatus Status { get; }

    public UnityPlayModeTestFailureKind FailureKind { get; }

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

public interface IUnityPlayModeTestValidator
{
    Task<UnityTestResultArtifactBaseline> CaptureBaselineAsync(
        string testResultsPath,
        CancellationToken cancellationToken = default);

    Task<UnityPlayModeTestValidationResult> ValidateAsync(
        ToolProcessResult processResult,
        string testResultsPath,
        UnityTestResultArtifactBaseline baseline,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies the already-hardened Unity NUnit3 artifact validation contract to PlayMode execution while
/// enforcing PlayMode operation identity. This keeps stale-artifact, bounded-read, secure XML, counter,
/// cancellation and process/result disagreement handling identical between EditMode and PlayMode.
/// </summary>
public sealed class UnityPlayModeTestValidator : IUnityPlayModeTestValidator
{
    public const long MaxResultFileBytes = UnityEditModeTestValidator.MaxResultFileBytes;

    private const string PlayModeOperation = "unity.run-tests.playmode";
    private const string SharedValidationOperation = "unity.run-tests.editmode";

    private readonly UnityEditModeTestValidator _sharedValidator;

    public UnityPlayModeTestValidator()
        : this(new UnityEditModeTestValidator())
    {
    }

    internal UnityPlayModeTestValidator(UnityEditModeTestValidator sharedValidator)
    {
        _sharedValidator = sharedValidator ?? throw new ArgumentNullException(nameof(sharedValidator));
    }

    public Task<UnityTestResultArtifactBaseline> CaptureBaselineAsync(
        string testResultsPath,
        CancellationToken cancellationToken = default) =>
        _sharedValidator.CaptureBaselineAsync(testResultsPath, cancellationToken);

    public async Task<UnityPlayModeTestValidationResult> ValidateAsync(
        ToolProcessResult processResult,
        string testResultsPath,
        UnityTestResultArtifactBaseline baseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processResult);
        ArgumentNullException.ThrowIfNull(baseline);

        if (!string.Equals(processResult.Operation, PlayModeOperation, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unity PlayMode validation requires operation '{PlayModeOperation}'.",
                nameof(processResult));
        }

        var projected = new ToolProcessResult(
            processResult.Tool,
            SharedValidationOperation,
            processResult.Status,
            processResult.LaunchStatus,
            processResult.ExitCode,
            processResult.ForcedTerminationRequested,
            processResult.Output,
            "Unity PlayMode process result projected into the shared NUnit validation contract.");

        var shared = await _sharedValidator
            .ValidateAsync(projected, testResultsPath, baseline, cancellationToken)
            .ConfigureAwait(false);

        return new UnityPlayModeTestValidationResult(
            MapStatus(shared.Status),
            MapFailureKind(shared.FailureKind),
            shared.Total,
            shared.Passed,
            shared.Failed,
            shared.Skipped,
            shared.Inconclusive,
            shared.Failures,
            shared.FailuresTruncated,
            shared.NUnitResult,
            shared.ResultFileBytes,
            shared.ResultFileSha256,
            shared.Summary.Replace("EditMode", "PlayMode", StringComparison.Ordinal));
    }

    private static UnityPlayModeTestValidationStatus MapStatus(UnityEditModeTestValidationStatus status) =>
        status switch
        {
            UnityEditModeTestValidationStatus.Succeeded => UnityPlayModeTestValidationStatus.Succeeded,
            UnityEditModeTestValidationStatus.Failed => UnityPlayModeTestValidationStatus.Failed,
            UnityEditModeTestValidationStatus.Cancelled => UnityPlayModeTestValidationStatus.Cancelled,
            UnityEditModeTestValidationStatus.Indeterminate => UnityPlayModeTestValidationStatus.Indeterminate,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported shared Unity test validation status."),
        };

    private static UnityPlayModeTestFailureKind MapFailureKind(UnityEditModeTestFailureKind failureKind) =>
        failureKind switch
        {
            UnityEditModeTestFailureKind.None => UnityPlayModeTestFailureKind.None,
            UnityEditModeTestFailureKind.Cancellation => UnityPlayModeTestFailureKind.Cancellation,
            UnityEditModeTestFailureKind.ProcessFailure => UnityPlayModeTestFailureKind.ProcessFailure,
            UnityEditModeTestFailureKind.ResultArtifactUnavailable => UnityPlayModeTestFailureKind.ResultArtifactUnavailable,
            UnityEditModeTestFailureKind.ResultArtifactInvalid => UnityPlayModeTestFailureKind.ResultArtifactInvalid,
            UnityEditModeTestFailureKind.ResultArtifactTooLarge => UnityPlayModeTestFailureKind.ResultArtifactTooLarge,
            UnityEditModeTestFailureKind.StaleResultArtifact => UnityPlayModeTestFailureKind.StaleResultArtifact,
            UnityEditModeTestFailureKind.ResultMalformed => UnityPlayModeTestFailureKind.ResultMalformed,
            UnityEditModeTestFailureKind.NoTestsDiscovered => UnityPlayModeTestFailureKind.NoTestsDiscovered,
            UnityEditModeTestFailureKind.TestsFailed => UnityPlayModeTestFailureKind.TestsFailed,
            UnityEditModeTestFailureKind.NonPassingResult => UnityPlayModeTestFailureKind.NonPassingResult,
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "Unsupported shared Unity test failure kind."),
        };
}
