using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace FCCCodeDesktop.App.Conversation;

public partial class ConversationComposer : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(ComposerState),
        typeof(ConversationComposer),
        new PropertyMetadata(null, OnStateChanged));

    public ConversationComposer()
    {
        InitializeComponent();
        State ??= new ComposerState();
    }

    public ComposerState State
    {
        get => (ComposerState)GetValue(StateProperty);
        set => SetValue(StateProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ConversationComposer composer && args.NewValue is null)
        {
            composer.SetCurrentValue(StateProperty, new ComposerState());
        }
    }

    private void AttachFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateFileDialog("Attach files to this message");
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        foreach (var path in dialog.FileNames)
        {
            if (!State.TryAddAttachment(path))
            {
                break;
            }
        }
    }

    private void AddContextButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateFileDialog("Add file references as conversation context");
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        foreach (var path in dialog.FileNames)
        {
            var fullPath = Path.GetFullPath(path);
            if (!State.TryAddContextReference(
                    ComposerContextKind.File,
                    fullPath,
                    Path.GetFileName(fullPath)))
            {
                break;
            }
        }
    }

    private void ComposerTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        if (State.SubmitCommand.CanExecute(null))
        {
            State.SubmitCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static OpenFileDialog CreateFileDialog(string title) =>
        new()
        {
            Title = title,
            Multiselect = true,
            CheckFileExists = true,
            CheckPathExists = true,
            ValidateNames = true,
        };
}
