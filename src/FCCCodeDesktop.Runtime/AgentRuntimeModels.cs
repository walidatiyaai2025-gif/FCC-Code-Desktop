namespace FCCCodeDesktop.Runtime;

public enum AgentRuntimeTransport
{
    Unknown = 0,
    StructuredProcess = 1,
    CliFallback = 2,
    Fixture = 3
}

public sealed record AgentRuntimeCapabilities
{
    public AgentRuntimeCapabilities(
        bool supportsStreaming,
        bool supportsSessions,
        bool supportsResume,
        bool supportsCancellation,
        bool supportsToolActivity)
    {
        if (supportsResume && !supportsSessions)
        {
            throw new ArgumentException(
                "A runtime cannot advertise resume support without session identity support.",
                nameof(supportsResume));
        }

        SupportsStreaming = supportsStreaming;
        SupportsSessions = supportsSessions;
        SupportsResume = supportsResume;
        SupportsCancellation = supportsCancellation;
        SupportsToolActivity = supportsToolActivity;
    }

    public bool SupportsStreaming { get; }

    public bool SupportsSessions { get; }

    public bool SupportsResume { get; }

    public bool SupportsCancellation { get; }

    public bool SupportsToolActivity { get; }
}

public sealed record AgentRuntimeDescriptor
{
    public AgentRuntimeDescriptor(
        string runtimeId,
        string displayName,
        AgentRuntimeTransport transport,
        AgentRuntimeCapabilities capabilities,
        string? version = null)
    {
        if (string.IsNullOrWhiteSpace(runtimeId))
        {
            throw new ArgumentException("Runtime identifier is required.", nameof(runtimeId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Runtime display name is required.", nameof(displayName));
        }

        if (transport == AgentRuntimeTransport.Unknown)
        {
            throw new ArgumentException("Runtime transport must be explicit.", nameof(transport));
        }

        ArgumentNullException.ThrowIfNull(capabilities);

        RuntimeId = runtimeId.Trim();
        DisplayName = displayName.Trim();
        Transport = transport;
        Capabilities = capabilities;
        Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
    }

    public string RuntimeId { get; }

    public string DisplayName { get; }

    public AgentRuntimeTransport Transport { get; }

    public AgentRuntimeCapabilities Capabilities { get; }

    public string? Version { get; }
}

public sealed record AgentRuntimeRequest
{
    public AgentRuntimeRequest(
        Guid taskId,
        Guid runId,
        string prompt,
        string workingDirectory,
        string? resumeSessionId = null)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("Task identifier must not be empty.", nameof(taskId));
        }

        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run identifier must not be empty.", nameof(runId));
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required.", nameof(prompt));
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("Working directory is required.", nameof(workingDirectory));
        }

        TaskId = taskId;
        RunId = runId;
        Prompt = prompt;
        WorkingDirectory = workingDirectory.Trim();
        ResumeSessionId = string.IsNullOrWhiteSpace(resumeSessionId) ? null : resumeSessionId.Trim();
    }

    public Guid TaskId { get; }

    public Guid RunId { get; }

    public string Prompt { get; }

    public string WorkingDirectory { get; }

    public string? ResumeSessionId { get; }
}

public enum AgentRuntimeTerminalState
{
    Unknown = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3
}

public enum AgentRuntimeFailureKind
{
    UnknownFailure = 0,
    RuntimeNotFound = 1,
    FccUnavailable = 2,
    AuthenticationFailure = 3,
    ModelUnavailable = 4,
    ProviderUnavailable = 5,
    ProviderBusyOrOverloaded = 6,
    RateLimited = 7,
    Timeout = 8,
    MalformedStream = 9,
    Interrupted = 10,
    ProcessCrash = 11,
    NonZeroExit = 12
}

public enum AgentRuntimeRetryability
{
    Unknown = 0,
    Retryable = 1,
    NotRetryable = 2
}

public enum AgentRuntimeUserAction
{
    Unknown = 0,
    Required = 1,
    NotRequired = 2
}

public sealed record AgentRuntimeFailure
{
    public AgentRuntimeFailure(
        AgentRuntimeFailureKind kind,
        string message,
        AgentRuntimeRetryability retryability = AgentRuntimeRetryability.Unknown,
        AgentRuntimeUserAction userAction = AgentRuntimeUserAction.Unknown,
        int? statusCode = null,
        string? source = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Failure message is required.", nameof(message));
        }

        if (statusCode is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Status code must be positive when supplied.");
        }

        Kind = kind;
        Message = message;
        Retryability = retryability;
        UserAction = userAction;
        StatusCode = statusCode;
        Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
    }

    public AgentRuntimeFailureKind Kind { get; }

    public string Message { get; }

    public AgentRuntimeRetryability Retryability { get; }

    public AgentRuntimeUserAction UserAction { get; }

    public int? StatusCode { get; }

    public string? Source { get; }
}

public sealed record AgentRuntimeResult
{
    public AgentRuntimeResult(
        Guid taskId,
        Guid runId,
        AgentRuntimeTerminalState state,
        string? sessionId = null,
        AgentRuntimeFailure? failure = null)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("Task identifier must not be empty.", nameof(taskId));
        }

        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run identifier must not be empty.", nameof(runId));
        }

        if (state == AgentRuntimeTerminalState.Unknown)
        {
            throw new ArgumentException("Terminal result state must be explicit.", nameof(state));
        }

        if (state == AgentRuntimeTerminalState.Succeeded && failure is not null)
        {
            throw new ArgumentException("A successful runtime result cannot contain a failure.", nameof(failure));
        }

        if (state == AgentRuntimeTerminalState.Failed && failure is null)
        {
            throw new ArgumentException("A failed runtime result must contain a classified failure.", nameof(failure));
        }

        TaskId = taskId;
        RunId = runId;
        State = state;
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim();
        Failure = failure;
    }

    public Guid TaskId { get; }

    public Guid RunId { get; }

    public AgentRuntimeTerminalState State { get; }

    public string? SessionId { get; }

    public AgentRuntimeFailure? Failure { get; }
}
