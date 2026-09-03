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

function Assert-NoPaletteLeak {
    param([string]$Text, [string]$Label)

    if ($Text -match '#[0-9A-Fa-f]{6,8}') {
        throw "$Label must not hard-code theme colors."
    }
    if ($Text -match '\{StaticResource\s+FccBrush') {
        throw "$Label must consume theme brushes through DynamicResource."
    }
}

function Assert-AppChromeContract {
    param(
        [string]$AppText,
        [string]$ChromeText,
        [string]$MainText,
        [string]$TitleText,
        [string]$CodeText
    )

    Assert-ValidXaml $AppText 'App.xaml'
    Assert-ValidXaml $ChromeText 'AppChrome.xaml'
    Assert-ValidXaml $MainText 'MainWindow.xaml'
    Assert-ValidXaml $TitleText 'AppTitleBar.xaml'

    Assert-ContainsLiteral $AppText 'StartupUri="MainWindow.xaml"' 'App.xaml'

    $lastIndex = -1
    foreach ($source in @(
        'DesignSystem/DesignTokens.xaml',
        'DesignSystem/Typography.xaml',
        'DesignSystem/Themes/Theme.Dark.xaml',
        'DesignSystem/AppChrome.xaml'
    )) {
        $needle = 'Source="' + $source + '"'
        $index = $AppText.IndexOf($needle, [StringComparison]::Ordinal)
        if ($index -lt 0) {
            throw "App.xaml is missing merged dictionary '$source'."
        }
        if ($index -le $lastIndex) {
            throw 'App.xaml merged dictionary order must remain tokens -> typography -> theme -> app chrome.'
        }
        $lastIndex = $index
    }

    foreach ($key in @(
        'FccAppChromeHeight',
        'FccAppChromeCaptionButtonWidth',
        'FccAppChromeGlyphSize',
        'FccAppChromeGlyphStroke',
        'FccAppChromeResizeBorderThickness',
        'FccAppChromeFrameBorderThickness',
        'FccAppChromeTitlePadding',
        'FccAppChromeContextPadding',
        'FccAppChromeStatusPadding',
        'FccAppChromeBottomDividerThickness',
        'FccChromeMinimizeGlyph',
        'FccChromeMaximizeGlyph',
        'FccChromeRestoreGlyph',
        'FccChromeCloseGlyph',
        'FccChromeCaptionButton',
        'FccChromeCloseButton'
    )) {
        Assert-ContainsLiteral $ChromeText ('x:Key="' + $key + '"') 'AppChrome.xaml'
    }

    foreach ($literal in @(
        '<sys:Double x:Key="FccAppChromeHeight">40</sys:Double>',
        '<Thickness x:Key="FccAppChromeResizeBorderThickness">6</Thickness>',
        'shell:WindowChrome.IsHitTestVisibleInChrome" Value="True"',
        '{DynamicResource FccBrushTextSecondary}',
        '{DynamicResource FccBrushHoverOverlay}',
        '{DynamicResource FccBrushPressedOverlay}',
        '{DynamicResource FccBrushFocus}',
        '{DynamicResource FccBrushErrorBackground}',
        '{DynamicResource FccBrushError}'
    )) {
        Assert-ContainsLiteral $ChromeText $literal 'AppChrome.xaml'
    }

    if ($ChromeText -match '<Color\b' -or $ChromeText -match '<SolidColorBrush\b') {
        throw 'AppChrome.xaml must not define a second color/brush palette.'
    }

    Assert-NoPaletteLeak $ChromeText 'AppChrome.xaml'
    Assert-NoPaletteLeak $MainText 'MainWindow.xaml'
    Assert-NoPaletteLeak $TitleText 'AppTitleBar.xaml'

    foreach ($literal in @(
        'WindowStyle="None"',
        'ResizeMode="CanResize"',
        'Background="{DynamicResource FccBrushCanvas}"',
        '<shell:WindowChrome CaptionHeight="{StaticResource FccAppChromeHeight}"',
        'ResizeBorderThickness="{StaticResource FccAppChromeResizeBorderThickness}"',
        'UseAeroCaptionButtons="False"',
        '<chrome:AppTitleBar x:Name="AppTitleBarHost"',
        '<ContentControl x:Name="WorkspaceHost"'
    )) {
        Assert-ContainsLiteral $MainText $literal 'MainWindow.xaml'
    }

    if ($MainText.Contains('AllowsTransparency="True"')) {
        throw 'MainWindow must preserve native WindowChrome rendering and must not use AllowsTransparency=True.'
    }

    foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
        if ($MainText.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "MainWindow contains forbidden placeholder text '$placeholder'."
        }
    }

    foreach ($literal in @(
        'Text="{Binding ProductName, ElementName=Root}"',
        'Content="{Binding ContextContent, ElementName=Root}"',
        'Content="{Binding StatusContent, ElementName=Root}"',
        'x:Name="MinimizeButton"',
        'x:Name="MaximizeButton"',
        'x:Name="RestoreButton"',
        'x:Name="CloseButton"',
        'AutomationProperties.Name="Minimize window"',
        'AutomationProperties.Name="Maximize window"',
        'AutomationProperties.Name="Restore window"',
        'AutomationProperties.Name="Close window"',
        'Click="OnMinimizeClick"',
        'Click="OnMaximizeRestoreClick"',
        'Click="OnCloseClick"',
        'Value="Maximized"',
        '{DynamicResource FccBrushSurface}',
        '{DynamicResource FccBrushDivider}'
    )) {
        Assert-ContainsLiteral $TitleText $literal 'AppTitleBar.xaml'
    }

    foreach ($glyph in @('Minimize', 'Maximize', 'Restore', 'Close')) {
        Assert-ContainsLiteral $TitleText ('Data="{StaticResource FccChrome' + $glyph + 'Glyph}"') 'AppTitleBar.xaml'
    }

    foreach ($literal in @(
        'DependencyProperty ProductNameProperty',
        'DependencyProperty ContextContentProperty',
        'DependencyProperty StatusContentProperty',
        'Window.GetWindow(this)',
        'WindowState.Minimized',
        'window.WindowState == WindowState.Maximized',
        '? WindowState.Normal',
        ': WindowState.Maximized',
        'Window.GetWindow(this)?.Close();'
    )) {
        Assert-ContainsLiteral $CodeText $literal 'AppTitleBar.xaml.cs'
    }

    foreach ($forbidden in @(
        'Process.Start',
        'System.IO.File',
        'FCCCodeDesktop.Runtime',
        'FCCCodeDesktop.Persistence',
        'FCCCodeDesktop.Git',
        'FCCCodeDesktop.Terminal'
    )) {
        if ($CodeText.Contains($forbidden)) {
            throw "AppTitleBar code-behind crosses the presentation-only boundary: $forbidden"
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

    throw "Negative app-chrome fixture was not rejected: $Label"
}

function Invoke-AppChromeRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime app-chrome fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime app-chrome fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime app-chrome fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-app-chrome-runtime-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'AppChromeRuntimeFixture.csproj'
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
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
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

        Assert(window.WindowStyle == WindowStyle.None, "custom window style");
        Assert(window.ResizeMode == ResizeMode.CanResize, "native resize mode");

        var chrome = WindowChrome.GetWindowChrome(window)
            ?? throw new InvalidOperationException("WindowChrome was not attached.");
        Assert(Math.Abs(chrome.CaptionHeight - 40d) < 0.01d, "caption height");
        Assert(!chrome.UseAeroCaptionButtons, "app-owned caption buttons");
        Assert(chrome.ResizeBorderThickness.Left >= 6d && chrome.ResizeBorderThickness.Top >= 6d, "resize border");

        var titleBar = window.FindName("AppTitleBarHost") as AppTitleBar
            ?? throw new InvalidOperationException("AppTitleBarHost was not created.");
        Assert(window.FindName("WorkspaceHost") is ContentControl, "workspace seam");
        Assert(titleBar.ProductName == "FCC Code Desktop", "product identity");

        var minimize = RequireButton(titleBar, "MinimizeButton", "Minimize window");
        var maximize = RequireButton(titleBar, "MaximizeButton", "Maximize window");
        var restore = RequireButton(titleBar, "RestoreButton", "Restore window");
        _ = RequireButton(titleBar, "CloseButton", "Close window");

        maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(window.WindowState == WindowState.Maximized, "maximize transition");
        restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(window.WindowState == WindowState.Normal, "restore transition");
        minimize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(window.WindowState == WindowState.Minimized, "minimize transition");
        window.WindowState = WindowState.Normal;

        titleBar.ContextContent = new TextBlock { Text = "Workspace context" };
        titleBar.StatusContent = new TextBlock { Text = "Runtime status" };
        Assert(titleBar.ContextContent is TextBlock && titleBar.StatusContent is TextBlock, "extension content seams");

        var detached = new AppTitleBar();
        var detachedMinimize = detached.FindName("MinimizeButton") as Button
            ?? throw new InvalidOperationException("Detached title bar caption button missing.");
        detachedMinimize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var darkSurface = RequireBrush(titleBar.Background, "dark surface").Color;
        var themes = new ThemeService(app.Resources);
        Assert(themes.CurrentTheme == AppearanceTheme.Dark, "default dark theme");

        themes.Apply(AppearanceTheme.Light);
        titleBar.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var lightSurface = RequireBrush(titleBar.Background, "light surface").Color;
        Assert(lightSurface != darkSurface, "dark to light DynamicResource update");

        themes.Apply(AppearanceTheme.Dark);
        titleBar.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Assert(RequireBrush(titleBar.Background, "recovered dark surface").Color == darkSurface, "theme recovery");

        Console.WriteLine("Runtime app-chrome happy/negative/theme-recovery fixture: PASS.");
    }

    private static Button RequireButton(AppTitleBar titleBar, string name, string automationName)
    {
        var button = titleBar.FindName(name) as Button
            ?? throw new InvalidOperationException($"Missing caption button '{name}'.");
        Assert(AutomationProperties.GetName(button) == automationName, $"automation name for {name}");
        Assert(WindowChrome.GetIsHitTestVisibleInChrome(button), $"chrome hit testing for {name}");
        return button;
    }

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush
        ?? throw new InvalidOperationException($"Expected SolidColorBrush for {label}.");

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"App-chrome runtime assertion failed: {label}");
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime app-chrome fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$appPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\App.xaml'
$chromePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\DesignSystem\AppChrome.xaml'
$mainPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$titlePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\AppTitleBar.xaml'
$codePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\AppTitleBar.xaml.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($appPath, $chromePath, $mainPath, $titlePath, $codePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required app-chrome path is missing: $path"
    }
}

$appText = Get-Content -LiteralPath $appPath -Raw
$chromeText = Get-Content -LiteralPath $chromePath -Raw
$mainText = Get-Content -LiteralPath $mainPath -Raw
$titleText = Get-Content -LiteralPath $titlePath -Raw
$codeText = Get-Content -LiteralPath $codePath -Raw

Assert-AppChromeContract $appText $chromeText $mainText $titleText $codeText
Write-Host 'Static premium app-chrome validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-AppChromeContract ($appText.Replace(' StartupUri="MainWindow.xaml"', '')) $chromeText $mainText $titleText $codeText
    } 'missing startup window'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText $chromeText ($mainText.Replace('WindowStyle="None"', 'WindowStyle="SingleBorderWindow"')) $titleText $codeText
    } 'default WPF chrome regression'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText $chromeText ($mainText.Replace('UseAeroCaptionButtons="False"', 'UseAeroCaptionButtons="True"')) $titleText $codeText
    } 'native caption buttons re-enabled'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText $chromeText $mainText ($titleText.Replace('AutomationProperties.Name="Close window"', '')) $codeText
    } 'missing close accessibility name'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText ($chromeText.Replace('{DynamicResource FccBrushHoverOverlay}', '#FFFFFF')) $mainText $titleText $codeText
    } 'hard-coded hover color'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText $chromeText ($mainText.Replace('<ContentControl x:Name="WorkspaceHost"', '<ContentControl x:Name="TemporaryHost"')) $titleText $codeText
    } 'missing workspace seam'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText $chromeText $mainText $titleText ($codeText.Replace('window.WindowState == WindowState.Maximized', 'false'))
    } 'maximize/restore state contract removed'

    Assert-AppChromeContract $appText $chromeText $mainText $titleText $codeText
    Write-Host 'App-chrome recovery fixture: PASS.'
    Write-Host 'Deterministic app-chrome negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-AppChromeRuntimeFixture $appProjectPath
}
