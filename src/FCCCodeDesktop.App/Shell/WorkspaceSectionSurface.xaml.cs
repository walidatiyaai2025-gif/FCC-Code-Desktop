using System.Windows;
using System.Windows.Controls;

namespace FCCCodeDesktop.App.Shell;

public partial class WorkspaceSectionSurface : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(WorkspaceNavigationState),
        typeof(WorkspaceSectionSurface),
        new PropertyMetadata(null, OnStateChanged));

    public WorkspaceSectionSurface()
    {
        InitializeComponent();
        State ??= new WorkspaceNavigationState();
    }

    public WorkspaceNavigationState State
    {
        get => (WorkspaceNavigationState)GetValue(StateProperty);
        set => SetValue(StateProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is WorkspaceSectionSurface surface && args.NewValue is null)
        {
            surface.SetCurrentValue(StateProperty, new WorkspaceNavigationState());
        }
    }
}
