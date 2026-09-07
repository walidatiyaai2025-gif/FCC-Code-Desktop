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

function Assert-InteractiveTerminalContract {
    param(
        [string]$MainXaml,
        [string]$MainCode,
        [string]$TerminalXaml,
        [string]$TerminalCode
    )

    Assert-ValidXaml $MainXaml 'MainWindow.xaml'
    Assert-ValidXaml $TerminalXaml 'InteractiveTerminalSurface.xaml'

    foreach ($literal in @(
        'xmlns:terminal="clr-namespace:FCCCodeDesktop.App.Terminal"',
        '<terminal:InteractiveTerminalSurface x:Key="InteractiveTerminalSurface" />',
        '<chrome:BottomToolPanelState x:Key="BottomToolPanelState" />'
    )) { Assert-ContainsLiteral $MainXaml $literal 'MainWindow.xaml' }

    foreach ($literal in @(
        'RequireResource<BottomToolPanelState>("BottomToolPanelState")',
        'RequireResource<InteractiveTerminalSurface>("InteractiveTerminalSurface")',
        'bottomToolPanelState.TerminalContent = terminalSurface',
        '_terminalShutdownStarted',
        '_terminalShutdownCompleted',
        'e.Cancel = true',
        'ShutdownTerminalAndCloseAsync()',
        'await terminalSurface.DisposeAsync()',
        '_projectWorkspaceSurface?.EditorWorkspace.Dispose()'
    )) { Assert-ContainsLiteral $MainCode $literal 'MainWindow.xaml.cs' }

    foreach ($literal in @(
        'x:Name="ShellSelector"',
        'AutomationProperties.Name="Terminal shell profile"',
        'x:Name="StartButton"',
        'AutomationProperties.Name="Start terminal session"',
        'x:Name="CloseButton"',
        'AutomationProperties.Name="Close terminal session"',
        '<RichTextBox x:Name="TerminalOutput"',
        'PreviewKeyDown="OnPreviewKeyDown"',
        'PreviewTextInput="OnPreviewTextInput"',
        'SizeChanged="OnSurfaceSizeChanged"'
    )) { Assert-ContainsLiteral $TerminalXaml $literal 'InteractiveTerminalSurface.xaml' }

    foreach ($literal in @(
        'MaximumTranscriptCharacters = 250_000',
        'MaximumPendingOutputCharacters = 65_536',
        'WindowsConPtyTerminalHost()',
        'WindowsOptionalShellDetector()',
        '_terminalHost.StartAsync(request, cancellation.Token)',
        'Encoding.UTF8',
        'QueueTerminalOutput(new string(characters, 0, charactersUsed))',
        'Interlocked.CompareExchange(ref _outputFlushScheduled, 1, 0)',
        'Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(FlushPendingOutput))',
        'AppendAnsiText(chunk)',
        'ApplySgr(input.AsSpan',
        'Color.FromRgb(',
        'StandardAnsiColor(',
        'Ansi256Color(',
        'TrimTranscript()',
        '_renderedRuns.RemoveFirst()',
        'TerminalOutput.Selection.Text',
        'await SendInputAsync("\u0003")',
        'Clipboard.ContainsText()',
        'Key.Left => "\u001b[D"',
        'Key.Right => "\u001b[C"',
        'Key.Up => "\u001b[A"',
        'Key.Down => "\u001b[B"',
        'Task.Delay(75, cancellationToken)',
        'await session.ResizeAsync(size, cancellationToken)',
        'CancelPendingResize()',
        'await session.DisposeAsync()',
        'await outputPump.ConfigureAwait(true)',
        'public async ValueTask DisposeAsync()'
    )) { Assert-ContainsLiteral $TerminalCode $literal 'InteractiveTerminalSurface.xaml.cs' }

    foreach ($forbidden in @(
        'AnsiEscape.Replace(text, string.Empty)',
        'TerminalOutput.Text =',
        'Process.Start(',
        'cmd.exe /c',
        'powershell.exe -Command'
    )) {
        if ($TerminalCode.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Interactive terminal contains forbidden implementation: $forbidden"
        }
    }

    foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
        if ($TerminalXaml.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $TerminalCode.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Interactive terminal contains forbidden placeholder text '$placeholder'."
        }
    }
}

function Assert-Rejected {
    param([scriptblock]$Action, [string]$Label)
    try { & $Action }
    catch {
        Write-Host "Negative fixture rejected as expected: $Label"
        return
    }
    throw "Negative P08-007 fixture was not rejected: $Label"
}

function Invoke-RuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) { throw 'P08-007 runtime fixture requires Windows/WPF.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet is required for P08-007 runtime fixture.' }
    $sdk = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdk -ne '10.0.400') {
        throw "P08-007 runtime fixture requires .NET SDK 10.0.400 but resolved '$sdk'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-p08-007-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)
    try {
        $projectPath = Join-Path $fixtureRoot 'InteractiveTerminalFixture.csproj'
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
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using FCCCodeDesktop.App;
using FCCCodeDesktop.App.Shell;
using FCCCodeDesktop.App.Terminal;

internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        var terminal = window.Resources["InteractiveTerminalSurface"] as InteractiveTerminalSurface
            ?? throw new InvalidOperationException("InteractiveTerminalSurface resource was not created.");
        var panelState = window.Resources["BottomToolPanelState"] as BottomToolPanelState
            ?? throw new InvalidOperationException("BottomToolPanelState resource was not created.");
        Assert(ReferenceEquals(panelState.TerminalContent, terminal), "terminal content composition");

        var shellSelector = terminal.FindName("ShellSelector") as ComboBox
            ?? throw new InvalidOperationException("ShellSelector was not created.");
        var start = terminal.FindName("StartButton") as Button
            ?? throw new InvalidOperationException("StartButton was not created.");
        var close = terminal.FindName("CloseButton") as Button
            ?? throw new InvalidOperationException("CloseButton was not created.");
        var output = terminal.FindName("TerminalOutput") as RichTextBox
            ?? throw new InvalidOperationException("TerminalOutput RichTextBox was not created.");
        Assert(shellSelector is not null, "shell selector");
        Assert(AutomationProperties.GetName(start) == "Start terminal session", "start accessibility name");
        Assert(AutomationProperties.GetName(close) == "Close terminal session", "close accessibility name");
        Assert(AutomationProperties.GetName(output) == "Terminal output and input surface", "output accessibility name");

        var appendAnsi = typeof(InteractiveTerminalSurface).GetMethod("AppendAnsiText", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AppendAnsiText was not found.");
        appendAnsi.Invoke(terminal, new object[] { "plain \u001b[31mred\u001b[0m normal" });
        var renderedText = new TextRange(output.Document.ContentStart, output.Document.ContentEnd).Text;
        Assert(renderedText.Contains("plain", StringComparison.Ordinal) && renderedText.Contains("red", StringComparison.Ordinal), "ANSI text retained");
        var runs = output.Document.Blocks.OfType<Paragraph>().SelectMany(paragraph => paragraph.Inlines.OfType<Run>()).ToList();
        Assert(runs.Any(run => run.Foreground is SolidColorBrush brush && brush.Color.R > brush.Color.G), "ANSI foreground color rendered");

        var queueOutput = typeof(InteractiveTerminalSurface).GetMethod("QueueTerminalOutput", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("QueueTerminalOutput was not found.");
        for (var index = 0; index < 500; index++)
        {
            queueOutput.Invoke(terminal, new object[] { new string('x', 4096) });
        }

        var scheduledField = typeof(InteractiveTerminalSurface).GetField("_outputFlushScheduled", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_outputFlushScheduled was not found.");
        Assert((int)(scheduledField.GetValue(terminal) ?? 0) == 1, "high-output dispatcher work coalesced");
        await output.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        renderedText = new TextRange(output.Document.ContentStart, output.Document.ContentEnd).Text;
        Assert(renderedText.Length <= 250_256, "bounded high-output transcript");

        await terminal.DisposeAsync();
        await terminal.DisposeAsync();
        Console.WriteLine("P08-007 interactive terminal ANSI/high-output/lifecycle runtime fixture: PASS.");
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"P08-007 assertion failed: {label}");
    }
}
'@
        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM
        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) { throw "P08-007 runtime fixture failed with exit code $LASTEXITCODE." }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$mainXamlPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$mainCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml.cs'
$terminalXamlPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Terminal\InteractiveTerminalSurface.xaml'
$terminalCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Terminal\InteractiveTerminalSurface.xaml.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'
foreach ($path in @($mainXamlPath, $mainCodePath, $terminalXamlPath, $terminalCodePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required P08-007 path is missing: $path" }
}

$mainXaml = Get-Content -LiteralPath $mainXamlPath -Raw
$mainCode = Get-Content -LiteralPath $mainCodePath -Raw
$terminalXaml = Get-Content -LiteralPath $terminalXamlPath -Raw
$terminalCode = Get-Content -LiteralPath $terminalCodePath -Raw
Assert-InteractiveTerminalContract $mainXaml $mainCode $terminalXaml $terminalCode
Write-Host 'Static P08-007 interactive terminal UX validation: PASS.'

if ($RunFixtures) {
    Assert-Rejected { Assert-InteractiveTerminalContract $mainXaml ($mainCode.Replace('bottomToolPanelState.TerminalContent = terminalSurface', '')) $terminalXaml $terminalCode } 'terminal composition removed'
    Assert-Rejected { Assert-InteractiveTerminalContract $mainXaml ($mainCode.Replace('await terminalSurface.DisposeAsync()', 'await Task.CompletedTask')) $terminalXaml $terminalCode } 'window-close terminal disposal removed'
    Assert-Rejected { Assert-InteractiveTerminalContract $mainXaml $mainCode ($terminalXaml.Replace('<RichTextBox x:Name="TerminalOutput"', '<TextBox x:Name="TerminalOutput"')) $terminalCode } 'rich ANSI surface removed'
    Assert-Rejected { Assert-InteractiveTerminalContract $mainXaml $mainCode $terminalXaml ($terminalCode.Replace('MaximumTranscriptCharacters = 250_000', 'MaximumTranscriptCharacters = int.MaxValue')) } 'transcript bound removed'
    Assert-Rejected { Assert-InteractiveTerminalContract $mainXaml $mainCode $terminalXaml ($terminalCode.Replace('MaximumPendingOutputCharacters = 65_536', 'MaximumPendingOutputCharacters = int.MaxValue')) } 'pending-output bound removed'
    Assert-Rejected { Assert-InteractiveTerminalContract $mainXaml $mainCode $terminalXaml ($terminalCode.Replace('Interlocked.CompareExchange(ref _outputFlushScheduled, 1, 0)', '0')) } 'dispatcher coalescing removed'
    Assert-Rejected { Assert-InteractiveTerminalContract $mainXaml $mainCode $terminalXaml ($terminalCode.Replace('ApplySgr(input.AsSpan', 'IgnoreSgr(input.AsSpan')) } 'ANSI rendering removed'
    Assert-Rejected { Assert-InteractiveTerminalContract $mainXaml $mainCode $terminalXaml ($terminalCode.Replace('await SendInputAsync("\u0003")', 'await SendInputAsync(string.Empty)')) } 'Ctrl+C interrupt removed'
    Assert-Rejected { Assert-InteractiveTerminalContract $mainXaml $mainCode $terminalXaml ($terminalCode.Replace('await session.ResizeAsync(size, cancellationToken)', 'await Task.CompletedTask')) } 'ConPTY resize forwarding removed'
    Assert-InteractiveTerminalContract $mainXaml $mainCode $terminalXaml $terminalCode
    Write-Host 'P08-007 negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) { Invoke-RuntimeFixture $appProjectPath }
