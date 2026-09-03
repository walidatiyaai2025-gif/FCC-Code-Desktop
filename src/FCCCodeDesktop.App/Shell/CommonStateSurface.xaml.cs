using System.Windows;
using System.Windows.Controls;

namespace FCCCodeDesktop.App.Shell;

public partial class CommonStateSurface : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(CommonStateModel),
        typeof(CommonStateSurface),
        new PropertyMetadata(null, OnStateChanged));

    public CommonStateSurface()
    {
        InitializeComponent();
        State ??= CommonStateModel.Empty("Nothing to show", "This surface has no content yet.");
    }

    public CommonStateModel State
    {
        get => (CommonStateModel)GetValue(StateProperty);
        set => SetValue(StateProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is CommonStateSurface surface && args.NewValue is null)
        {
            surface.SetCurrentValue(
                StateProperty,
                CommonStateModel.Empty("Nothing to show", "This surface has no content yet."));
        }
    }
}
