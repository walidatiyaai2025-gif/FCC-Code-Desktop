using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FCCCodeDesktop.App.Shell;

public partial class CommandPalette : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(CommandPaletteState),
        typeof(CommandPalette),
        new PropertyMetadata(null, OnStateChanged));

    private IInputElement? _focusBeforeOpen;

    public CommandPalette()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public CommandPaletteState State
    {
        get => (CommandPaletteState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var control = (CommandPalette)dependencyObject;
        control.DetachState(args.OldValue as CommandPaletteState);
        control.AttachState(args.NewValue as CommandPaletteState);
        control.UpdateOpenState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        State ??= new CommandPaletteState();
        UpdateOpenState();
    }

    private void AttachState(CommandPaletteState? state)
    {
        if (state is not null)
        {
            state.PropertyChanged += OnStatePropertyChanged;
        }
    }

    private void DetachState(CommandPaletteState? state)
    {
        if (state is not null)
        {
            state.PropertyChanged -= OnStatePropertyChanged;
        }
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandPaletteState.IsOpen))
        {
            UpdateOpenState();
        }
    }

    private void UpdateOpenState()
    {
        var shouldOpen = State?.IsOpen == true;
        if (shouldOpen)
        {
            if (Visibility != Visibility.Visible)
            {
                _focusBeforeOpen = Keyboard.FocusedElement;
            }

            Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(
                    () =>
                    {
                        SearchBox.Focus();
                        SearchBox.SelectAll();
                    }));
            return;
        }

        Visibility = Visibility.Collapsed;
        var focusTarget = _focusBeforeOpen;
        _focusBeforeOpen = null;
        if (focusTarget is not null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() => Keyboard.Focus(focusTarget)));
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (State?.IsOpen != true)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                State.MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                State.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                State.ExecuteSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                State.Close();
                e.Handled = true;
                break;
        }
    }

    private void OnCommandDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (State?.IsOpen == true)
        {
            State.ExecuteSelected();
        }
    }
}
