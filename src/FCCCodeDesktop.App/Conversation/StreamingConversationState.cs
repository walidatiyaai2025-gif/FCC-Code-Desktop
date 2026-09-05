using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Threading;
using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.App.Conversation;

public enum ConversationMessageRole
{
    User,
    Assistant,
}

public enum ToolActivityStatus
{
    Running,
    ResultReceived,
}

public sealed class ConversationMessageState : INotifyPropertyChanged
{
    private readonly StringBuilder _text = new();
    private bool _isStreaming;
    private bool _contentParsed;
    private IReadOnlyList<ConversationContentBlock> _contentBlocks = Array.Empty<ConversationContentBlock>();

    internal ConversationMessageState(
        long messageId,
        ConversationMessageRole role,
        string initialText,
        bool isStreaming,
        bool deferContentParsing = false)
    {
        if (messageId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(messageId), "Message identifier must be positive.");
        }

        MessageId = messageId;
        Role = role;
        _isStreaming = isStreaming;
        _text.Append(initialText);
        if (!isStreaming && !deferContentParsing)
        {
            _contentBlocks = ConversationContentParser.Parse(initialText);
            _contentParsed = true;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public long MessageId { get; }

    public ConversationMessageRole Role { get; }

    public string SenderLabel => Role switch
    {
        ConversationMessageRole.User => "You",
        ConversationMessageRole.Assistant => "Agent",
        _ => throw new InvalidOperationException($"Unsupported conversation role: {Role}"),
    };

    public string Text => _text.ToString();

    public IReadOnlyList<ConversationContentBlock> ContentBlocks
    {
        get
        {
            if (!IsStreaming && !_contentParsed)
            {
                _contentBlocks = ConversationContentParser.Parse(Text);
                _contentParsed = true;
            }

            return _contentBlocks;
        }
    }

    public bool IsStreaming
    {
        get => _isStreaming;
        private set
        {
            if (_isStreaming == value)
            {
                return;
            }

            _isStreaming = value;
            OnPropertyChanged();
        }
    }

    internal void AppendDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        _text.Append(delta);
        OnPropertyChanged(nameof(Text));
    }

    internal void Complete()
    {
        if (!IsStreaming && _contentParsed)
        {
            return;
        }

        _contentBlocks = ConversationContentParser.Parse(Text);
        _contentParsed = true;
        OnPropertyChanged(nameof(ContentBlocks));
        IsStreaming = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class ToolActivityState : INotifyPropertyChanged
{
    private string? _progressText;
    private string? _resultText;
    private ToolActivityStatus _status;
    private DateTimeOffset _lastUpdatedUtc;

    internal ToolActivityState(
        long activityId,
        string? correlationId,
        string toolName,
        DateTimeOffset startedUtc)
    {
        if (activityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activityId), "Tool activity identifier must be positive.");
        }

        ActivityId = activityId;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        ToolName = string.IsNullOrWhiteSpace(toolName) ? "Tool" : toolName.Trim();
        StartedUtc = startedUtc.ToUniversalTime();
        _lastUpdatedUtc = StartedUtc;
        _status = ToolActivityStatus.Running;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public long ActivityId { get; }

    public string? CorrelationId { get; }

    public string ToolName { get; }

    public DateTimeOffset StartedUtc { get; }

    public DateTimeOffset LastUpdatedUtc
    {
        get => _lastUpdatedUtc;
        private set
        {
            if (_lastUpdatedUtc == value)
            {
                return;
            }

            _lastUpdatedUtc = value;
            OnPropertyChanged();
        }
    }

    public ToolActivityStatus Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusLabel));
        }
    }

    public string StatusLabel => Status switch
    {
        ToolActivityStatus.Running => "Running",
        ToolActivityStatus.ResultReceived => "Result",
        _ => throw new InvalidOperationException($"Unsupported tool activity status: {Status}"),
    };

    public string? ProgressText
    {
        get => _progressText;
        private set
        {
            if (string.Equals(_progressText, value, StringComparison.Ordinal))
            {
                return;
            }

            _progressText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProgress));
        }
    }

    public string? ResultText
    {
        get => _resultText;
        private set
        {
            if (string.Equals(_resultText, value, StringComparison.Ordinal))
            {
                return;
            }

            _resultText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasResult));
        }
    }

    public bool HasProgress => !string.IsNullOrWhiteSpace(ProgressText);

    public bool HasResult => !string.IsNullOrWhiteSpace(ResultText);

    internal void RecordProgress(string? text, DateTimeOffset occurredUtc)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            ProgressText = text;
        }

        LastUpdatedUtc = occurredUtc.ToUniversalTime();
    }

    internal void RecordResult(string? text, DateTimeOffset occurredUtc)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            ResultText = text;
        }

        LastUpdatedUtc = occurredUtc.ToUniversalTime();
        Status = ToolActivityStatus.ResultReceived;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class StreamingConversationState : DispatcherObject, INotifyPropertyChanged
{
    private readonly ObservableCollection<ConversationMessageState> _messages = [];
    private readonly ObservableCollection<ToolActivityState> _toolActivities = [];
    private readonly Dictionary<string, ToolActivityState> _activeToolsByCorrelation = new(StringComparer.Ordinal);
    private readonly ReadOnlyObservableCollection<ConversationMessageState> _readonlyMessages;
    private readonly ReadOnlyObservableCollection<ToolActivityState> _readonlyToolActivities;
    private ConversationMessageState? _activeAssistantMessage;
    private long? _lastRuntimeSequence;
    private long _nextMessageId = 1;
    private long _nextToolActivityId = 1;

    public StreamingConversationState()
    {
        _readonlyMessages = new ReadOnlyObservableCollection<ConversationMessageState>(_messages);
        _readonlyToolActivities = new ReadOnlyObservableCollection<ToolActivityState>(_toolActivities);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<ConversationMessageState> Messages => _readonlyMessages;

    public ReadOnlyObservableCollection<ToolActivityState> ToolActivities => _readonlyToolActivities;

    public bool HasMessages => _messages.Count > 0;

    public bool HasToolActivities => _toolActivities.Count > 0;

    public bool IsStreaming => _activeAssistantMessage is not null;

    public long? LastRuntimeSequence => _lastRuntimeSequence;

    public void AddUserMessage(string text)
    {
        VerifyAccess();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("A user message must contain visible text.", nameof(text));
        }

        AddMessage(new ConversationMessageState(NextMessageId(), ConversationMessageRole.User, text, false));
    }

    public void LoadPersistedMessages(IReadOnlyList<PersistedMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        VerifyAccess();
        Reset();

        long? previousSequence = null;
        foreach (var message in messages)
        {
            if (previousSequence is long previous && message.Sequence <= previous)
            {
                throw new InvalidOperationException("Persisted conversation messages must have a strictly increasing sequence.");
            }

            var role = message.Role.Trim().ToLowerInvariant() switch
            {
                "user" => ConversationMessageRole.User,
                "assistant" => ConversationMessageRole.Assistant,
                _ => throw new InvalidOperationException($"Unsupported persisted conversation role: {message.Role}"),
            };
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                throw new InvalidOperationException("Persisted conversation messages must contain visible text.");
            }

            AddMessage(new ConversationMessageState(
                NextMessageId(),
                role,
                message.Content,
                false,
                deferContentParsing: true));
            previousSequence = message.Sequence;
        }
    }

    public Task ApplyRuntimeEventAsync(AgentRuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);
        cancellationToken.ThrowIfCancellationRequested();
        if (Dispatcher.CheckAccess())
        {
            ApplyRuntimeEventCore(runtimeEvent);
            return Task.CompletedTask;
        }

        return Dispatcher.InvokeAsync(
            () => ApplyRuntimeEventCore(runtimeEvent),
            DispatcherPriority.DataBind,
            cancellationToken).Task;
    }

    public void Reset()
    {
        VerifyAccess();
        _activeAssistantMessage?.Complete();

        _messages.Clear();
        _toolActivities.Clear();
        _activeToolsByCorrelation.Clear();
        _activeAssistantMessage = null;
        _lastRuntimeSequence = null;
        _nextMessageId = 1;
        _nextToolActivityId = 1;
        OnPropertyChanged(nameof(HasMessages));
        OnPropertyChanged(nameof(HasToolActivities));
        OnPropertyChanged(nameof(IsStreaming));
        OnPropertyChanged(nameof(LastRuntimeSequence));
    }

    private void ApplyRuntimeEventCore(AgentRuntimeEvent runtimeEvent)
    {
        VerifyAccess();
        AssertRuntimeSequence(runtimeEvent.Sequence);
        _lastRuntimeSequence = runtimeEvent.Sequence;
        OnPropertyChanged(nameof(LastRuntimeSequence));

        switch (runtimeEvent.Kind)
        {
            case AgentRuntimeEventKind.AssistantTextDelta:
                AppendAssistantDelta(runtimeEvent.Text);
                break;
            case AgentRuntimeEventKind.ToolStarted:
                RecordToolStarted(runtimeEvent);
                break;
            case AgentRuntimeEventKind.ToolProgress:
                RecordToolProgress(runtimeEvent);
                break;
            case AgentRuntimeEventKind.ToolResult:
                RecordToolResult(runtimeEvent);
                break;
            case AgentRuntimeEventKind.Completion:
                CompleteAssistantMessage();
                break;
            default:
                break;
        }
    }

    private void AppendAssistantDelta(string? delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        if (_activeAssistantMessage is null)
        {
            _activeAssistantMessage = new ConversationMessageState(
                NextMessageId(),
                ConversationMessageRole.Assistant,
                string.Empty,
                true);
            AddMessage(_activeAssistantMessage);
            OnPropertyChanged(nameof(IsStreaming));
        }

        _activeAssistantMessage.AppendDelta(delta);
    }

    private void CompleteAssistantMessage()
    {
        if (_activeAssistantMessage is null)
        {
            return;
        }

        _activeAssistantMessage.Complete();
        _activeAssistantMessage = null;
        OnPropertyChanged(nameof(IsStreaming));
    }

    private void RecordToolStarted(AgentRuntimeEvent runtimeEvent)
    {
        var activity = CreateToolActivity(
            runtimeEvent.CorrelationId,
            string.IsNullOrWhiteSpace(runtimeEvent.Text) ? "Tool" : runtimeEvent.Text,
            runtimeEvent.OccurredUtc);
        if (activity.CorrelationId is { } correlationId)
        {
            _activeToolsByCorrelation[correlationId] = activity;
        }
    }

    private void RecordToolProgress(AgentRuntimeEvent runtimeEvent)
    {
        var activity = ResolveToolActivity(runtimeEvent.CorrelationId)
            ?? CreateToolActivity(runtimeEvent.CorrelationId, "Tool activity", runtimeEvent.OccurredUtc);
        activity.RecordProgress(runtimeEvent.Text, runtimeEvent.OccurredUtc);
    }

    private void RecordToolResult(AgentRuntimeEvent runtimeEvent)
    {
        var activity = ResolveToolActivity(runtimeEvent.CorrelationId)
            ?? CreateToolActivity(runtimeEvent.CorrelationId, "Tool result", runtimeEvent.OccurredUtc);
        activity.RecordResult(runtimeEvent.Text, runtimeEvent.OccurredUtc);
        if (activity.CorrelationId is { } correlationId
            && _activeToolsByCorrelation.TryGetValue(correlationId, out var activeActivity)
            && ReferenceEquals(activeActivity, activity))
        {
            _activeToolsByCorrelation.Remove(correlationId);
        }
    }

    private ToolActivityState? ResolveToolActivity(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return null;
        }

        return _activeToolsByCorrelation.TryGetValue(correlationId.Trim(), out var activity)
            ? activity
            : null;
    }

    private ToolActivityState CreateToolActivity(string? correlationId, string toolName, DateTimeOffset occurredUtc)
    {
        var activity = new ToolActivityState(NextToolActivityId(), correlationId, toolName, occurredUtc);
        _toolActivities.Add(activity);
        OnPropertyChanged(nameof(HasToolActivities));
        return activity;
    }

    private void AssertRuntimeSequence(long sequence)
    {
        if (_lastRuntimeSequence is not long lastSequence)
        {
            return;
        }

        var expectedSequence = checked(lastSequence + 1);
        if (sequence != expectedSequence)
        {
            throw new InvalidOperationException(
                $"Runtime event sequence must remain contiguous. Expected {expectedSequence}, received {sequence}.");
        }
    }

    private long NextMessageId()
    {
        var messageId = _nextMessageId;
        _nextMessageId = checked(_nextMessageId + 1);
        return messageId;
    }

    private long NextToolActivityId()
    {
        var activityId = _nextToolActivityId;
        _nextToolActivityId = checked(_nextToolActivityId + 1);
        return activityId;
    }

    private void AddMessage(ConversationMessageState message)
    {
        _messages.Add(message);
        OnPropertyChanged(nameof(HasMessages));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
