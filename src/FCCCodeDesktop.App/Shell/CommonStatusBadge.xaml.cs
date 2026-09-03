using System.Windows;
using System.Windows.Controls;

namespace FCCCodeDesktop.App.Shell;

public partial class CommonStatusBadge : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(CommonStateKind),
        typeof(CommonStatusBadge),
        new PropertyMetadata(CommonStateKind.Info));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(CommonStatusBadge),
        new PropertyMetadata(string.Empty));

    public CommonStatusBadge()
    {
        InitializeComponent();
    }

    public CommonStateKind Kind
    {
        get => (CommonStateKind)GetValue(KindProperty);
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown common state kind.");
            }

            SetValue(KindProperty, value);
        }
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value ?? string.Empty);
    }
}
