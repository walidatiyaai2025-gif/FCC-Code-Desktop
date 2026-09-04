namespace FCCCodeDesktop.Runtime;

public sealed record AgentRuntimeSupervisionOptions
{
    public const int DefaultMaximumAttempts = 3;
    public const int MaximumSupportedAttempts = 10;

    public AgentRuntimeSupervisionOptions(
        int maximumAttempts = DefaultMaximumAttempts,
        bool automaticRetryEnabled = true)
    {
        if (maximumAttempts < 1 || maximumAttempts > MaximumSupportedAttempts)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAttempts),
                $"Maximum attempts must be between 1 and {MaximumSupportedAttempts}.");
        }

        MaximumAttempts = maximumAttempts;
        AutomaticRetryEnabled = automaticRetryEnabled;
    }

    public int MaximumAttempts { get; }

    public bool AutomaticRetryEnabled { get; }

    internal bool ShouldRetry(
        AgentRuntimeResult result,
        int completedAttempt,
        bool cancellationRequested)
    {
        ArgumentNullException.ThrowIfNull(result);

        return AutomaticRetryEnabled
            && !cancellationRequested
            && completedAttempt < MaximumAttempts
            && result.State == AgentRuntimeTerminalState.Failed
            && result.Failure is
            {
                Retryability: AgentRuntimeRetryability.Retryable,
                UserAction: AgentRuntimeUserAction.NotRequired
            };
    }
}
