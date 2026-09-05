using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using FCCCodeDesktop.Application.Persistence;
using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.App.Conversation;

public enum TaskLifecycleState
{
    Idle = 0,
    Starting = 1,
    Running = 2,
    StopRequested = 3,
    Succeeded = 4,
    Failed = 5,
    Cancelled = 6,
}

public sealed class TaskExecutionState : DispatcherObject, INotifyPropertyChanged
{
    public const int MaxTaskSummaryLength = 240;

    private readonly IExecutionJournalStore _journalStore;
    private readonly SessionWorkspaceState _sessionWorkspace;
    private readonly StreamingConversationState _conversation;
    private readonly IAgentRuntime? _runtime;
    private readonly TimeProvider _timeProvider;
    private readonly string? _runtimeUnavailableReason;
    private IAgentRuntimeExecution? _activeExecution;
    private Task? _activePumpTask;
    private Guid? _activeTaskId;
    private Guid? _activeRunId;
    private Guid? _taskSessionId;
    private DateTimeOffset _taskCreatedUtc;
    private DateTimeOffset _runStartedUtc;
    private string? _prompt;
    private string? _summary;
    private string? _failureMessage;
    private int _attempt;
    private int _automaticRetryCount;
    private long _nextJournalSequence;
    private TaskLifecycleState _state = TaskLifecycleState.Idle;

    public TaskExecutionState(
        IExecutionJournalStore journalStore,
        SessionWorkspaceState sessionWorkspace,
        StreamingConversationState conversation,
        IAgentRuntime? runtime,
        string? runtimeUnavailableReason = null,
        TimeProvider? timeProvider = null)
    {
        _journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
        _sessionWorkspace = sessionWorkspace ?? throw new ArgumentNullException(nameof(sessionWorkspace));
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        _runtime = runtime;
        _runtimeUnavailableReason = string.IsNullOrWhiteSpace(runtimeUnavailableReason)
            ? null
            : runtimeUnavailableReason.Trim();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TaskLifecycleState State => _state;

    public Guid? ActiveTaskId => _activeTaskId;

    public Guid? ActiveRunId => _activeRunId;

    public Guid? TaskSessionId => _taskSessionId;

    public string? Summary => _summary;

    public string? FailureMessage => _failureMessage;

    public bool HasFailure => !string.IsNullOrWhiteSpace(FailureMessage);

    public int Attempt => _attempt;

    public int AutomaticRetryCount => _automaticRetryCount;

    public bool IsRuntimeAvailable => _runtime is not null;

    public string RuntimeAvailabilityText => IsRuntimeAvailable
        ? $"{_runtime!.Descriptor.DisplayName} ready"
        : _runtimeUnavailableReason ?? "FCC runtime is unavailable";

    public bool HasTask => ActiveTaskId is not null;

    public bool IsActive => State is TaskLifecycleState.Starting
        or TaskLifecycleState.Running
        or TaskLifecycleState.StopRequested;

    public bool CanStop => State == TaskLifecycleState.Running && _activeExecution is not null;

    public bool CanRetry =>
        State is TaskLifecycleState.Failed or TaskLifecycleState.Cancelled
        && _activePumpTask is null
        && _activeExecution is null
        && _runtime is not null
        && _activeTaskId is not null
        && _taskSessionId is not null
        && !string.IsNullOrWhiteSpace(_prompt);

    public string StateLabel => State switch
    {
        TaskLifecycleState.Idle => "Idle",
        TaskLifecycleState.Starting => "Starting",
        TaskLifecycleState.Running => "Running",
        TaskLifecycleState.StopRequested => "Stopping",
        TaskLifecycleState.Succeeded => "Succeeded",
        TaskLifecycleState.Failed => "Failed",
        TaskLifecycleState.Cancelled => "Cancelled",
        _ => throw new InvalidOperationException($"Unsupported task lifecycle state: {State}"),
    };

    public void ValidateCanStart()
    {
        VerifyAccess();
        if (_runtime is null)
        {
            throw new InvalidOperationException(_runtimeUnavailableReason ?? "FCC runtime is unavailable.");
        }

        if (IsActive || _activePumpTask is not null || _activeExecution is not null)
        {
            throw new InvalidOperationException("Another task is already active or still settling in this workspace.");
        }

        if (_sessionWorkspace.ActiveSessionId is null || _sessionWorkspace.ActiveProject is null)
        {
            throw new InvalidOperationException("Create or resume a persisted project session before starting a task.");
        }

        if (!Directory.Exists(_sessionWorkspace.ActiveProject.RootPath))
        {
            throw new DirectoryNotFoundException(
                $"The active project directory does not exist: '{_sessionWorkspace.ActiveProject.RootPath}'.");
        }
    }

    public async Task StartTaskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ValidateCanStart();
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var activeSession = _sessionWorkspace.ActiveSession
            ?? throw new InvalidOperationException("An active session is required to own the task.");
        var now = _timeProvider.GetUtcNow();

        _activeTaskId = Guid.NewGuid();
        _taskSessionId = activeSession.Id;
        _taskCreatedUtc = now;
        _prompt = prompt;
        _summary = NormalizeSummary(prompt);
        _attempt = 0;
        Interlocked.Exchange(ref _nextJournalSequence, 0);
        NotifyIdentityChanged();

        await StartAttemptAsync(isManualRetry: false, cancellationToken).ConfigureAwait(true);
    }

    public async Task RequestStopAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        cancellationToken.ThrowIfCancellationRequested();

        if (State == TaskLifecycleState.StopRequested)
        {
            return;
        }

        if (!CanStop || _activeExecution is null)
        {
            throw new InvalidOperationException("The active task cannot be stopped in its current state.");
        }

        var execution = _activeExecution;
        var occurredUtc = _timeProvider.GetUtcNow();
        TransitionTo(TaskLifecycleState.StopRequested);

        string? persistenceDiagnostic = null;
        try
        {
            await PersistTaskAsync("StopRequested", occurredUtc, CancellationToken.None).ConfigureAwait(true);
            await AppendJournalEventAsync(
                ExecutionJournalCategory.Task,
                "StopRequested",
                occurredUtc,
                CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            persistenceDiagnostic = $"Stop-request journal update failed: {SanitizeFailureMessage(exception.Message)}";
            SetFailureMessage(persistenceDiagnostic);
        }

        try
        {
            await execution.CancelAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            var cancelDiagnostic = $"Runtime stop request failed: {SanitizeFailureMessage(exception.Message)}";
            var combined = CombineDiagnostics(persistenceDiagnostic, cancelDiagnostic);
            SetFailureMessage(combined);
            try
            {
                await AppendJournalEventAsync(
                    ExecutionJournalCategory.Task,
                    "StopRequestFailed",
                    _timeProvider.GetUtcNow(),
                    CancellationToken.None,
                    JsonSerializer.Serialize(new { stopRequestFailed = true })).ConfigureAwait(true);
            }
            catch (Exception journalException) when (journalException is not OperationCanceledException)
            {
                SetFailureMessage(CombineDiagnostics(
                    combined,
                    $"Stop-failure journal update also failed: {SanitizeFailureMessage(journalException.Message)}"));
            }

            throw new InvalidOperationException(cancelDiagnostic, exception);
        }
    }

    public async Task RetryAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        cancellationToken.ThrowIfCancellationRequested();

        if (!CanRetry || _activeTaskId is null || _taskSessionId is null || _prompt is null)
        {
            throw new InvalidOperationException(
                "Only a failed or cancelled task can be retried after its prior run is fully settled.");
        }

        if (_sessionWorkspace.ActiveSessionId != _taskSessionId)
        {
            throw new InvalidOperationException("Return to the task's owning session before retrying it.");
        }

        var events = await _journalStore
            .ListEventsAsync(_activeTaskId.Value, cancellationToken)
            .ConfigureAwait(true);
        var nextSequence = events.Count == 0 ? 0 : checked(events[^1].Sequence + 1);
        Interlocked.Exchange(ref _nextJournalSequence, nextSequence);

        await StartAttemptAsync(isManualRetry: true, cancellationToken).ConfigureAwait(true);
    }

    public void ReportControlError(string message)
    {
        VerifyAccess();
        SetFailureMessage(message);
    }

    private async Task StartAttemptAsync(bool isManualRetry, CancellationToken cancellationToken)
    {
        VerifyAccess();
        if (_runtime is null || _prompt is null || _activeTaskId is null || _taskSessionId is null)
        {
            throw new InvalidOperationException("Task execution prerequisites are incomplete.");
        }

        if (_activePumpTask is not null || _activeExecution is not null)
        {
            throw new InvalidOperationException("The previous task run is still settling.");
        }

        var activeProject = _sessionWorkspace.ActiveProject
            ?? throw new InvalidOperationException("An active project is required to execute the task.");
        if (_sessionWorkspace.ActiveSessionId != _taskSessionId)
        {
            throw new InvalidOperationException("The task can run only inside its owning session.");
        }

        if (!Directory.Exists(activeProject.RootPath))
        {
            throw new DirectoryNotFoundException(
                $"The active project directory does not exist: '{activeProject.RootPath}'.");
        }

        var now = _timeProvider.GetUtcNow();
        _activeRunId = Guid.NewGuid();
        _runStartedUtc = now;
        _attempt = checked(_attempt + 1);
        _automaticRetryCount = 0;
        SetFailureMessage(null);
        OnPropertyChanged(nameof(ActiveRunId));
        OnPropertyChanged(nameof(Attempt));
        OnPropertyChanged(nameof(AutomaticRetryCount));
        OnPropertyChanged(nameof(CanRetry));
        TransitionTo(TaskLifecycleState.Starting);

        IAgentRuntimeExecution? startedExecution = null;
        try
        {
            await PersistTaskAsync("Starting", now, cancellationToken).ConfigureAwait(true);
            await PersistAgentRunAsync("Starting", completedUtc: null, cancellationToken).ConfigureAwait(true);
            await AppendJournalEventAsync(
                ExecutionJournalCategory.Task,
                isManualRetry ? "ManualRetryStarting" : "TaskStarting",
                now,
                cancellationToken).ConfigureAwait(true);

            var request = new AgentRuntimeRequest(
                _activeTaskId.Value,
                _activeRunId.Value,
                _prompt,
                activeProject.RootPath,
                _sessionWorkspace.ActiveRuntimeSessionId);
            startedExecution = await _runtime.StartAsync(request, cancellationToken).ConfigureAwait(true);
            EnsureExecutionIdentity(startedExecution, request);
            _activeExecution = startedExecution;
            OnPropertyChanged(nameof(CanStop));
            OnPropertyChanged(nameof(CanRetry));

            var runningUtc = _timeProvider.GetUtcNow();
            TransitionTo(TaskLifecycleState.Running);
            await PersistTaskAsync("Running", runningUtc, cancellationToken).ConfigureAwait(true);
            await PersistAgentRunAsync("Running", completedUtc: null, cancellationToken).ConfigureAwait(true);
            await AppendJournalEventAsync(
                ExecutionJournalCategory.Agent,
                "RuntimeStarted",
                runningUtc,
                cancellationToken).ConfigureAwait(true);

            var pumpTask = PumpExecutionAsync(
                startedExecution,
                _activeTaskId.Value,
                _activeRunId.Value,
                _taskSessionId.Value);
            _activePumpTask = pumpTask;
            OnPropertyChanged(nameof(CanRetry));
            _ = TrackPumpCompletionAsync(pumpTask);
            startedExecution = null;
        }
        catch (OperationCanceledException)
        {
            var cleanupDiagnostic = await CleanupUnownedExecutionAsync(startedExecution).ConfigureAwait(true);
            await RecordStartCancellationAsync(cleanupDiagnostic).ConfigureAwait(true);
            throw;
        }
        catch (Exception exception)
        {
            var cleanupDiagnostic = await CleanupUnownedExecutionAsync(startedExecution).ConfigureAwait(true);
            await RecordStartFailureAsync(exception, cleanupDiagnostic).ConfigureAwait(true);
        }
    }

    private async Task PumpExecutionAsync(
        IAgentRuntimeExecution execution,
        Guid taskId,
        Guid runId,
        Guid sessionId)
    {
        var assistantText = new StringBuilder();
        var disposed = false;
        try
        {
            await foreach (var runtimeEvent in execution.Events.ConfigureAwait(false))
            {
                if (_sessionWorkspace.ActiveSessionId != sessionId)
                {
                    await execution.CancelAsync(CancellationToken.None).ConfigureAwait(false);
                    throw new InvalidOperationException(
                        "The active session changed while a task was running. The task was stopped to prevent cross-session output corruption.");
                }

                await _conversation.ApplyRuntimeEventAsync(runtimeEvent, CancellationToken.None).ConfigureAwait(false);
                if (runtimeEvent.Kind == AgentRuntimeEventKind.AssistantTextDelta
                    && !string.IsNullOrEmpty(runtimeEvent.Text))
                {
                    assistantText.Append(runtimeEvent.Text);
                }

                if (runtimeEvent.Kind == AgentRuntimeEventKind.SessionIdentified
                    && !string.IsNullOrWhiteSpace(runtimeEvent.SessionId))
                {
                    await _sessionWorkspace
                        .BindRuntimeSessionAsync(runtimeEvent.SessionId, CancellationToken.None)
                        .ConfigureAwait(false);
                }

                if (runtimeEvent.Kind == AgentRuntimeEventKind.Retry)
                {
                    await InvokeOnDispatcherAsync(
                        () =>
                        {
                            _automaticRetryCount = checked(_automaticRetryCount + 1);
                            OnPropertyChanged(nameof(AutomaticRetryCount));
                        }).ConfigureAwait(false);
                }

                await AppendRuntimeJournalEventAsync(runtimeEvent).ConfigureAwait(false);
            }

            var result = await execution.Completion.ConfigureAwait(false);
            EnsureResultIdentity(result, taskId, runId);
            if (!string.IsNullOrWhiteSpace(result.SessionId))
            {
                await _sessionWorkspace
                    .BindRuntimeSessionAsync(result.SessionId, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            if (assistantText.Length > 0)
            {
                await _sessionWorkspace
                    .AppendMessageAsync("assistant", assistantText.ToString(), CancellationToken.None)
                    .ConfigureAwait(false);
            }

            await execution.DisposeAsync().ConfigureAwait(false);
            disposed = true;
            await CompleteFromRuntimeResultAsync(result).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var effectiveException = exception;
            if (!disposed)
            {
                try
                {
                    await execution.DisposeAsync().ConfigureAwait(false);
                    disposed = true;
                }
                catch (Exception cleanupException)
                {
                    effectiveException = new InvalidOperationException(
                        $"{SanitizeFailureMessage(exception.Message)} Runtime cleanup also failed: {SanitizeFailureMessage(cleanupException.Message)}");
                }
            }

            await RecordPumpFailureAsync(effectiveException).ConfigureAwait(false);
        }
        finally
        {
            await InvokeOnDispatcherAsync(
                () =>
                {
                    if (ReferenceEquals(_activeExecution, execution))
                    {
                        _activeExecution = null;
                    }

                    OnPropertyChanged(nameof(CanStop));
                    OnPropertyChanged(nameof(CanRetry));
                }).ConfigureAwait(false);
        }
    }

    private async Task TrackPumpCompletionAsync(Task pumpTask)
    {
        try
        {
            await pumpTask.ConfigureAwait(false);
        }
        finally
        {
            await InvokeOnDispatcherAsync(
                () =>
                {
                    if (ReferenceEquals(_activePumpTask, pumpTask))
                    {
                        _activePumpTask = null;
                    }

                    OnPropertyChanged(nameof(CanStop));
                    OnPropertyChanged(nameof(CanRetry));
                }).ConfigureAwait(false);
        }
    }

    private async Task<string?> CleanupUnownedExecutionAsync(IAgentRuntimeExecution? execution)
    {
        if (execution is null)
        {
            return null;
        }

        string? cleanupDiagnostic = null;
        try
        {
            if (!execution.Completion.IsCompleted)
            {
                await execution.CancelAsync(CancellationToken.None).ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            cleanupDiagnostic = $"Runtime cancellation cleanup failed: {SanitizeFailureMessage(exception.Message)}";
        }

        try
        {
            await execution.DisposeAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            var disposeDiagnostic = $"Runtime dispose cleanup failed: {SanitizeFailureMessage(exception.Message)}";
            cleanupDiagnostic = cleanupDiagnostic is null
                ? disposeDiagnostic
                : $"{cleanupDiagnostic} {disposeDiagnostic}";
        }

        if (ReferenceEquals(_activeExecution, execution))
        {
            _activeExecution = null;
        }

        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRetry));
        return cleanupDiagnostic;
    }

    private async Task CompleteFromRuntimeResultAsync(AgentRuntimeResult result)
    {
        var completedUtc = _timeProvider.GetUtcNow();
        var terminalState = result.State switch
        {
            AgentRuntimeTerminalState.Succeeded => TaskLifecycleState.Succeeded,
            AgentRuntimeTerminalState.Failed => TaskLifecycleState.Failed,
            AgentRuntimeTerminalState.Cancelled => TaskLifecycleState.Cancelled,
            _ => throw new InvalidOperationException($"Unsupported runtime terminal state: {result.State}"),
        };
        var persistenceState = terminalState.ToString();

        try
        {
            await PersistTaskAsync(persistenceState, completedUtc, CancellationToken.None).ConfigureAwait(false);
            await PersistAgentRunAsync(persistenceState, completedUtc, CancellationToken.None).ConfigureAwait(false);
            await AppendJournalEventAsync(
                ExecutionJournalCategory.Task,
                $"Task{persistenceState}",
                completedUtc,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                CombineDiagnostics(
                    result.Failure?.Message,
                    $"Terminal task-state persistence failed: {SanitizeFailureMessage(exception.Message)}"),
                exception);
        }

        await InvokeOnDispatcherAsync(
            () =>
            {
                SetFailureMessage(result.Failure?.Message);
                TransitionTo(terminalState);
            }).ConfigureAwait(false);
    }

    private async Task RecordStartFailureAsync(Exception exception, string? cleanupDiagnostic)
    {
        var failedUtc = _timeProvider.GetUtcNow();
        SetFailureMessage(CombineDiagnostics(exception.Message, cleanupDiagnostic));
        TransitionTo(TaskLifecycleState.Failed);
        await TryPersistTerminalStateAsync("Failed", "TaskFailed", failedUtc).ConfigureAwait(true);
    }

    private async Task RecordStartCancellationAsync(string? cleanupDiagnostic)
    {
        var cancelledUtc = _timeProvider.GetUtcNow();
        SetFailureMessage(cleanupDiagnostic);
        if (State is TaskLifecycleState.Starting or TaskLifecycleState.Running or TaskLifecycleState.StopRequested)
        {
            TransitionTo(TaskLifecycleState.Cancelled);
        }

        await TryPersistTerminalStateAsync("Cancelled", "TaskCancelled", cancelledUtc).ConfigureAwait(true);
    }

    private async Task RecordPumpFailureAsync(Exception exception)
    {
        var failedUtc = _timeProvider.GetUtcNow();
        await InvokeOnDispatcherAsync(
            () =>
            {
                SetFailureMessage(exception.Message);
                if (State is TaskLifecycleState.Starting
                    or TaskLifecycleState.Running
                    or TaskLifecycleState.StopRequested)
                {
                    TransitionTo(TaskLifecycleState.Failed);
                }
            }).ConfigureAwait(false);
        await TryPersistTerminalStateAsync("Failed", "TaskFailed", failedUtc).ConfigureAwait(false);
    }

    private async Task TryPersistTerminalStateAsync(
        string state,
        string eventType,
        DateTimeOffset occurredUtc)
    {
        try
        {
            await PersistTaskAsync(state, occurredUtc, CancellationToken.None).ConfigureAwait(false);
            await PersistAgentRunAsync(state, occurredUtc, CancellationToken.None).ConfigureAwait(false);
            await AppendJournalEventAsync(
                ExecutionJournalCategory.Task,
                eventType,
                occurredUtc,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception persistenceException) when (persistenceException is not OperationCanceledException)
        {
            await InvokeOnDispatcherAsync(
                () => SetFailureMessage(
                    CombineDiagnostics(
                        _failureMessage,
                        $"Task journal update also failed: {SanitizeFailureMessage(persistenceException.Message)}")))
                .ConfigureAwait(false);
        }
    }

    private Task AppendRuntimeJournalEventAsync(AgentRuntimeEvent runtimeEvent)
    {
        var category = runtimeEvent.Kind is AgentRuntimeEventKind.ToolStarted
            or AgentRuntimeEventKind.ToolProgress
            or AgentRuntimeEventKind.ToolResult
            ? ExecutionJournalCategory.Tool
            : ExecutionJournalCategory.Agent;
        var safeData = runtimeEvent.Kind switch
        {
            AgentRuntimeEventKind.SessionIdentified => JsonSerializer.Serialize(new { sessionIdentified = true }),
            AgentRuntimeEventKind.Retry => JsonSerializer.Serialize(new { automaticRetry = true }),
            AgentRuntimeEventKind.Error => JsonSerializer.Serialize(new { runtimeError = true }),
            _ => null,
        };

        return AppendJournalEventAsync(
            category,
            $"Runtime{runtimeEvent.Kind}",
            runtimeEvent.OccurredUtc,
            CancellationToken.None,
            safeData);
    }

    private Task AppendJournalEventAsync(
        ExecutionJournalCategory category,
        string eventType,
        DateTimeOffset occurredUtc,
        CancellationToken cancellationToken,
        string? dataJson = null)
    {
        if (_activeTaskId is not Guid taskId)
        {
            throw new InvalidOperationException("A task identity is required before journal events can be persisted.");
        }

        var sequence = Interlocked.Increment(ref _nextJournalSequence) - 1;
        var taskEvent = new PersistedTaskEvent(
            Guid.NewGuid(),
            taskId,
            sequence,
            category,
            eventType,
            _activeRunId,
            null,
            null,
            dataJson,
            occurredUtc);
        return _journalStore.AppendEventAsync(taskEvent, cancellationToken);
    }

    private Task PersistTaskAsync(string state, DateTimeOffset updatedUtc, CancellationToken cancellationToken)
    {
        if (_activeTaskId is not Guid taskId || _taskSessionId is not Guid sessionId)
        {
            throw new InvalidOperationException("Task/session identity is incomplete.");
        }

        return _journalStore.UpsertTaskAsync(
            new PersistedTask(taskId, sessionId, state, _summary, _taskCreatedUtc, updatedUtc),
            cancellationToken);
    }

    private Task PersistAgentRunAsync(
        string state,
        DateTimeOffset? completedUtc,
        CancellationToken cancellationToken)
    {
        if (_activeTaskId is not Guid taskId || _activeRunId is not Guid runId)
        {
            throw new InvalidOperationException("Task/run identity is incomplete.");
        }

        return _journalStore.UpsertAgentRunAsync(
            new PersistedAgentRun(
                runId,
                taskId,
                _runtime?.Descriptor.RuntimeId ?? "unavailable",
                state,
                _runStartedUtc,
                completedUtc),
            cancellationToken);
    }

    private void TransitionTo(TaskLifecycleState next)
    {
        VerifyAccess();
        if (!IsAllowedTransition(State, next))
        {
            throw new InvalidOperationException($"Illegal task-state transition: {State} -> {next}.");
        }

        if (_state == next)
        {
            return;
        }

        _state = next;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRetry));
    }

    private static bool IsAllowedTransition(TaskLifecycleState current, TaskLifecycleState next) =>
        (current, next) switch
        {
            (TaskLifecycleState.Idle, TaskLifecycleState.Starting) => true,
            (TaskLifecycleState.Starting, TaskLifecycleState.Running) => true,
            (TaskLifecycleState.Starting, TaskLifecycleState.Failed) => true,
            (TaskLifecycleState.Starting, TaskLifecycleState.Cancelled) => true,
            (TaskLifecycleState.Running, TaskLifecycleState.StopRequested) => true,
            (TaskLifecycleState.Running, TaskLifecycleState.Succeeded) => true,
            (TaskLifecycleState.Running, TaskLifecycleState.Failed) => true,
            (TaskLifecycleState.Running, TaskLifecycleState.Cancelled) => true,
            (TaskLifecycleState.StopRequested, TaskLifecycleState.Succeeded) => true,
            (TaskLifecycleState.StopRequested, TaskLifecycleState.Failed) => true,
            (TaskLifecycleState.StopRequested, TaskLifecycleState.Cancelled) => true,
            (TaskLifecycleState.Succeeded, TaskLifecycleState.Starting) => true,
            (TaskLifecycleState.Failed, TaskLifecycleState.Starting) => true,
            (TaskLifecycleState.Cancelled, TaskLifecycleState.Starting) => true,
            _ when current == next => true,
            _ => false,
        };

    private static void EnsureExecutionIdentity(IAgentRuntimeExecution execution, AgentRuntimeRequest request)
    {
        ArgumentNullException.ThrowIfNull(execution);
        if (execution.TaskId != request.TaskId || execution.RunId != request.RunId)
        {
            throw new InvalidOperationException("Runtime execution identity does not match the prepared task/run identity.");
        }
    }

    private static void EnsureResultIdentity(AgentRuntimeResult result, Guid taskId, Guid runId)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.TaskId != taskId || result.RunId != runId)
        {
            throw new InvalidOperationException("Runtime terminal result identity does not match the active task/run identity.");
        }
    }

    private static string NormalizeSummary(string prompt)
    {
        var normalized = string.Join(' ', prompt.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= MaxTaskSummaryLength
            ? normalized
            : normalized[..MaxTaskSummaryLength];
    }

    private void SetFailureMessage(string? message)
    {
        _failureMessage = string.IsNullOrWhiteSpace(message) ? null : SanitizeFailureMessage(message);
        OnPropertyChanged(nameof(FailureMessage));
        OnPropertyChanged(nameof(HasFailure));
    }

    private static string CombineDiagnostics(string? first, string? second)
    {
        var left = string.IsNullOrWhiteSpace(first) ? null : first.Trim();
        var right = string.IsNullOrWhiteSpace(second) ? null : second.Trim();
        return (left, right) switch
        {
            (null, null) => "Task execution failed without a diagnostic message.",
            ({ } value, null) => value,
            (null, { } value) => value,
            ({ } leftValue, { } rightValue) => $"{leftValue} {rightValue}",
        };
    }

    private static string SanitizeFailureMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Task execution failed without a diagnostic message.";
        }

        const int maxLength = 1024;
        var normalized = message.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private Task InvokeOnDispatcherAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.InvokeAsync(action, DispatcherPriority.DataBind).Task;
    }

    private void NotifyIdentityChanged()
    {
        OnPropertyChanged(nameof(ActiveTaskId));
        OnPropertyChanged(nameof(ActiveRunId));
        OnPropertyChanged(nameof(TaskSessionId));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Attempt));
        OnPropertyChanged(nameof(AutomaticRetryCount));
        OnPropertyChanged(nameof(HasTask));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRetry));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
