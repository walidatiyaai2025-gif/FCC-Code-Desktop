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
}
