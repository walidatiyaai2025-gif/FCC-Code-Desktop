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

function Get-CSharpMethodBlock {
    param(
        [string]$Text,
        [string]$Signature,
        [string]$Label
    )

    $signatureIndex = $Text.IndexOf($Signature, [StringComparison]::Ordinal)
    if ($signatureIndex -lt 0) {
        throw "$Label method signature was not found: $Signature"
    }

    $openingBraceIndex = $Text.IndexOf('{', $signatureIndex + $Signature.Length)
    if ($openingBraceIndex -lt 0) {
        throw "$Label method body opening brace was not found."
    }

    $depth = 0
    for ($index = $openingBraceIndex; $index -lt $Text.Length; $index++) {
        if ($Text[$index] -eq '{') {
            $depth++
        }
        elseif ($Text[$index] -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return $Text.Substring($signatureIndex, ($index - $signatureIndex) + 1)
            }
        }
    }

    throw "$Label method body was not balanced."
}

function Assert-DpiLayoutContract {
    param(
        [string]$ManifestText,
        [string]$ProjectText,
        [string]$MainCodeText,
        [string]$CoordinatorText
    )

    try {
        [void][xml]$ManifestText
    }
    catch {
        throw "app.manifest is not valid XML: $($_.Exception.Message)"
    }

    foreach ($literal in @(
        '<dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>',
        '<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2, PerMonitor</dpiAwareness>'
    )) {
        Assert-ContainsLiteral $ManifestText $literal 'app.manifest'
    }

    Assert-ContainsLiteral $ProjectText '<ApplicationManifest>app.manifest</ApplicationManifest>' 'FCCCodeDesktop.App.csproj'

    foreach ($literal in @(
        'WorkspaceViewportCoordinator _viewportCoordinator',
        'Loaded += OnViewportLoaded;',
        'SizeChanged += OnViewportSizeChanged;',
        'DpiChanged += OnViewportDpiChanged;',
        'private void OnViewportLoaded(object sender, RoutedEventArgs e) => ApplyViewportPolicy();',
        'private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e) => ApplyViewportPolicy();',
        'private void OnViewportDpiChanged(object sender, DpiChangedEventArgs e) => ApplyViewportPolicy();',
        'VisualTreeHelper.GetDpi(this)',
        '_viewportCoordinator.Update('
    )) {
        Assert-ContainsLiteral $MainCodeText $literal 'MainWindow.xaml.cs'
    }

    foreach ($literal in @(
        'CompactWidthThreshold = 800d',
        'WideWidthThreshold = 1180d',
        'CompactHeightThreshold = 560d',
        'WorkspaceViewportProfile.Compact',
        'WorkspaceViewportProfile.Standard',
        'WorkspaceViewportProfile.Wide',
        'ForceCollapseLeftPane(state)',
        'ForceCollapseRightPane(state)',
        'RestoreForcedLeftPane(state)',
        'RestoreForcedRightPane(state)',
        '_bottomPanelForcedCollapsed',
        'double.IsFinite'
    )) {
        Assert-ContainsLiteral $CoordinatorText $literal 'WorkspaceViewportCoordinator.cs'
    }

    $viewportPolicyText = Get-CSharpMethodBlock $MainCodeText 'private void ApplyViewportPolicy()' 'MainWindow.ApplyViewportPolicy'

    foreach ($forbidden in @(
        'FCCCodeDesktop.Persistence',
        'FCCCodeDesktop.Runtime',
        'FCCCodeDesktop.Files',
        'FCCCodeDesktop.Git',
        'FCCCodeDesktop.Terminal',
        'System.IO.File',
        'Process.Start',
        'Registry.',
        'Microsoft.Win32',
        'SQLite'
    )) {
        if ($viewportPolicyText.Contains($forbidden) -or $CoordinatorText.Contains($forbidden)) {
            throw "P02-009 crossed the presentation-only responsive-layout boundary: $forbidden"
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

    throw "Negative DPI/layout fixture was not rejected: $Label"
}

function Invoke-DpiLayoutRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime DPI/layout fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime DPI/layout fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime DPI/layout fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-dpi-layout-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'DpiLayoutFixture.csproj'
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
using FCCCodeDesktop.App.Shell;

internal static class Program
{
    private static void Main()
    {
        var state = new WorkspaceLayoutState();
        var coordinator = new WorkspaceViewportCoordinator();

        coordinator.Update(state, 1366d, 768d, 1d, 1d);
        Assert(coordinator.Profile == WorkspaceViewportProfile.Wide, "1366x768 wide profile");
        Assert(!state.IsLeftPaneCollapsed && !state.IsRightPaneCollapsed, "wide panes visible");
        Assert(!state.IsBottomPanelCollapsed, "wide bottom panel visible");

        coordinator.Update(state, 1024d, 700d, 1.25d, 1.25d);
        Assert(coordinator.Profile == WorkspaceViewportProfile.Standard, "standard profile");
        Assert(!state.IsLeftPaneCollapsed && state.IsRightPaneCollapsed, "standard right collapse");
        Assert(coordinator.DpiScaleX == 1.25d && coordinator.DpiScaleY == 1.25d, "DPI scale capture");

        coordinator.Update(state, 720d, 520d, 1.5d, 1.5d);
        Assert(coordinator.Profile == WorkspaceViewportProfile.Compact, "compact profile");
        Assert(state.IsLeftPaneCollapsed && state.IsRightPaneCollapsed, "compact side panes collapsed");
        Assert(state.IsBottomPanelCollapsed, "compact-height bottom panel collapsed");

        coordinator.Update(state, 1366d, 768d, 2d, 2d);
        Assert(!state.IsLeftPaneCollapsed && !state.IsRightPaneCollapsed, "forced side panes recover");
        Assert(!state.IsBottomPanelCollapsed, "forced bottom panel recovers");

        state.CollapseRightPane();
        coordinator.Update(state, 1366d, 768d, 1d, 1d);
        Assert(state.IsRightPaneCollapsed, "user-collapsed right pane preserved");

        AssertThrows(() => coordinator.Update(state, 0d, 768d, 1d, 1d), "zero width rejection");
        AssertThrows(() => coordinator.Update(state, 1366d, 768d, double.NaN, 1d), "invalid DPI rejection");

        Console.WriteLine("Runtime DPI/resolution layout happy/negative/recovery fixture: PASS.");
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"DPI/layout assertion failed: {label}");
        }
    }

    private static void AssertThrows(Action action, string label)
    {
        try
        {
            action();
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected rejection: {label}");
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime DPI/layout fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$manifestPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\app.manifest'
$projectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'
$mainCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml.cs'
$coordinatorPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceViewportCoordinator.cs'

foreach ($path in @($manifestPath, $projectPath, $mainCodePath, $coordinatorPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required DPI/layout path is missing: $path"
    }
}

$manifestText = Get-Content -LiteralPath $manifestPath -Raw
$projectText = Get-Content -LiteralPath $projectPath -Raw
$mainCodeText = Get-Content -LiteralPath $mainCodePath -Raw
$coordinatorText = Get-Content -LiteralPath $coordinatorPath -Raw

Assert-DpiLayoutContract $manifestText $projectText $mainCodeText $coordinatorText
Write-Host 'Static DPI/resolution layout validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-DpiLayoutContract ($manifestText.Replace('PerMonitorV2, PerMonitor', 'System')) $projectText $mainCodeText $coordinatorText
    } 'per-monitor v2 awareness removed'

    Assert-ContractRejects {
        Assert-DpiLayoutContract $manifestText ($projectText.Replace('<ApplicationManifest>app.manifest</ApplicationManifest>', '')) $mainCodeText $coordinatorText
    } 'manifest binding removed'

    Assert-ContractRejects {
        Assert-DpiLayoutContract $manifestText $projectText ($mainCodeText.Replace('DpiChanged += OnViewportDpiChanged;', '')) $coordinatorText
    } 'runtime DPI response removed'

    Assert-ContractRejects {
        Assert-DpiLayoutContract $manifestText $projectText $mainCodeText ($coordinatorText.Replace('RestoreForcedRightPane(state)', 'RemovedForcedRightPaneRecovery(state)'))
    } 'forced-pane recovery removed'

    Assert-ContractRejects {
        $leakedMainCodeText = $mainCodeText.Replace(
            'var dpi = VisualTreeHelper.GetDpi(this);',
            "// FCCCodeDesktop.Persistence`n        var dpi = VisualTreeHelper.GetDpi(this);")
        Assert-DpiLayoutContract $manifestText $projectText $leakedMainCodeText $coordinatorText
    } 'persistence leaked into responsive layout policy'

    Assert-DpiLayoutContract $manifestText $projectText $mainCodeText $coordinatorText
    Write-Host 'DPI/resolution layout recovery fixture: PASS.'
    Write-Host 'Deterministic DPI/resolution layout negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-DpiLayoutRuntimeFixture $projectPath
}
