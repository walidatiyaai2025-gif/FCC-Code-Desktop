using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FCCCodeDesktop.App.Conversation;
using FCCCodeDesktop.App.Shell;

namespace FCCCodeDesktop.App;

public partial class MainWindow : Window
{
    private readonly WorkspaceViewportCoordinator _viewportCoordinator = new();

    public MainWindow()
    {
        InitializeComponent();
        ConfigureConversationSurface();
        ConfigureShellCommandFramework();
        Loaded += OnViewportLoaded;
        SizeChanged += OnViewportSizeChanged;
        DpiChanged += OnViewportDpiChanged;
    }

    private void ConfigureConversationSurface()
    {
        var navigationState = RequireResource<WorkspaceNavigationState>("WorkspaceNavigationState");
        var conversationSurface = RequireResource<ConversationSurface>("ConversationSurface");
        var composerState = RequireResource<ComposerState>("ComposerState");

        composerState.SubmissionRequested += OnComposerSubmissionRequested;
        navigationState.SessionsContent = conversationSurface;
    }

    private void OnComposerSubmissionRequested(object? sender, ComposerSubmissionRequestedEventArgs e)
    {
        if (sender is not ComposerState composerState)
        {
            throw new InvalidOperationException("Composer submission sender is invalid.");
        }

        var conversationState = RequireResource<StreamingConversationState>("StreamingConversationState");
        conversationState.AddUserMessage(e.Submission.Text);
        composerState.AcceptSubmission(e.Submission.SubmissionId);
    }

    private void ConfigureShellCommandFramework()
    {
        var paletteState = RequireResource<CommandPaletteState>("CommandPaletteState");
        var navigationState = RequireResource<WorkspaceNavigationState>("WorkspaceNavigationState");
        var layoutState = RequireResource<WorkspaceLayoutState>("WorkspaceLayoutState");

        paletteState.RegisterCommand(
            new ShellCommandDescriptor(
                "workspace.projects",
                "Show Projects",
                "Workspace",
                null,
                navigationState.SelectSectionCommand,
                WorkspaceSection.Projects));
        paletteState.RegisterCommand(
            new ShellCommandDescriptor(
                "workspace.sessions",
                "Show Sessions",
                "Workspace",
                null,
                navigationState.SelectSectionCommand,
                WorkspaceSection.Sessions));
        paletteState.RegisterCommand(
            new ShellCommandDescriptor(
                "workspace.tasks",
                "Show Tasks",
                "Workspace",
                null,
                navigationState.SelectSectionCommand,
                WorkspaceSection.Tasks));
        paletteState.RegisterCommand(
            new ShellCommandDescriptor(
                "workspace.toggleBottomPanel",
                "Toggle Bottom Panel",
                "View",
                "Ctrl+J",
                layoutState.ToggleBottomPanelCommand));

        InputBindings.Add(
            new KeyBinding(
                paletteState.OpenCommand,
                new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(
            new KeyBinding(
                paletteState.OpenCommand,
                new KeyGesture(Key.F1)));
        InputBindings.Add(
            new KeyBinding(
                layoutState.ToggleBottomPanelCommand,
                new KeyGesture(Key.J, ModifierKeys.Control)));
    }

    private void OnViewportLoaded(object sender, RoutedEventArgs e) => ApplyViewportPolicy();

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e) => ApplyViewportPolicy();

    private void OnViewportDpiChanged(object sender, DpiChangedEventArgs e) => ApplyViewportPolicy();

    private void ApplyViewportPolicy()
    {
        if (ActualWidth <= 0d || ActualHeight <= 0d)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var layoutState = RequireResource<WorkspaceLayoutState>("WorkspaceLayoutState");
        _viewportCoordinator.Update(
            layoutState,
            ActualWidth,
            ActualHeight,
            dpi.DpiScaleX,
            dpi.DpiScaleY);
    }

    private T RequireResource<T>(string key)
        where T : class
    {
        return Resources[key] as T
            ?? throw new InvalidOperationException($"Required shell resource '{key}' is missing or has the wrong type.");
    }
}
