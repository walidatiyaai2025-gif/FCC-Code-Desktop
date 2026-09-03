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

    if (-not $Text.Contains($Literal)) {
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

function Assert-BottomToolPanelContract {
    param(
        [string]$MainText,
        [string]$LayoutText,
        [string]$LayoutCodeText,
        [string]$LayoutStateText,
        [string]$PanelText,
        [string]$PanelCodeText,
        [string]$PanelStateText
    )

    Assert-ValidXaml $MainText 'MainWindow.xaml'
    Assert-ValidXaml $LayoutText 'WorkspaceLayout.xaml'
    Assert-ValidXaml $PanelText 'BottomToolPanel.xaml'

    foreach ($literal in @(
        '<chrome:WorkspaceLayoutState x:Key="WorkspaceLayoutState" />',
        '<chrome:BottomToolPanelState x:Key="BottomToolPanelState" />',
        'State="{StaticResource WorkspaceLayoutState}"',
        '<chrome:WorkspaceLayout.BottomContent>',
        'x:Name="BottomToolPanelHost"',
        'State="{StaticResource BottomToolPanelState}"',
        'LayoutState="{StaticResource WorkspaceLayoutState}"'
    )) {
        Assert-ContainsLiteral $MainText $literal 'MainWindow.xaml'
    }

    foreach ($literal in @(
        'x:Name="BottomPaneRow"',
        'MinHeight="{StaticResource FccControlHeightComfortable}"',
        'Height="{Binding State.BottomPanelHeight, ElementName=Root, Mode=TwoWay}"',
        'x:Name="BottomSplitter"',
        'ResizeDirection="Rows"',
        'ResizeBehavior="PreviousAndNext"',
        'AutomationProperties.Name="Resize bottom tool panel"',
        'x:Name="BottomRegionHost"',
        'Content="{Binding BottomContent, ElementName=Root}"',
        'AutomationProperties.Name="Bottom tool panel region"',
        '{DynamicResource FccBrushSurfaceRaised}'
    )) {
        Assert-ContainsLiteral $LayoutText $literal 'WorkspaceLayout.xaml'
    }

    foreach ($literal in @(
        'DependencyProperty BottomContentProperty',
        'public object? BottomContent'
    )) {
        Assert-ContainsLiteral $LayoutCodeText $literal 'WorkspaceLayout.xaml.cs'
    }

    foreach ($literal in @(
        'DefaultBottomPanelHeight = 220d',
        'MinimumBottomPanelHeight = 120d',
        'MaximumBottomPanelHeight = 480d',
        'CollapsedBottomPanelHeight = 36d',
        'ICommand ToggleBottomPanelCommand',
        'GridLength BottomPanelHeight',
        'bool IsBottomPanelCollapsed',
        'CollapseBottomPanel()',
        'RestoreBottomPanel()',
        'ToggleBottomPanel()',
        'ClampBottomPanelHeight',
        'Math.Clamp',
        'ArgumentOutOfRangeException'
    )) {
        Assert-ContainsLiteral $LayoutStateText $literal 'WorkspaceLayoutState.cs'
    }

    foreach ($literal in @(
        'x:Name="OutputToolButton"',
        'x:Name="ProblemsToolButton"',
        'x:Name="TerminalToolButton"',
        'Command="{Binding SelectSectionCommand}"',
        'CommandParameter="{x:Static shell:BottomToolSection.Output}"',
        'CommandParameter="{x:Static shell:BottomToolSection.Problems}"',
        'CommandParameter="{x:Static shell:BottomToolSection.Terminal}"',
        'x:Name="ToggleBottomPanelButton"',
        'Command="{Binding LayoutState.ToggleBottomPanelCommand, ElementName=Root}"',
        'Tag="{Binding LayoutState.IsBottomPanelCollapsed, ElementName=Root}"',
        'x:Name="PanelContentHost"',
        'Content="{Binding State.SelectedContent, ElementName=Root}"',
        'AutomationProperties.Name="Selected bottom tool panel content"',
        '{DynamicResource FccBrushSelectionBackground}',
        '{DynamicResource FccBrushFocus}'
    )) {
        Assert-ContainsLiteral $PanelText $literal 'BottomToolPanel.xaml'
    }

    foreach ($literal in @(
        'DependencyProperty StateProperty',
        'DependencyProperty LayoutStateProperty',
        'State ??= new BottomToolPanelState();',
        'LayoutState ??= new WorkspaceLayoutState();'
    )) {
        Assert-ContainsLiteral $PanelCodeText $literal 'BottomToolPanel.xaml.cs'
    }

    foreach ($literal in @(
        'public enum BottomToolSection',
        'Output,',
        'Problems,',
        'Terminal,',
        'INotifyPropertyChanged',
        'ICommand SelectSectionCommand',
        'BottomToolSection SelectedSection',
        'SelectedContent',
        'OutputContent',
        'ProblemsContent',
        'TerminalContent',
        'SelectSection(BottomToolSection section)',
        'Enum.IsDefined(section)',
        'ArgumentOutOfRangeException'
    )) {
        Assert-ContainsLiteral $PanelStateText $literal 'BottomToolPanelState.cs'
    }

    foreach ($text in @($LayoutText, $PanelText)) {
        if ($text -match '#[0-9A-Fa-f]{6,8}') {
            throw 'P02-006 surfaces must use semantic theme resources rather than hard-coded colors.'
        }

        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P02-006 surface contains forbidden placeholder text '$placeholder'."
            }
        }
    }

    foreach ($forbidden in @(
        'FCCCodeDesktop.Persistence',
        'FCCCodeDesktop.Runtime',
        'FCCCodeDesktop.Terminal',
        'System.IO.File',
        'Process.Start',
        'Registry.',
        'Microsoft.Win32',
        'SQLite'
    )) {
        if ($LayoutCodeText.Contains($forbidden) -or
            $LayoutStateText.Contains($forbidden) -or
            $PanelCodeText.Contains($forbidden) -or
            $PanelStateText.Contains($forbidden)) {
            throw "P02-006 crossed the shell-framework-only boundary: $forbidden"
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

    throw "Negative bottom-tool-panel fixture was not rejected: $Label"
}

function Invoke-BottomToolPanelRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime bottom-tool-panel fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime bottom-tool-panel fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime bottom-tool-panel fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-bottom-tool-panel-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'BottomToolPanelFixture.csproj'
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
using FCCCodeDesktop.App.DesignSystem;
using FCCCodeDesktop.App.Shell;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();

        var layout = window.FindName("WorkspaceLayoutHost") as WorkspaceLayout
            ?? throw new InvalidOperationException("WorkspaceLayoutHost was not created.");
        var panel = layout.BottomContent as BottomToolPanel
            ?? throw new InvalidOperationException("BottomToolPanel production content was not created.");

        Assert(ReferenceEquals(panel.LayoutState, layout.State), "shared production layout state");
        Assert(layout.State.BottomPanelHeight.Value == WorkspaceLayoutState.DefaultBottomPanelHeight, "default bottom panel height");
        Assert(!layout.State.IsBottomPanelCollapsed, "default expanded bottom panel");
        Assert(panel.State.SelectedSection == BottomToolSection.Output, "default output selection");

        var outputMarker = new TextBlock { Text = "Output fixture" };
        var problemsMarker = new TextBlock { Text = "Problems fixture" };
        var terminalMarker = new TextBlock { Text = "Terminal fixture" };
        panel.State.OutputContent = outputMarker;
        panel.State.ProblemsContent = problemsMarker;
        panel.State.TerminalContent = terminalMarker;
        Assert(ReferenceEquals(panel.State.SelectedContent, outputMarker), "output content seam");

        panel.State.SelectSection(BottomToolSection.Problems);
        Assert(panel.State.IsProblemsSelected, "problems selection");
        Assert(ReferenceEquals(panel.State.SelectedContent, problemsMarker), "problems content seam");

        Assert(panel.State.SelectSectionCommand.CanExecute(BottomToolSection.Terminal), "terminal command can execute");
        panel.State.SelectSectionCommand.Execute(BottomToolSection.Terminal);
        Assert(panel.State.IsTerminalSelected, "terminal command selection");
        Assert(ReferenceEquals(panel.State.SelectedContent, terminalMarker), "terminal content seam");

        var rejected = false;
        try
        {
            panel.State.SelectSection((BottomToolSection)999);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejected = true;
        }
        Assert(rejected, "invalid section rejection");
        Assert(panel.State.SelectedSection == BottomToolSection.Terminal, "selection preserved after invalid section");

        layout.State.BottomPanelHeight = new GridLength(80d);
        Assert(layout.State.BottomPanelHeight.Value == WorkspaceLayoutState.MinimumBottomPanelHeight, "bottom height minimum clamp");
        Assert(!layout.State.IsBottomPanelCollapsed, "clamped bottom panel remains expanded");

        Assert(layout.State.ToggleBottomPanelCommand.CanExecute(null), "toggle command can execute");
        layout.State.ToggleBottomPanelCommand.Execute(null);
        Assert(layout.State.IsBottomPanelCollapsed, "bottom panel collapse");
        Assert(layout.State.BottomPanelHeight.Value == WorkspaceLayoutState.CollapsedBottomPanelHeight, "collapsed header height");
        layout.State.ToggleBottomPanelCommand.Execute(null);
        Assert(!layout.State.IsBottomPanelCollapsed, "bottom panel restore");
        Assert(layout.State.BottomPanelHeight.Value == WorkspaceLayoutState.MinimumBottomPanelHeight, "restored prior expanded height");

        layout.State.BottomPanelHeight = new GridLength(900d);
        Assert(layout.State.BottomPanelHeight.Value == WorkspaceLayoutState.MaximumBottomPanelHeight, "bottom height maximum clamp");
        layout.State.Reset();
        Assert(layout.State.BottomPanelHeight.Value == WorkspaceLayoutState.DefaultBottomPanelHeight, "bottom panel reset");

        Assert(layout.FindName("BottomSplitter") is GridSplitter, "bottom splitter");
        Assert(layout.FindName("BottomRegionHost") is ContentControl, "bottom content host");
        Assert(panel.FindName("OutputToolButton") is Button, "output tool button");
        Assert(panel.FindName("ProblemsToolButton") is Button, "problems tool button");
        Assert(panel.FindName("TerminalToolButton") is Button, "terminal tool button");
        Assert(panel.FindName("ToggleBottomPanelButton") is Button, "toggle bottom panel button");
        Assert(panel.FindName("PanelContentHost") is ContentControl, "selected bottom content host");

        var darkBackground = RequireBrush(panel.Background, "dark bottom panel background").Color;
        var themes = new ThemeService(app.Resources);
        themes.Apply(AppearanceTheme.Light);
        panel.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var lightBackground = RequireBrush(panel.Background, "light bottom panel background").Color;
        Assert(lightBackground != darkBackground, "dynamic theme parity");
        themes.Apply(AppearanceTheme.Dark);

        Console.WriteLine("Runtime bottom-tool-panel happy/negative/recovery fixture: PASS.");
    }

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush
        ?? throw new InvalidOperationException($"Expected SolidColorBrush for {label}.");

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Bottom-tool-panel assertion failed: {label}");
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime bottom-tool-panel fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$mainPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$layoutPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceLayout.xaml'
$layoutCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceLayout.xaml.cs'
$layoutStatePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceLayoutState.cs'
$panelPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\BottomToolPanel.xaml'
$panelCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\BottomToolPanel.xaml.cs'
$panelStatePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\BottomToolPanelState.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($mainPath, $layoutPath, $layoutCodePath, $layoutStatePath, $panelPath, $panelCodePath, $panelStatePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required bottom-tool-panel path is missing: $path"
    }
}

$mainText = Get-Content -LiteralPath $mainPath -Raw
$layoutText = Get-Content -LiteralPath $layoutPath -Raw
$layoutCodeText = Get-Content -LiteralPath $layoutCodePath -Raw
$layoutStateText = Get-Content -LiteralPath $layoutStatePath -Raw
$panelText = Get-Content -LiteralPath $panelPath -Raw
$panelCodeText = Get-Content -LiteralPath $panelCodePath -Raw
$panelStateText = Get-Content -LiteralPath $panelStatePath -Raw

Assert-BottomToolPanelContract $mainText $layoutText $layoutCodeText $layoutStateText $panelText $panelCodeText $panelStateText
Write-Host 'Static bottom tool-panel framework validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-BottomToolPanelContract ($mainText.Replace('x:Name="BottomToolPanelHost"', 'x:Name="RemovedBottomToolPanelHost"')) $layoutText $layoutCodeText $layoutStateText $panelText $panelCodeText $panelStateText
    } 'missing production bottom tool panel composition'

    Assert-ContractRejects {
        Assert-BottomToolPanelContract $mainText ($layoutText.Replace('x:Name="BottomSplitter"', 'x:Name="RemovedBottomSplitter"')) $layoutCodeText $layoutStateText $panelText $panelCodeText $panelStateText
    } 'missing bottom panel splitter'

    Assert-ContractRejects {
        Assert-BottomToolPanelContract $mainText $layoutText $layoutCodeText $layoutStateText ($panelText.Replace('x:Name="TerminalToolButton"', 'x:Name="RemovedTerminalToolButton"')) $panelCodeText $panelStateText
    } 'missing terminal framework slot'

    Assert-ContractRejects {
        Assert-BottomToolPanelContract $mainText $layoutText $layoutCodeText $layoutStateText ($panelText.Replace('{DynamicResource FccBrushSelectionBackground}', '#112233')) $panelCodeText $panelStateText
    } 'hard-coded selected panel color'

    Assert-ContractRejects {
        Assert-BottomToolPanelContract $mainText $layoutText $layoutCodeText ($layoutStateText.Replace('ToggleBottomPanel()', 'RemovedBottomPanelToggle()')) $panelText $panelCodeText $panelStateText
    } 'bottom panel toggle contract removed'

    Assert-ContractRejects {
        Assert-BottomToolPanelContract $mainText $layoutText $layoutCodeText $layoutStateText $panelText $panelCodeText ($panelStateText.Replace('SelectSection(BottomToolSection section)', 'RemovedBottomToolSelection(BottomToolSection section)'))
    } 'panel selection state contract removed'

    Assert-BottomToolPanelContract $mainText $layoutText $layoutCodeText $layoutStateText $panelText $panelCodeText $panelStateText
    Write-Host 'Bottom tool-panel recovery fixture: PASS.'
    Write-Host 'Deterministic bottom-tool-panel negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-BottomToolPanelRuntimeFixture $appProjectPath
}
