using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace FCCCodeDesktop.App.Editor;

public partial class CodeEditorControl : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(CodeEditorControl),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTextPropertyChanged));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly),
        typeof(bool),
        typeof(CodeEditorControl),
        new PropertyMetadata(false, OnIsReadOnlyPropertyChanged));

    public static readonly DependencyProperty DocumentLabelProperty = DependencyProperty.Register(
        nameof(DocumentLabel),
        typeof(string),
        typeof(CodeEditorControl),
        new PropertyMetadata("Untitled"));

    public static readonly DependencyProperty LanguageLabelProperty = DependencyProperty.Register(
        nameof(LanguageLabel),
        typeof(string),
        typeof(CodeEditorControl),
        new PropertyMetadata("Plain text"));

    public CodeEditorControl()
    {
        InitializeComponent();
        EditorTextBox.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler(OnEditorScrollChanged));
        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value ?? string.Empty);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public string DocumentLabel
    {
        get => (string)GetValue(DocumentLabelProperty);
        set => SetValue(DocumentLabelProperty, value ?? string.Empty);
    }

    public string LanguageLabel
    {
        get => (string)GetValue(LanguageLabelProperty);
        set => SetValue(LanguageLabelProperty, value ?? string.Empty);
    }

    public string ModeLabel => IsReadOnly ? "Read only" : "Editable";

    private static void OnTextPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CodeEditorControl editor && editor.IsLoaded)
        {
            editor.UpdateLineNumbers();
            editor.UpdateCaretStatus();
        }
    }

    private static void OnIsReadOnlyPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CodeEditorControl editor)
        {
            editor.PropertyChanged?.Invoke(editor, new PropertyChangedEventArgs(nameof(ModeLabel)));
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateLineNumbers();
        UpdateCaretStatus();
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLineNumbers();
        UpdateCaretStatus();
    }

    private void OnEditorSelectionChanged(object sender, RoutedEventArgs e) => UpdateCaretStatus();

    private void OnEditorScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0)
        {
            return;
        }

        var firstVisibleLine = EditorTextBox.GetFirstVisibleLineIndex();
        if (firstVisibleLine >= 0)
        {
            LineNumberGutter.ScrollToLine(firstVisibleLine);
        }
    }

    private void UpdateLineNumbers()
    {
        var lineCount = CodeEditorTextMetrics.CountLogicalLines(EditorTextBox.Text);
        LineNumberGutter.Text = string.Join(
            "\n",
            Enumerable.Range(1, lineCount)
                .Select(static number => number.ToString(CultureInfo.InvariantCulture)));
    }

    private void UpdateCaretStatus()
    {
        var position = CodeEditorTextMetrics.GetCaretPosition(EditorTextBox.Text, EditorTextBox.CaretIndex);
        CaretStatusText.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Ln {position.Line}, Col {position.Column}");
    }
}
