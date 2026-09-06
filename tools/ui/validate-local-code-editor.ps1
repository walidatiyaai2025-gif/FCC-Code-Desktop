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

function Assert-LocalEditorContract {
    param(
        [string]$ControlXamlText,
        [string]$ControlCodeText,
        [string]$MetricsText,
        [string]$MainWindowText,
        [string]$DocText
    )

    Assert-ValidXaml $ControlXamlText 'CodeEditorControl.xaml'
    Assert-ValidXaml $MainWindowText 'MainWindow.xaml'

    foreach ($literal in @(
        'x:Class="FCCCodeDesktop.App.Editor.CodeEditorControl"',
        'AutomationProperties.Name="Local code editor"',
        'x:Name="LineNumberGutter"',
        'AutomationProperties.Name="Editor line numbers"',
        'x:Name="EditorTextBox"',
        'AutomationProperties.Name="Code editor text"',
        'AcceptsReturn="True"',
        'AcceptsTab="True"',
        'IsUndoEnabled="True"',
        'IsReadOnly="{Binding IsReadOnly, ElementName=Root}"',
        'Text="{Binding Text, ElementName=Root, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"',
        'TextWrapping="NoWrap"',
        'FontFamily="Consolas"',
        'SpellCheck.IsEnabled="False"',
        'ScrollViewer.HorizontalScrollBarVisibility="Auto"',
        'ScrollViewer.VerticalScrollBarVisibility="Auto"',
        'x:Name="CaretStatusText"',
        '{DynamicResource FccBrushSurface}',
        '{DynamicResource FccBrushTextPrimary}',
        '{DynamicResource FccBrushBorder}'
    )) {
        Assert-ContainsLiteral $ControlXamlText $literal 'CodeEditorControl.xaml'
    }

    if ($ControlXamlText -match '#[0-9A-Fa-f]{6,8}') {
        throw 'P06-005 editor must use semantic theme resources instead of hard-coded colors.'
    }

    foreach ($literal in @(
        'public partial class CodeEditorControl : UserControl, INotifyPropertyChanged',
        'FrameworkPropertyMetadataOptions.BindsTwoWayByDefault',
        'public static readonly DependencyProperty IsReadOnlyProperty',
        'public static readonly DependencyProperty DocumentLabelProperty',
        'public static readonly DependencyProperty LanguageLabelProperty',
        'public string ModeLabel => IsReadOnly ? "Read only" : "Editable";',
        'ScrollViewer.ScrollChangedEvent',
        'GetFirstVisibleLineIndex()',
        'LineNumberGutter.ScrollToLine(firstVisibleLine)',
        'CodeEditorTextMetrics.CountLogicalLines(EditorTextBox.Text)',
        'CodeEditorTextMetrics.GetCaretPosition(EditorTextBox.Text, EditorTextBox.CaretIndex)'
    )) {
        Assert-ContainsLiteral $ControlCodeText $literal 'CodeEditorControl.xaml.cs'
    }

    foreach ($literal in @(
        'public readonly record struct CodeEditorCaretPosition',
        'public static class CodeEditorTextMetrics',
        'public static int CountLogicalLines(string? text)',
        'public static CodeEditorCaretPosition GetCaretPosition(string? text, int caretIndex)',
        "text[index] == '\\r'",
        "text[index] == '\\n'",
        'Math.Clamp(caretIndex, 0, text.Length)',
        'return new CodeEditorCaretPosition(line, column);'
    )) {
        Assert-ContainsLiteral $MetricsText $literal 'CodeEditorTextMetrics.cs'
    }

    foreach ($literal in @(
        'xmlns:editor="clr-namespace:FCCCodeDesktop.App.Editor"',
        '<editor:CodeEditorControl x:Key="LocalCodeEditor" />'
    )) {
        Assert-ContainsLiteral $MainWindowText $literal 'MainWindow.xaml'
    }

    foreach ($literal in @(
        'implemented entirely with WPF types',
        'does not embed a browser, WebView, JavaScript editor, CDN asset, HTTP dependency, external executable, or runtime package download',
        '`MainWindow` owns a production `LocalCodeEditor` resource',
        '`FCCD-P06-006` owns tabs, file loading/saving, reload, and dirty-state behavior',
        'The evidence class for P06-005 is cloud/self-test',
        'does not alter `FINAL_OWNER_ACCEPTANCE_QUEUE`'
    )) {
        Assert-ContainsLiteral $DocText $literal 'LOCAL_CODE_EDITOR.md'
    }

    foreach ($text in @($ControlXamlText, $ControlCodeText, $MetricsText, $MainWindowText, $DocText)) {
        foreach ($marker in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P06-005 contains forbidden unfinished-work marker '$marker'."
            }
        }
    }

    foreach ($forbidden in @(
        '<WebBrowser',
        '<WebView',
        'WebView2',
        'NavigateToString',
        'System.Net.Http',
        'HttpClient',
        'Process.Start',
        'ProcessStartInfo',
        'Source="http',
        'Source="https'
    )) {
        if ($ControlXamlText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $ControlCodeText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $MetricsText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
            throw "P06-005 crossed the locally bundled native-editor boundary: $forbidden"
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

    throw "Negative P06-005 local-editor fixture was not rejected: $Label"
}

function Invoke-LocalEditorRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime P06-005 local code editor fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime P06-005 local code editor fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime P06-005 local code editor fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-local-editor-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'LocalEditorFixture.csproj'
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
using FCCCodeDesktop.App.Editor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AssertMetrics();
        AssertProductionControl();
        Console.WriteLine("Runtime P06-005 local code editor fixture: PASS.");
    }

    private static void AssertMetrics()
    {
        Assert(CodeEditorTextMetrics.CountLogicalLines(string.Empty) == 1, "empty document has one line");
        Assert(CodeEditorTextMetrics.CountLogicalLines("a\r\nb\nc\rd") == 4, "CRLF LF CR line accounting");

        const string text = "one\r\nمرحبا\nthree";
        var caret = text.IndexOf("three", StringComparison.Ordinal) + 2;
        var position = CodeEditorTextMetrics.GetCaretPosition(text, caret);
        Assert(position.Line == 3 && position.Column == 3, "one-based caret metrics");

        var clamped = CodeEditorTextMetrics.GetCaretPosition("abc", 200);
        Assert(clamped.Line == 1 && clamped.Column == 4, "caret index is bounded safely");
    }

    private static void AssertProductionControl()
    {
        var app = new App();
        app.InitializeComponent();
        var mainWindow = new MainWindow();
        var editor = mainWindow.Resources["LocalCodeEditor"] as CodeEditorControl
            ?? throw new InvalidOperationException("Production LocalCodeEditor resource is missing.");

        editor.DocumentLabel = "Program.cs";
        editor.LanguageLabel = "C#";
        editor.Text = "alpha\r\nمرحبا\tworld\r\nomega";

        var host = new Window
        {
            Width = 760,
            Height = 420,
            Content = editor,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10_000,
            Top = -10_000,
        };

        host.Show();
        host.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
        host.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

        var textBox = editor.FindName("EditorTextBox") as TextBox
            ?? throw new InvalidOperationException("EditorTextBox was not created.");
        var gutter = editor.FindName("LineNumberGutter") as TextBox
            ?? throw new InvalidOperationException("LineNumberGutter was not created.");
        var caretStatus = editor.FindName("CaretStatusText") as TextBlock
            ?? throw new InvalidOperationException("CaretStatusText was not created.");

        Assert(textBox.AcceptsReturn && textBox.AcceptsTab, "multiline and Tab editing enabled");
        Assert(textBox.Text.Contains("مرحبا", StringComparison.Ordinal), "Unicode editor content retained");
        Assert(textBox.Text.Contains('\t'), "Tab content retained");
        Assert(textBox.TextWrapping == TextWrapping.NoWrap, "code content does not wrap");
        Assert(textBox.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto, "horizontal scrolling enabled");
        Assert(textBox.VerticalScrollBarVisibility == ScrollBarVisibility.Auto, "vertical scrolling enabled");
        Assert(gutter.Text.Split('\n').Length == 3, "three logical line numbers rendered");

        textBox.CaretIndex = textBox.Text.IndexOf("world", StringComparison.Ordinal) + 2;
        host.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert(caretStatus.Text.StartsWith("Ln 2, Col ", StringComparison.Ordinal), "caret status tracks second line");

        editor.IsReadOnly = true;
        host.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert(textBox.IsReadOnly, "read-only mode reaches native editor");
        Assert(editor.ModeLabel == "Read only", "read-only mode is truthfully labeled");

        editor.Text = string.Join("\r\n", Enumerable.Range(1, 512).Select(index => $"line {index:D4}"));
        host.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert(gutter.Text.Split('\n').Length == 512, "multiline gutter remains deterministic");

        host.Close();
        mainWindow.Close();
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"P06-005 local editor assertion failed: {label}");
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime P06-005 local code editor fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$controlXamlPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Editor\CodeEditorControl.xaml'
$controlCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Editor\CodeEditorControl.xaml.cs'
$metricsPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Editor\CodeEditorTextMetrics.cs'
$mainWindowPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$docPath = Join-Path $RepositoryRoot 'docs\projects\LOCAL_CODE_EDITOR.md'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($controlXamlPath, $controlCodePath, $metricsPath, $mainWindowPath, $docPath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required P06-005 local-editor path is missing: $path"
    }
}

$controlXamlText = Get-Content -LiteralPath $controlXamlPath -Raw
$controlCodeText = Get-Content -LiteralPath $controlCodePath -Raw
$metricsText = Get-Content -LiteralPath $metricsPath -Raw
$mainWindowText = Get-Content -LiteralPath $mainWindowPath -Raw
$docText = Get-Content -LiteralPath $docPath -Raw

Assert-LocalEditorContract $controlXamlText $controlCodeText $metricsText $mainWindowText $docText
Write-Host 'Static P06-005 local code editor validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-LocalEditorContract ($controlXamlText.Replace('TextWrapping="NoWrap"', 'TextWrapping="Wrap"')) $controlCodeText $metricsText $mainWindowText $docText
    } 'no-wrap code behavior removed'
    Assert-ContractRejects {
        Assert-LocalEditorContract ($controlXamlText.Replace('x:Name="LineNumberGutter"', 'x:Name="RemovedLineNumberGutter"')) $controlCodeText $metricsText $mainWindowText $docText
    } 'line-number gutter removed'
    Assert-ContractRejects {
        Assert-LocalEditorContract ($controlXamlText.Replace('ScrollViewer.HorizontalScrollBarVisibility="Auto"', 'ScrollViewer.HorizontalScrollBarVisibility="Disabled"')) $controlCodeText $metricsText $mainWindowText $docText
    } 'horizontal scrolling removed'
    Assert-ContractRejects {
        Assert-LocalEditorContract $controlXamlText ($controlCodeText.Replace('LineNumberGutter.ScrollToLine(firstVisibleLine)', 'RemovedLineNumberScroll(firstVisibleLine)')) $metricsText $mainWindowText $docText
    } 'line-number scroll synchronization removed'
    Assert-ContractRejects {
        Assert-LocalEditorContract $controlXamlText $controlCodeText $metricsText ($mainWindowText.Replace('<editor:CodeEditorControl x:Key="LocalCodeEditor" />', '')) $docText
    } 'production editor composition removed'
    Assert-ContractRejects {
        Assert-LocalEditorContract $controlXamlText ($controlCodeText + "`n// WebView2") $metricsText $mainWindowText $docText
    } 'browser-backed editor dependency introduced'
    Write-Host 'P06-005 deterministic negative fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-LocalEditorRuntimeFixture -AppProjectPath $appProjectPath
}
