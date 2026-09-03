using System.Windows;
using System.Windows.Controls;

namespace FCCCodeDesktop.App.Shell;

public partial class WorkspaceLayout : UserControl
{
    public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(
        nameof(LeftContent),
        typeof(object),
        typeof(WorkspaceLayout),
        new PropertyMetadata(null));

    public static readonly DependencyProperty PrimaryContentProperty = DependencyProperty.Register(
        nameof(PrimaryContent),
        typeof(object),
        typeof(WorkspaceLayout),
        new PropertyMetadata(null));

    public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register(
        nameof(RightContent),
        typeof(object),
        typeof(WorkspaceLayout),
        new PropertyMetadata(null));

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(WorkspaceLayoutState),
        typeof(WorkspaceLayout),
        new PropertyMetadata(null, OnStateChanged));

    public WorkspaceLayout()
    {
        InitializeComponent();
        State ??= new WorkspaceLayoutState();
    }

    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    public object? PrimaryContent
    {
        get => GetValue(PrimaryContentProperty);
        set => SetValue(PrimaryContentProperty, value);
    }

    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    public WorkspaceLayoutState State
    {
        get => (WorkspaceLayoutState)GetValue(StateProperty);
        set => SetValue(StateProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not WorkspaceLayout layout)
        {
            return;
        }

        if (args.NewValue is null)
        {
            layout.SetCurrentValue(StateProperty, new WorkspaceLayoutState());
        }
    }
}
