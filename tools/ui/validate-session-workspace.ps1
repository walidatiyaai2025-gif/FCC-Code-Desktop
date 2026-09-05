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

function Assert-SessionContract {
    param(
        [string]$StateText,
        [string]$StreamingText,
        [string]$SurfaceText,
        [string]$SurfaceCodeText,
        [string]$MainWindowText,
        [string]$MainWindowCodeText
    )

    Assert-ValidXaml $SurfaceText 'SessionWorkspaceSurface.xaml'
    Assert-ValidXaml $MainWindowText 'MainWindow.xaml'

    foreach ($literal in @(
        'IConversationStateStore _store',
        'GetProjectAsync(projectId',
        'ListSessionsAsync(projectId',
        'UpsertSessionAsync(session',
        'GetSessionAsync(sessionId',
        'ListMessagesAsync(sessionId',
        'AppendMessageAsync(message',
        'session.ProjectId != projectId',
        'A session cannot be resumed from a different active project.',
        'public async Task BindRuntimeSessionAsync(',
        'RuntimeSessionId = runtimeSessionId.Trim()',
        'private static void ValidatePersistedMessages(',
        'message.Sequence <= previous',
        'private readonly object _messageWriteSync = new();',
        'private Task _messageWriteTail = Task.CompletedTask;',
        'lock (_messageWriteSync)',
        'TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)',
        'await predecessor.ConfigureAwait(false);',
        'completion.TrySetResult(true);',
        'OrderByDescending(item => item.UpdatedUtc)'
    )) {
        Assert-ContainsLiteral $StateText $literal 'SessionWorkspaceState.cs'
    }

    foreach ($literal in @(
        'public void LoadPersistedMessages(IReadOnlyList<PersistedMessage> messages)',
        'Reset();',
        '"user" => ConversationMessageRole.User',
        '"assistant" => ConversationMessageRole.Assistant',
        'message.Sequence <= previous'
    )) {
        Assert-ContainsLiteral $StreamingText $literal 'StreamingConversationState.cs'
    }

    foreach ($literal in @(
        'x:Name="SessionHistoryItems"',
        'ItemsSource="{Binding Sessions}"',
        'x:Name="NewSessionButton"',
        'Content="New session"',
        'x:Name="RefreshSessionsButton"',
        'SelectionChanged="OnSessionSelectionChanged"',
        'Content="{Binding ConversationContent, ElementName=Root}"',
        '{DynamicResource FccBrushCanvas}',
        '{DynamicResource FccBrushSurfaceRaised}',
        '{DynamicResource FccBrushBorder}',
        '{DynamicResource FccBrushAccent}',
        '{DynamicResource FccBrushError}'
    )) {
        Assert-ContainsLiteral $SurfaceText $literal 'SessionWorkspaceSurface.xaml'
    }

    foreach ($literal in @(
        'DependencyProperty StateProperty',
        'DependencyProperty ConversationContentProperty',
        'State.CreateSessionAsync()',
        'State.RefreshAsync()',
        'State.ResumeSessionAsync(selectedSession.Id)'
    )) {
        Assert-ContainsLiteral $SurfaceCodeText $literal 'SessionWorkspaceSurface.xaml.cs'
    }

    foreach ($literal in @(
        '<conversation:SessionWorkspaceSurface x:Key="SessionWorkspaceSurface"',
        'ConversationContent="{StaticResource ConversationSurface}"'
    )) {
        Assert-ContainsLiteral $MainWindowText $literal 'MainWindow.xaml'
    }

    foreach ($literal in @(
        'Task<SessionWorkspaceState>? _sessionInitializationTask',
        '_sessionInitializationTask ??= InitializeSessionWorkspaceCoreAsync()',
        'new SqliteDatabaseOptions(Path.Combine(stateDirectory, "fcc-code-desktop.db"))',
        'new SqliteDatabaseInitializer(options).InitializeAsync',
        'new SessionWorkspaceState(new SqliteConversationStateStore(options))',
        'state.SessionChanged += OnSessionChanged',
        'navigationState.SessionsContent = sessionWorkspaceSurface',
        'public async Task ActivateProjectSessionsAsync(',
        'public async Task BindActiveRuntimeSessionAsync(',
        'conversationState.LoadPersistedMessages(e.Messages)',
        '_sessionWorkspaceState.AppendMessageAsync(',
        'conversationState.AddUserMessage(e.Submission.Text)',
        'composerState.AcceptSubmission(e.Submission.SubmissionId)',
        'composerState.RejectSubmission(e.Submission.SubmissionId, exception.Message)'
    )) {
        Assert-ContainsLiteral $MainWindowCodeText $literal 'MainWindow.xaml.cs'
    }

    if ($SurfaceText -match '#[0-9A-Fa-f]{6,8}') {
        throw 'P05-004 session surface must use semantic resources instead of hard-coded colors.'
    }

    foreach ($text in @($StateText, $StreamingText, $SurfaceText, $SurfaceCodeText, $MainWindowText, $MainWindowCodeText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P05-004 contains forbidden placeholder text '$placeholder'."
            }
        }
    }

    foreach ($forbidden in @('IAgentRuntime', 'AgentRuntimeRequest', 'PayloadJson', 'Process.Start', 'fcc-claude')) {
        if ($StateText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $SurfaceText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $SurfaceCodeText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
            throw "P05-004 crossed the session UX/persistence boundary: $forbidden"
        }
    }
}

function Assert-Rejected {
    param([scriptblock]$Action, [string]$Label)
    try { & $Action }
    catch { Write-Host "Negative fixture rejected as expected: $Label"; return }
    throw "Negative session-workspace fixture was not rejected: $Label"
}

function Invoke-RuntimeFixture {
    param([string]$AppProjectPath, [string]$PersistenceProjectPath)

    if (-not $IsWindows) { throw 'Runtime session-workspace fixture requires Windows/WPF.' }
    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime session-workspace fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $root = Join-Path ([IO.Path]::GetTempPath()) ('fccd-session-workspace-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $root -Force)
    try {
        $projectPath = Join-Path $root 'Fixture.csproj'
        $programPath = Join-Path $root 'Program.cs'
        $appReference = [Security.SecurityElement]::Escape($AppProjectPath)
        $persistenceReference = [Security.SecurityElement]::Escape($PersistenceProjectPath)
        $databasePath = (Join-Path $root 'durable state.db').Replace('"', '""')

        Set-Content -LiteralPath $projectPath -Encoding utf8NoBOM -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF><EnableWindowsTargeting>true</EnableWindowsTargeting>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$appReference" />
    <ProjectReference Include="$persistenceReference" />
  </ItemGroup>
</Project>
"@

        $program = @'
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FCCCodeDesktop.App;
using FCCCodeDesktop.App.Conversation;
using FCCCodeDesktop.App.DesignSystem;
using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Persistence;

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
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
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
        var store = new SqliteConversationStateStore(options);
        var now = new DateTimeOffset(2026, 9, 5, 4, 0, 0, TimeSpan.Zero);
        var projectA = new PersistedProject(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "a"), "Project A", now, now);
        var projectB = new PersistedProject(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "b"), "Project B", now, now);
        await store.UpsertProjectAsync(projectA, CancellationToken.None);
        await store.UpsertProjectAsync(projectB, CancellationToken.None);

        var state = new SessionWorkspaceState(store);
        IReadOnlyList<PersistedMessage>? resumed = null;
        state.SessionChanged += (_, e) => resumed = e.Messages;
        await state.ActivateProjectAsync(projectA.Id, CancellationToken.None);
        var first = await state.CreateSessionAsync("First", CancellationToken.None);
        await state.BindRuntimeSessionAsync("fcc-resume-001", CancellationToken.None);
        await state.AppendMessageAsync("user", "hello", CancellationToken.None);
        await state.AppendMessageAsync("assistant", "world", CancellationToken.None);
        var second = await state.CreateSessionAsync("Second", CancellationToken.None);
        Assert(state.Sessions.Count == 2 && state.Sessions[0].Id == second.Id, "newest-first history");
        await state.ResumeSessionAsync(first.Id, CancellationToken.None);
        Assert(state.ActiveRuntimeSessionId == "fcc-resume-001", "runtime id restore");
        Assert(resumed is { Count: 2 } && resumed[0].Content == "hello" && resumed[1].Content == "world", "message restore");

        var foreign = new PersistedSession(Guid.NewGuid(), projectB.Id, null, "Foreign", now, now);
        await store.UpsertSessionAsync(foreign, CancellationToken.None);
        var rejected = false;
        try { await state.ResumeSessionAsync(foreign.Id, CancellationToken.None); }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected, "cross-project rejection");

        var restarted = new SessionWorkspaceState(new SqliteConversationStateStore(options));
        IReadOnlyList<PersistedMessage>? afterRestart = null;
        restarted.SessionChanged += (_, e) => afterRestart = e.Messages;
        await restarted.ActivateProjectAsync(projectA.Id, CancellationToken.None);
        Assert(restarted.Sessions.Count == 2, "history after state recreation");
        await restarted.ResumeSessionAsync(first.Id, CancellationToken.None);
        Assert(restarted.ActiveRuntimeSessionId == "fcc-resume-001" && afterRestart is { Count: 2 }, "resume after state recreation");

        var conversation = new StreamingConversationState();
        conversation.LoadPersistedMessages(afterRestart!);
        Assert(conversation.Messages.Count == 2 && !conversation.IsStreaming && !conversation.HasToolActivities, "conversation projection/reset");

        var sessionSurface = new SessionWorkspaceSurface
        {
            State = restarted,
            ConversationContent = new ConversationSurface { State = conversation, Composer = new ComposerState() },
        };
        var window = new Window { Content = sessionSurface, Width = 1000, Height = 700 };
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert(sessionSurface.FindName("SessionHistoryItems") is ListBox list && list.Items.Count == 2, "history rendered");
        var dark = ((SolidColorBrush)sessionSurface.Background).Color;
        var themes = new ThemeService(app.Resources);
        themes.Apply(AppearanceTheme.Light);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var light = ((SolidColorBrush)sessionSurface.Background).Color;
        Assert(dark != light, "semantic theme parity");
        window.Close();
        Console.WriteLine("Runtime session-workspace create/history/resume/restart fixture: PASS.");
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Session-workspace assertion failed: {label}");
    }
}
'@
        $program = $program.Replace('__DB__', $databasePath)
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM
        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) { throw "Runtime session-workspace fixture failed with exit code $LASTEXITCODE." }
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$statePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\SessionWorkspaceState.cs'
$streamingPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\StreamingConversationState.cs'
$surfacePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\SessionWorkspaceSurface.xaml'
$surfaceCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\SessionWorkspaceSurface.xaml.cs'
$mainWindowPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$mainWindowCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'
$persistenceProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Persistence\FCCCodeDesktop.Persistence.csproj'
foreach ($path in @($statePath, $streamingPath, $surfacePath, $surfaceCodePath, $mainWindowPath, $mainWindowCodePath, $appProjectPath, $persistenceProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required session-workspace path is missing: $path" }
}

$stateText = Get-Content -LiteralPath $statePath -Raw
$streamingText = Get-Content -LiteralPath $streamingPath -Raw
$surfaceText = Get-Content -LiteralPath $surfacePath -Raw
$surfaceCodeText = Get-Content -LiteralPath $surfaceCodePath -Raw
$mainWindowText = Get-Content -LiteralPath $mainWindowPath -Raw
$mainWindowCodeText = Get-Content -LiteralPath $mainWindowCodePath -Raw
Assert-SessionContract $stateText $streamingText $surfaceText $surfaceCodeText $mainWindowText $mainWindowCodeText
Write-Host 'Static session-workspace create/history/resume validation: PASS.'

if ($RunFixtures) {
    Assert-Rejected { Assert-SessionContract ($stateText.Replace('session.ProjectId != projectId', 'false')) $streamingText $surfaceText $surfaceCodeText $mainWindowText $mainWindowCodeText } 'cross-project guard removed'
    Assert-Rejected { Assert-SessionContract ($stateText.Replace('message.Sequence <= previous', 'false')) $streamingText $surfaceText $surfaceCodeText $mainWindowText $mainWindowCodeText } 'message sequence guard removed'
    Assert-Rejected { Assert-SessionContract ($stateText.Replace('RuntimeSessionId = runtimeSessionId.Trim()', 'RuntimeSessionId = null')) $streamingText $surfaceText $surfaceCodeText $mainWindowText $mainWindowCodeText } 'runtime-session binding removed'
    Assert-Rejected { Assert-SessionContract $stateText $streamingText ($surfaceText.Replace('x:Name="SessionHistoryItems"', 'x:Name="RemovedHistoryItems"')) $surfaceCodeText $mainWindowText $mainWindowCodeText } 'session history removed'
    Assert-Rejected { Assert-SessionContract $stateText $streamingText ($surfaceText.Replace('{DynamicResource FccBrushSurfaceRaised}', '#010203')) $surfaceCodeText $mainWindowText $mainWindowCodeText } 'hard-coded session color'
    Assert-Rejected { Assert-SessionContract ($stateText.Replace('lock (_messageWriteSync)', '')) $streamingText $surfaceText $surfaceCodeText $mainWindowText $mainWindowCodeText } 'serialized message write guard removed'
    Write-Host 'Deterministic session-workspace negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) { Invoke-RuntimeFixture $appProjectPath $persistenceProjectPath }
