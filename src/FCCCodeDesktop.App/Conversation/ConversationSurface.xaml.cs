using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FCCCodeDesktop.App.Conversation;

public partial class ConversationSurface : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(StreamingConversationState),
        typeof(ConversationSurface),
        new PropertyMetadata(null, OnStateChanged));

    private bool _scrollPending;

    public ConversationSurface()
    {
        InitializeComponent();
        State ??= new StreamingConversationState();
    }

    public StreamingConversationState State
    {
        get => (StreamingConversationState)GetValue(StateProperty);
        set => SetValue(StateProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ConversationSurface surface)
        {
            return;
        }

        if (args.OldValue is StreamingConversationState oldState)
        {
            oldState.PropertyChanged -= surface.OnStatePropertyChanged;
            ((INotifyCollectionChanged)oldState.Messages).CollectionChanged -= surface.OnMessagesChanged;
        }

        if (args.NewValue is not StreamingConversationState newState)
        {
            surface.SetCurrentValue(StateProperty, new StreamingConversationState());
            return;
        }

        newState.PropertyChanged += surface.OnStatePropertyChanged;
        ((INotifyCollectionChanged)newState.Messages).CollectionChanged += surface.OnMessagesChanged;
        surface.ScheduleScrollToLatest();
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ScheduleScrollToLatest();

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StreamingConversationState.LastRuntimeSequence)
            or nameof(StreamingConversationState.IsStreaming))
        {
            ScheduleScrollToLatest();
        }
    }

    private void ScheduleScrollToLatest()
    {
        if (_scrollPending)
        {
            return;
        }

        _scrollPending = true;
        _ = Dispatcher.BeginInvoke(
            new Action(
                () =>
                {
                    _scrollPending = false;
                    ScrollToLatest();
                }),
            DispatcherPriority.Background);
    }

    private void ScrollToLatest()
    {
        if (State.Messages.Count == 0)
        {
            return;
        }

        ConversationItems.ScrollIntoView(State.Messages[^1]);
    }
}
