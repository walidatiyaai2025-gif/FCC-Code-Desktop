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

    public bool IsActive => State is TaskLifecycleState.Starting or TaskLifecycleState.Running or TaskLifecycleState.StopRequested;

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

        if (IsActive)
        {
            throw new InvalidOperationException("Another task is already active in this workspace.");
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
        var activeProject = _sessionWorkspace.ActiveProject
            ?? throw new InvalidOperationException("An active project is required to execute the task.");
        var now = _timeProvider.GetUtcNow();

        _activeTaskId = Guid.NewGuid();
        _activeRunId = Guid.NewGuid();
        _taskSessionId = activeSession.Id;
        _taskCreatedUtc = now;
        _runStartedUtc = now;
        _prompt = prompt;
        _summary = NormalizeSummary(prompt);
        _failureMessage = null;
        _attempt = 1;
        _automaticRetryCount = 0;
        Interlocked.Exchange(ref _nextJournalSequence, 0);
        NotifyIdentityChanged();
        TransitionTo(TaskLifecycleState.Starting);

        try
        {
            await PersistTaskAsync("Starting", now, cancellationToken).ConfigureAwait(true);
            await PersistAgentRunAsync("Starting", completedUtc: null, cancellationToken).ConfigureAwait(true);
            await AppendJournalEventAsync(
                ExecutionJournalCategory.Task,
                "TaskStarting",
                now,
                cancellationToken).ConfigureAwait(true);

            var request = new AgentRuntimeRequest(
                _activeTaskId.Value,
                _activeRunId.Value,
                prompt,
                activeProject.RootPath,
                _sessionWorkspace.ActiveRuntimeSessionId);
            var execution = await _runtime!.StartAsync(request, cancellationToken).ConfigureAwait(true);
            EnsureExecutionIdentity(execution, request);
            _activeExecution = execution;

            var runningUtc = _timeProvider.GetUtcNow();
            TransitionTo(TaskLifecycleState.Running);
            await PersistTaskAsync("Running", runningUtc, cancellationToken).ConfigureAwait(true);
            await PersistAgentRunAsync("Running", completedUtc: null, cancellationToken).ConfigureAwait(true);
            await AppendJournalEventAsync(
                ExecutionJournalCategory.Agent,
                "RuntimeStarted",
                runningUtc,
                cancellationToken).ConfigureAwait(true);

            _activePumpTask = PumpExecutionAsync(execution, _activeTaskId.Value, _activeRunId.Value, _taskSessionId.Value);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordStartFailureAsync(exception).ConfigureAwait(true);
        }
    }

    private async Task PumpExecutionAsync(
        IAgentRuntimeExecution execution,
        Guid taskId,
        Guid runId,
        Guid sessionId)
    {
        var assistantText = new StringBuilder();
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
                if (runtimeEvent.Kind == AgentRuntimeEventKind.AssistantTextDelta && !string.IsNullOrEmpty(runtimeEvent.Text))
                {
                    assistantText.Append(runtimeEvent.Text);
                }

                if (runtimeEvent.Kind == AgentRuntimeEventKind.SessionIdentified && !string.IsNullOrWhiteSpace(runtimeEvent.SessionId))
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

            await CompleteFromRuntimeResultAsync(result).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await RecordPumpFailureAsync(exception).ConfigureAwait(false);
        }
        finally
        {
            await execution.DisposeAsync().ConfigureAwait(false);
            await InvokeOnDispatcherAsync(
                () =>
                {
                    if (ReferenceEquals(_activeExecution, execution))
                    {
                        _activeExecution = null;
                    }

                    _activePumpTask = null;
                }).ConfigureAwait(false);
        }
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

        await InvokeOnDispatcherAsync(
            () =>
            {
                _failureMessage = result.Failure?.Message;
                OnPropertyChanged(nameof(FailureMessage));
                OnPropertyChanged(nameof(HasFailure));
                TransitionTo(terminalState);
            }).ConfigureAwait(false);

        await PersistAgentRunAsync(persistenceState, completedUtc, CancellationToken.None).ConfigureAwait(false);
        await PersistTaskAsync(persistenceState, completedUtc, CancellationToken.None).ConfigureAwait(false);
        await AppendJournalEventAsync(
            ExecutionJournalCategory.Task,
            $"Task{persistenceState}",
            completedUtc,
            CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RecordStartFailureAsync(Exception exception)
    {
        var failedUtc = _timeProvider.GetUtcNow();
        _failureMessage = SanitizeFailureMessage(exception.Message);
        OnPropertyChanged(nameof(FailureMessage));
        OnPropertyChanged(nameof(HasFailure));
        TransitionTo(TaskLifecycleState.Failed);

        await TryPersistFailureAsync("Failed", failedUtc).ConfigureAwait(true);
    }

    private async Task RecordPumpFailureAsync(Exception exception)
    {
        var failedUtc = _timeProvider.GetUtcNow();
        await InvokeOnDispatcherAsync(
            () =>
            {
                _failureMessage = SanitizeFailureMessage(exception.Message);
                OnPropertyChanged(nameof(FailureMessage));
                OnPropertyChanged(nameof(HasFailure));
                if (State is TaskLifecycleState.Starting or TaskLifecycleState.Running or TaskLifecycleState.StopRequested)
                {
                    TransitionTo(TaskLifecycleState.Failed);
                }
            }).ConfigureAwait(false);
        await TryPersistFailureAsync("Failed", failedUtc).ConfigureAwait(false);
    }

    private async Task TryPersistFailureAsync(string state, DateTimeOffset occurredUtc)
    {
        try
        {
            await PersistAgentRunAsync(state, occurredUtc, CancellationToken.None).ConfigureAwait(false);
            await PersistTaskAsync(state, occurredUtc, CancellationToken.None).ConfigureAwait(false);
            await AppendJournalEventAsync(
                ExecutionJournalCategory.Task,
                "TaskFailed",
                occurredUtc,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception persistenceException) when (persistenceException is not OperationCanceledException)
        {
            await InvokeOnDispatcherAsync(
                () =>
                {
                    _failureMessage = $"{_failureMessage} Task journal update also failed: {SanitizeFailureMessage(persistenceException.Message)}";
                    OnPropertyChanged(nameof(FailureMessage));
                    OnPropertyChanged(nameof(HasFailure));
                }).ConfigureAwait(false);
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
        if (execution.TaskId != request.TaskId || execution.RunId != request.RunId)
        {
            throw new InvalidOperationException("Runtime execution identity does not match the prepared task/run identity.");
        }
    }

    private static void EnsureResultIdentity(AgentRuntimeResult result, Guid taskId, Guid runId)
    {
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
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
