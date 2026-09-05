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

    public static readonly DependencyProperty ComposerProperty = DependencyProperty.Register(
        nameof(Composer),
        typeof(ComposerState),
        typeof(ConversationSurface),
        new PropertyMetadata(null, OnComposerChanged));

    private static readonly TimeSpan TailScrollCoalesceInterval = TimeSpan.FromMilliseconds(50);
    private const double TailTolerancePixels = 32d;

    private readonly DispatcherTimer _tailScrollTimer;
    private bool _conversationFollowsTail = true;
    private bool _toolTimelineFollowsTail = true;
    private bool _conversationScrollRequested;
    private bool _toolTimelineScrollRequested;

    public ConversationSurface()
    {
        InitializeComponent();
        ConfigureVirtualization(ConversationItems);
        ConfigureVirtualization(ToolTimelineItems);
        ConversationItems.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler(OnConversationScrollChanged));
        ToolTimelineItems.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler(OnToolTimelineScrollChanged));
        _tailScrollTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TailScrollCoalesceInterval,
        };
        _tailScrollTimer.Tick += OnTailScrollTimerTick;

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

    private static void ConfigureVirtualization(ListBox listBox)
    {
        ScrollViewer.SetCanContentScroll(listBox, true);
        VirtualizingPanel.SetIsVirtualizing(listBox, true);
        VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(listBox, ScrollUnit.Pixel);
        VirtualizingPanel.SetCacheLength(listBox, new VirtualizationCacheLength(1d));
        VirtualizingPanel.SetCacheLengthUnit(listBox, VirtualizationCacheLengthUnit.Page);
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
        surface._conversationFollowsTail = true;
        surface._toolTimelineFollowsTail = true;
        surface.ScheduleConversationTail(force: true);
        surface.ScheduleToolTimelineTail(force: true);
    }

    private static void OnComposerChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ConversationSurface surface && args.NewValue is null)
        {
            surface.SetCurrentValue(ComposerProperty, new ComposerState());
        }
    }

    private void OnPresentationCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, State.Messages))
        {
            ScheduleConversationTail(force: e.Action == NotifyCollectionChangedAction.Reset);
            return;
        }

        if (ReferenceEquals(sender, State.ToolActivities))
        {
            ScheduleToolTimelineTail(force: e.Action == NotifyCollectionChangedAction.Reset);
        }
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StreamingConversationState.LastRuntimeSequence)
            or nameof(StreamingConversationState.IsStreaming))
        {
            ScheduleConversationTail();
        }

        if (e.PropertyName is nameof(StreamingConversationState.HasToolActivities))
        {
            ScheduleToolTimelineTail();
        }
    }

    private void OnConversationScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange != 0d || e.ViewportHeightChange != 0d)
        {
            return;
        }

        _conversationFollowsTail = IsNearTail(e);
    }

    private void OnToolTimelineScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange != 0d || e.ViewportHeightChange != 0d)
        {
            return;
        }

        _toolTimelineFollowsTail = IsNearTail(e);
    }

    private static bool IsNearTail(ScrollChangedEventArgs e) =>
        e.VerticalOffset >= Math.Max(0d, e.ExtentHeight - e.ViewportHeight - TailTolerancePixels);

    private void ScheduleConversationTail(bool force = false)
    {
        if (force)
        {
            _conversationFollowsTail = true;
        }

        if (!_conversationFollowsTail)
        {
            return;
        }

        _conversationScrollRequested = true;
        EnsureTailScrollTimer();
    }

    private void ScheduleToolTimelineTail(bool force = false)
    {
        if (force)
        {
            _toolTimelineFollowsTail = true;
        }

        if (!_toolTimelineFollowsTail)
        {
            return;
        }

        _toolTimelineScrollRequested = true;
        EnsureTailScrollTimer();
    }

    private void EnsureTailScrollTimer()
    {
        if (!_tailScrollTimer.IsEnabled)
        {
            _tailScrollTimer.Start();
        }
    }

    private void OnTailScrollTimerTick(object? sender, EventArgs e)
    {
        _tailScrollTimer.Stop();

        var scrollConversation = _conversationScrollRequested;
        var scrollToolTimeline = _toolTimelineScrollRequested;
        _conversationScrollRequested = false;
        _toolTimelineScrollRequested = false;

        if (scrollConversation && _conversationFollowsTail && State.Messages.Count > 0)
        {
            ConversationItems.ScrollIntoView(State.Messages[^1]);
        }

        if (scrollToolTimeline && _toolTimelineFollowsTail && State.ToolActivities.Count > 0)
        {
            ToolTimelineItems.ScrollIntoView(State.ToolActivities[^1]);
        }
    }
}
