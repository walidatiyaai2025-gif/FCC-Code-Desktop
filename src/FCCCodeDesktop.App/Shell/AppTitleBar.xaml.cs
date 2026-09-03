using System.Windows;
using System.Windows.Controls;

namespace FCCCodeDesktop.App.Shell;

public partial class AppTitleBar : UserControl
{
    public static readonly DependencyProperty ProductNameProperty = DependencyProperty.Register(
        nameof(ProductName),
        typeof(string),
        typeof(AppTitleBar),
        new PropertyMetadata("FCC Code Desktop"));

    public static readonly DependencyProperty ContextContentProperty = DependencyProperty.Register(
        nameof(ContextContent),
        typeof(object),
        typeof(AppTitleBar),
        new PropertyMetadata(null));

    public static readonly DependencyProperty StatusContentProperty = DependencyProperty.Register(
        nameof(StatusContent),
        typeof(object),
        typeof(AppTitleBar),
        new PropertyMetadata(null));

    public AppTitleBar()
    {
        InitializeComponent();
    }

    public string ProductName
    {
        get => (string)GetValue(ProductNameProperty);
        set => SetValue(ProductNameProperty, value);
    }

    public object? ContextContent
    {
        get => GetValue(ContextContentProperty);
        set => SetValue(ContextContentProperty, value);
    }

    public object? StatusContent
    {
        get => GetValue(StatusContentProperty);
        set => SetValue(StatusContentProperty, value);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not { } window)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }
}
