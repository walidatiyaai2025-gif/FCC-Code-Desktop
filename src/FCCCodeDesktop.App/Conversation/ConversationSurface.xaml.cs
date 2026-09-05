using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FCCCodeDesktop.App.Conversation;

public partial class ConversationSurface : UserControl
{
    private static readonly TimeSpan ScrollThrottle = TimeSpan.FromMilliseconds(75);

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(StreamingConversationState),
        typeof(ConversationSurface),
        new PropertyMetadata(null, OnStateChanged));

    public static readonly DependencyProperty ComposerProperty = DependencyProperty.Register(
        nameof(Composer),
        typeof(ComposerState),
        typeof(ConversationSurface),
        new PropertyMetadata(null, OnComposerChanged));

    private readonly DispatcherTimer _scrollTimer;

    public ConversationSurface()
    {
        InitializeComponent();
        _scrollTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = ScrollThrottle,
        };
        _scrollTimer.Tick += OnScrollTimerTick;
        State ??= new StreamingConversationState();
        Composer ??= new ComposerState();
    }

    public StreamingConversationState State
    {
        get => (StreamingConversationState)GetValue(StateProperty);
        set => SetValue(StateProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    public ComposerState Composer
    {
        get => (ComposerState)GetValue(ComposerProperty);
        set => SetValue(ComposerProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ConversationSurface surface) return;
        if (args.OldValue is StreamingConversationState oldState)
        {
            oldState.PropertyChanged -= surface.OnStatePropertyChanged;
            ((INotifyCollectionChanged)oldState.Messages).CollectionChanged -= surface.OnPresentationCollectionChanged;
            ((INotifyCollectionChanged)oldState.ToolActivities).CollectionChanged -= surface.OnPresentationCollectionChanged;
        }

        if (args.NewValue is not StreamingConversationState newState)
        {
            surface.SetCurrentValue(StateProperty, new StreamingConversationState());
            return;
        }

        newState.PropertyChanged += surface.OnStatePropertyChanged;
        ((INotifyCollectionChanged)newState.Messages).CollectionChanged += surface.OnPresentationCollectionChanged;
        ((INotifyCollectionChanged)newState.ToolActivities).CollectionChanged += surface.OnPresentationCollectionChanged;
        surface.ScheduleScrollToLatest();
    }

    private static void OnComposerChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ConversationSurface surface && args.NewValue is null)
        {
            surface.SetCurrentValue(ComposerProperty, new ComposerState());
        }
    }

    private void OnPresentationCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleScrollToLatest();

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StreamingConversationState.LastRuntimeSequence)
            or nameof(StreamingConversationState.IsStreaming)
            or nameof(StreamingConversationState.HasToolActivities))
        {
            ScheduleScrollToLatest();
        }
    }

    private void ScheduleScrollToLatest()
    {
        if (!_scrollTimer.IsEnabled)
        {
            _scrollTimer.Start();
        }
    }

    private void OnScrollTimerTick(object? sender, EventArgs e)
    {
        _scrollTimer.Stop();
        ScrollToLatest();
    }

    private void ScrollToLatest()
    {
        if (State.Messages.Count > 0) ConversationItems.ScrollIntoView(State.Messages[^1]);
        if (State.ToolActivities.Count > 0) ToolTimelineItems.ScrollIntoView(State.ToolActivities[^1]);
    }
}
