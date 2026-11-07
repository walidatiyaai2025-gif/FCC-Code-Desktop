using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FCCCodeDesktop.App.Conversation;
using FCCCodeDesktop.App.Projects;
using FCCCodeDesktop.App.Shell;
using FCCCodeDesktop.App.Terminal;
using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Fcc;
using FCCCodeDesktop.Files;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.App;

public partial class MainWindow : Window, IAsyncDisposable
{
    private readonly WorkspaceViewportCoordinator _viewportCoordinator = new();
    private Task<SessionWorkspaceState>? _sessionInitializationTask;
    private SessionWorkspaceState? _sessionWorkspaceState;
    private TaskExecutionState? _taskExecutionState;
    private ProjectWorkspaceSurface? _projectWorkspaceSurface;
    private readonly FccServerLifecycle _fccServer = new();
    private readonly CancellationTokenSource _windowLifetime = new();
    private bool _terminalShutdownStarted;
    private bool _terminalShutdownCompleted;
    private bool _windowDisposed;
    private bool _submissionStarting;
    private SqliteSettingsStore? _settingsStore;
    private string? _preferredModel;

    public MainWindow()
    {
        InitializeComponent();
        _fccServer.StatusChanged += OnFccStatusChanged;
        ConfigureConversationSurface();
        ConfigureShellCommandFramework();
        Loaded += OnSessionPersistenceLoaded;
        Loaded += OnViewportLoaded;
        SizeChanged += OnViewportSizeChanged;
        DpiChanged += OnViewportDpiChanged;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
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
        var bottomToolPanelState = RequireResource<BottomToolPanelState>("BottomToolPanelState");
        var terminalSurface = RequireResource<InteractiveTerminalSurface>("InteractiveTerminalSurface");

        _projectWorkspaceSurface = new ProjectWorkspaceSurface();
        composerState.SubmissionRequested += OnComposerSubmissionRequested;
        navigationState.SessionsContent = sessionWorkspaceSurface;
        var startupState = RequireResource<FccStartupState>("FccStartupState");
        var modelSettings = new StackPanel { DataContext = startupState };
        modelSettings.Children.Add(new TextBlock { Text = "Coding model", Margin = new Thickness(0, 0, 0, 4) });
        var modelPicker = new ComboBox { MaxDropDownHeight = 320 };
        modelPicker.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(FccStartupState.Models)));
        modelPicker.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
            new Binding(nameof(FccStartupState.SelectedModel)) { Mode = BindingMode.TwoWay });
        modelPicker.SetBinding(IsEnabledProperty, new Binding(nameof(FccStartupState.CanSelectModel)));
        System.Windows.Automation.AutomationProperties.SetName(modelPicker, "FCC coding model");
        modelSettings.Children.Add(modelPicker);
        modelSettings.Children.Add(new TextBlock
        {
            Text = "File edits enabled for coding tasks. Review changes before committing.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        });
        sessionWorkspaceSurface.RuntimeSettingsContent = modelSettings;
        navigationState.TasksContent = taskExecutionSurface;
        bottomToolPanelState.TerminalContent = terminalSurface;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_terminalShutdownCompleted)
        {
            return;
        }

        var editorWorkspace = _projectWorkspaceSurface?.EditorWorkspace;
        if (editorWorkspace is { IsBusy: true })
        {
            e.Cancel = true;
            MessageBox.Show(
                this,
                "An editor save, reload, or open operation is still in progress. Wait for it to finish before exiting FCC Code Desktop.",
                "Editor operation in progress",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!_terminalShutdownStarted)
        {
            var dirtyDocumentCount = editorWorkspace?.Documents.Count(document => document.IsDirty) ?? 0;
            if (dirtyDocumentCount > 0)
            {
                var noun = dirtyDocumentCount == 1 ? "tab has" : "tabs have";
                var result = MessageBox.Show(
                    this,
                    $"{dirtyDocumentCount} editor {noun} unsaved changes. Discard those changes and exit FCC Code Desktop?",
                    "Unsaved editor changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        e.Cancel = true;
        if (_terminalShutdownStarted)
        {
            return;
        }

        _terminalShutdownStarted = true;
        _ = ShutdownTerminalAndCloseAsync();
    }

    private async Task ShutdownTerminalAndCloseAsync()
    {
        await Task.Yield();
        try
        {
            await DisposeAsync().ConfigureAwait(true);
            _terminalShutdownCompleted = true;
            _terminalShutdownStarted = false;
            Close();
        }
        catch (Exception exception) when (exception is IOException
                                           or InvalidOperationException
                                           or ObjectDisposedException)
        {
            _terminalShutdownStarted = false;
            MessageBox.Show(
                this,
                $"The terminal session could not be cleaned up safely. {exception.Message}",
                "Terminal cleanup failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _fccServer.StatusChanged -= OnFccStatusChanged;
        _projectWorkspaceSurface?.EditorWorkspace.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_windowDisposed) { return; }
        await _windowLifetime.CancelAsync().ConfigureAwait(true);
        if (_taskExecutionState is not null)
        {
            await _taskExecutionState.ShutdownAsync().ConfigureAwait(true);
        }
        await RequireResource<InteractiveTerminalSurface>("InteractiveTerminalSurface").DisposeAsync().ConfigureAwait(true);
        await _fccServer.DisposeAsync().ConfigureAwait(true);
        _windowLifetime.Dispose();
        _windowDisposed = true;
        GC.SuppressFinalize(this);
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
        _settingsStore = new SqliteSettingsStore(options);
        var modelSetting = await _settingsStore.GetGlobalSettingAsync("runtime.model", _windowLifetime.Token).ConfigureAwait(true);
        if (modelSetting is not null)
        {
            try
            {
                _preferredModel = System.Text.Json.JsonSerializer.Deserialize<string>(modelSetting.ValueJson);
            }
            catch (System.Text.Json.JsonException)
            {
                // Invalid non-secret preference is recovered by the live model catalog.
            }
        }

        var state = new SessionWorkspaceState(new SqliteConversationStateStore(options));
        state.CanChangeSession = () => !_submissionStarting && !_terminalShutdownStarted && _taskExecutionState?.IsActive != true;
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
        projectState.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(ProjectWorkspaceState.ActiveProject) && projectState.HasActiveProject)
            {
                AppTitleBarHost.ContextContent = projectState.ActiveProjectName;
                RequireResource<WorkspaceNavigationState>("WorkspaceNavigationState")
                    .SelectSectionCommand.Execute(WorkspaceSection.Sessions);
            }
        };
        var navigationState = RequireResource<WorkspaceNavigationState>("WorkspaceNavigationState");
        navigationState.ProjectsContent = _projectWorkspaceSurface;
        await projectState.InitializeAsync(CancellationToken.None).ConfigureAwait(true);

        await InitializeTaskExecutionAsync(options, state).ConfigureAwait(true);
        return state;
    }

    private async Task InitializeTaskExecutionAsync(SqliteDatabaseOptions options, SessionWorkspaceState sessionState)
    {
        var startupState = RequireResource<FccStartupState>("FccStartupState");
        IAgentRuntime runtime = new ConversationSequencedAgentRuntime(new FccManagedAgentRuntime(_fccServer,
            () => startupState.SelectedModel, ToolApprovalHost.RequestAsync));

        var taskState = new TaskExecutionState(
            new SqliteExecutionJournalStore(options),
            sessionState,
            RequireResource<StreamingConversationState>("StreamingConversationState"),
            runtime);
        _taskExecutionState = taskState;
        taskState.PropertyChanged += (_, _) => startupState.SetTaskActive(taskState.IsActive);
        RequireResource<TaskExecutionSurface>("TaskExecutionSurface").State = taskState;
        await PrepareFccAsync().ConfigureAwait(true);
    }

    private void OnFccStatusChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => OnFccStatusChanged(sender, e));
            return;
        }
        RequireResource<FccStartupState>("FccStartupState").Update(_fccServer.Status);
    }

    private async Task PrepareFccAsync()
    {
        try
        {
            var snapshot = await _fccServer.EnsureReadyAsync(_windowLifetime.Token).ConfigureAwait(true);
            var models = await FccModelCatalog.ListAsync(snapshot.LoopbackHealth.Endpoint, _windowLifetime.Token).ConfigureAwait(true);
            RequireResource<FccStartupState>("FccStartupState").SetModels(models, _preferredModel);
        }
        catch (OperationCanceledException) when (_windowLifetime.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException
                                           or HttpRequestException or System.Text.Json.JsonException)
        {
            RequireResource<FccStartupState>("FccStartupState").Update(
                _fccServer.Status.State == FccServerState.Failed ? _fccServer.Status
                    : new FccServerStatus(FccServerState.Failed, "FCC model discovery failed. Check the local server and retry."));
        }
    }

    private async void OnFccRetryClick(object sender, RoutedEventArgs e) => await PrepareFccAsync().ConfigureAwait(true);

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
            var owningProjectId = sessionState.ActiveProjectId;
            var taskState = _taskExecutionState
                ?? throw new InvalidOperationException("Task execution state is not initialized.");
            if (!sessionState.HasActiveSession && sessionState.HasActiveProject)
            {
                await sessionState.CreateSessionAsync(cancellationToken: _windowLifetime.Token).ConfigureAwait(true);
            }
            taskState.ValidateCanStart();
            if (sessionState.ActiveProjectId != owningProjectId)
            {
                throw new InvalidOperationException("The selected repository changed while preparing this request. Send the request again in its intended repository.");
            }
            _submissionStarting = true;
            var startupState = RequireResource<FccStartupState>("FccStartupState");
            startupState.SetTaskActive(true);
            if (_settingsStore is not null && startupState.SelectedModel is not null)
            {
                await _settingsStore.UpsertGlobalSettingAsync(new FCCCodeDesktop.Core.State.PersistedSetting(
                    "runtime.model", System.Text.Json.JsonSerializer.Serialize(startupState.SelectedModel), DateTimeOffset.UtcNow),
                    _windowLifetime.Token).ConfigureAwait(true);
            }

            await sessionState.AppendMessageAsync(
                "user",
                e.Submission.Text,
                CancellationToken.None).ConfigureAwait(true);
            conversationState.AddUserMessage(e.Submission.Text);
            await taskState.StartTaskAsync(e.Submission.Text, _windowLifetime.Token).ConfigureAwait(true);
            composerState.AcceptSubmission(e.Submission.SubmissionId);
        }
        catch (OperationCanceledException) when (_windowLifetime.IsCancellationRequested)
        {
            composerState.RejectSubmission(e.Submission.SubmissionId, "Task startup cancelled because the application is closing.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            composerState.RejectSubmission(e.Submission.SubmissionId, exception.Message);
        }
        finally
        {
            _submissionStarting = false;
            RequireResource<FccStartupState>("FccStartupState").SetTaskActive(_taskExecutionState?.IsActive == true);
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
