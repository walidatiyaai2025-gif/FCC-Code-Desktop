[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RunFixtures,
    [switch]$RequireRuntime
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ContainsLiteral {
    param(
        [string]$Text,
        [string]$Literal,
        [string]$Label
    )

    if (-not $Text.Contains($Literal)) {
        throw "$Label is missing required text: $Literal"
    }
}

function Assert-ValidXml {
    param(
        [string]$Text,
        [string]$Label
    )

    try {
        [void][xml]$Text
    }
    catch {
        throw "$Label is not valid XML/XAML: $($_.Exception.Message)"
    }
}

function Assert-NoHardCodedThemeColor {
    param(
        [string]$Text,
        [string]$Label
    )

    if ($Text -match '#[0-9A-Fa-f]{6,8}') {
        throw "$Label must not hard-code theme colors."
    }

    if ($Text -match '\{StaticResource\s+FccBrush') {
        throw "$Label must consume theme brushes with DynamicResource."
    }
}

function Assert-AppChromeContract {
    param(
        [string]$AppText,
        [string]$ResourcesText,
        [string]$MainText,
        [string]$TitleText,
        [string]$CodeText
    )

    Assert-ValidXml $AppText 'App.xaml'
    Assert-ValidXml $ResourcesText 'AppChrome.xaml'
    Assert-ValidXml $MainText 'MainWindow.xaml'
    Assert-ValidXml $TitleText 'AppTitleBar.xaml'

    Assert-ContainsLiteral $AppText 'StartupUri="MainWindow.xaml"' 'App.xaml'

    $resourceOrder = @(
        'DesignSystem/DesignTokens.xaml',
        'DesignSystem/Typography.xaml',
        'DesignSystem/Themes/Theme.Dark.xaml',
        'DesignSystem/AppChrome.xaml'
    )
    $lastIndex = -1
    foreach ($source in $resourceOrder) {
        $index = $AppText.IndexOf("Source=\"$source\"", [StringComparison]::Ordinal)
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
        Assert-ContainsLiteral $ResourcesText "x:Key=\"$key\"" 'AppChrome.xaml'
    }

    foreach ($requiredResourceText in @(
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
        Assert-ContainsLiteral $ResourcesText $requiredResourceText 'AppChrome.xaml'
    }

    if ($ResourcesText -match '<Color\b' -or $ResourcesText -match '<SolidColorBrush\b') {
        throw 'AppChrome.xaml must consume P02-002 semantic brushes instead of defining a new palette.'
    }

    Assert-NoHardCodedThemeColor $ResourcesText 'AppChrome.xaml'
    Assert-NoHardCodedThemeColor $MainText 'MainWindow.xaml'
    Assert-NoHardCodedThemeColor $TitleText 'AppTitleBar.xaml'

    foreach ($requiredMainText in @(
        'WindowStyle="None"',
        'ResizeMode="CanResize"',
        'Background="{DynamicResource FccBrushCanvas}"',
        '<shell:WindowChrome CaptionHeight="{StaticResource FccAppChromeHeight}"',
        'ResizeBorderThickness="{StaticResource FccAppChromeResizeBorderThickness}"',
        'UseAeroCaptionButtons="False"',
        '<chrome:AppTitleBar x:Name="AppTitleBarHost"',
        '<ContentControl x:Name="WorkspaceHost"'
    )) {
        Assert-ContainsLiteral $MainText $requiredMainText 'MainWindow.xaml'
    }

    if ($MainText.Contains('AllowsTransparency="True"')) {
        throw 'MainWindow must not enable AllowsTransparency; native WindowChrome behavior and rendering performance must be preserved.'
    }

    foreach ($forbiddenPlaceholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
        if ($MainText.Contains($forbiddenPlaceholder, [StringComparison]::OrdinalIgnoreCase)) {
            throw "MainWindow contains forbidden placeholder text '$forbiddenPlaceholder'."
        }
    }

    foreach ($requiredTitleText in @(
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
        Assert-ContainsLiteral $TitleText $requiredTitleText 'AppTitleBar.xaml'
    }

    foreach ($glyphKey in @(
        'FccChromeMinimizeGlyph',
        'FccChromeMaximizeGlyph',
        'FccChromeRestoreGlyph',
        'FccChromeCloseGlyph'
    )) {
        Assert-ContainsLiteral $TitleText "Data=\"{StaticResource $glyphKey}\"" 'AppTitleBar.xaml'
    }

    foreach ($requiredCodeText in @(
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
        Assert-ContainsLiteral $CodeText $requiredCodeText 'AppTitleBar.xaml.cs'
    }

    foreach ($forbiddenCodeText in @(
        'Process.Start',
        'System.IO.File',
        'FCCCodeDesktop.Runtime',
        'FCCCodeDesktop.Persistence',
        'FCCCodeDesktop.Git',
        'FCCCodeDesktop.Terminal'
    )) {
        if ($CodeText.Contains($forbiddenCodeText, [StringComparison]::Ordinal)) {
            throw "AppTitleBar code-behind crosses the presentation-only chrome boundary: $forbiddenCodeText"
        }
    }
}

function Assert-ContractRejects {
    param(
        [scriptblock]$Action,
        [string]$Label
    )

    $rejected = $false
    try {
        & $Action
    }
    catch {
        $rejected = $true
        Write-Host "Negative fixture rejected as expected: $Label"
    }

    if (-not $rejected) {
        throw "Negative app-chrome fixture was not rejected: $Label"
    }
}

function Invoke-AppChromeRuntimeFixture {
    param(
        [string]$AppProjectPath
    )

    if (-not $IsWindows) {
        throw 'Runtime app-chrome fixture requires Windows/WPF.'
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw 'dotnet is required for the runtime app-chrome fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime app-chrome fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("fccd-app-chrome-runtime-{0}" -f [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $fixtureProjectPath = Join-Path $fixtureRoot 'AppChromeRuntimeFixture.csproj'
        $programPath = Join-Path $fixtureRoot 'Program.cs'
        $escapedProjectReference = [Security.SecurityElement]::Escape($AppProjectPath)

        $projectText = @"
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
    <ProjectReference Include="$escapedProjectReference" />
  </ItemGroup>
</Project>
"@

        $programText = @'
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
        if (window.WindowStyle != WindowStyle.None || window.ResizeMode != ResizeMode.CanResize)
        {
            throw new InvalidOperationException("MainWindow does not expose the required custom resizable chrome contract.");
        }

        var chrome = WindowChrome.GetWindowChrome(window)
            ?? throw new InvalidOperationException("MainWindow has no WindowChrome instance.");
        if (Math.Abs(chrome.CaptionHeight - 40d) > 0.01d || chrome.UseAeroCaptionButtons)
        {
            throw new InvalidOperationException("WindowChrome caption/native-button contract is incorrect.");
        }
        if (chrome.ResizeBorderThickness.Left < 6d || chrome.ResizeBorderThickness.Top < 6d)
        {
            throw new InvalidOperationException("WindowChrome resize border is below the required usable threshold.");
        }

        var titleBar = window.FindName("AppTitleBarHost") as AppTitleBar
            ?? throw new InvalidOperationException("MainWindow title bar host was not created.");
        if (window.FindName("WorkspaceHost") is not ContentControl)
        {
            throw new InvalidOperationException("MainWindow workspace seam was not created.");
        }
        if (!string.Equals(titleBar.ProductName, "FCC Code Desktop", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Product identity text did not flow through the title bar contract.");
        }

        var minimize = RequireButton(titleBar, "MinimizeButton", "Minimize window");
        var maximize = RequireButton(titleBar, "MaximizeButton", "Maximize window");
        var restore = RequireButton(titleBar, "RestoreButton", "Restore window");
        _ = RequireButton(titleBar, "CloseButton", "Close window");

        maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (window.WindowState != WindowState.Maximized)
        {
            throw new InvalidOperationException("Maximize action did not update the host window state.");
        }

        restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (window.WindowState != WindowState.Normal)
        {
            throw new InvalidOperationException("Restore action did not return the host window to normal state.");
        }

        minimize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (window.WindowState != WindowState.Minimized)
        {
            throw new InvalidOperationException("Minimize action did not update the host window state.");
        }
        window.WindowState = WindowState.Normal;

        titleBar.ContextContent = new TextBlock { Text = "Workspace context" };
        titleBar.StatusContent = new TextBlock { Text = "Runtime status" };
        if (titleBar.ContextContent is not TextBlock || titleBar.StatusContent is not TextBlock)
        {
            throw new InvalidOperationException("App chrome extension content was not retained.");
        }

        var detached = new AppTitleBar();
        var detachedMinimize = detached.FindName("MinimizeButton") as Button
            ?? throw new InvalidOperationException("Detached title bar did not create caption controls.");
        detachedMinimize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var darkSurface = RequireBrush(titleBar.Background, "dark title bar surface").Color;
        var themeService = new ThemeService(app.Resources);
        if (themeService.CurrentTheme != AppearanceTheme.Dark)
        {
            throw new InvalidOperationException("Runtime fixture did not start in the canonical dark theme.");
        }

        themeService.Apply(AppearanceTheme.Light);
        titleBar.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var lightSurface = RequireBrush(titleBar.Background, "light title bar surface").Color;
        if (lightSurface == darkSurface)
        {
            throw new InvalidOperationException("Title bar DynamicResource did not follow the dark-to-light theme switch.");
        }

        themeService.Apply(AppearanceTheme.Dark);
        titleBar.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var recoveredSurface = RequireBrush(titleBar.Background, "recovered dark title bar surface").Color;
        if (recoveredSurface != darkSurface)
        {
            throw new InvalidOperationException("Title bar theme recovery did not return to the original dark semantic surface.");
        }

        Console.WriteLine("Runtime app-chrome happy/negative/theme-recovery fixture: PASS.");
    }

    private static Button RequireButton(AppTitleBar titleBar, string name, string expectedAutomationName)
    {
        var button = titleBar.FindName(name) as Button
            ?? throw new InvalidOperationException($"Missing caption button '{name}'.");
        var automationName = AutomationProperties.GetName(button);
        if (!string.Equals(automationName, expectedAutomationName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Caption button '{name}' has invalid automation name '{automationName}'.");
        }
        if (!WindowChrome.GetIsHitTestVisibleInChrome(button))
        {
            throw new InvalidOperationException($"Caption button '{name}' is not interactive inside WindowChrome.");
        }
        return button;
    }

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush
        ?? throw new InvalidOperationException($"Expected a SolidColorBrush for {label}.");
}
'@

        Set-Content -LiteralPath $fixtureProjectPath -Value $projectText -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $programText -Encoding utf8NoBOM

        & dotnet run --project $fixtureProjectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime app-chrome fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$appPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\App.xaml'
$resourcesPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\DesignSystem\AppChrome.xaml'
$mainPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$titlePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\AppTitleBar.xaml'
$codePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\AppTitleBar.xaml.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($requiredPath in @($appPath, $resourcesPath, $mainPath, $titlePath, $codePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required app-chrome path is missing: $requiredPath"
    }
}

$appText = Get-Content -LiteralPath $appPath -Raw
$resourcesText = Get-Content -LiteralPath $resourcesPath -Raw
$mainText = Get-Content -LiteralPath $mainPath -Raw
$titleText = Get-Content -LiteralPath $titlePath -Raw
$codeText = Get-Content -LiteralPath $codePath -Raw

Assert-AppChromeContract $appText $resourcesText $mainText $titleText $codeText
Write-Host 'Static premium app-chrome validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-AppChromeContract ($appText.Replace(' StartupUri="MainWindow.xaml"', '')) $resourcesText $mainText $titleText $codeText
    } 'missing startup window'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText $resourcesText ($mainText.Replace('WindowStyle="None"', 'WindowStyle="SingleBorderWindow"')) $titleText $codeText
    } 'default Windows/WPF chrome regression'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText $resourcesText ($mainText.Replace('<shell:WindowChrome CaptionHeight="{StaticResource FccAppChromeHeight}"', '<shell:WindowChrome CaptionHeight="0"')) $titleText $codeText
    } 'caption chrome contract regression'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText $resourcesText $mainText ($titleText.Replace('AutomationProperties.Name="Close window"', '')) $codeText
    } 'missing close-button accessibility name'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText ($resourcesText.Replace('{DynamicResource FccBrushHoverOverlay}', '#FFFFFF')) $mainText $titleText $codeText
    } 'hard-coded caption hover color'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText $resourcesText ($mainText.Replace('<ContentControl x:Name="WorkspaceHost"', '<ContentControl x:Name="TemporaryHost"')) $titleText $codeText
    } 'missing later-shell workspace seam'

    Assert-ContractRejects {
        Assert-AppChromeContract $appText $resourcesText $mainText $titleText ($codeText.Replace('window.WindowState == WindowState.Maximized', 'false'))
    } 'maximize/restore state contract removed'

    Assert-AppChromeContract $appText $resourcesText $mainText $titleText $codeText
    Write-Host 'App-chrome recovery fixture: PASS.'
    Write-Host 'Deterministic app-chrome negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-AppChromeRuntimeFixture $appProjectPath
}
