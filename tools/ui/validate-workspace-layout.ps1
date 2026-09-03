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

function Assert-WorkspaceContract {
    param(
        [string]$MainText,
        [string]$LayoutText,
        [string]$LayoutCodeText,
        [string]$StateText
    )

    Assert-ValidXaml $MainText 'MainWindow.xaml'
    Assert-ValidXaml $LayoutText 'WorkspaceLayout.xaml'

    Assert-ContainsLiteral $MainText '<chrome:WorkspaceLayout x:Name="WorkspaceLayoutHost"' 'MainWindow.xaml'
    Assert-ContainsLiteral $MainText '<ContentControl x:Name="WorkspaceHost"' 'MainWindow.xaml'

    foreach ($literal in @(
        'x:Name="LeftPaneColumn"',
        'x:Name="RightPaneColumn"',
        'x:Name="LeftSplitter"',
        'x:Name="RightSplitter"',
        'ResizeDirection="Columns"',
        'ResizeBehavior="PreviousAndNext"',
        'x:Name="LeftRegionHost"',
        'x:Name="PrimaryRegionHost"',
        'x:Name="RightRegionHost"',
        'Content="{Binding LeftContent, ElementName=Root}"',
        'Content="{Binding PrimaryContent, ElementName=Root}"',
        'Content="{Binding RightContent, ElementName=Root}"',
        'Width="{Binding State.LeftPaneWidth, ElementName=Root, Mode=TwoWay}"',
        'Width="{Binding State.RightPaneWidth, ElementName=Root, Mode=TwoWay}"',
        'AutomationProperties.Name="Resize navigation region"',
        'AutomationProperties.Name="Resize context region"',
        '{DynamicResource FccBrushCanvas}',
        '{DynamicResource FccBrushSurface}',
        '{DynamicResource FccBrushDivider}'
    )) {
        Assert-ContainsLiteral $LayoutText $literal 'WorkspaceLayout.xaml'
    }

    if ($LayoutText -match '#[0-9A-Fa-f]{6,8}') {
        throw 'WorkspaceLayout.xaml must consume semantic theme resources rather than hard-coded colors.'
    }

    foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
        if ($LayoutText.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "WorkspaceLayout.xaml contains forbidden placeholder text '$placeholder'."
        }
    }

    foreach ($literal in @(
        'DependencyProperty LeftContentProperty',
        'DependencyProperty PrimaryContentProperty',
        'DependencyProperty RightContentProperty',
        'DependencyProperty StateProperty',
        'State ??= new WorkspaceLayoutState();'
    )) {
        Assert-ContainsLiteral $LayoutCodeText $literal 'WorkspaceLayout.xaml.cs'
    }

    foreach ($literal in @(
        'INotifyPropertyChanged',
        'DefaultLeftPaneWidth = 240d',
        'DefaultRightPaneWidth = 300d',
        'MinimumSidePaneWidth = 160d',
        'MaximumSidePaneWidth = 480d',
        'CollapseLeftPane()',
        'RestoreLeftPane()',
        'CollapseRightPane()',
        'RestoreRightPane()',
        'Math.Clamp',
        'throw new ArgumentOutOfRangeException'
    )) {
        Assert-ContainsLiteral $StateText $literal 'WorkspaceLayoutState.cs'
    }

    foreach ($forbidden in @(
        'FCCCodeDesktop.Persistence',
        'System.IO.File',
        'Process.Start',
        'Registry.',
        'Microsoft.Win32'
    )) {
        if ($LayoutCodeText.Contains($forbidden) -or $StateText.Contains($forbidden)) {
            throw "P02-004 crossed the presentation-only workspace boundary: $forbidden"
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

    throw "Negative workspace-layout fixture was not rejected: $Label"
}

function Invoke-WorkspaceRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime workspace-layout fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime workspace-layout fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime workspace-layout fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-workspace-layout-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'WorkspaceLayoutFixture.csproj'
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
        var state = layout.State;

        Assert(state.LeftPaneWidth.Value == WorkspaceLayoutState.DefaultLeftPaneWidth, "default left width");
        Assert(state.RightPaneWidth.Value == WorkspaceLayoutState.DefaultRightPaneWidth, "default right width");

        state.LeftPaneWidth = new GridLength(80d);
        Assert(state.LeftPaneWidth.Value == WorkspaceLayoutState.MinimumSidePaneWidth, "left clamp");
        state.RightPaneWidth = new GridLength(900d);
        Assert(state.RightPaneWidth.Value == WorkspaceLayoutState.MaximumSidePaneWidth, "right clamp");

        state.CollapseLeftPane();
        Assert(state.IsLeftPaneCollapsed && state.LeftPaneWidth.Value == 0d, "left collapse");
        state.RestoreLeftPane();
        Assert(!state.IsLeftPaneCollapsed && state.LeftPaneWidth.Value == WorkspaceLayoutState.MinimumSidePaneWidth, "left restore");

        state.CollapseRightPane();
        Assert(state.IsRightPaneCollapsed && state.RightPaneWidth.Value == 0d, "right collapse");
        state.RestoreRightPane();
        Assert(!state.IsRightPaneCollapsed && state.RightPaneWidth.Value == WorkspaceLayoutState.MaximumSidePaneWidth, "right restore");

        state.Reset();
        Assert(state.LeftPaneWidth.Value == 240d && state.RightPaneWidth.Value == 300d, "layout reset");

        layout.LeftContent = new TextBlock { Text = "Left fixture" };
        layout.PrimaryContent = new TextBlock { Text = "Primary fixture" };
        layout.RightContent = new TextBlock { Text = "Right fixture" };
        Assert(layout.LeftContent is TextBlock && layout.PrimaryContent is TextBlock && layout.RightContent is TextBlock, "content seams");

        Assert(layout.FindName("LeftSplitter") is GridSplitter, "left splitter");
        Assert(layout.FindName("RightSplitter") is GridSplitter, "right splitter");
        Assert(layout.FindName("LeftRegionHost") is ContentControl, "left host");
        Assert(layout.FindName("PrimaryRegionHost") is ContentControl, "primary host");
        Assert(layout.FindName("RightRegionHost") is ContentControl, "right host");

        var darkBackground = RequireBrush(layout.Background, "dark layout background").Color;
        var themes = new ThemeService(app.Resources);
        themes.Apply(AppearanceTheme.Light);
        layout.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var lightBackground = RequireBrush(layout.Background, "light layout background").Color;
        Assert(lightBackground != darkBackground, "dynamic theme update");
        themes.Apply(AppearanceTheme.Dark);

        Console.WriteLine("Runtime workspace-layout happy/negative/recovery fixture: PASS.");
    }

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush
        ?? throw new InvalidOperationException($"Expected SolidColorBrush for {label}.");

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Workspace-layout assertion failed: {label}");
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime workspace-layout fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$mainPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$layoutPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceLayout.xaml'
$layoutCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceLayout.xaml.cs'
$statePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceLayoutState.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($mainPath, $layoutPath, $layoutCodePath, $statePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required workspace-layout path is missing: $path"
    }
}

$mainText = Get-Content -LiteralPath $mainPath -Raw
$layoutText = Get-Content -LiteralPath $layoutPath -Raw
$layoutCodeText = Get-Content -LiteralPath $layoutCodePath -Raw
$stateText = Get-Content -LiteralPath $statePath -Raw

Assert-WorkspaceContract $mainText $layoutText $layoutCodeText $stateText
Write-Host 'Static resizable workspace-layout validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-WorkspaceContract ($mainText.Replace('<chrome:WorkspaceLayout x:Name="WorkspaceLayoutHost" />', '')) $layoutText $layoutCodeText $stateText
    } 'missing production workspace composition'

    Assert-ContractRejects {
        Assert-WorkspaceContract $mainText ($layoutText.Replace('x:Name="LeftSplitter"', 'x:Name="RemovedLeftSplitter"')) $layoutCodeText $stateText
    } 'missing left splitter'

    Assert-ContractRejects {
        Assert-WorkspaceContract $mainText ($layoutText.Replace('AutomationProperties.Name="Resize context region"', '')) $layoutCodeText $stateText
    } 'missing splitter accessibility name'

    Assert-ContractRejects {
        Assert-WorkspaceContract $mainText ($layoutText.Replace('{DynamicResource FccBrushCanvas}', '#112233')) $layoutCodeText $stateText
    } 'hard-coded workspace color'

    Assert-ContractRejects {
        Assert-WorkspaceContract $mainText $layoutText $layoutCodeText ($stateText.Replace('CollapseLeftPane()', 'RemovedLeftPaneCollapse()'))
    } 'collapse contract removed'

    Assert-WorkspaceContract $mainText $layoutText $layoutCodeText $stateText
    Write-Host 'Workspace-layout recovery fixture: PASS.'
    Write-Host 'Deterministic workspace-layout negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-WorkspaceRuntimeFixture $appProjectPath
}
