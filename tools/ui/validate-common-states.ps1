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
    try { [void][xml]$Text }
    catch { throw "$Label is not valid XML/XAML: $($_.Exception.Message)" }
}

function Assert-CommonStateContract {
    param(
        [string]$ModelText,
        [string]$SurfaceText,
        [string]$SurfaceCodeText,
        [string]$BadgeText,
        [string]$BadgeCodeText,
        [string]$NavigationStateText,
        [string]$WorkspaceSurfaceText
    )

    Assert-ValidXaml $SurfaceText 'CommonStateSurface.xaml'
    Assert-ValidXaml $BadgeText 'CommonStatusBadge.xaml'
    Assert-ValidXaml $WorkspaceSurfaceText 'WorkspaceSectionSurface.xaml'

    foreach ($literal in @(
        'public enum CommonStateKind', 'Empty,', 'Loading,', 'Info,', 'Success,', 'Warning,',
        'Error,', 'Unavailable,', 'Offline,', 'Blocked,', 'public sealed class CommonStateModel',
        'ICommand? ActionCommand', 'bool IsBusy', 'bool HasDetail', 'bool HasAction',
        'Enum.IsDefined(kind)', 'Action label and action command must be supplied together.',
        'static CommonStateModel Empty', 'static CommonStateModel Loading', 'static CommonStateModel Error',
        'static CommonStateModel Offline', 'static CommonStateModel Blocked'
    )) { Assert-ContainsLiteral $ModelText $literal 'CommonStateModel.cs' }

    foreach ($literal in @(
        'x:Name="StateCard"', 'x:Name="StateIndicator"', 'x:Name="StateTitle"',
        'x:Name="StateMessage"', 'x:Name="StateDetail"', 'x:Name="LoadingProgress"',
        'IsIndeterminate="True"', 'State.IsBusy', 'State.HasDetail', 'x:Name="StateActionButton"',
        'State.HasAction', 'State.ActionLabel', 'State.ActionCommand',
        'AutomationProperties.Name="{Binding State.Title, ElementName=Root}"',
        '{DynamicResource FccBrushInfoBackground}', '{DynamicResource FccBrushSuccessBackground}',
        '{DynamicResource FccBrushWarningBackground}', '{DynamicResource FccBrushErrorBackground}',
        '{DynamicResource FccBrushFocus}'
    )) { Assert-ContainsLiteral $SurfaceText $literal 'CommonStateSurface.xaml' }

    foreach ($literal in @('DependencyProperty StateProperty', 'CommonStateModel.Empty(', 'args.NewValue is null')) {
        Assert-ContainsLiteral $SurfaceCodeText $literal 'CommonStateSurface.xaml.cs'
    }

    foreach ($literal in @(
        'x:Name="BadgeBorder"', 'x:Name="BadgeIndicator"', 'x:Name="BadgeText"',
        'Text="{Binding Text, ElementName=Root}"',
        'AutomationProperties.Name="{Binding Text, ElementName=Root}"',
        '{DynamicResource FccBrushInfoBackground}', '{DynamicResource FccBrushSuccessBackground}',
        '{DynamicResource FccBrushWarningBackground}', '{DynamicResource FccBrushErrorBackground}'
    )) { Assert-ContainsLiteral $BadgeText $literal 'CommonStatusBadge.xaml' }

    foreach ($literal in @('DependencyProperty KindProperty', 'DependencyProperty TextProperty', 'Enum.IsDefined(value)')) {
        Assert-ContainsLiteral $BadgeCodeText $literal 'CommonStatusBadge.xaml.cs'
    }

    foreach ($literal in @(
        'CommonStateModel SelectedEmptyState', 'bool HasSelectedContent', '"No project open"',
        '"No sessions"', '"No tasks"', 'OnPropertyChanged(nameof(SelectedEmptyState))',
        'OnPropertyChanged(nameof(HasSelectedContent))'
    )) { Assert-ContainsLiteral $NavigationStateText $literal 'WorkspaceNavigationState.cs' }

    foreach ($literal in @(
        'x:Name="SectionContentHost"', 'Binding HasSelectedContent', 'x:Name="SectionEmptyState"',
        'State="{Binding SelectedEmptyState}"', '<shell:CommonStateSurface.Style>'
    )) { Assert-ContainsLiteral $WorkspaceSurfaceText $literal 'WorkspaceSectionSurface.xaml' }

    foreach ($text in @($SurfaceText, $BadgeText, $WorkspaceSurfaceText)) {
        if ($text -match '#[0-9A-Fa-f]{6,8}') {
            throw 'P02-008 surfaces must use semantic theme resources rather than hard-coded colors.'
        }
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P02-008 surface contains forbidden placeholder text '$placeholder'."
            }
        }
    }

    foreach ($forbidden in @(
        'FCCCodeDesktop.Persistence', 'FCCCodeDesktop.Runtime', 'FCCCodeDesktop.Fcc',
        'FCCCodeDesktop.Files', 'FCCCodeDesktop.Git', 'FCCCodeDesktop.Terminal',
        'System.IO.File', 'Process.Start', 'Registry.', 'SQLite', 'HttpClient'
    )) {
        if ($ModelText.Contains($forbidden) -or $SurfaceCodeText.Contains($forbidden) -or
            $BadgeCodeText.Contains($forbidden) -or $NavigationStateText.Contains($forbidden)) {
            throw "P02-008 crossed the presentation/state-only boundary: $forbidden"
        }
    }
}

function Assert-ContractRejects {
    param([scriptblock]$Action, [string]$Label)
    try { & $Action }
    catch {
        Write-Host "Negative fixture rejected as expected: $Label"
        return
    }
    throw "Negative common-state fixture was not rejected: $Label"
}

function Invoke-CommonStateRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) { throw 'Runtime common-state fixture requires Windows/WPF.' }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet is required for the runtime common-state fixture.' }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime common-state fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-common-states-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'CommonStatesFixture.csproj'
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
using System.Windows.Input;
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
        var workspace = layout.PrimaryContent as WorkspaceSectionSurface
            ?? throw new InvalidOperationException("WorkspaceSectionSurface was not composed into the primary region.");
        var state = workspace.State;

        Assert(state.SelectedSection == WorkspaceSection.Projects, "default project selection");
        Assert(!state.HasSelectedContent, "default project content absent");
        Assert(state.SelectedEmptyState.Kind == CommonStateKind.Empty, "project empty kind");
        Assert(state.SelectedEmptyState.Title == "No project open", "project empty title");
        state.SelectSection(WorkspaceSection.Sessions);
        Assert(state.SelectedEmptyState.Title == "No sessions", "session empty title");
        state.SelectSection(WorkspaceSection.Tasks);
        Assert(state.SelectedEmptyState.Title == "No tasks", "task empty title");

        var projectContent = new TextBlock { Text = "Project runtime fixture" };
        state.ProjectsContent = projectContent;
        state.SelectSection(WorkspaceSection.Projects);
        Assert(state.HasSelectedContent && ReferenceEquals(state.SelectedContent, projectContent), "project content seam");

        var command = new FixtureCommand();
        var states = new[]
        {
            CommonStateModel.Empty("Empty", "Empty state"),
            CommonStateModel.Loading("Loading", "Loading state", "Working"),
            CommonStateModel.Info("Info", "Info state"),
            CommonStateModel.Success("Success", "Success state"),
            CommonStateModel.Warning("Warning", "Warning state"),
            CommonStateModel.Error("Error", "Error state", "Details", "Retry", command),
            CommonStateModel.Unavailable("Unavailable", "Unavailable state"),
            CommonStateModel.Offline("Offline", "Offline state"),
            CommonStateModel.Blocked("Blocked", "Blocked state"),
        };

        foreach (var commonState in states)
        {
            Assert(commonState.IsBusy == (commonState.Kind == CommonStateKind.Loading), $"busy derivation {commonState.Kind}");
        }
        Assert(states[1].HasDetail, "loading detail derivation");
        Assert(states[5].HasDetail && states[5].HasAction, "error detail/action derivation");

        var rejectedPair = false;
        try { _ = new CommonStateModel(CommonStateKind.Error, "Error", "Message", actionLabel: "Retry"); }
        catch (ArgumentException) { rejectedPair = true; }
        Assert(rejectedPair, "unpaired action rejection");

        var commonSurface = new CommonStateSurface { Width = 600, Height = 260, State = states[1] };
        Prepare(commonSurface, 600, 260);
        var progress = commonSurface.FindName("LoadingProgress") as ProgressBar
            ?? throw new InvalidOperationException("LoadingProgress was not created.");
        Assert(progress.Visibility == Visibility.Visible, "loading progress visible");

        commonSurface.State = states[5];
        Prepare(commonSurface, 600, 260);
        var actionButton = commonSurface.FindName("StateActionButton") as Button
            ?? throw new InvalidOperationException("StateActionButton was not created.");
        Assert(actionButton.Visibility == Visibility.Visible, "state action visible");
        Assert(ReferenceEquals(actionButton.Command, command), "state action command binding");
        Assert(progress.Visibility == Visibility.Collapsed, "loading progress clears on terminal state");

        var stateCard = commonSurface.FindName("StateCard") as Border
            ?? throw new InvalidOperationException("StateCard was not created.");
        var darkErrorBackground = RequireBrush(stateCard.Background, "dark error background").Color;

        var badge = new CommonStatusBadge { Kind = CommonStateKind.Success, Text = "Ready" };
        Prepare(badge, 160, 40);
        var badgeBorder = badge.FindName("BadgeBorder") as Border
            ?? throw new InvalidOperationException("BadgeBorder was not created.");
        var darkBadgeBackground = RequireBrush(badgeBorder.Background, "dark success badge background").Color;

        var themes = new ThemeService(app.Resources);
        themes.Apply(AppearanceTheme.Light);
        commonSurface.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        badge.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        Prepare(commonSurface, 600, 260);
        Prepare(badge, 160, 40);
        Assert(RequireBrush(stateCard.Background, "light error background").Color != darkErrorBackground, "common state dynamic theme parity");
        Assert(RequireBrush(badgeBorder.Background, "light success badge background").Color != darkBadgeBackground, "status badge dynamic theme parity");
        themes.Apply(AppearanceTheme.Dark);

        state.ProjectsContent = null;
        Prepare(workspace, 800, 500);
        var sectionEmpty = workspace.FindName("SectionEmptyState") as CommonStateSurface
            ?? throw new InvalidOperationException("SectionEmptyState was not created.");
        var contentHost = workspace.FindName("SectionContentHost") as ContentControl
            ?? throw new InvalidOperationException("SectionContentHost was not created.");
        Assert(sectionEmpty.Visibility == Visibility.Visible, "workspace empty fallback visible");
        Assert(contentHost.Visibility == Visibility.Collapsed, "empty workspace hides content host");

        state.ProjectsContent = projectContent;
        Prepare(workspace, 800, 500);
        Assert(sectionEmpty.Visibility == Visibility.Collapsed, "workspace empty fallback clears with content");
        Assert(contentHost.Visibility == Visibility.Visible, "workspace content visible");

        Console.WriteLine("Runtime common empty/loading/error/status happy/negative/recovery fixture: PASS.");
    }

    private static void Prepare(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        element.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        element.UpdateLayout();
    }

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush ?? throw new InvalidOperationException($"Expected SolidColorBrush for {label}.");

    private static void Assert(bool condition, string label)
    {
        if (!condition) { throw new InvalidOperationException($"Common-state assertion failed: {label}"); }
    }

    private sealed class FixtureCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM
        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) { throw "Runtime common-state fixture failed with exit code $LASTEXITCODE." }
    }
    finally { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue }
}

$modelPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\CommonStateModel.cs'
$surfacePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\CommonStateSurface.xaml'
$surfaceCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\CommonStateSurface.xaml.cs'
$badgePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\CommonStatusBadge.xaml'
$badgeCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\CommonStatusBadge.xaml.cs'
$navigationStatePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceNavigationState.cs'
$workspaceSurfacePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceSectionSurface.xaml'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($modelPath, $surfacePath, $surfaceCodePath, $badgePath, $badgeCodePath, $navigationStatePath, $workspaceSurfacePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required common-state path is missing: $path" }
}

$modelText = Get-Content -LiteralPath $modelPath -Raw
$surfaceText = Get-Content -LiteralPath $surfacePath -Raw
$surfaceCodeText = Get-Content -LiteralPath $surfaceCodePath -Raw
$badgeText = Get-Content -LiteralPath $badgePath -Raw
$badgeCodeText = Get-Content -LiteralPath $badgeCodePath -Raw
$navigationStateText = Get-Content -LiteralPath $navigationStatePath -Raw
$workspaceSurfaceText = Get-Content -LiteralPath $workspaceSurfacePath -Raw

Assert-CommonStateContract $modelText $surfaceText $surfaceCodeText $badgeText $badgeCodeText $navigationStateText $workspaceSurfaceText
Write-Host 'Static common empty/loading/error/status validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-CommonStateContract ($modelText.Replace('Blocked,', 'RemovedBlocked,')) $surfaceText $surfaceCodeText $badgeText $badgeCodeText $navigationStateText $workspaceSurfaceText
    } 'missing blocked state taxonomy'
    Assert-ContractRejects {
        Assert-CommonStateContract $modelText ($surfaceText.Replace('{DynamicResource FccBrushErrorBackground}', '#55112233')) $surfaceCodeText $badgeText $badgeCodeText $navigationStateText $workspaceSurfaceText
    } 'hard-coded error background'
    Assert-ContractRejects {
        Assert-CommonStateContract $modelText $surfaceText $surfaceCodeText $badgeText $badgeCodeText $navigationStateText ($workspaceSurfaceText.Replace('State="{Binding SelectedEmptyState}"', 'State="{x:Null}"'))
    } 'workspace empty-state binding removed'
    Assert-ContractRejects {
        Assert-CommonStateContract $modelText ($surfaceText.Replace('Command="{Binding State.ActionCommand, ElementName=Root}"', 'Command="{x:Null}"')) $surfaceCodeText $badgeText $badgeCodeText $navigationStateText $workspaceSurfaceText
    } 'state action command binding removed'
    Assert-CommonStateContract $modelText $surfaceText $surfaceCodeText $badgeText $badgeCodeText $navigationStateText $workspaceSurfaceText
    Write-Host 'Common-state recovery fixture: PASS.'
    Write-Host 'Deterministic common-state negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) { Invoke-CommonStateRuntimeFixture $appProjectPath }
