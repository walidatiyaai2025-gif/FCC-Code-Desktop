using System.Windows;
using System.Windows.Controls;
using FCCCodeDesktop.Core.State;

namespace FCCCodeDesktop.App.Conversation;

public partial class SessionWorkspaceSurface : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(SessionWorkspaceState),
        typeof(SessionWorkspaceSurface),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ConversationContentProperty = DependencyProperty.Register(
        nameof(ConversationContent),
        typeof(object),
        typeof(SessionWorkspaceSurface),
        new PropertyMetadata(null));

    private bool _selectionChangeInProgress;

    public SessionWorkspaceSurface()
    {
        InitializeComponent();
    }

    public SessionWorkspaceState? State
    {
        get => (SessionWorkspaceState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public object? ConversationContent
    {
        get => GetValue(ConversationContentProperty);
        set => SetValue(ConversationContentProperty, value);
    }

    private async void OnNewSessionClicked(object sender, RoutedEventArgs e)
    {
        if (State is null || !State.HasActiveProject || State.IsBusy)
        {
            return;
        }

        var session = await State.CreateSessionAsync().ConfigureAwait(true);
        SessionHistoryItems.SelectedItem = session;
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        if (State is null || !State.HasActiveProject || State.IsBusy)
        {
            return;
        }

        await State.RefreshAsync().ConfigureAwait(true);
    }

    private async void OnSessionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectionChangeInProgress
            || State is null
            || State.IsBusy
            || SessionHistoryItems.SelectedItem is not PersistedSession selectedSession
            || State.ActiveSessionId == selectedSession.Id)
        {
            return;
        }

        _selectionChangeInProgress = true;
        try
        {
            await State.ResumeSessionAsync(selectedSession.Id).ConfigureAwait(true);
        }
        catch (InvalidOperationException)
        {
            SessionHistoryItems.SelectedItem = State.ActiveSession;
        }
        finally
        {
            _selectionChangeInProgress = false;
        }
    }
}
