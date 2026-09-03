using System.Windows;
using System.Windows.Controls;

namespace FCCCodeDesktop.App.Shell;

public partial class BottomToolPanel : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(BottomToolPanelState),
        typeof(BottomToolPanel),
        new PropertyMetadata(null, OnStateChanged));

    public static readonly DependencyProperty LayoutStateProperty = DependencyProperty.Register(
        nameof(LayoutState),
        typeof(WorkspaceLayoutState),
        typeof(BottomToolPanel),
        new PropertyMetadata(null, OnLayoutStateChanged));

    public BottomToolPanel()
    {
        InitializeComponent();
        State ??= new BottomToolPanelState();
        LayoutState ??= new WorkspaceLayoutState();
    }

    public BottomToolPanelState State
    {
        get => (BottomToolPanelState)GetValue(StateProperty);
        set => SetValue(StateProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    public WorkspaceLayoutState LayoutState
    {
        get => (WorkspaceLayoutState)GetValue(LayoutStateProperty);
        set => SetValue(LayoutStateProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is BottomToolPanel panel && args.NewValue is null)
        {
            panel.SetCurrentValue(StateProperty, new BottomToolPanelState());
        }
    }

    private static void OnLayoutStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is BottomToolPanel panel && args.NewValue is null)
        {
            panel.SetCurrentValue(LayoutStateProperty, new WorkspaceLayoutState());
        }
    }
}
