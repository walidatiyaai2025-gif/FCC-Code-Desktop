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

function Assert-ConversationVirtualizationContract {
    param(
        [string]$StateText,
        [string]$SurfaceXamlText,
        [string]$SurfaceCodeText
    )

    Assert-ValidXaml $SurfaceXamlText 'ConversationSurface.xaml'

    foreach ($literal in @(
        'private bool _contentParsed;',
        'bool deferContentParsing = false',
        'if (!isStreaming && !deferContentParsing)',
        '_contentBlocks = ConversationContentParser.Parse(initialText);',
        'if (!IsStreaming && !_contentParsed)',
        '_contentBlocks = ConversationContentParser.Parse(Text);',
        'deferContentParsing: true',
        '_activeAssistantMessage?.Complete();'
    )) {
        Assert-ContainsLiteral $StateText $literal 'StreamingConversationState.cs'
    }

    if ($StateText.Contains('foreach (var message in _messages)', [StringComparison]::Ordinal)) {
        throw 'Reset must not materialize every historical message before clearing the conversation.'
    }

    foreach ($literal in @(
        'VirtualizingPanel.IsVirtualizing="True"',
        'VirtualizingPanel.VirtualizationMode="Recycling"'
    )) {
        Assert-ContainsLiteral $SurfaceXamlText $literal 'ConversationSurface.xaml'
    }

    foreach ($literal in @(
        'TailScrollCoalesceInterval = TimeSpan.FromMilliseconds(50)',
        'private const double TailTolerancePixels = 32d;',
        'ConfigureVirtualization(ConversationItems);',
        'ConfigureVirtualization(ToolTimelineItems);',
        'ScrollViewer.SetCanContentScroll(listBox, true);',
        'VirtualizingPanel.SetIsVirtualizing(listBox, true);',
        'VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);',
        'VirtualizingPanel.SetScrollUnit(listBox, ScrollUnit.Pixel);',
        'VirtualizingPanel.SetCacheLength(listBox, new VirtualizationCacheLength(1d));',
        'VirtualizingPanel.SetCacheLengthUnit(listBox, VirtualizationCacheLengthUnit.Page);',
        'e.ExtentHeightChange != 0d || e.ViewportHeightChange != 0d',
        '_conversationFollowsTail = IsNearTail(e);',
        'if (!_conversationFollowsTail)',
        'ConversationItems.ScrollIntoView(State.Messages[^1]);',
        '_tailScrollTimer.Stop();'
    )) {
        Assert-ContainsLiteral $SurfaceCodeText $literal 'ConversationSurface.xaml.cs'
    }

    foreach ($text in @($StateText, $SurfaceXamlText, $SurfaceCodeText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P05-008 contains forbidden placeholder text '$placeholder'."
            }
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

    throw "Negative P05-008 virtualization fixture was not rejected: $Label"
}

function Invoke-ConversationVirtualizationRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime P05-008 conversation virtualization fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime P05-008 conversation virtualization fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime P05-008 conversation virtualization fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-conversation-virtualization-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'ConversationVirtualizationFixture.csproj'
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
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FCCCodeDesktop.App;
using FCCCodeDesktop.App.Conversation;
using FCCCodeDesktop.Core.State;

internal static class Program
{
    private const int MessageCount = 2000;

    [STAThread]
    private static void Main()
    {
        var app = new App();
        app.InitializeComponent();

        var state = new StreamingConversationState();
        var sessionId = Guid.NewGuid();
        var persisted = Enumerable.Range(1, MessageCount)
            .Select(index => new PersistedMessage(
                Guid.NewGuid(),
                sessionId,
                index,
                index % 2 == 0 ? "assistant" : "user",
                $"# Message {index}\n\nParagraph {index}\n\n```text\nline {index}\n```",
                DateTimeOffset.UtcNow.AddSeconds(index)))
            .ToArray();

        state.LoadPersistedMessages(persisted);
        Assert(state.Messages.Count == MessageCount, "all persisted messages loaded");

        var parsedField = typeof(ConversationMessageState).GetField(
            "_contentParsed",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Conversation content materialization field was not found.");
        Assert(CountParsed(state, parsedField) == 0, "persisted Markdown is deferred before realization");

        var surface = new ConversationSurface { State = state };
        var window = new Window
        {
            Content = surface,
            Width = 1000,
            Height = 700,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.ToolWindow,
        };

        window.Show();
        surface.UpdateLayout();
        Pump(TimeSpan.FromMilliseconds(150));
        surface.UpdateLayout();

        var list = surface.FindName("ConversationItems") as ListBox
            ?? throw new InvalidOperationException("ConversationItems list was not found.");
        Assert(VirtualizingPanel.GetIsVirtualizing(list), "virtualization enabled at runtime");
        Assert(VirtualizingPanel.GetVirtualizationMode(list) == VirtualizationMode.Recycling, "recycling mode enabled");
        Assert(VirtualizingPanel.GetScrollUnit(list) == ScrollUnit.Pixel, "pixel scroll unit enabled");
        Assert(VirtualizingPanel.GetCacheLengthUnit(list) == VirtualizationCacheLengthUnit.Page, "page cache unit enabled");
        Assert(ScrollViewer.GetCanContentScroll(list), "logical content scrolling enabled");

        var realized = CountRealizedContainers(list);
        Assert(realized > 0, "visible conversation containers realized");
        Assert(realized < 500, $"realized container count remains bounded ({realized}/{MessageCount})");

        var parsedAfterRealization = CountParsed(state, parsedField);
        Assert(parsedAfterRealization > 0, "visible messages progressively materialized");
        Assert(parsedAfterRealization < 500, $"historical Markdown parsing remains bounded ({parsedAfterRealization}/{MessageCount})");

        var scrollViewer = FindVisualChild<ScrollViewer>(list)
            ?? throw new InvalidOperationException("Conversation ScrollViewer was not found.");
        scrollViewer.ScrollToTop();
        Pump(TimeSpan.FromMilliseconds(40));
        surface.UpdateLayout();
        Assert(scrollViewer.VerticalOffset <= 2d, "history viewport positioned at top");

        state.AddUserMessage("Message added while reviewing history");
        Pump(TimeSpan.FromMilliseconds(120));
        surface.UpdateLayout();
        Assert(scrollViewer.VerticalOffset <= 2d, "new output does not yank a user away from history");

        scrollViewer.ScrollToEnd();
        Pump(TimeSpan.FromMilliseconds(40));
        surface.UpdateLayout();
        state.AddUserMessage("Message added while following tail");
        Pump(TimeSpan.FromMilliseconds(120));
        surface.UpdateLayout();
        Assert(
            scrollViewer.VerticalOffset >= Math.Max(0d, scrollViewer.ScrollableHeight - 40d),
            "tail-follow resumes when user returns to the bottom");

        Assert(CountRealizedContainers(list) < 500, "virtualized realization remains bounded after tail movement");
        window.Close();
        Console.WriteLine("Runtime P05-008 conversation virtualization/performance fixture: PASS.");
    }

    private static int CountParsed(StreamingConversationState state, FieldInfo parsedField) =>
        state.Messages.Count(message => parsedField.GetValue(message) is true);

    private static int CountRealizedContainers(ListBox list)
    {
        var count = 0;
        for (var index = 0; index < list.Items.Count; index++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is not null)
            {
                count++;
            }
        }

        return count;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(
            duration,
            DispatcherPriority.Background,
            (_, _) => frame.Continue = false,
            Dispatcher.CurrentDispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"P05-008 virtualization assertion failed: {label}");
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime P05-008 conversation virtualization fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$statePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\StreamingConversationState.cs'
$surfaceXamlPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationSurface.xaml'
$surfaceCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationSurface.xaml.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($statePath, $surfaceXamlPath, $surfaceCodePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P05-008 conversation virtualization path is missing: $path"
    }
}

$stateText = Get-Content -LiteralPath $statePath -Raw
$surfaceXamlText = Get-Content -LiteralPath $surfaceXamlPath -Raw
$surfaceCodeText = Get-Content -LiteralPath $surfaceCodePath -Raw

Assert-ConversationVirtualizationContract $stateText $surfaceXamlText $surfaceCodeText
Write-Host 'Static P05-008 conversation virtualization/performance validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-ConversationVirtualizationContract ($stateText.Replace('deferContentParsing: true', 'deferContentParsing: false')) $surfaceXamlText $surfaceCodeText
    } 'persisted progressive parsing removed'

    Assert-ContractRejects {
        Assert-ConversationVirtualizationContract $stateText $surfaceXamlText ($surfaceCodeText.Replace('VirtualizingPanel.SetIsVirtualizing(listBox, true);', 'VirtualizingPanel.SetIsVirtualizing(listBox, false);'))
    } 'runtime virtualization disabled'

    Assert-ContractRejects {
        Assert-ConversationVirtualizationContract $stateText $surfaceXamlText ($surfaceCodeText.Replace('TailScrollCoalesceInterval = TimeSpan.FromMilliseconds(50)', 'TailScrollCoalesceInterval = TimeSpan.Zero'))
    } 'tail-scroll coalescing removed'

    Assert-ContractRejects {
        Assert-ConversationVirtualizationContract $stateText $surfaceXamlText ($surfaceCodeText.Replace('if (!_conversationFollowsTail)', 'if (false)'))
    } 'history viewport preservation removed'

    Write-Host 'P05-008 deterministic negative fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-ConversationVirtualizationRuntimeFixture -AppProjectPath $appProjectPath
}
