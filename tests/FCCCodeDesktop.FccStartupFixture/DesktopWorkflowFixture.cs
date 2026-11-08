using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FCCCodeDesktop.App.Conversation;
using FCCCodeDesktop.App.Shell;
using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Fcc;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Runtime;

internal static class DesktopWorkflowFixture
{
    private static readonly string[] ResourceDictionaries = ["DesignTokens.xaml", "Typography.xaml", "Themes/Theme.Dark.xaml", "AppChrome.xaml"];
    public static Task RunAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            _ = dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    foreach (var dictionary in ResourceDictionaries)
                    {
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary
                        {
                            Source = new Uri("pack://application:,,,/FCCCodeDesktop.App;component/DesignSystem/" + dictionary)
                        });
                    }
                    await RunOnDispatcherAsync();
                    completion.TrySetResult();
                }
                catch (Exception exception) { completion.TrySetException(exception); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }

    private static async Task RunOnDispatcherAsync()
    {
        var surface = new ToolApprovalSurface();
        var request = new FccToolPermissionRequest("Bash", "{\"command\":\"fixture-command\"}", Path.GetTempPath());
        var approval = surface.RequestAsync(request, CancellationToken.None);
        Require(surface.Visibility == Visibility.Visible, "Approval did not appear.");
        Require(!await surface.RequestAsync(request, CancellationToken.None), "Concurrent approval was accepted.");
        Button(surface, "Allow once").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Require(await approval && surface.Visibility == Visibility.Collapsed, "Allow-once UI failed.");
        var denial = surface.RequestAsync(request, CancellationToken.None);
        Button(surface, "Deny").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Require(!await denial, "Deny UI failed.");
        using var cancelled = new CancellationTokenSource();
        var cancellation = surface.RequestAsync(request, cancelled.Token);
        await cancelled.CancelAsync();
        try { await cancellation; throw new InvalidOperationException("Cancelled approval succeeded."); }
        catch (OperationCanceledException) { }
        Require(surface.Visibility == Visibility.Collapsed, "Cancelled approval remained visible.");

        var root = Path.Combine(Path.GetTempPath(), "FCCD desktop ownership " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var options = new SqliteDatabaseOptions(Path.Combine(root, "state.db"));
            await new SqliteDatabaseInitializer(options).InitializeAsync();
            var store = new SqliteConversationStateStore(options);
            var first = new PersistedProject(Guid.NewGuid(), root, "First repo", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            var secondRoot = Path.Combine(root, "second repository");
            Directory.CreateDirectory(secondRoot);
            var second = first with { Id = Guid.NewGuid(), RootPath = secondRoot, DisplayName = "Second repo" };
            await store.UpsertProjectAsync(first);
            await store.UpsertProjectAsync(second);
            var state = new SessionWorkspaceState(store);
            await state.ActivateProjectAsync(first.Id);
            var session = await state.CreateSessionAsync();
            state.CanChangeSession = () => false;
            foreach (var blocked in new Func<Task>[] { () => state.ActivateProjectAsync(second.Id), () => state.CreateSessionAsync(), () => state.ResumeSessionAsync(session.Id) })
            {
                try { await blocked(); throw new IOException("Session ownership guard was bypassed."); }
                catch (InvalidOperationException) { }
            }
            Require(state.ActiveProjectId == first.Id && state.ActiveSessionId == session.Id && state.HasError, "Blocked switch changed task ownership.");
            state.CanChangeSession = () => true;
            await state.ActivateProjectAsync(second.Id);
            Require(state.ActiveProjectId == second.Id, "Session change did not recover after task completion.");
            var startup = new FccStartupState();
            startup.SetModels(["fixture/model-a", "fixture/model-b"]);
            startup.SelectedModel = "fixture/model-b";
            startup.SetTaskActive(true);
            startup.SelectedModel = "fixture/model-a";
            Require(startup.SelectedModel == "fixture/model-b" && !startup.CanSelectModel, "Active task model changed.");
            startup.SetTaskActive(false);
            Require(startup.CanSelectModel, "Model picker did not recover.");
            startup.Update(new FccServerStatus(FccServerState.Failed, "Fixture server unavailable"));
            Require(startup.CanRetry, "Failed FCC startup did not enable retry.");
            startup.SetTaskActive(true);
            Require(!startup.CanRetry, "FCC retry remained enabled during a coding task.");
            startup.SetTaskActive(false);
            Require(startup.CanRetry, "FCC retry did not recover after coding task finished.");
            startup.Update(new FccServerStatus(FccServerState.Checking, "Checking FCC"));
            Require(!startup.CanRetry, "FCC retry remained enabled while startup was checking.");
            startup.SetModels(["fixture/model-a", "fixture/model-b"]);
            Require(startup.SelectedModel == "fixture/model-b", "Refreshing models lost the chosen model.");
            var restored = new FccStartupState();
            restored.SetModels(["fixture/model-a", "fixture/model-b"], "fixture/model-b");
            Require(restored.SelectedModel == "fixture/model-b", "Initial catalog ignored the persisted preference.");
            var settings = new SqliteSettingsStore(options);
            await settings.UpsertGlobalSettingAsync(new PersistedSetting("runtime.model", "\"fixture/model-b\"", DateTimeOffset.UtcNow));
            Require((await settings.GetGlobalSettingAsync("runtime.model"))?.ValueJson == "\"fixture/model-b\"", "Model preference did not persist.");
            await using var window = new FCCCodeDesktop.App.MainWindow();
            Require(window.FindName("ToolApprovalHost") is ToolApprovalSurface, "Main window approval overlay is missing.");
            await state.CreateSessionAsync();
            var task = new TaskExecutionState(new SqliteExecutionJournalStore(options), state,
                new StreamingConversationState(), new FailedModelRuntime());
            var conversation = new ConversationSurface { Execution = task };
            await task.StartTaskAsync("fixture request");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (task.IsActive || !task.CanRetry) { await Task.Delay(20, deadline.Token); }
            conversation.Measure(new Size(900, 700));
            conversation.Arrange(new Rect(0, 0, 900, 700));
            conversation.UpdateLayout();
            await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.DataBind);
            Require(((Border)conversation.FindName("ExecutionStatusBanner")).Visibility == Visibility.Visible,
                "Failed task status is hidden in chat.");
            Require(((TextBlock)conversation.FindName("ChatExecutionFailure")).Text.Contains("model", StringComparison.Ordinal),
                "Provider error is not shown in chat.");
            Require(Button(conversation, "Retry").IsEnabled && !Button(conversation, "Stop").IsEnabled,
                "Chat controls do not reflect terminal failure.");

            // Hold asynchronous workspace initialization to verify that a coding request
            // locks model/project ownership synchronously, before the first await.
            var heldWorkspace = new TaskCompletionSource<SessionWorkspaceState>(TaskCreationOptions.RunContinuationsAsynchronously);
            var initializationField = typeof(FCCCodeDesktop.App.MainWindow).GetField(
                "_sessionInitializationTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Main window workspace initialization seam changed.");
            initializationField.SetValue(window, heldWorkspace.Task);
            var windowStartup = (FccStartupState)window.Resources["FccStartupState"];
            var composer = (ComposerState)window.Resources["ComposerState"];
            windowStartup.SetModels(["fixture/model-a"]);
            Require(windowStartup.CanSelectModel, "Model picker was not selectable before submission.");
            composer.DraftText = "fixture coding request";
            Require(composer.RequestSubmission(), "Initial coding request was rejected.");
            Require(!windowStartup.CanSelectModel, "Model picker remained enabled during asynchronous submission preparation.");
            Require(!composer.RequestSubmission(), "Second request bypassed pending-submission ownership.");
            heldWorkspace.SetException(new InvalidOperationException("Fixture-held initialization was intentionally rejected."));
            await Dispatcher.Yield(DispatcherPriority.Background);
            Require(windowStartup.CanSelectModel, "Model picker remained locked after failed submission preparation.");
            Require(composer.CanSubmit && composer.HasValidationMessage, "Failed submission did not restore the composer draft.");

            await using (var cancelledWindow = new FCCCodeDesktop.App.MainWindow())
            {
                var cancelledWorkspace = new TaskCompletionSource<SessionWorkspaceState>(TaskCreationOptions.RunContinuationsAsynchronously);
                initializationField.SetValue(cancelledWindow, cancelledWorkspace.Task);
                var cancelledStartup = (FccStartupState)cancelledWindow.Resources["FccStartupState"];
                var cancelledComposer = (ComposerState)cancelledWindow.Resources["ComposerState"];
                cancelledStartup.SetModels(["fixture/model-a"]);
                cancelledComposer.DraftText = "fixture cancelled coding request";
                Require(cancelledComposer.RequestSubmission(), "Cancellation fixture submission was rejected unexpectedly.");
                Require(!cancelledStartup.CanSelectModel, "Cancellation fixture did not lock model selection.");
                cancelledWorkspace.SetCanceled();
                for (var attempt = 0; attempt < 30 && !cancelledComposer.CanSubmit; attempt++)
                {
                    await Task.Delay(25);
                }
                Require(cancelledComposer.CanSubmit && cancelledComposer.HasValidationMessage,
                    "Cancelled workspace preparation did not restore a retryable composer draft.");
                Require(cancelledStartup.CanSelectModel, "Cancelled workspace preparation kept the model locked.");
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Require(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()) + "FCCD desktop ownership ", StringComparison.OrdinalIgnoreCase), "Fixture cleanup escaped its owned temporary root.");
            Directory.Delete(root, recursive: true);
        }
    }

    private static Button Button(DependencyObject root, string content)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is Button button && Equals(button.Content, content)) { return button; }
            try { return Button(child, content); }
            catch (KeyNotFoundException) { }
        }
        throw new KeyNotFoundException(content);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }

    private sealed class FailedModelRuntime : IAgentRuntime
    {
        public AgentRuntimeDescriptor Descriptor { get; } = new("fixture", "Fixture", AgentRuntimeTransport.StructuredProcess,
            new AgentRuntimeCapabilities(true, true, true, true, true));
        public Task<IAgentRuntimeExecution> StartAsync(AgentRuntimeRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IAgentRuntimeExecution>(new FailedModelExecution(request));
    }

    private sealed class FailedModelExecution(AgentRuntimeRequest request) : IAgentRuntimeExecution
    {
        public Guid TaskId => request.TaskId;
        public Guid RunId => request.RunId;
        public IAsyncEnumerable<AgentRuntimeEvent> Events => NoEvents();
        public Task<AgentRuntimeResult> Completion => Task.FromResult(new AgentRuntimeResult(TaskId, RunId,
            AgentRuntimeTerminalState.Failed, null, new AgentRuntimeFailure(AgentRuntimeFailureKind.Timeout, "FCC model did not respond. Choose another model or retry.")));
        public ValueTask CancelAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        private static async IAsyncEnumerable<AgentRuntimeEvent> NoEvents() { await Task.CompletedTask; yield break; }
    }
}
