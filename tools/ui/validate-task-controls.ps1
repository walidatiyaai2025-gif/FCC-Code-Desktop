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

function Get-MethodSlice {
    param([string]$Text, [string]$StartLiteral, [string]$EndLiteral, [string]$Label)
    $start = $Text.IndexOf($StartLiteral, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "$Label start marker not found: $StartLiteral" }
    $end = $Text.IndexOf($EndLiteral, $start + $StartLiteral.Length, [StringComparison]::Ordinal)
    if ($end -lt 0) { throw "$Label end marker not found: $EndLiteral" }
    return $Text.Substring($start, $end - $start)
}

function Assert-ControlContract {
    param([string]$StateText, [string]$SurfaceText, [string]$SurfaceCodeText)

    Assert-ValidXaml $SurfaceText 'TaskExecutionSurface.xaml'

    foreach ($literal in @(
        'private string? _prompt;',
        'public bool CanStop => State == TaskLifecycleState.Running && _activeExecution is not null;',
        'State is TaskLifecycleState.Failed or TaskLifecycleState.Cancelled',
        '_activePumpTask is null',
        '_activeExecution is null',
        'public async Task RequestStopAsync(',
        'if (State == TaskLifecycleState.StopRequested)',
        'var execution = _activeExecution;',
        'TransitionTo(TaskLifecycleState.StopRequested);',
        'PersistTaskAsync("StopRequested"',
        '"StopRequested",',
        'await execution.CancelAsync(CancellationToken.None)',
        '"StopRequestFailed"',
        'public async Task RetryAsync(',
        '.ListEventsAsync(_activeTaskId.Value',
        'checked(events[^1].Sequence + 1)',
        'StartAttemptAsync(isManualRetry: true',
        'private async Task StartAttemptAsync(bool isManualRetry',
        '_activeRunId = Guid.NewGuid();',
        '_attempt = checked(_attempt + 1);',
        'isManualRetry ? "ManualRetryStarting" : "TaskStarting"',
        'Return to the task''s owning session before retrying it.',
        'OnPropertyChanged(nameof(CanStop));',
        'OnPropertyChanged(nameof(CanRetry));'
    )) {
        Assert-ContainsLiteral $StateText $literal 'TaskExecutionState.cs'
    }

    $retryText = Get-MethodSlice $StateText 'public async Task RetryAsync(' 'public void ReportControlError(' 'RetryAsync'
    if ($retryText.Contains('_activeTaskId = Guid.NewGuid()', [StringComparison]::Ordinal)) {
        throw 'P05-006 retry must preserve the existing logical task identity.'
    }
    if ($retryText.Contains('AppendMessageAsync("user"', [StringComparison]::Ordinal)) {
        throw 'P05-006 retry must not duplicate the original durable user message.'
    }

    $attemptText = Get-MethodSlice $StateText 'private async Task StartAttemptAsync(' 'private async Task PumpExecutionAsync(' 'StartAttemptAsync'
    Assert-ContainsLiteral $attemptText '_activeRunId = Guid.NewGuid();' 'StartAttemptAsync'
    Assert-ContainsLiteral $attemptText '_prompt,' 'StartAttemptAsync'

    foreach ($literal in @(
        'x:Name="StopTaskButton"',
        'Content="Stop"',
        'IsEnabled="{Binding CanStop}"',
        'Click="OnStopClick"',
        'AutomationProperties.Name="Stop active task"',
        'x:Name="RetryTaskButton"',
        'Content="Retry"',
        'IsEnabled="{Binding CanRetry}"',
        'Click="OnRetryClick"',
        'AutomationProperties.Name="Retry failed or cancelled task"'
    )) {
        Assert-ContainsLiteral $SurfaceText $literal 'TaskExecutionSurface.xaml'
    }

    foreach ($literal in @(
        'private async void OnStopClick(',
        'State.RequestStopAsync(CancellationToken.None)',
        'private async void OnRetryClick(',
        'State.RetryAsync(CancellationToken.None)',
        'State.ReportControlError(exception.Message)'
    )) {
        Assert-ContainsLiteral $SurfaceCodeText $literal 'TaskExecutionSurface.xaml.cs'
    }

    if ($SurfaceText -match '#[0-9A-Fa-f]{6,8}') {
        throw 'P05-006 task controls must use semantic resources instead of hard-coded colors.'
    }

    foreach ($text in @($StateText, $SurfaceText, $SurfaceCodeText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P05-006 contains forbidden placeholder text '$placeholder'."
            }
        }
    }
}

function Assert-Rejected {
    param([scriptblock]$Action, [string]$Label)
    try { & $Action }
    catch { Write-Host "Negative fixture rejected as expected: $Label"; return }
    throw "Negative P05-006 fixture was not rejected: $Label"
}

function Invoke-RuntimeFixture {
    param(
        [string]$AppProjectPath,
        [string]$PersistenceProjectPath,
        [string]$RuntimeProjectPath
    )

    if (-not $IsWindows) { throw 'Runtime P05-006 fixture requires Windows/WPF.' }
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime P05-006 fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $root = Join-Path ([IO.Path]::GetTempPath()) ('fccd-p05-006-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $root -Force)
    try {
        $projectPath = Join-Path $root 'Fixture.csproj'
        $programPath = Join-Path $root 'Program.cs'
        $databasePath = (Join-Path $root 'task-controls.db').Replace('"', '""')
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
using System.Windows.Controls;
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
        var now = new DateTimeOffset(2026, 9, 5, 8, 20, 0, TimeSpan.Zero);
        var project = new PersistedProject(Guid.NewGuid(), @"__WORKSPACE__", "P05-006 fixture", now, now);
        await conversationStore.UpsertProjectAsync(project, CancellationToken.None);

        var sessionState = new SessionWorkspaceState(conversationStore);
        await sessionState.ActivateProjectAsync(project.Id, CancellationToken.None);
        var session = await sessionState.CreateSessionAsync("P05-006", CancellationToken.None);
        var conversation = new StreamingConversationState();
        var controlled = new ControlledRuntime();
        var state = new TaskExecutionState(
            journalStore,
            sessionState,
            conversation,
            new ConversationSequencedAgentRuntime(controlled));

        const string prompt = "retry this  exact prompt";
        await sessionState.AppendMessageAsync("user", prompt, CancellationToken.None);
        conversation.AddUserMessage(prompt);
        await state.StartTaskAsync(prompt, CancellationToken.None);
        Assert(state.State == TaskLifecycleState.Running, "initial task running");
        Assert(state.CanStop && !state.CanRetry, "control availability while running");
        var taskId = state.ActiveTaskId ?? throw new InvalidOperationException("P05-006 assertion failed: task id");
        var firstRunId = state.ActiveRunId ?? throw new InvalidOperationException("P05-006 assertion failed: first run id");
        Assert(state.Attempt == 1, "first attempt number");

        await state.RequestStopAsync(CancellationToken.None);
        await state.RequestStopAsync(CancellationToken.None);
        var firstExecution = controlled.FirstExecution
            ?? throw new InvalidOperationException("P05-006 assertion failed: first execution");
        Assert(firstExecution.CancelCount == 1, "stop request is idempotent");
        Assert(state.State == TaskLifecycleState.StopRequested, "stop requested state");
        Assert(!state.CanStop && !state.CanRetry, "controls while stop is pending");
        firstExecution.CompleteCancelled();
        await WaitForSettledAsync(state);
        Assert(state.State == TaskLifecycleState.Cancelled, "cancelled terminal state");
        Assert(state.CanRetry && !state.CanStop, "retry available only after cancelled run settles");
        Assert((await journalStore.GetTaskAsync(taskId, CancellationToken.None))?.State == "Cancelled", "cancelled task durable state");

        var beforeRetryMessages = await conversationStore.ListMessagesAsync(session.Id, CancellationToken.None);
        Assert(beforeRetryMessages.Count == 1 && beforeRetryMessages[0].Role == "user", "single durable user message before retry");

        await state.RetryAsync(CancellationToken.None);
        var secondRunId = state.ActiveRunId ?? throw new InvalidOperationException("P05-006 assertion failed: second run id");
        Assert(state.ActiveTaskId == taskId, "manual retry preserves logical task id");
        Assert(secondRunId != firstRunId, "manual retry creates a new run id");
        Assert(state.Attempt == 2, "manual retry increments attempt");
        await WaitForSettledAsync(state);
        Assert(state.State == TaskLifecycleState.Succeeded, "retry success state");
        Assert(!state.CanRetry && !state.CanStop, "controls disabled after success");
        Assert(controlled.Requests.Count == 2, "two runtime attempts");
        Assert(controlled.Requests[0].TaskId == controlled.Requests[1].TaskId, "runtime requests preserve task id");
        Assert(controlled.Requests[0].RunId != controlled.Requests[1].RunId, "runtime requests use distinct run ids");
        Assert(controlled.Requests[1].Prompt == prompt, "manual retry preserves exact original prompt");

        var messages = await conversationStore.ListMessagesAsync(session.Id, CancellationToken.None);
        Assert(messages.Count == 2, "retry does not duplicate durable user message");
        Assert(messages.Count(item => item.Role == "user") == 1, "one durable user message after retry");
        Assert(messages[1].Role == "assistant" && messages[1].Content == "retry answer", "retry assistant output persisted");

        var events = await journalStore.ListEventsAsync(taskId, CancellationToken.None);
        Assert(events.Count(item => item.EventType == "StopRequested") == 1, "one durable StopRequested event");
        Assert(events.Count(item => item.EventType == "ManualRetryStarting") == 1, "one durable ManualRetryStarting event");
        for (var index = 0; index < events.Count; index++)
        {
            Assert(events[index].Sequence == index, "durable journal sequence remains contiguous across retry");
        }

        await state.StartTaskAsync("failure before foreign-session retry", CancellationToken.None);
        await WaitForSettledAsync(state);
        Assert(state.State == TaskLifecycleState.Failed && state.CanRetry, "failed task is retryable after settling");
        var failedTaskId = state.ActiveTaskId;
        var startsBeforeForeignRetry = controlled.Requests.Count;
        await sessionState.CreateSessionAsync("other session", CancellationToken.None);
        var foreignRejected = false;
        try { await state.RetryAsync(CancellationToken.None); }
        catch (InvalidOperationException exception) when (exception.Message.Contains("owning session", StringComparison.Ordinal))
        {
            foreignRejected = true;
        }
        Assert(foreignRejected, "cross-session retry rejected");
        Assert(controlled.Requests.Count == startsBeforeForeignRetry && state.ActiveTaskId == failedTaskId, "rejected retry creates no runtime attempt or new task identity");

        var surface = new TaskExecutionSurface { State = state };
        var window = new Window { Width = 900, Height = 600, Content = surface };
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert(surface.FindName("StopTaskButton") is Button, "production Stop button exists");
        Assert(surface.FindName("RetryTaskButton") is Button, "production Retry button exists");
        window.Close();

        Console.WriteLine("Runtime P05-006 stop/cancel/retry identity/durability fixture: PASS.");
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
        throw new InvalidOperationException("P05-006 assertion failed: task did not fully settle before timeout.");
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"P05-006 assertion failed: {label}");
    }

    private sealed class ControlledRuntime : IAgentRuntime
    {
        public AgentRuntimeDescriptor Descriptor { get; } = FixtureDescriptor();
        public List<AgentRuntimeRequest> Requests { get; } = new();
        public ManualExecution? FirstExecution { get; private set; }

        public Task<IAgentRuntimeExecution> StartAsync(AgentRuntimeRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (Requests.Count == 1)
            {
                FirstExecution = new ManualExecution(request);
                return Task.FromResult<IAgentRuntimeExecution>(FirstExecution);
            }
            if (Requests.Count == 2)
            {
                return Task.FromResult<IAgentRuntimeExecution>(new ImmediateExecution(request, success: true));
            }
            if (Requests.Count == 3)
            {
                return Task.FromResult<IAgentRuntimeExecution>(new ImmediateExecution(request, success: false));
            }
            throw new InvalidOperationException("Unexpected P05-006 runtime attempt.");
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
        public int CancelCount { get; private set; }
        public IAsyncEnumerable<AgentRuntimeEvent> Events => EmptyEvents();
        public Task<AgentRuntimeResult> Completion => _completion.Task;

        public ValueTask CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancelCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void CompleteCancelled() =>
            _completion.TrySetResult(new AgentRuntimeResult(TaskId, RunId, AgentRuntimeTerminalState.Cancelled));

        private static async IAsyncEnumerable<AgentRuntimeEvent> EmptyEvents()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class ImmediateExecution : IAgentRuntimeExecution
    {
        private readonly bool _success;

        public ImmediateExecution(AgentRuntimeRequest request, bool success)
        {
            TaskId = request.TaskId;
            RunId = request.RunId;
            _success = success;
            Completion = Task.FromResult(success
                ? new AgentRuntimeResult(TaskId, RunId, AgentRuntimeTerminalState.Succeeded)
                : new AgentRuntimeResult(
                    TaskId,
                    RunId,
                    AgentRuntimeTerminalState.Failed,
                    failure: new AgentRuntimeFailure(
                        AgentRuntimeFailureKind.ProviderUnavailable,
                        "fixture failure",
                        AgentRuntimeRetryability.NotRetryable,
                        AgentRuntimeUserAction.NotRequired)));
        }

        public Guid TaskId { get; }
        public Guid RunId { get; }
        public IAsyncEnumerable<AgentRuntimeEvent> Events => EnumerateAsync();
        public Task<AgentRuntimeResult> Completion { get; }
        public ValueTask CancelAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async IAsyncEnumerable<AgentRuntimeEvent> EnumerateAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            if (_success)
            {
                yield return new AgentRuntimeEvent(0, DateTimeOffset.UtcNow, AgentRuntimeEventKind.AssistantTextDelta, text: "retry answer", sourceType: "fixture/delta");
                yield return new AgentRuntimeEvent(1, DateTimeOffset.UtcNow, AgentRuntimeEventKind.Completion, sourceType: "fixture/completion");
            }
            else
            {
                yield return new AgentRuntimeEvent(0, DateTimeOffset.UtcNow, AgentRuntimeEventKind.Error, text: "fixture failure", sourceType: "fixture/error");
            }
        }
    }

    private static AgentRuntimeDescriptor FixtureDescriptor() =>
        new(
            "fixture.p05-006",
            "P05-006 fixture runtime",
            AgentRuntimeTransport.Fixture,
            new AgentRuntimeCapabilities(true, true, true, true, true),
            "fixture");
}
'@

        $program = $program.Replace('__DB__', $databasePath).Replace('__WORKSPACE__', $workspacePath.Replace('"', '""'))
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM
        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) { throw "Runtime P05-006 fixture failed with exit code $LASTEXITCODE." }
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$statePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\TaskExecutionState.cs'
$surfacePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\TaskExecutionSurface.xaml'
$surfaceCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\TaskExecutionSurface.xaml.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'
$persistenceProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Persistence\FCCCodeDesktop.Persistence.csproj'
$runtimeProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Runtime\FCCCodeDesktop.Runtime.csproj'

foreach ($path in @($statePath, $surfacePath, $surfaceCodePath, $appProjectPath, $persistenceProjectPath, $runtimeProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P05-006 path is missing: $path"
    }
}

$stateText = Get-Content -LiteralPath $statePath -Raw
$surfaceText = Get-Content -LiteralPath $surfacePath -Raw
$surfaceCodeText = Get-Content -LiteralPath $surfaceCodePath -Raw

Assert-ControlContract $stateText $surfaceText $surfaceCodeText
Write-Host 'Static P05-006 stop/cancel/retry validation: PASS.'

if ($RunFixtures) {
    Assert-Rejected { Assert-ControlContract ($stateText.Replace('await execution.CancelAsync(CancellationToken.None)', 'await Task.CompletedTask')) $surfaceText $surfaceCodeText } 'owned runtime cancellation removed'
    Assert-Rejected { Assert-ControlContract ($stateText.Replace('isManualRetry ? "ManualRetryStarting" : "TaskStarting"', '"TaskStarting"')) $surfaceText $surfaceCodeText } 'manual retry journal identity removed'
    Assert-Rejected { Assert-ControlContract ($stateText.Replace('Return to the task''s owning session before retrying it.', 'Retry session unchecked.')) $surfaceText $surfaceCodeText } 'retry session ownership guard removed'
    Assert-Rejected { Assert-ControlContract ($stateText.Replace('_activeRunId = Guid.NewGuid();', '_activeRunId = _activeRunId;')) $surfaceText $surfaceCodeText } 'new run identity removed'
    Assert-Rejected { Assert-ControlContract ($stateText.Replace('public async Task RetryAsync(CancellationToken cancellationToken = default)', 'public async Task RetryAsync(CancellationToken cancellationToken = default)\n    {\n        _activeTaskId = Guid.NewGuid();\n    }\n\n    private async Task RetryAsyncRemoved(CancellationToken cancellationToken = default)')) $surfaceText $surfaceCodeText } 'retry changes logical task identity'
    Assert-Rejected { Assert-ControlContract $stateText ($surfaceText.Replace('IsEnabled="{Binding CanStop}"', 'IsEnabled="True"')) $surfaceCodeText } 'Stop enablement binding removed'
    Assert-Rejected { Assert-ControlContract $stateText ($surfaceText.Replace('IsEnabled="{Binding CanRetry}"', 'IsEnabled="True"')) $surfaceCodeText } 'Retry enablement binding removed'
    Assert-Rejected { Assert-ControlContract $stateText ($surfaceText.Replace('{DynamicResource FccBrushCanvas}', '#112233')) $surfaceCodeText } 'hard-coded control-surface color'
    Write-Host 'Negative P05-006 fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-RuntimeFixture $appProjectPath $persistenceProjectPath $runtimeProjectPath
}
