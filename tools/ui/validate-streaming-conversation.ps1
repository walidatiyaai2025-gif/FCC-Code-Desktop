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

function Assert-StreamingConversationContract {
    param(
        [string]$MainXamlText,
        [string]$MainCodeText,
        [string]$SurfaceXamlText,
        [string]$SurfaceCodeText,
        [string]$StateText
    )

    Assert-ValidXaml $MainXamlText 'MainWindow.xaml'
    Assert-ValidXaml $SurfaceXamlText 'ConversationSurface.xaml'

    foreach ($literal in @(
        'xmlns:conversation="clr-namespace:FCCCodeDesktop.App.Conversation"',
        '<conversation:StreamingConversationState x:Key="StreamingConversationState" />',
        '<conversation:ConversationSurface x:Key="ConversationSurface"',
        'State="{StaticResource StreamingConversationState}"'
    )) {
        Assert-ContainsLiteral $MainXamlText $literal 'MainWindow.xaml'
    }

    foreach ($literal in @(
        'ConfigureConversationSurface();',
        'RequireResource<ConversationSurface>("ConversationSurface")',
        'navigationState.SessionsContent = conversationSurface;'
    )) {
        Assert-ContainsLiteral $MainCodeText $literal 'MainWindow.xaml.cs'
    }

    foreach ($literal in @(
        'x:Name="ConversationItems"',
        'ItemsSource="{Binding Messages}"',
        'x:Name="MessageText"',
        'Text="{Binding Text}"',
        'TextWrapping="Wrap"',
        'Value="{x:Static conversation:ConversationMessageRole.User}"',
        'Text="Streaming"',
        'Text="Sent"',
        'AutomationProperties.Name="Conversation messages"',
        '{DynamicResource FccBrushCanvas}',
        '{DynamicResource FccBrushSurfaceRaised}',
        '{DynamicResource FccBrushSelectionBackground}',
        '{DynamicResource FccBrushAccent}'
    )) {
        Assert-ContainsLiteral $SurfaceXamlText $literal 'ConversationSurface.xaml'
    }

    foreach ($literal in @(
        'DependencyProperty StateProperty',
        'INotifyCollectionChanged',
        'StreamingConversationState.LastRuntimeSequence',
        'ConversationItems.ScrollIntoView',
        'Dispatcher.BeginInvoke'
    )) {
        Assert-ContainsLiteral $SurfaceCodeText $literal 'ConversationSurface.xaml.cs'
    }

    foreach ($literal in @(
        'ConversationMessageRole',
        'ReadOnlyObservableCollection<ConversationMessageState>',
        'AddUserMessage(string text)',
        'ApplyRuntimeEventAsync(AgentRuntimeEvent runtimeEvent',
        'AgentRuntimeEventKind.AssistantTextDelta',
        'AgentRuntimeEventKind.Completion',
        'AssertRuntimeSequence(runtimeEvent.Sequence)',
        'sequence != expectedSequence',
        'Dispatcher.InvokeAsync',
        'DispatcherPriority.DataBind',
        'VerifyAccess();'
    )) {
        Assert-ContainsLiteral $StateText $literal 'StreamingConversationState.cs'
    }

    foreach ($text in @($SurfaceXamlText, $SurfaceCodeText, $StateText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P05-001 contains forbidden placeholder text '$placeholder'."
            }
        }
    }

    if ($SurfaceXamlText -match '#[0-9A-Fa-f]{6,8}') {
        throw 'P05-001 conversation surface must consume semantic theme resources instead of hard-coded colors.'
    }

    foreach ($forbidden in @(
        'PayloadJson',
        'System.Diagnostics.Process',
        'Process.Start',
        'cmd.exe',
        'powershell.exe',
        'fcc-claude --print'
    )) {
        if ($SurfaceXamlText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $SurfaceCodeText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $StateText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
            throw "P05-001 crossed the normalized presentation boundary: $forbidden"
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

    throw "Negative streaming-conversation fixture was not rejected: $Label"
}

function Invoke-StreamingConversationRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime streaming-conversation fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime streaming-conversation fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime streaming-conversation fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-streaming-conversation-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'StreamingConversationFixture.csproj'
        $programPath = Join-Path $fixtureRoot 'Program.cs'
        $projectReference = [Security.SecurityElement]::Escape($AppProjectPath)

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
    <ProjectReference Include="$projectReference" />
  </ItemGroup>
</Project>
"@

        $program = @'
using System.Windows.Media;
using System.Windows.Threading;
using FCCCodeDesktop.App;
using FCCCodeDesktop.App.Conversation;
using FCCCodeDesktop.App.DesignSystem;
using FCCCodeDesktop.App.Shell;
using FCCCodeDesktop.Runtime;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();

        var state = window.Resources["StreamingConversationState"] as StreamingConversationState
            ?? throw new InvalidOperationException("StreamingConversationState production resource is missing.");
        var surface = window.Resources["ConversationSurface"] as ConversationSurface
            ?? throw new InvalidOperationException("ConversationSurface production resource is missing.");
        var navigation = window.Resources["WorkspaceNavigationState"] as WorkspaceNavigationState
            ?? throw new InvalidOperationException("WorkspaceNavigationState production resource is missing.");

        Assert(ReferenceEquals(surface.State, state), "surface/state composition");
        Assert(ReferenceEquals(navigation.SessionsContent, surface), "sessions production composition");
        Assert(!state.HasMessages && state.Messages.Count == 0, "initial empty state");

        var whitespaceRejected = false;
        try
        {
            state.AddUserMessage("   ");
        }
        catch (ArgumentException)
        {
            whitespaceRejected = true;
        }
        Assert(whitespaceRejected, "whitespace-only user message rejection");

        state.AddUserMessage("Explain the runtime contract.");
        Apply(state, Event(0, AgentRuntimeEventKind.RuntimeStatus, "ready"));
        Apply(state, Event(1, AgentRuntimeEventKind.AssistantTextDelta, "Hello "));
        Assert(state.Messages.Count == 2, "assistant bubble created on first delta");
        Assert(state.Messages[0].Role == ConversationMessageRole.User, "user role preserved");
        Assert(state.Messages[1].Role == ConversationMessageRole.Assistant, "assistant role preserved");
        Assert(state.Messages[1].Text == "Hello ", "first delta rendered");
        Assert(state.Messages[1].IsStreaming && state.IsStreaming, "streaming state visible");

        Apply(state, Event(2, AgentRuntimeEventKind.ToolStarted, "must not enter assistant text"));
        Assert(state.Messages[1].Text == "Hello ", "tool activity excluded from assistant text");

        Apply(state, Event(3, AgentRuntimeEventKind.AssistantTextDelta, "world"));
        Assert(state.Messages[1].Text == "Hello world", "ordered delta append");
        Apply(state, Event(4, AgentRuntimeEventKind.Completion));
        Assert(!state.Messages[1].IsStreaming && !state.IsStreaming, "completion closes streaming bubble");

        var duplicateRejected = false;
        try
        {
            Apply(state, Event(4, AgentRuntimeEventKind.AssistantTextDelta, "duplicate"));
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        Assert(duplicateRejected, "duplicate sequence rejection");
        Assert(state.LastRuntimeSequence == 4, "duplicate rejection preserves accepted sequence");
        Assert(state.Messages[1].Text == "Hello world", "duplicate rejection preserves text");

        var gapRejected = false;
        try
        {
            Apply(state, Event(6, AgentRuntimeEventKind.AssistantTextDelta, "gap"));
        }
        catch (InvalidOperationException)
        {
            gapRejected = true;
        }
        Assert(gapRejected, "sequence-gap rejection");
        Assert(state.LastRuntimeSequence == 4, "gap rejection preserves accepted sequence");

        Apply(state, new AgentRuntimeEvent(5, DateTimeOffset.UtcNow, AgentRuntimeEventKind.Unknown, sourceType: "future/event"));
        Assert(state.Messages.Count == 2, "unknown typed event does not leak raw payload into chat");
        Apply(state, Event(6, AgentRuntimeEventKind.AssistantTextDelta, "Second answer"));
        Apply(state, Event(7, AgentRuntimeEventKind.Completion));
        Assert(state.Messages.Count == 3, "new assistant response starts after completion");
        Assert(state.Messages[2].Text == "Second answer", "second assistant response rendered");

        navigation.SelectSection(WorkspaceSection.Sessions);
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert(surface.FindName("ConversationItems") is System.Windows.Controls.ListBox list && list.Items.Count == 3, "runtime message items rendered");

        var darkBackground = RequireBrush(surface.Background, "dark conversation background").Color;
        var themes = new ThemeService(app.Resources);
        themes.Apply(AppearanceTheme.Light);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var lightBackground = RequireBrush(surface.Background, "light conversation background").Color;
        Assert(lightBackground != darkBackground, "dynamic theme parity");
        themes.Apply(AppearanceTheme.Dark);

        state.Reset();
        Assert(!state.HasMessages && !state.IsStreaming && state.LastRuntimeSequence is null, "reset recovery");
        Assert(state.Messages.Count == 0, "reset clears presentation state");

        window.Close();
        Console.WriteLine("Runtime streaming-conversation happy/negative/recovery fixture: PASS.");
    }

    private static AgentRuntimeEvent Event(long sequence, AgentRuntimeEventKind kind, string? text = null) =>
        new(sequence, DateTimeOffset.UtcNow, kind, text: text);

    private static void Apply(StreamingConversationState state, AgentRuntimeEvent runtimeEvent) =>
        state.ApplyRuntimeEventAsync(runtimeEvent).GetAwaiter().GetResult();

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush
        ?? throw new InvalidOperationException($"Expected SolidColorBrush for {label}.");

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Streaming-conversation assertion failed: {label}");
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime streaming-conversation fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$mainXamlPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$mainCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml.cs'
$surfaceXamlPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationSurface.xaml'
$surfaceCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationSurface.xaml.cs'
$statePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\StreamingConversationState.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($mainXamlPath, $mainCodePath, $surfaceXamlPath, $surfaceCodePath, $statePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required streaming-conversation path is missing: $path"
    }
}

$mainXamlText = Get-Content -LiteralPath $mainXamlPath -Raw
$mainCodeText = Get-Content -LiteralPath $mainCodePath -Raw
$surfaceXamlText = Get-Content -LiteralPath $surfaceXamlPath -Raw
$surfaceCodeText = Get-Content -LiteralPath $surfaceCodePath -Raw
$stateText = Get-Content -LiteralPath $statePath -Raw

Assert-StreamingConversationContract $mainXamlText $mainCodeText $surfaceXamlText $surfaceCodeText $stateText
Write-Host 'Static streaming-conversation validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-StreamingConversationContract ($mainXamlText.Replace('<conversation:StreamingConversationState x:Key="StreamingConversationState" />', '')) $mainCodeText $surfaceXamlText $surfaceCodeText $stateText
    } 'missing production streaming state'

    Assert-ContractRejects {
        Assert-StreamingConversationContract $mainXamlText $mainCodeText ($surfaceXamlText.Replace('{DynamicResource FccBrushSurfaceRaised}', '#112233')) $surfaceCodeText $stateText
    } 'hard-coded assistant bubble color'

    Assert-ContractRejects {
        Assert-StreamingConversationContract $mainXamlText $mainCodeText $surfaceXamlText $surfaceCodeText ($stateText.Replace('AgentRuntimeEventKind.AssistantTextDelta', 'AgentRuntimeEventKind.Unknown'))
    } 'typed assistant-delta handling removed'

    Assert-ContractRejects {
        Assert-StreamingConversationContract $mainXamlText $mainCodeText $surfaceXamlText $surfaceCodeText ($stateText.Replace('AssertRuntimeSequence(runtimeEvent.Sequence);', ''))
    } 'runtime ordering guard removed'

    Assert-ContractRejects {
        Assert-StreamingConversationContract $mainXamlText ($mainCodeText.Replace('navigationState.SessionsContent = conversationSurface;', '')) $surfaceXamlText $surfaceCodeText $stateText
    } 'sessions composition removed'

    Assert-StreamingConversationContract $mainXamlText $mainCodeText $surfaceXamlText $surfaceCodeText $stateText
    Write-Host 'Streaming-conversation recovery fixture: PASS.'
    Write-Host 'Deterministic streaming-conversation negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-StreamingConversationRuntimeFixture $appProjectPath
}
