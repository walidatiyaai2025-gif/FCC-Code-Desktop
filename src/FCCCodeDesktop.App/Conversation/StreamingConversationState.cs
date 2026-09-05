using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Threading;
using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.App.Conversation;

public enum ConversationMessageRole
{
    User,
    Assistant,
}

public sealed class ConversationMessageState : INotifyPropertyChanged
{
    private readonly StringBuilder _text = new();
    private bool _isStreaming;

    internal ConversationMessageState(long messageId, ConversationMessageRole role, string initialText, bool isStreaming)
    {
        if (messageId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(messageId), "Message identifier must be positive.");
        }

        MessageId = messageId;
        Role = role;
        _isStreaming = isStreaming;
        _text.Append(initialText);
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
        IsStreaming = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class StreamingConversationState : DispatcherObject, INotifyPropertyChanged
{
    private readonly ObservableCollection<ConversationMessageState> _messages = [];
    private readonly ReadOnlyObservableCollection<ConversationMessageState> _readonlyMessages;
    private ConversationMessageState? _activeAssistantMessage;
    private long? _lastRuntimeSequence;
    private long _nextMessageId = 1;

    public StreamingConversationState()
    {
        _readonlyMessages = new ReadOnlyObservableCollection<ConversationMessageState>(_messages);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<ConversationMessageState> Messages => _readonlyMessages;

    public bool HasMessages => _messages.Count > 0;

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

        foreach (var message in _messages)
        {
            message.Complete();
        }

        _messages.Clear();
        _activeAssistantMessage = null;
        _lastRuntimeSequence = null;
        _nextMessageId = 1;
        OnPropertyChanged(nameof(HasMessages));
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
            case AgentRuntimeEventKind.Completion:
                CompleteAssistantMessage();
                break;
            default:
                // P05-001 intentionally projects only assistant text. Tool/runtime/status events
                // remain typed and are rendered by their owning later conversation tasks.
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

    private void AddMessage(ConversationMessageState message)
    {
        _messages.Add(message);
        OnPropertyChanged(nameof(HasMessages));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
