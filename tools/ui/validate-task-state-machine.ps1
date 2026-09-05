[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RunFixtures,
    [switch]$RequireRuntime
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ContainsLiteral {
    param([string]$Text, [string]$Literal, [string]$Label)
    if (-not $Text.Contains($Literal, [StringComparison]::Ordinal)) {
        throw "$Label is missing required text: $Literal"
    }
}

function Assert-ValidXaml {
    param([string]$Text, [string]$Label)
    try { [void][xml]$Text }
    catch { throw "$Label is not valid XML/XAML: $($_.Exception.Message)" }
}

function Assert-TaskStateContract {
    param(
        [string]$StateText,
        [string]$SurfaceText,
        [string]$SurfaceCodeText,
        [string]$SequenceText,
        [string]$MainWindowText,
        [string]$MainWindowCodeText
    )

    Assert-ValidXaml $SurfaceText 'TaskExecutionSurface.xaml'
    Assert-ValidXaml $MainWindowText 'MainWindow.xaml'

    foreach ($literal in @(
        'public enum TaskLifecycleState',
        'Idle = 0',
        'Starting = 1',
        'Running = 2',
        'StopRequested = 3',
        'Succeeded = 4',
        'Failed = 5',
        'Cancelled = 6',
        'IExecutionJournalStore _journalStore',
        'SessionWorkspaceState _sessionWorkspace',
        'StreamingConversationState _conversation',
        'public async Task StartTaskAsync(',
        'IsActive || _activePumpTask is not null || _activeExecution is not null',
        'Another task is already active or still settling in this workspace.',
        'Create or resume a persisted project session before starting a task.',
        'new AgentRuntimeRequest(',
        'EnsureExecutionIdentity(startedExecution, request)',
        'EnsureResultIdentity(result, taskId, runId)',
        'CleanupUnownedExecutionAsync(startedExecution)',
        'TrackPumpCompletionAsync(pumpTask)',
        'RecordStartCancellationAsync(cleanupDiagnostic)',
        'The active session changed while a task was running.',
        'AppendMessageAsync("assistant", assistantText.ToString()',
        'SetFailureMessage(result.Failure?.Message)',
        'UpsertTaskAsync(',
        'UpsertAgentRunAsync(',
        'AppendEventAsync(taskEvent',
        'private static bool IsAllowedTransition(',
        '(TaskLifecycleState.Idle, TaskLifecycleState.Starting) => true',
        '(TaskLifecycleState.Starting, TaskLifecycleState.Running) => true',
        '(TaskLifecycleState.Running, TaskLifecycleState.Succeeded) => true',
        '(TaskLifecycleState.Running, TaskLifecycleState.Failed) => true',
        '(TaskLifecycleState.Running, TaskLifecycleState.Cancelled) => true',
        '(TaskLifecycleState.Succeeded, TaskLifecycleState.Starting) => true',
        '(TaskLifecycleState.Failed, TaskLifecycleState.Starting) => true',
        '(TaskLifecycleState.Cancelled, TaskLifecycleState.Starting) => true'
    )) {
        Assert-ContainsLiteral $StateText $literal 'TaskExecutionState.cs'
    }

    if ($StateText.Contains('runtimeEvent.PayloadJson', [StringComparison]::Ordinal)) {
        throw 'P05-005 must not persist raw provider payload JSON into the task journal.'
    }

    foreach ($literal in @(
        'public sealed class ConversationSequencedAgentRuntime : IAgentRuntime',
        'Source runtime event sequence must start at zero.',
        'Source runtime event sequence must remain contiguous.',
        '_owner.NextPresentationSequence()',
        'runtimeEvent.PayloadJson'
    )) {
        Assert-ContainsLiteral $SequenceText $literal 'ConversationSequencedAgentRuntime.cs'
    }

    foreach ($literal in @(
        'x:Name="TaskStateLabel"',
        'Text="{Binding StateLabel}"',
        'Text="{Binding RuntimeAvailabilityText}"',
        'Text="{Binding ActiveTaskId}"',
        'Text="{Binding ActiveRunId}"',
        'Text="{Binding Attempt}"',
        'Binding HasFailure',
        '{DynamicResource FccBrushCanvas}',
        '{DynamicResource FccBrushSurface}',
        '{DynamicResource FccBrushBorder}',
        '{DynamicResource FccBrushError}'
    )) {
        Assert-ContainsLiteral $SurfaceText $literal 'TaskExecutionSurface.xaml'
    }

    foreach ($literal in @('DependencyProperty StateProperty', 'typeof(TaskExecutionState)', 'nameof(State)')) {
        Assert-ContainsLiteral $SurfaceCodeText $literal 'TaskExecutionSurface.xaml.cs'
    }

    Assert-ContainsLiteral $MainWindowText '<conversation:TaskExecutionSurface x:Key="TaskExecutionSurface"' 'MainWindow.xaml'

    foreach ($literal in @(
        'TaskExecutionState? _taskExecutionState',
        'navigationState.TasksContent = taskExecutionSurface',
        'new SqliteExecutionJournalStore(options)',
        'new FccEnvironmentDiscoveryService()',
        'new ConversationSequencedAgentRuntime(',
        'new AgentRuntimeSupervisor(new FccStructuredAgentRuntime(discovery.FccClaude))',
        'taskState.ValidateCanStart()',
        'await taskState.StartTaskAsync(e.Submission.Text',
        'composerState.AcceptSubmission(e.Submission.SubmissionId)',
        'composerState.RejectSubmission(e.Submission.SubmissionId, exception.Message)'
    )) {
        Assert-ContainsLiteral $MainWindowCodeText $literal 'MainWindow.xaml.cs'
    }

    if ($SurfaceText -match '#[0-9A-Fa-f]{6,8}') {
        throw 'P05-005 task surface must use semantic resources instead of hard-coded colors.'
    }

    foreach ($text in @($StateText, $SurfaceText, $SurfaceCodeText, $SequenceText, $MainWindowText, $MainWindowCodeText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P05-005 contains forbidden placeholder text '$placeholder'."
            }
        }
    }
}

function Assert-Rejected {
    param([scriptblock]$Action, [string]$Label)
    try { & $Action }
    catch { Write-Host "Negative fixture rejected as expected: $Label"; return }
    throw "Negative task-state fixture was not rejected: $Label"
}

function Invoke-RuntimeFixture {
    param(
        [string]$AppProjectPath,
        [string]$PersistenceProjectPath,
        [string]$RuntimeProjectPath
    )

    if (-not $IsWindows) { throw 'Runtime task-state fixture requires Windows/WPF.' }
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime task-state fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $root = Join-Path ([IO.Path]::GetTempPath()) ('fccd-p05-005-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $root -Force)
    try {
        $projectPath = Join-Path $root 'Fixture.csproj'
        $programPath = Join-Path $root 'Program.cs'
        $databasePath = (Join-Path $root 'task-state.db').Replace('"', '""')
        $workspacePath = Join-Path $root 'workspace'
        [void](New-Item -ItemType Directory -Path $workspacePath -Force)

        $appReference = [Security.SecurityElement]::Escape($AppProjectPath)
        $persistenceReference = [Security.SecurityElement]::Escape($PersistenceProjectPath)
        $runtimeReference = [Security.SecurityElement]::Escape($RuntimeProjectPath)

        Set-Content -LiteralPath $projectPath -Encoding utf8NoBOM -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$appReference" />
    <ProjectReference Include="$persistenceReference" />
    <ProjectReference Include="$runtimeReference" />
  </ItemGroup>
</Project>
"@

        $program = @'
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using FCCCodeDesktop.App;
using FCCCodeDesktop.App.Conversation;
using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Persistence;
using FCCCodeDesktop.Runtime;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var task = RunAsync();
        while (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        var app = new App();
        app.InitializeComponent();

        var options = new SqliteDatabaseOptions(@"__DB__");
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var conversationStore = new SqliteConversationStateStore(options);
        var journalStore = new SqliteExecutionJournalStore(options);
        var now = new DateTimeOffset(2026, 9, 5, 7, 0, 0, TimeSpan.Zero);
        var project = new PersistedProject(Guid.NewGuid(), @"__WORKSPACE__", "Fixture project", now, now);
        await conversationStore.UpsertProjectAsync(project, CancellationToken.None);

        var sessionState = new SessionWorkspaceState(conversationStore);
        await sessionState.ActivateProjectAsync(project.Id, CancellationToken.None);
        var session = await sessionState.CreateSessionAsync("P05-005", CancellationToken.None);
        var conversation = new StreamingConversationState();

        var runtime = new ScriptedRuntime(
            Scenario.Success("runtime-session-1", "first answer"),
            Scenario.Success("runtime-session-1", "second answer"));
        var state = new TaskExecutionState(
            journalStore,
            sessionState,
            conversation,
            new ConversationSequencedAgentRuntime(runtime));

        await sessionState.AppendMessageAsync("user", "first prompt", CancellationToken.None);
        conversation.AddUserMessage("first prompt");
        await state.StartTaskAsync("first prompt", CancellationToken.None);
        await WaitForSettledAsync(state);
        Assert(state.State == TaskLifecycleState.Succeeded, "first task success state");
        var firstTaskId = state.ActiveTaskId ?? throw new InvalidOperationException("P05-005 assertion failed: first task id");
        var firstRunId = state.ActiveRunId ?? throw new InvalidOperationException("P05-005 assertion failed: first run id");
        Assert(sessionState.ActiveRuntimeSessionId == "runtime-session-1", "runtime session binding");
        var firstPersisted = await journalStore.GetTaskAsync(firstTaskId, CancellationToken.None);
        Assert(firstPersisted?.State == "Succeeded" && firstPersisted.SessionId == session.Id, "first durable task");
        var firstEvents = await journalStore.ListEventsAsync(firstTaskId, CancellationToken.None);
        Assert(firstEvents.Count >= 6 && firstEvents[0].Sequence == 0, "first durable event journal");

        await sessionState.AppendMessageAsync("user", "second prompt", CancellationToken.None);
        conversation.AddUserMessage("second prompt");
        await state.StartTaskAsync("second prompt", CancellationToken.None);
        await WaitForSettledAsync(state);
        Assert(state.State == TaskLifecycleState.Succeeded, "second task success state");
        var secondTaskId = state.ActiveTaskId ?? throw new InvalidOperationException("P05-005 assertion failed: second task id");
        var secondRunId = state.ActiveRunId ?? throw new InvalidOperationException("P05-005 assertion failed: second run id");
        Assert(secondTaskId != firstTaskId, "new logical task identity");
        Assert(secondRunId != firstRunId, "new run identity");
        Assert(conversation.LastRuntimeSequence == 5, "monotonic presentation sequence across executions");
        var messages = await conversationStore.ListMessagesAsync(session.Id, CancellationToken.None);
        Assert(messages.Count == 4, "durable user and assistant history");
        Assert(messages[1].Role == "assistant" && messages[1].Content == "first answer", "first assistant persistence");
        Assert(messages[3].Role == "assistant" && messages[3].Content == "second answer", "second assistant persistence");

        var activeRuntime = new ManualRuntime();
        var activeState = new TaskExecutionState(
            journalStore,
            sessionState,
            new StreamingConversationState(),
            new ConversationSequencedAgentRuntime(activeRuntime));
        await activeState.StartTaskAsync("long task", CancellationToken.None);
        Assert(activeState.State == TaskLifecycleState.Running, "active task running");
        var activeRejected = false;
        try { activeState.ValidateCanStart(); }
        catch (InvalidOperationException exception) when (exception.Message.Contains("active or still settling", StringComparison.Ordinal))
        {
            activeRejected = true;
        }
        Assert(activeRejected, "one-active-task guard");
        activeRuntime.LastExecution!.CompleteSucceeded();
        await WaitForSettledAsync(activeState);
        Assert(activeState.State == TaskLifecycleState.Succeeded, "manual execution completion");

        var longFailure = new AgentRuntimeFailure(
            AgentRuntimeFailureKind.ProviderUnavailable,
            new string('x', 5000),
            AgentRuntimeRetryability.NotRetryable,
            AgentRuntimeUserAction.NotRequired);
        var failingState = new TaskExecutionState(
            journalStore,
            sessionState,
            new StreamingConversationState(),
            new ConversationSequencedAgentRuntime(new ScriptedRuntime(Scenario.Failed(longFailure))));
        await failingState.StartTaskAsync("failure task", CancellationToken.None);
        await WaitForSettledAsync(failingState);
        Assert(failingState.State == TaskLifecycleState.Failed, "classified failure state");
        Assert(failingState.FailureMessage?.Length == 1024, "failure diagnostic bound");
        var failedTaskId = failingState.ActiveTaskId ?? throw new InvalidOperationException("P05-005 assertion failed: failed task id");
        var failedTask = await journalStore.GetTaskAsync(failedTaskId, CancellationToken.None);
        Assert(failedTask?.State == "Failed", "failed task durable state");

        var gapState = new TaskExecutionState(
            journalStore,
            sessionState,
            new StreamingConversationState(),
            new ConversationSequencedAgentRuntime(new ScriptedRuntime(Scenario.SequenceGap())));
        await gapState.StartTaskAsync("gap task", CancellationToken.None);
        await WaitForSettledAsync(gapState);
        Assert(gapState.State == TaskLifecycleState.Failed, "source sequence gap fails closed");

        var originState = new TaskExecutionState(
            journalStore,
            sessionState,
            new StreamingConversationState(),
            new ConversationSequencedAgentRuntime(new ScriptedRuntime(Scenario.InvalidOrigin())));
        await originState.StartTaskAsync("origin task", CancellationToken.None);
        await WaitForSettledAsync(originState);
        Assert(originState.State == TaskLifecycleState.Failed, "source sequence origin fails closed");

        var mismatchRuntime = new MismatchedRuntime();
        var mismatchState = new TaskExecutionState(
            journalStore,
            sessionState,
            new StreamingConversationState(),
            mismatchRuntime);
        await mismatchState.StartTaskAsync("identity task", CancellationToken.None);
        Assert(mismatchState.State == TaskLifecycleState.Failed, "mismatched runtime identity fails closed");
        Assert(mismatchRuntime.Execution is { CancelCalled: true, DisposeCalled: true }, "mismatched execution cleanup");
        mismatchState.ValidateCanStart();

        var unavailable = new TaskExecutionState(
            journalStore,
            sessionState,
            new StreamingConversationState(),
            runtime: null,
            runtimeUnavailableReason: "fixture unavailable");
        var unavailableRejected = false;
        try { unavailable.ValidateCanStart(); }
        catch (InvalidOperationException exception) when (exception.Message.Contains("fixture unavailable", StringComparison.Ordinal))
        {
            unavailableRejected = true;
        }
        Assert(unavailableRejected && !unavailable.HasTask, "runtime unavailable fails before task creation");

        var surface = new TaskExecutionSurface { State = state };
        var window = new Window { Width = 900, Height = 600, Content = surface };
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert(surface.State.State == TaskLifecycleState.Succeeded, "production task surface state binding");
        window.Close();

        Console.WriteLine("Runtime P05-005 task-state lifecycle/persistence/cleanup/sequence fixture: PASS.");
    }

    private static async Task WaitForSettledAsync(TaskExecutionState state)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < timeout)
        {
            if (!state.IsActive)
            {
                try
                {
                    state.ValidateCanStart();
                    return;
                }
                catch (InvalidOperationException exception) when (exception.Message.Contains("still settling", StringComparison.Ordinal))
                {
                }
            }
            await Task.Delay(20);
        }
        throw new InvalidOperationException("P05-005 assertion failed: task did not fully settle before timeout.");
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"P05-005 assertion failed: {label}");
    }

    private sealed record Scenario(
        AgentRuntimeTerminalState TerminalState,
        AgentRuntimeFailure? Failure,
        string? SessionId,
        IReadOnlyList<AgentRuntimeEvent> Events)
    {
        public static Scenario Success(string sessionId, string answer) =>
            new(
                AgentRuntimeTerminalState.Succeeded,
                null,
                sessionId,
                new AgentRuntimeEvent[]
                {
                    new(0, DateTimeOffset.UtcNow, AgentRuntimeEventKind.SessionIdentified, sessionId: sessionId, sourceType: "fixture/session"),
                    new(1, DateTimeOffset.UtcNow, AgentRuntimeEventKind.AssistantTextDelta, text: answer, sessionId: sessionId, sourceType: "fixture/delta"),
                    new(2, DateTimeOffset.UtcNow, AgentRuntimeEventKind.Completion, sessionId: sessionId, sourceType: "fixture/completion"),
                });

        public static Scenario Failed(AgentRuntimeFailure failure) =>
            new(
                AgentRuntimeTerminalState.Failed,
                failure,
                null,
                new AgentRuntimeEvent[]
                {
                    new(0, DateTimeOffset.UtcNow, AgentRuntimeEventKind.Error, text: "runtime failure", sourceType: "fixture/error"),
                });

        public static Scenario SequenceGap() =>
            new(
                AgentRuntimeTerminalState.Succeeded,
                null,
                null,
                new AgentRuntimeEvent[]
                {
                    new(0, DateTimeOffset.UtcNow, AgentRuntimeEventKind.AssistantTextDelta, text: "partial", sourceType: "fixture/delta"),
                    new(2, DateTimeOffset.UtcNow, AgentRuntimeEventKind.Completion, sourceType: "fixture/completion"),
                });

        public static Scenario InvalidOrigin() =>
            new(
                AgentRuntimeTerminalState.Succeeded,
                null,
                null,
                new AgentRuntimeEvent[]
                {
                    new(1, DateTimeOffset.UtcNow, AgentRuntimeEventKind.Completion, sourceType: "fixture/completion"),
                });
    }

    private sealed class ScriptedRuntime : IAgentRuntime
    {
        private readonly Queue<Scenario> _scenarios;

        public ScriptedRuntime(params Scenario[] scenarios)
        {
            _scenarios = new Queue<Scenario>(scenarios);
            Descriptor = FixtureDescriptor();
        }

        public AgentRuntimeDescriptor Descriptor { get; }

        public Task<IAgentRuntimeExecution> StartAsync(AgentRuntimeRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_scenarios.Count == 0) throw new InvalidOperationException("No scripted runtime scenario remains.");
            return Task.FromResult<IAgentRuntimeExecution>(new ScriptedExecution(request, _scenarios.Dequeue()));
        }
    }

    private sealed class ScriptedExecution : IAgentRuntimeExecution
    {
        private readonly Scenario _scenario;

        public ScriptedExecution(AgentRuntimeRequest request, Scenario scenario)
        {
            TaskId = request.TaskId;
            RunId = request.RunId;
            _scenario = scenario;
            Completion = Task.FromResult(
                new AgentRuntimeResult(TaskId, RunId, scenario.TerminalState, scenario.SessionId, scenario.Failure));
        }

        public Guid TaskId { get; }
        public Guid RunId { get; }
        public IAsyncEnumerable<AgentRuntimeEvent> Events => EnumerateAsync();
        public Task<AgentRuntimeResult> Completion { get; }
        public ValueTask CancelAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async IAsyncEnumerable<AgentRuntimeEvent> EnumerateAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in _scenario.Events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return item;
            }
        }
    }

    private sealed class ManualRuntime : IAgentRuntime
    {
        public AgentRuntimeDescriptor Descriptor { get; } = FixtureDescriptor();
        public ManualExecution? LastExecution { get; private set; }

        public Task<IAgentRuntimeExecution> StartAsync(AgentRuntimeRequest request, CancellationToken cancellationToken = default)
        {
            LastExecution = new ManualExecution(request);
            return Task.FromResult<IAgentRuntimeExecution>(LastExecution);
        }
    }

    private sealed class ManualExecution : IAgentRuntimeExecution
    {
        private readonly TaskCompletionSource<AgentRuntimeResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ManualExecution(AgentRuntimeRequest request)
        {
            TaskId = request.TaskId;
            RunId = request.RunId;
        }

        public Guid TaskId { get; }
        public Guid RunId { get; }
        public IAsyncEnumerable<AgentRuntimeEvent> Events => EmptyEvents();
        public Task<AgentRuntimeResult> Completion => _completion.Task;
        public ValueTask CancelAsync(CancellationToken cancellationToken = default)
        {
            _completion.TrySetResult(new AgentRuntimeResult(TaskId, RunId, AgentRuntimeTerminalState.Cancelled));
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void CompleteSucceeded() =>
            _completion.TrySetResult(new AgentRuntimeResult(TaskId, RunId, AgentRuntimeTerminalState.Succeeded));

        private static async IAsyncEnumerable<AgentRuntimeEvent> EmptyEvents()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class MismatchedRuntime : IAgentRuntime
    {
        public AgentRuntimeDescriptor Descriptor { get; } = FixtureDescriptor();
        public MismatchedExecution? Execution { get; private set; }

        public Task<IAgentRuntimeExecution> StartAsync(AgentRuntimeRequest request, CancellationToken cancellationToken = default)
        {
            Execution = new MismatchedExecution(request);
            return Task.FromResult<IAgentRuntimeExecution>(Execution);
        }
    }

    private sealed class MismatchedExecution : IAgentRuntimeExecution
    {
        private readonly TaskCompletionSource<AgentRuntimeResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public MismatchedExecution(AgentRuntimeRequest request)
        {
            TaskId = Guid.NewGuid();
            RunId = request.RunId;
        }

        public Guid TaskId { get; }
        public Guid RunId { get; }
        public bool CancelCalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public IAsyncEnumerable<AgentRuntimeEvent> Events => EmptyEvents();
        public Task<AgentRuntimeResult> Completion => _completion.Task;
        public ValueTask CancelAsync(CancellationToken cancellationToken = default)
        {
            CancelCalled = true;
            _completion.TrySetResult(new AgentRuntimeResult(TaskId, RunId, AgentRuntimeTerminalState.Cancelled));
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }

        private static async IAsyncEnumerable<AgentRuntimeEvent> EmptyEvents()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private static AgentRuntimeDescriptor FixtureDescriptor() =>
        new(
            "fixture.p05-005",
            "P05-005 fixture runtime",
            AgentRuntimeTransport.Fixture,
            new AgentRuntimeCapabilities(true, true, true, true, true),
            "fixture");
}
'@

        $program = $program.Replace('__DB__', $databasePath).Replace('__WORKSPACE__', $workspacePath.Replace('"', '""'))
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM
        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) { throw "Runtime P05-005 fixture failed with exit code $LASTEXITCODE." }
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$statePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\TaskExecutionState.cs'
$surfacePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\TaskExecutionSurface.xaml'
$surfaceCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\TaskExecutionSurface.xaml.cs'
$sequencePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationSequencedAgentRuntime.cs'
$mainWindowPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$mainWindowCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'
$persistenceProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Persistence\FCCCodeDesktop.Persistence.csproj'
$runtimeProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Runtime\FCCCodeDesktop.Runtime.csproj'

foreach ($path in @(
    $statePath, $surfacePath, $surfaceCodePath, $sequencePath, $mainWindowPath, $mainWindowCodePath,
    $appProjectPath, $persistenceProjectPath, $runtimeProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P05-005 path is missing: $path"
    }
}

$stateText = Get-Content -LiteralPath $statePath -Raw
$surfaceText = Get-Content -LiteralPath $surfacePath -Raw
$surfaceCodeText = Get-Content -LiteralPath $surfaceCodePath -Raw
$sequenceText = Get-Content -LiteralPath $sequencePath -Raw
$mainWindowText = Get-Content -LiteralPath $mainWindowPath -Raw
$mainWindowCodeText = Get-Content -LiteralPath $mainWindowCodePath -Raw

Assert-TaskStateContract $stateText $surfaceText $surfaceCodeText $sequenceText $mainWindowText $mainWindowCodeText
Write-Host 'Static P05-005 task state-machine validation: PASS.'

if ($RunFixtures) {
    Assert-Rejected { Assert-TaskStateContract ($stateText.Replace('IsActive || _activePumpTask is not null || _activeExecution is not null', 'IsActive')) $surfaceText $surfaceCodeText $sequenceText $mainWindowText $mainWindowCodeText } 'settling-task guard removed'
    Assert-Rejected { Assert-TaskStateContract ($stateText.Replace('(TaskLifecycleState.Running, TaskLifecycleState.Succeeded) => true', '(TaskLifecycleState.Running, TaskLifecycleState.Succeeded) => false')) $surfaceText $surfaceCodeText $sequenceText $mainWindowText $mainWindowCodeText } 'success transition removed'
    Assert-Rejected { Assert-TaskStateContract ($stateText.Replace('AppendEventAsync(taskEvent', 'AppendEventAsync_REMOVED(taskEvent')) $surfaceText $surfaceCodeText $sequenceText $mainWindowText $mainWindowCodeText } 'durable event journal removed'
    Assert-Rejected { Assert-TaskStateContract ($stateText.Replace('CleanupUnownedExecutionAsync(startedExecution)', 'Task.FromResult<string?>(null)')) $surfaceText $surfaceCodeText $sequenceText $mainWindowText $mainWindowCodeText } 'startup execution cleanup removed'
    Assert-Rejected { Assert-TaskStateContract $stateText $surfaceText $surfaceCodeText ($sequenceText.Replace('Source runtime event sequence must start at zero.', 'Sequence origin unchecked.')) $mainWindowText $mainWindowCodeText } 'source sequence origin guard removed'
    Assert-Rejected { Assert-TaskStateContract $stateText $surfaceText $surfaceCodeText ($sequenceText.Replace('Source runtime event sequence must remain contiguous.', 'Sequence unchecked.')) $mainWindowText $mainWindowCodeText } 'source sequence continuity guard removed'
    Assert-Rejected { Assert-TaskStateContract $stateText ($surfaceText.Replace('{DynamicResource FccBrushCanvas}', '#112233')) $surfaceCodeText $sequenceText $mainWindowText $mainWindowCodeText } 'hard-coded task surface color'
    Assert-Rejected { Assert-TaskStateContract $stateText $surfaceText $surfaceCodeText $sequenceText $mainWindowText ($mainWindowCodeText.Replace('navigationState.TasksContent = taskExecutionSurface', '')) } 'task navigation composition removed'
    Write-Host 'Negative P05-005 fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-RuntimeFixture $appProjectPath $persistenceProjectPath $runtimeProjectPath
}
