using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using FCCCodeDesktop.Application.Persistence;
using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.App.Conversation;

public sealed class SessionChangedEventArgs : EventArgs
{
    public SessionChangedEventArgs(PersistedSession? session, IReadOnlyList<PersistedMessage> messages)
    {
        Session = session;
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    public PersistedSession? Session { get; }

    public IReadOnlyList<PersistedMessage> Messages { get; }
}

public sealed class SessionWorkspaceState : DispatcherObject, INotifyPropertyChanged
{
    public const int MaxSessionTitleLength = 200;

    private readonly IConversationStateStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly object _messageWriteSync = new();
    private readonly ObservableCollection<PersistedSession> _sessions = [];
    private readonly ReadOnlyObservableCollection<PersistedSession> _readonlySessions;
    private Task _messageWriteTail = Task.CompletedTask;
    private PersistedProject? _activeProject;
    private PersistedSession? _activeSession;
    private bool _isBusy;
    private string? _errorMessage;

    public SessionWorkspaceState(IConversationStateStore store, TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _readonlySessions = new ReadOnlyObservableCollection<PersistedSession>(_sessions);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<SessionChangedEventArgs>? SessionChanged;

    public ReadOnlyObservableCollection<PersistedSession> Sessions => _readonlySessions;

    public PersistedProject? ActiveProject => _activeProject;

    public PersistedSession? ActiveSession => _activeSession;

    public Guid? ActiveProjectId => ActiveProject?.Id;

    public Guid? ActiveSessionId => ActiveSession?.Id;

    public string ActiveProjectName => ActiveProject?.DisplayName ?? "No project open";

    public string ActiveSessionTitle => ActiveSession?.Title ?? "No active session";

    public string? ActiveRuntimeSessionId => ActiveSession?.RuntimeSessionId;

    public bool HasRuntimeSessionId => !string.IsNullOrWhiteSpace(ActiveRuntimeSessionId);

    public bool HasActiveProject => ActiveProject is not null;

    public bool HasActiveSession => ActiveSession is not null;

    public bool HasSessions => _sessions.Count > 0;

    public bool IsBusy => _isBusy;

    public string? ErrorMessage => _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string StatusText => IsBusy
        ? "Updating sessions…"
        : HasActiveSession
            ? HasRuntimeSessionId ? "Session ready to resume runtime context" : "Local session active"
            : HasActiveProject ? "Choose a session or create a new one" : "Open a project to manage sessions";

    public async Task ActivateProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project identifier must not be empty.", nameof(projectId));
        }

        await SetBusyAsync(true, cancellationToken).ConfigureAwait(false);
        try
        {
            var project = await _store.GetProjectAsync(projectId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Project '{projectId:D}' is not persisted and cannot own sessions.");
            var sessions = await _store.ListSessionsAsync(projectId, cancellationToken).ConfigureAwait(false);

            await InvokeOnDispatcherAsync(
                () =>
                {
                    _activeProject = project;
                    _activeSession = null;
                    ReplaceSessions(sessions);
                    SetErrorMessage(null);
                    NotifyProjectStateChanged();
                    NotifySessionStateChanged();
                    SessionChanged?.Invoke(this, new SessionChangedEventArgs(null, Array.Empty<PersistedMessage>()));
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await SetErrorAsync(exception.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await SetBusyAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var projectId = ReadOnDispatcher(() => ActiveProjectId)
            ?? throw new InvalidOperationException("A persisted project must be active before session history can refresh.");

        await SetBusyAsync(true, cancellationToken).ConfigureAwait(false);
        try
        {
            var sessions = await _store.ListSessionsAsync(projectId, cancellationToken).ConfigureAwait(false);
            await InvokeOnDispatcherAsync(
                () =>
                {
                    ReplaceSessions(sessions);
                    SetErrorMessage(null);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await SetErrorAsync(exception.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await SetBusyAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<PersistedSession> CreateSessionAsync(
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        var project = ReadOnDispatcher(() => ActiveProject)
            ?? throw new InvalidOperationException("A persisted project must be active before a session can be created.");
        var now = _timeProvider.GetUtcNow();
        var normalizedTitle = NormalizeTitle(title, now);
        var session = new PersistedSession(Guid.NewGuid(), project.Id, null, normalizedTitle, now, now);

        await SetBusyAsync(true, cancellationToken).ConfigureAwait(false);
        try
        {
            await _store.UpsertSessionAsync(session, cancellationToken).ConfigureAwait(false);
            await InvokeOnDispatcherAsync(
                () =>
                {
                    ReplaceOrInsertSession(session);
                    _activeSession = session;
                    SetErrorMessage(null);
                    NotifySessionStateChanged();
                    SessionChanged?.Invoke(this, new SessionChangedEventArgs(session, Array.Empty<PersistedMessage>()));
                },
                cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await SetErrorAsync(exception.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await SetBusyAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task ResumeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session identifier must not be empty.", nameof(sessionId));
        }

        var projectId = ReadOnDispatcher(() => ActiveProjectId)
            ?? throw new InvalidOperationException("A persisted project must be active before a session can resume.");

        await SetBusyAsync(true, cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await _store.GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Session '{sessionId:D}' does not exist.");
            if (session.ProjectId != projectId)
            {
                throw new InvalidOperationException("A session cannot be resumed from a different active project.");
            }

            var messages = await _store.ListMessagesAsync(sessionId, cancellationToken).ConfigureAwait(false);
            ValidatePersistedMessages(session, messages);

            await InvokeOnDispatcherAsync(
                () =>
                {
                    ReplaceOrInsertSession(session);
                    _activeSession = session;
                    SetErrorMessage(null);
                    NotifySessionStateChanged();
                    SessionChanged?.Invoke(this, new SessionChangedEventArgs(session, messages));
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await SetErrorAsync(exception.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await SetBusyAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task BindRuntimeSessionAsync(
        string runtimeSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeSessionId);
        var activeSession = ReadOnDispatcher(() => ActiveSession)
            ?? throw new InvalidOperationException("A local session must be active before a runtime session ID can be bound.");
        var now = _timeProvider.GetUtcNow();
        var updated = activeSession with
        {
            RuntimeSessionId = runtimeSessionId.Trim(),
            UpdatedUtc = now > activeSession.UpdatedUtc ? now : activeSession.UpdatedUtc,
        };

        await _store.UpsertSessionAsync(updated, cancellationToken).ConfigureAwait(false);
        await InvokeOnDispatcherAsync(
            () =>
            {
                if (_activeSession?.Id == updated.Id)
                {
                    _activeSession = updated;
                    ReplaceOrInsertSession(updated);
                    SetErrorMessage(null);
                    NotifySessionStateChanged();
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<PersistedMessage> AppendMessageAsync(
        string role,
        string content,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = NormalizeRole(role);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Persisted conversation content must contain visible text.", nameof(content));
        }

        Task predecessor;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_messageWriteSync)
        {
            predecessor = _messageWriteTail;
            _messageWriteTail = completion.Task;
        }

        return AppendMessageSerializedAsync(predecessor, completion, normalizedRole, content, cancellationToken);
    }

    public void ClearActiveProject()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(ClearActiveProject);
            return;
        }

        _activeProject = null;
        _activeSession = null;
        _sessions.Clear();
        SetErrorMessage(null);
        NotifyProjectStateChanged();
        NotifySessionStateChanged();
        OnPropertyChanged(nameof(HasSessions));
        SessionChanged?.Invoke(this, new SessionChangedEventArgs(null, Array.Empty<PersistedMessage>()));
    }

    private async Task<PersistedMessage> AppendMessageSerializedAsync(
        Task predecessor,
        TaskCompletionSource<bool> completion,
        string normalizedRole,
        string content,
        CancellationToken cancellationToken)
    {
        await predecessor.ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var activeSession = ReadOnDispatcher(() => ActiveSession)
                ?? throw new InvalidOperationException("A local session must be active before a message can be persisted.");
            var existingMessages = await _store.ListMessagesAsync(activeSession.Id, cancellationToken).ConfigureAwait(false);
            ValidatePersistedMessages(activeSession, existingMessages);
            var nextSequence = existingMessages.Count == 0
                ? 0
                : checked(existingMessages[^1].Sequence + 1);
            var message = new PersistedMessage(
                Guid.NewGuid(),
                activeSession.Id,
                nextSequence,
                normalizedRole,
                content,
                _timeProvider.GetUtcNow());

            await _store.AppendMessageAsync(message, cancellationToken).ConfigureAwait(false);
            var refreshedSession = await _store.GetSessionAsync(activeSession.Id, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The active session disappeared after its message was persisted.");

            await InvokeOnDispatcherAsync(
                () =>
                {
                    ReplaceOrInsertSession(refreshedSession);
                    if (_activeSession?.Id == refreshedSession.Id)
                    {
                        _activeSession = refreshedSession;
                        NotifySessionStateChanged();
                    }
                    SetErrorMessage(null);
                },
                cancellationToken).ConfigureAwait(false);
            return message;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await SetErrorAsync(exception.Message, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            completion.TrySetResult(true);
        }
    }

    private static string NormalizeTitle(string? title, DateTimeOffset now)
    {
        var normalized = string.IsNullOrWhiteSpace(title)
            ? $"Session {now.ToLocalTime():yyyy-MM-dd HH:mm}"
            : title.Trim();
        if (normalized.Length > MaxSessionTitleLength)
        {
            throw new ArgumentOutOfRangeException(nameof(title), $"Session title cannot exceed {MaxSessionTitleLength} characters.");
        }

        return normalized;
    }

    private static string NormalizeRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        var normalized = role.Trim().ToLowerInvariant();
        return normalized switch
        {
            "user" => "user",
            "assistant" => "assistant",
            _ => throw new ArgumentException("Only user and assistant messages belong in conversation history.", nameof(role)),
        };
    }

    private static void ValidatePersistedMessages(
        PersistedSession session,
        IReadOnlyList<PersistedMessage> messages)
    {
        long? previousSequence = null;
        foreach (var message in messages)
        {
            if (message.SessionId != session.Id)
            {
                throw new InvalidOperationException("Persisted session history contains a message owned by another session.");
            }

            if (previousSequence is long previous && message.Sequence <= previous)
            {
                throw new InvalidOperationException("Persisted session message sequence must be strictly increasing.");
            }

            _ = NormalizeRole(message.Role);
            previousSequence = message.Sequence;
        }
    }

    private void ReplaceSessions(IEnumerable<PersistedSession> sessions)
    {
        _sessions.Clear();
        foreach (var session in sessions.OrderByDescending(item => item.UpdatedUtc).ThenBy(item => item.Id))
        {
            _sessions.Add(session);
        }
        OnPropertyChanged(nameof(HasSessions));
    }

    private void ReplaceOrInsertSession(PersistedSession session)
    {
        var existingIndex = -1;
        for (var index = 0; index < _sessions.Count; index++)
        {
            if (_sessions[index].Id == session.Id)
            {
                existingIndex = index;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            _sessions.RemoveAt(existingIndex);
        }

        var insertIndex = 0;
        while (insertIndex < _sessions.Count
               && (_sessions[insertIndex].UpdatedUtc > session.UpdatedUtc
                   || (_sessions[insertIndex].UpdatedUtc == session.UpdatedUtc
                       && _sessions[insertIndex].Id.CompareTo(session.Id) < 0)))
        {
            insertIndex++;
        }

        _sessions.Insert(insertIndex, session);
        OnPropertyChanged(nameof(HasSessions));
    }

    private async Task SetBusyAsync(bool value, CancellationToken cancellationToken)
    {
        await InvokeOnDispatcherAsync(
            () =>
            {
                if (_isBusy == value)
                {
                    return;
                }
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(StatusText));
            },
            cancellationToken).ConfigureAwait(false);
    }

    private Task SetErrorAsync(string? message, CancellationToken cancellationToken) =>
        InvokeOnDispatcherAsync(() => SetErrorMessage(message), cancellationToken);

    private void SetErrorMessage(string? message)
    {
        var normalized = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        if (string.Equals(_errorMessage, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _errorMessage = normalized;
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
    }

    private void NotifyProjectStateChanged()
    {
        OnPropertyChanged(nameof(ActiveProject));
        OnPropertyChanged(nameof(ActiveProjectId));
        OnPropertyChanged(nameof(ActiveProjectName));
        OnPropertyChanged(nameof(HasActiveProject));
        OnPropertyChanged(nameof(StatusText));
    }

    private void NotifySessionStateChanged()
    {
        OnPropertyChanged(nameof(ActiveSession));
        OnPropertyChanged(nameof(ActiveSessionId));
        OnPropertyChanged(nameof(ActiveSessionTitle));
        OnPropertyChanged(nameof(ActiveRuntimeSessionId));
        OnPropertyChanged(nameof(HasRuntimeSessionId));
        OnPropertyChanged(nameof(HasActiveSession));
        OnPropertyChanged(nameof(StatusText));
    }

    private T ReadOnDispatcher<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Dispatcher.CheckAccess() ? action() : Dispatcher.Invoke(action);
    }

    private Task InvokeOnDispatcherAsync(Action action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        if (Dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.InvokeAsync(action, DispatcherPriority.DataBind, cancellationToken).Task;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
