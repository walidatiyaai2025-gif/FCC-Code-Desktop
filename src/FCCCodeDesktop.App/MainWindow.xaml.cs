using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FCCCodeDesktop.App.Conversation;
using FCCCodeDesktop.App.Projects;
using FCCCodeDesktop.App.Shell;
using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Fcc;
using FCCCodeDesktop.Files;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.App;

public partial class MainWindow : Window
{
    private readonly WorkspaceViewportCoordinator _viewportCoordinator = new();
    private Task<SessionWorkspaceState>? _sessionInitializationTask;
    private SessionWorkspaceState? _sessionWorkspaceState;
    private TaskExecutionState? _taskExecutionState;
    private ProjectWorkspaceSurface? _projectWorkspaceSurface;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureConversationSurface();
        ConfigureShellCommandFramework();
        Loaded += OnSessionPersistenceLoaded;
        Loaded += OnViewportLoaded;
        SizeChanged += OnViewportSizeChanged;
        DpiChanged += OnViewportDpiChanged;
    }

    public async Task ActivateProjectSessionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        var sessionState = await EnsureSessionWorkspaceInitializedAsync(cancellationToken).ConfigureAwait(true);
        await sessionState.ActivateProjectAsync(projectId, cancellationToken).ConfigureAwait(true);
    }

    public async Task BindActiveRuntimeSessionAsync(
        string runtimeSessionId,
        CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        var sessionState = await EnsureSessionWorkspaceInitializedAsync(cancellationToken).ConfigureAwait(true);
        await sessionState.BindRuntimeSessionAsync(runtimeSessionId, cancellationToken).ConfigureAwait(true);
    }

    private void ConfigureConversationSurface()
    {
        var navigationState = RequireResource<WorkspaceNavigationState>("WorkspaceNavigationState");
        _ = RequireResource<ConversationSurface>("ConversationSurface");
        var sessionWorkspaceSurface = RequireResource<SessionWorkspaceSurface>("SessionWorkspaceSurface");
        var taskExecutionSurface = RequireResource<TaskExecutionSurface>("TaskExecutionSurface");
        var composerState = RequireResource<ComposerState>("ComposerState");

        _projectWorkspaceSurface = new ProjectWorkspaceSurface();
        composerState.SubmissionRequested += OnComposerSubmissionRequested;
        navigationState.SessionsContent = sessionWorkspaceSurface;
        navigationState.TasksContent = taskExecutionSurface;
    }

    private async void OnSessionPersistenceLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnSessionPersistenceLoaded;
        try
        {
            await EnsureSessionWorkspaceInitializedAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidOperationException)
        {
            MessageBox.Show(
                this,
                $"Workspace storage could not be initialized. {exception.Message}",
                "Workspace storage unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task<SessionWorkspaceState> EnsureSessionWorkspaceInitializedAsync(
        CancellationToken cancellationToken)
    {
        VerifyAccess();
        _sessionInitializationTask ??= InitializeSessionWorkspaceCoreAsync();
        return await _sessionInitializationTask.WaitAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task<SessionWorkspaceState> InitializeSessionWorkspaceCoreAsync()
    {
        VerifyAccess();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("Windows LocalApplicationData could not be resolved for workspace persistence.");
        }

        var stateDirectory = Path.Combine(localAppData, "FCC Code Desktop", "State");
        Directory.CreateDirectory(stateDirectory);
        var options = new SqliteDatabaseOptions(Path.Combine(stateDirectory, "fcc-code-desktop.db"));
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None).ConfigureAwait(true);

        var state = new SessionWorkspaceState(new SqliteConversationStateStore(options));
        state.SessionChanged += OnSessionChanged;
        _sessionWorkspaceState = state;
        RequireResource<SessionWorkspaceSurface>("SessionWorkspaceSurface").State = state;

        var projectSurface = _projectWorkspaceSurface
            ?? throw new InvalidOperationException("Project workspace surface was not composed before persistence initialization.");
        var projectState = new ProjectWorkspaceState(
            new ProjectCatalogService(
                new SqliteProjectCatalogStore(options),
                new SystemProjectDirectoryProbe()),
            new FileSystemProjectTechnologyDetectionService(),
            state);
        projectSurface.State = projectState;
        var navigationState = RequireResource<WorkspaceNavigationState>("WorkspaceNavigationState");
        navigationState.ProjectsContent = _projectWorkspaceSurface;
        await projectState.InitializeAsync(CancellationToken.None).ConfigureAwait(true);

        await InitializeTaskExecutionAsync(options, state).ConfigureAwait(true);
        return state;
    }

    private async Task InitializeTaskExecutionAsync(SqliteDatabaseOptions options, SessionWorkspaceState sessionState)
    {
        var discovery = await new FccEnvironmentDiscoveryService()
            .DiscoverAsync(CancellationToken.None)
            .ConfigureAwait(true);
        IAgentRuntime? runtime = null;
        string? unavailableReason = null;
        if (discovery.IsFccClaudeAvailable)
        {
            runtime = new ConversationSequencedAgentRuntime(
                new AgentRuntimeSupervisor(new FccStructuredAgentRuntime(discovery.FccClaude)));
        }
        else
        {
            unavailableReason = discovery.FccClaude.ProbeFailure ?? "fcc-claude was not discovered.";
        }

        var taskState = new TaskExecutionState(
            new SqliteExecutionJournalStore(options),
            sessionState,
            RequireResource<StreamingConversationState>("StreamingConversationState"),
            runtime,
            unavailableReason);
        _taskExecutionState = taskState;
        RequireResource<TaskExecutionSurface>("TaskExecutionSurface").State = taskState;
    }

    private void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _sessionWorkspaceState))
        {
            throw new InvalidOperationException("Session change sender is not the active session workspace state.");
        }

        var conversationState = RequireResource<StreamingConversationState>("StreamingConversationState");
        conversationState.LoadPersistedMessages(e.Messages);
    }

    private async void OnComposerSubmissionRequested(object? sender, ComposerSubmissionRequestedEventArgs e)
    {
        if (sender is not ComposerState composerState)
        {
            throw new InvalidOperationException("Composer submission sender is invalid.");
        }

        var conversationState = RequireResource<StreamingConversationState>("StreamingConversationState");
        try
        {
            var sessionState = await EnsureSessionWorkspaceInitializedAsync(CancellationToken.None).ConfigureAwait(true);
            var taskState = _taskExecutionState
                ?? throw new InvalidOperationException("Task execution state is not initialized.");
            taskState.ValidateCanStart();
            if (!sessionState.HasActiveSession)
            {
                throw new InvalidOperationException("Create or resume a session before starting a task.");
            }

            await sessionState.AppendMessageAsync(
                "user",
                e.Submission.Text,
                CancellationToken.None).ConfigureAwait(true);
            conversationState.AddUserMessage(e.Submission.Text);
            await taskState.StartTaskAsync(e.Submission.Text, CancellationToken.None).ConfigureAwait(true);
            composerState.AcceptSubmission(e.Submission.SubmissionId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            composerState.RejectSubmission(e.Submission.SubmissionId, exception.Message);
        }
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
