using System.Windows;
using System.Windows.Controls;

namespace FCCCodeDesktop.App.Conversation;

public partial class TaskExecutionSurface : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(TaskExecutionState),
        typeof(TaskExecutionSurface),
        new PropertyMetadata(null));

    public TaskExecutionSurface()
    {
        InitializeComponent();
    }

    public TaskExecutionState? State
    {
        get => (TaskExecutionState?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        if (State is null)
        {
            return;
        }

        try
        {
            await State.RequestStopAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State.ReportControlError(exception.Message);
        }
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        if (State is null)
        {
            return;
        }

        try
        {
            await State.RetryAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State.ReportControlError(exception.Message);
        }
    }
}
