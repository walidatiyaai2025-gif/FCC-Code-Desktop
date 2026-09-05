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

    try {
        [void][xml]$Text
    }
    catch {
        throw "$Label is not valid XML/XAML: $($_.Exception.Message)"
    }
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
        'private readonly SemaphoreSlim _messageWriteGate',
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
        '{DynamicResource FccBrushSurface}',
        '{DynamicResource FccBrushSurfaceRaised}',
        '{DynamicResource FccBrushBorder}',
        '{DynamicResource FccBrushAccent}',
        '{DynamicResource FccBrushError}'
    )) {
        Assert-ContainsLiteral $SurfaceText $literal 'SessionWorkspaceSurface.xaml'
    }

    foreach ($literal in @(
        'public static readonly DependencyProperty StateProperty',
        'typeof(SessionWorkspaceState)',
        'public static readonly DependencyProperty ConversationContentProperty',
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

    foreach ($text in @($StateText, $StreamingText, $SurfaceText, $SurfaceCodeText, $MainWindowText, $MainWindowCodeText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P05-004 contains forbidden placeholder text '$placeholder'."
            }
        }
    }

    if ($SurfaceText -match '#[0-9A-Fa-f]{6,8}') {
        throw 'P05-004 session surface must consume semantic theme resources instead of hard-coded colors.'
    }

    foreach ($forbidden in @(
        'IAgentRuntime',
        'AgentRuntimeRequest',
        'PayloadJson',
        'System.Diagnostics.Process',
        'Process.Start',
        'fcc-claude'
    )) {
        if ($StateText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $SurfaceText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $SurfaceCodeText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
            throw "P05-004 crossed the session UX/persistence boundary: $forbidden"
        }
    }
}

function Assert-ContractRejects {
    param([scriptblock]$Action, [string]$Label)

    try {
        & $Action
    }
    catch {
        Write-Host "Negative fixture rejected as expected: $Label"
        return
    }

    throw "Negative session-workspace fixture was not rejected: $Label"
}

function Invoke-SessionRuntimeFixture {
    param(
        [string]$AppProjectPath,
        [string]$PersistenceProjectPath
    )

    if (-not $IsWindows) {
        throw 'Runtime session-workspace fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime session-workspace fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime session-workspace fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-session-workspace-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'SessionWorkspaceFixture.csproj'
        $programPath = Join-Path $fixtureRoot 'Program.cs'
        $appReference = [Security.SecurityElement]::Escape($AppProjectPath)
        $persistenceReference = [Security.SecurityElement]::Escape($PersistenceProjectPath)
        $databasePath = Join-Path $fixtureRoot 'durable state.db'
        $databaseLiteral = $databasePath.Replace('"', '""')

        $project = @"
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
  </ItemGroup>
</Project>
"@

        $programTemplate = @'
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

        var options = new SqliteDatabaseOptions(@"__DATABASE_PATH__");
        await new SqliteDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var store = new SqliteConversationStateStore(options);
        var now = new DateTimeOffset(2026, 9, 5, 4, 0, 0, TimeSpan.Zero);
        var projectA = new PersistedProject(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "project-a"), "Project A", now, now);
        var projectB = new PersistedProject(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "project-b"), "Project B", now, now);
        await store.UpsertProjectAsync(projectA, CancellationToken.None);
        await store.UpsertProjectAsync(projectB, CancellationToken.None);

        var state = new SessionWorkspaceState(store);
        IReadOnlyList<PersistedMessage>? resumedMessages = null;
        state.SessionChanged += (_, args) => resumedMessages = args.Messages;
        await state.ActivateProjectAsync(projectA.Id, CancellationToken.None);
        Assert(state.HasActiveProject && state.ActiveProjectId == projectA.Id, "project activation");
        Assert(state.Sessions.Count == 0, "initial project session history empty");

        var first = await state.CreateSessionAsync("First durable session", CancellationToken.None);
        await state.BindRuntimeSessionAsync("fcc-resume-001", CancellationToken.None);
        await state.AppendMessageAsync("user", "hello", CancellationToken.None);
        await state.AppendMessageAsync("assistant", "world", CancellationToken.None);
        var second = await state.CreateSessionAsync("Newest session", CancellationToken.None);
        Assert(state.Sessions.Count == 2 && state.Sessions[0].Id == second.Id, "history ordered newest first");

        await state.ResumeSessionAsync(first.Id, CancellationToken.None);
        Assert(state.ActiveRuntimeSessionId == "fcc-resume-001", "runtime session id restored");
        Assert(resumedMessages is { Count: 2 }, "message history restored");
        Assert(resumedMessages![0].Sequence == 0 && resumedMessages[0].Role == "user" && resumedMessages[0].Content == "hello", "user message restored");
        Assert(resumedMessages[1].Sequence == 1 && resumedMessages[1].Role == "assistant" && resumedMessages[1].Content == "world", "assistant message restored");

        var foreignSession = new PersistedSession(Guid.NewGuid(), projectB.Id, null, "Foreign", now, now);
        await store.UpsertSessionAsync(foreignSession, CancellationToken.None);
        var crossProjectRejected = false;
        try
        {
            await state.ResumeSessionAsync(foreignSession.Id, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            crossProjectRejected = true;
        }
        Assert(crossProjectRejected, "cross-project resume rejected");

        var restarted = new SessionWorkspaceState(new SqliteConversationStateStore(options));
        IReadOnlyList<PersistedMessage>? restartMessages = null;
        restarted.SessionChanged += (_, args) => restartMessages = args.Messages;
        await restarted.ActivateProjectAsync(projectA.Id, CancellationToken.None);
        Assert(restarted.Sessions.Count == 2, "session history survives state recreation");
        await restarted.ResumeSessionAsync(first.Id, CancellationToken.None);
        Assert(restarted.ActiveRuntimeSessionId == "fcc-resume-001", "runtime id survives state recreation");
        Assert(restartMessages is { Count: 2 }, "messages survive state recreation");

        var conversation = new StreamingConversationState();
        conversation.LoadPersistedMessages(restartMessages!);
        Assert(conversation.Messages.Count == 2, "persisted messages project into conversation");
        Assert(conversation.Messages[0].Role == ConversationMessageRole.User && conversation.Messages[1].Role == ConversationMessageRole.Assistant, "persisted roles project correctly");
        Assert(!conversation.IsStreaming && !conversation.HasToolActivities, "resume resets transient runtime state");

        var composer = new ComposerState();
        var conversationSurface = new ConversationSurface { State = conversation, Composer = composer };
        var sessionSurface = new SessionWorkspaceSurface { State = restarted, ConversationContent = conversationSurface };
        var window = new Window { Content = sessionSurface, Width = 1000, Height = 700 };
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

        Assert(sessionSurface.FindName("SessionHistoryItems") is ListBox history && history.Items.Count == 2, "session history rendered");
        Assert(sessionSurface.FindName("NewSessionButton") is Button createButton && createButton.IsEnabled, "new-session action enabled for active project");
        Assert(sessionSurface.FindName("RefreshSessionsButton") is Button, "refresh action rendered");

        var darkBackground = RequireBrush(sessionSurface.Background, "dark session background").Color;
        var themes = new ThemeService(app.Resources);
        themes.Apply(AppearanceTheme.Light);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var lightBackground = RequireBrush(sessionSurface.Background, "light session background").Color;
        Assert(lightBackground != darkBackground, "semantic theme parity");
        themes.Apply(AppearanceTheme.Dark);

        window.Close();
        Console.WriteLine("Runtime session-workspace create/history/resume/restart fixture: PASS.");
    }

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush
        ?? throw new InvalidOperationException($"Expected SolidColorBrush for {label}.");

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Session-workspace assertion failed: {label}");
        }
    }
}
'@
        $program = $programTemplate.Replace('__DATABASE_PATH__', $databaseLiteral)
        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime session-workspace fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
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
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required session-workspace path is missing: $path"
    }
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
    Assert-ContractRejects {
        Assert-SessionContract ($stateText.Replace('session.ProjectId != projectId', 'false')) $streamingText $surfaceText $surfaceCodeText $mainWindowText $mainWindowCodeText
    } 'cross-project resume guard removed'
    Assert-ContractRejects {
        Assert-SessionContract ($stateText.Replace('message.Sequence <= previous', 'false')) $streamingText $surfaceText $surfaceCodeText $mainWindowText $mainWindowCodeText
    } 'persisted sequence guard removed'
    Assert-ContractRejects {
        Assert-SessionContract ($stateText.Replace('RuntimeSessionId = runtimeSessionId.Trim()', 'RuntimeSessionId = null')) $streamingText $surfaceText $surfaceCodeText $mainWindowText $mainWindowCodeText
    } 'runtime-session binding persistence removed'
    Assert-ContractRejects {
        Assert-SessionContract $stateText $streamingText ($surfaceText.Replace('x:Name="SessionHistoryItems"', 'x:Name="RemovedHistoryItems"')) $surfaceCodeText $mainWindowText $mainWindowCodeText
    } 'session history surface removed'
    Assert-ContractRejects {
        Assert-SessionContract $stateText $streamingText ($surfaceText.Replace('{DynamicResource FccBrushSurface}', '#010203')) $surfaceCodeText $mainWindowText $mainWindowCodeText
    } 'hard-coded session surface color'
    Assert-ContractRejects {
        Assert-SessionContract $stateText ($streamingText.Replace('public void LoadPersistedMessages(IReadOnlyList<PersistedMessage> messages)', 'public void RemovedLoadPersistedMessages(IReadOnlyList<PersistedMessage> messages)')) $surfaceText $surfaceCodeText $mainWindowText $mainWindowCodeText
    } 'conversation history projection removed'
    Write-Host 'Deterministic session-workspace negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-SessionRuntimeFixture $appProjectPath $persistenceProjectPath
}
