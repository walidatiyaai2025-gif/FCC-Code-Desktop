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

function Assert-ToolTimelineContract {
    param(
        [string]$SurfaceXamlText,
        [string]$SurfaceCodeText,
        [string]$StateText
    )

    Assert-ValidXaml $SurfaceXamlText 'ConversationSurface.xaml'

    foreach ($literal in @(
        'x:Name="ToolTimelinePanel"',
        'AutomationProperties.Name="Tool activity timeline"',
        'x:Name="ToolTimelineItems"',
        'ItemsSource="{Binding ToolActivities}"',
        'DataTemplate DataType="{x:Type conversation:ToolActivityState}"',
        'Text="{Binding ToolName}"',
        'Text="{Binding StatusLabel}"',
        'Text="{Binding ProgressText}"',
        'Text="{Binding ResultText}"',
        'Binding HasToolActivities',
        'Binding HasProgress',
        'Binding HasResult',
        'conversation:ToolActivityStatus.ResultReceived',
        '{DynamicResource FccBrushSurface}',
        '{DynamicResource FccBrushSurfaceRaised}',
        '{DynamicResource FccBrushBorder}',
        '{DynamicResource FccBrushAccent}'
    )) {
        Assert-ContainsLiteral $SurfaceXamlText $literal 'ConversationSurface.xaml'
    }

    foreach ($literal in @(
        'oldState.ToolActivities',
        'newState.ToolActivities',
        'StreamingConversationState.HasToolActivities',
        'ToolTimelineItems.ScrollIntoView(State.ToolActivities[^1])'
    )) {
        Assert-ContainsLiteral $SurfaceCodeText $literal 'ConversationSurface.xaml.cs'
    }

    foreach ($literal in @(
        'public enum ToolActivityStatus',
        'public sealed class ToolActivityState',
        'ReadOnlyObservableCollection<ToolActivityState> ToolActivities',
        'Dictionary<string, ToolActivityState> _activeToolsByCorrelation',
        'AgentRuntimeEventKind.ToolStarted',
        'AgentRuntimeEventKind.ToolProgress',
        'AgentRuntimeEventKind.ToolResult',
        'RecordToolStarted(runtimeEvent)',
        'RecordToolProgress(runtimeEvent)',
        'RecordToolResult(runtimeEvent)',
        'ResolveToolActivity(runtimeEvent.CorrelationId)',
        'ReferenceEquals(activeActivity, activity)',
        'ToolActivityStatus.ResultReceived',
        'OnPropertyChanged(nameof(HasToolActivities))',
        '_toolActivities.Clear()',
        '_activeToolsByCorrelation.Clear()'
    )) {
        Assert-ContainsLiteral $StateText $literal 'StreamingConversationState.cs'
    }

    foreach ($text in @($SurfaceXamlText, $SurfaceCodeText, $StateText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P05-002 contains forbidden placeholder text '$placeholder'."
            }
        }
    }

    if ($SurfaceXamlText -match '#[0-9A-Fa-f]{6,8}') {
        throw 'P05-002 tool timeline must consume semantic theme resources instead of hard-coded colors.'
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
            throw "P05-002 crossed the normalized presentation boundary: $forbidden"
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

    throw "Negative tool-activity timeline fixture was not rejected: $Label"
}

function Invoke-ToolTimelineRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime tool-activity timeline fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime tool-activity timeline fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime tool-activity timeline fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-tool-timeline-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'ToolTimelineFixture.csproj'
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
using System.Windows;
using System.Windows.Controls;
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
        Assert(!state.HasToolActivities && state.ToolActivities.Count == 0, "initial timeline empty state");

        Apply(state, Event(0, AgentRuntimeEventKind.RuntimeStatus, "ready"));
        Apply(state, Event(1, AgentRuntimeEventKind.ToolStarted, "ReadFile", "tool-1"));
        Assert(state.HasToolActivities && state.ToolActivities.Count == 1, "tool start creates one activity");
        var first = state.ToolActivities[0];
        Assert(first.ToolName == "ReadFile", "tool name preserved");
        Assert(first.CorrelationId == "tool-1", "tool correlation preserved");
        Assert(first.Status == ToolActivityStatus.Running, "started tool is running");

        Apply(state, Event(2, AgentRuntimeEventKind.ToolProgress, "Reading source", "tool-1"));
        Assert(state.ToolActivities.Count == 1, "correlated progress updates existing activity");
        Assert(ReferenceEquals(first, state.ToolActivities[0]), "progress retains activity identity");
        Assert(first.HasProgress && first.ProgressText == "Reading source", "progress text projected");

        Apply(state, Event(3, AgentRuntimeEventKind.AssistantTextDelta, "Working..."));
        Assert(state.Messages.Count == 1 && state.Messages[0].Text == "Working...", "assistant stream remains independent");
        Assert(state.ToolActivities.Count == 1, "assistant text does not duplicate tool rows");

        Apply(state, Event(4, AgentRuntimeEventKind.ToolResult, "42 lines", "tool-1"));
        Assert(state.ToolActivities.Count == 1, "correlated result updates existing activity");
        Assert(first.Status == ToolActivityStatus.ResultReceived, "result state is neutral result-received status");
        Assert(first.HasResult && first.ResultText == "42 lines", "result text projected");

        Apply(state, Event(5, AgentRuntimeEventKind.ToolProgress, "orphan progress", "orphan-progress"));
        Assert(state.ToolActivities.Count == 2, "unmatched progress remains visible");
        Assert(state.ToolActivities[1].ToolName == "Tool activity", "unmatched progress has safe generic label");
        Assert(state.ToolActivities[1].ProgressText == "orphan progress", "unmatched progress text preserved");

        Apply(state, Event(6, AgentRuntimeEventKind.ToolResult, "orphan result", "orphan-result"));
        Assert(state.ToolActivities.Count == 3, "unmatched result remains visible");
        Assert(state.ToolActivities[2].ToolName == "Tool result", "unmatched result has safe generic label");
        Assert(state.ToolActivities[2].Status == ToolActivityStatus.ResultReceived, "unmatched result is terminal for its row");

        Apply(state, Event(7, AgentRuntimeEventKind.ToolStarted, "WriteFile", "tool-1"));
        Apply(state, Event(8, AgentRuntimeEventKind.ToolResult, "saved", "tool-1"));
        Assert(state.ToolActivities.Count == 4, "reused correlation creates a new start row");
        Assert(state.ToolActivities[3].ToolName == "WriteFile", "latest correlation maps to latest tool");
        Assert(state.ToolActivities[3].ResultText == "saved", "latest correlated result updates latest row");
        Assert(first.ResultText == "42 lines", "prior completed activity remains immutable by correlation reuse");

        Apply(state, new AgentRuntimeEvent(
            9,
            DateTimeOffset.UtcNow,
            AgentRuntimeEventKind.Unknown,
            sourceType: "future/tool-event",
            payloadJson: "{\"secret\":\"must-not-render\"}"));
        Assert(state.ToolActivities.Count == 4, "unknown event does not render raw payload");
        Apply(state, Event(10, AgentRuntimeEventKind.Completion));

        navigation.SelectSection(WorkspaceSection.Sessions);
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

        Assert(surface.FindName("ToolTimelineItems") is ListBox list && list.Items.Count == 4, "runtime tool rows rendered");
        Assert(surface.FindName("ToolTimelinePanel") is Border panel && panel.Visibility == Visibility.Visible, "timeline panel visible");

        var darkBackground = RequireBrush(((Border)surface.FindName("ToolTimelinePanel")!).Background, "dark timeline background").Color;
        var themes = new ThemeService(app.Resources);
        themes.Apply(AppearanceTheme.Light);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var lightBackground = RequireBrush(((Border)surface.FindName("ToolTimelinePanel")!).Background, "light timeline background").Color;
        Assert(lightBackground != darkBackground, "dynamic theme parity");
        themes.Apply(AppearanceTheme.Dark);

        state.Reset();
        Assert(!state.HasToolActivities && state.ToolActivities.Count == 0, "reset clears tool timeline");
        Assert(!state.HasMessages && state.LastRuntimeSequence is null, "reset preserves conversation recovery contract");

        window.Close();
        Console.WriteLine("Runtime tool-activity timeline happy/negative/recovery fixture: PASS.");
    }

    private static AgentRuntimeEvent Event(
        long sequence,
        AgentRuntimeEventKind kind,
        string? text = null,
        string? correlationId = null) =>
        new(sequence, DateTimeOffset.UtcNow, kind, text: text, correlationId: correlationId);

    private static void Apply(StreamingConversationState state, AgentRuntimeEvent runtimeEvent) =>
        state.ApplyRuntimeEventAsync(runtimeEvent).GetAwaiter().GetResult();

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush
        ?? throw new InvalidOperationException($"Expected SolidColorBrush for {label}.");

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Tool-activity timeline assertion failed: {label}");
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime tool-activity timeline fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$surfaceXamlPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationSurface.xaml'
$surfaceCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationSurface.xaml.cs'
$statePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\StreamingConversationState.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($surfaceXamlPath, $surfaceCodePath, $statePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required tool-activity timeline path is missing: $path"
    }
}

$surfaceXamlText = Get-Content -LiteralPath $surfaceXamlPath -Raw
$surfaceCodeText = Get-Content -LiteralPath $surfaceCodePath -Raw
$stateText = Get-Content -LiteralPath $statePath -Raw

Assert-ToolTimelineContract $surfaceXamlText $surfaceCodeText $stateText
Write-Host 'Static tool-activity timeline validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-ToolTimelineContract ($surfaceXamlText.Replace('x:Name="ToolTimelinePanel"', 'x:Name="RemovedTimelinePanel"')) $surfaceCodeText $stateText
    } 'missing production timeline panel'

    Assert-ContractRejects {
        Assert-ToolTimelineContract $surfaceXamlText $surfaceCodeText ($stateText.Replace('AgentRuntimeEventKind.ToolStarted', 'AgentRuntimeEventKind.Unknown'))
    } 'typed tool-start handling removed'

    Assert-ContractRejects {
        Assert-ToolTimelineContract $surfaceXamlText $surfaceCodeText ($stateText.Replace('AgentRuntimeEventKind.ToolResult', 'AgentRuntimeEventKind.Unknown'))
    } 'typed tool-result handling removed'

    Assert-ContractRejects {
        Assert-ToolTimelineContract $surfaceXamlText $surfaceCodeText ($stateText.Replace('ResolveToolActivity(runtimeEvent.CorrelationId)', 'null'))
    } 'correlation matching removed'

    Assert-ContractRejects {
        Assert-ToolTimelineContract ($surfaceXamlText.Replace('{DynamicResource FccBrushSurfaceRaised}', '#112233')) $surfaceCodeText $stateText
    } 'hard-coded timeline color'

    Assert-ContractRejects {
        Assert-ToolTimelineContract $surfaceXamlText ($surfaceCodeText.Replace('ToolTimelineItems.ScrollIntoView(State.ToolActivities[^1]);', '')) $stateText
    } 'latest tool activity scrolling removed'

    Assert-ToolTimelineContract $surfaceXamlText $surfaceCodeText $stateText
    Write-Host 'Tool-activity timeline recovery fixture: PASS.'
    Write-Host 'Deterministic tool-activity timeline negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-ToolTimelineRuntimeFixture $appProjectPath
}
