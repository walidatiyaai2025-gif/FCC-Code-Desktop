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

function Assert-ContentRenderingContract {
    param(
        [string]$ParserText,
        [string]$StateText,
        [string]$SurfaceText
    )

    Assert-ValidXaml $SurfaceText 'ConversationSurface.xaml'

    foreach ($literal in @(
        'public enum ConversationContentBlockKind',
        'public sealed record ConversationContentBlock',
        'MaxRenderedSourceCharacters = 1024 * 1024',
        'MaxLanguageIdentifierLength = 32',
        'TryParseHeading',
        'TryParseBullet',
        'ClassifyDiffLine',
        'string.Equals(language, "diff"',
        'string.Equals(language, "patch"',
        'ReadOnlyCollection<ConversationContentBlock>',
        'Rendering limited to the first'
    )) {
        Assert-ContainsLiteral $ParserText $literal 'ConversationContentParser.cs'
    }

    foreach ($literal in @(
        'private IReadOnlyList<ConversationContentBlock> _contentBlocks',
        'public IReadOnlyList<ConversationContentBlock> ContentBlocks',
        '_contentBlocks = ConversationContentParser.Parse(initialText)',
        '_contentBlocks = ConversationContentParser.Parse(Text)',
        'OnPropertyChanged(nameof(ContentBlocks))',
        '_text.Append(delta)',
        'OnPropertyChanged(nameof(Text))'
    )) {
        Assert-ContainsLiteral $StateText $literal 'StreamingConversationState.cs'
    }

    $appendDeltaStart = $StateText.IndexOf('internal void AppendDelta', [StringComparison]::Ordinal)
    $completeStart = $StateText.IndexOf('internal void Complete', [StringComparison]::Ordinal)
    if ($appendDeltaStart -lt 0 -or $completeStart -le $appendDeltaStart) {
        throw 'StreamingConversationState.cs method boundaries for AppendDelta/Complete were not found.'
    }
    $appendDeltaBody = $StateText.Substring($appendDeltaStart, $completeStart - $appendDeltaStart)
    if ($appendDeltaBody.Contains('ConversationContentParser.Parse', [StringComparison]::Ordinal)) {
        throw 'Streaming deltas must not reparse Markdown/code/diff content per token.'
    }

    foreach ($literal in @(
        'ItemsSource="{Binding ContentBlocks}"',
        'x:Name="RenderedContent"',
        'x:Name="MessageText"',
        'Visibility="Collapsed"',
        'conversation:ConversationContentBlockKind.Heading',
        'conversation:ConversationContentBlockKind.Code',
        'conversation:ConversationContentBlockKind.DiffHeader',
        'conversation:ConversationContentBlockKind.DiffAdded',
        'conversation:ConversationContentBlockKind.DiffRemoved',
        'conversation:ConversationContentBlockKind.DiffContext',
        'Style="{StaticResource FccTextCode}"',
        'TextWrapping="NoWrap"',
        'HorizontalScrollBarVisibility="Auto"',
        '<DataTrigger Binding="{Binding IsStreaming}" Value="True">',
        '<Setter TargetName="MessageText" Property="Visibility" Value="Visible" />',
        '<Setter TargetName="RenderedContent" Property="Visibility" Value="Collapsed" />',
        '{DynamicResource FccBrushSurface}',
        '{DynamicResource FccBrushBorder}',
        '{DynamicResource FccBrushAccent}',
        '{DynamicResource FccBrushError}'
    )) {
        Assert-ContainsLiteral $SurfaceText $literal 'ConversationSurface.xaml'
    }

    foreach ($text in @($ParserText, $StateText, $SurfaceText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P05-007 contains forbidden placeholder text '$placeholder'."
            }
        }
    }

    if ($SurfaceText -match '#[0-9A-Fa-f]{6,8}') {
        throw 'P05-007 rendering must consume semantic theme resources instead of hard-coded colors.'
    }

    foreach ($forbidden in @(
        '<WebBrowser',
        '<WebView',
        'NavigateToString',
        'HtmlDocument',
        'System.Diagnostics.Process',
        'Process.Start',
        'PayloadJson'
    )) {
        if ($ParserText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $StateText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $SurfaceText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
            throw "P05-007 crossed the safe native-rendering boundary: $forbidden"
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

    throw "Negative content-rendering fixture was not rejected: $Label"
}

function Invoke-ContentRenderingRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime P05-007 content-rendering fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime P05-007 content-rendering fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime P05-007 content-rendering fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-content-rendering-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'ContentRenderingFixture.csproj'
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
using System.Windows.Threading;
using FCCCodeDesktop.App;
using FCCCodeDesktop.App.Conversation;
using FCCCodeDesktop.Core.State;
using FCCCodeDesktop.Runtime;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AssertParserContract();
        AssertStreamingContract();
        AssertPersistedContract();
        AssertProductionSurface();
        Console.WriteLine("Runtime P05-007 Markdown/code/diff rendering fixture: PASS.");
    }

    private static void AssertParserContract()
    {
        const string markdown = "# Heading\n\nParagraph line\n\n- item\n\n```csharp\nConsole.WriteLine(42);\n```\n\n```diff\n--- a/file.txt\n+++ b/file.txt\n@@ -1 +1 @@\n-old\n+new\n same\n```";
        var blocks = ConversationContentParser.Parse(markdown);
        Assert(blocks.Any(x => x.Kind == ConversationContentBlockKind.Heading && x.Text == "Heading"), "heading block");
        Assert(blocks.Any(x => x.Kind == ConversationContentBlockKind.Paragraph && x.Text == "Paragraph line"), "paragraph block");
        Assert(blocks.Any(x => x.Kind == ConversationContentBlockKind.Bullet && x.Text == "• item"), "bullet block");
        Assert(blocks.Any(x => x.Kind == ConversationContentBlockKind.Code && x.Language == "csharp" && x.Text.Contains("Console.WriteLine", StringComparison.Ordinal)), "code fence");
        Assert(blocks.Any(x => x.Kind == ConversationContentBlockKind.DiffHeader && x.Text == "--- a/file.txt"), "diff old-file header classification");
        Assert(blocks.Any(x => x.Kind == ConversationContentBlockKind.DiffHeader && x.Text == "+++ b/file.txt"), "diff new-file header classification");
        Assert(blocks.Any(x => x.Kind == ConversationContentBlockKind.DiffAdded && x.Text == "+new"), "diff added line");
        Assert(blocks.Any(x => x.Kind == ConversationContentBlockKind.DiffRemoved && x.Text == "-old"), "diff removed line");
        Assert(blocks.Any(x => x.Kind == ConversationContentBlockKind.DiffContext && x.Text == " same"), "diff context line");

        var unclosed = ConversationContentParser.Parse("```text\nunclosed");
        Assert(unclosed.Count == 1 && unclosed[0].Kind == ConversationContentBlockKind.Code && unclosed[0].Text == "unclosed", "unclosed fence remains safe code");

        var longLanguage = new string('x', ConversationContentParser.MaxLanguageIdentifierLength + 10);
        var languageBlocks = ConversationContentParser.Parse($"```{longLanguage}\ncode\n```");
        Assert(languageBlocks.Single().Language?.Length == ConversationContentParser.MaxLanguageIdentifierLength, "language identifier bounded");

        var oversized = new string('a', ConversationContentParser.MaxRenderedSourceCharacters + 4096);
        var oversizedBlocks = ConversationContentParser.Parse(oversized);
        Assert(oversizedBlocks.Last().Text.Contains("Rendering limited to the first", StringComparison.Ordinal), "oversized rendering notice");
    }

    private static void AssertStreamingContract()
    {
        var state = new StreamingConversationState();
        Apply(state, Event(0, AgentRuntimeEventKind.AssistantTextDelta, "# Head"));
        Assert(state.Messages.Count == 1, "streaming assistant created");
        var message = state.Messages[0];
        Assert(message.IsStreaming, "assistant remains streaming");
        Assert(message.Text == "# Head", "streaming raw text exact");
        Assert(message.ContentBlocks.Count == 0, "streaming text is not reparsed per delta");

        Apply(state, Event(1, AgentRuntimeEventKind.AssistantTextDelta, "ing\n\n```diff\n-old\n+new\n```"));
        Assert(message.ContentBlocks.Count == 0, "second streaming delta remains unparsed");
        Apply(state, Event(2, AgentRuntimeEventKind.Completion));
        Assert(!message.IsStreaming, "completion ends streaming");
        Assert(message.ContentBlocks.Any(x => x.Kind == ConversationContentBlockKind.Heading && x.Text == "Heading"), "completed heading parsed");
        Assert(message.ContentBlocks.Any(x => x.Kind == ConversationContentBlockKind.DiffRemoved), "completed diff removal parsed");
        Assert(message.ContentBlocks.Any(x => x.Kind == ConversationContentBlockKind.DiffAdded), "completed diff addition parsed");
        Assert(message.Text == "# Heading\n\n```diff\n-old\n+new\n```", "durable/raw message text unchanged by rendering");
    }

    private static void AssertPersistedContract()
    {
        var state = new StreamingConversationState();
        var persisted = new[]
        {
            new PersistedMessage(Guid.NewGuid(), Guid.NewGuid(), 1, "assistant", "# Persisted\n\n```text\nbody\n```", DateTimeOffset.UtcNow)
        };
        state.LoadPersistedMessages(persisted);
        Assert(state.Messages.Count == 1, "persisted message loaded");
        Assert(!state.Messages[0].IsStreaming, "persisted message completed");
        Assert(state.Messages[0].ContentBlocks.Any(x => x.Kind == ConversationContentBlockKind.Heading), "persisted Markdown parsed");
        Assert(state.Messages[0].ContentBlocks.Any(x => x.Kind == ConversationContentBlockKind.Code), "persisted code parsed");
    }

    private static void AssertProductionSurface()
    {
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        var state = window.Resources["StreamingConversationState"] as StreamingConversationState
            ?? throw new InvalidOperationException("StreamingConversationState production resource is missing.");
        var surface = window.Resources["ConversationSurface"] as ConversationSurface
            ?? throw new InvalidOperationException("ConversationSurface production resource is missing.");
        Assert(ReferenceEquals(surface.State, state), "production surface/state composition");

        state.AddUserMessage("# UI heading\n\n```csharp\nvar x = 1;\n```");
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert(surface.FindName("ConversationItems") is ListBox list && list.Items.Count == 1, "production conversation item rendered");
        Assert(state.Messages[0].ContentBlocks.Count >= 2, "production completed content blocks available");
        window.Close();
    }

    private static AgentRuntimeEvent Event(long sequence, AgentRuntimeEventKind kind, string? text = null) =>
        new(sequence, DateTimeOffset.UtcNow, kind, text: text);

    private static void Apply(StreamingConversationState state, AgentRuntimeEvent runtimeEvent) =>
        state.ApplyRuntimeEventAsync(runtimeEvent).GetAwaiter().GetResult();

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"P05-007 rendering assertion failed: {label}");
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime P05-007 content-rendering fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$parserPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationContentParser.cs'
$statePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\StreamingConversationState.cs'
$surfacePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationSurface.xaml'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($parserPath, $statePath, $surfacePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P05-007 content-rendering path is missing: $path"
    }
}

$parserText = Get-Content -LiteralPath $parserPath -Raw
$stateText = Get-Content -LiteralPath $statePath -Raw
$surfaceText = Get-Content -LiteralPath $surfacePath -Raw

Assert-ContentRenderingContract $parserText $stateText $surfaceText
Write-Host 'Static P05-007 Markdown/code/diff rendering validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-ContentRenderingContract $parserText $stateText ($surfaceText.Replace('ItemsSource="{Binding ContentBlocks}"', 'ItemsSource="{Binding Text}"'))
    } 'structured content binding removed'

    Assert-ContractRejects {
        Assert-ContentRenderingContract $parserText ($stateText.Replace('_contentBlocks = ConversationContentParser.Parse(Text);', '_contentBlocks = Array.Empty<ConversationContentBlock>();')) $surfaceText
    } 'completion parsing removed'

    Assert-ContractRejects {
        Assert-ContentRenderingContract $parserText $stateText ($surfaceText.Replace('<Setter TargetName="RenderedContent" Property="Visibility" Value="Collapsed" />', ''))
    } 'streaming structured-content suppression removed'

    Assert-ContractRejects {
        Assert-ContentRenderingContract ($parserText.Replace('ClassifyDiffLine', 'RemovedDiffClassifier')) $stateText $surfaceText
    } 'diff classification removed'

    Assert-ContractRejects {
        Assert-ContentRenderingContract ($parserText.Replace('MaxRenderedSourceCharacters = 1024 * 1024', 'MaxRenderedSourceCharacters = int.MaxValue')) $stateText $surfaceText
    } 'rendering bound removed'

    Assert-ContractRejects {
        Assert-ContentRenderingContract $parserText $stateText ($surfaceText.Replace('{DynamicResource FccBrushSurface}', '#112233'))
    } 'hard-coded rendering color'

    Write-Host 'P05-007 deterministic negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-ContentRenderingRuntimeFixture -AppProjectPath $appProjectPath
}