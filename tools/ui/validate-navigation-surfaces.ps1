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

function Assert-NavigationContract {
    param(
        [string]$MainText,
        [string]$NavigationText,
        [string]$NavigationCodeText,
        [string]$SectionText,
        [string]$SectionCodeText,
        [string]$StateText
    )

    Assert-ValidXaml $MainText 'MainWindow.xaml'
    Assert-ValidXaml $NavigationText 'NavigationSurface.xaml'
    Assert-ValidXaml $SectionText 'WorkspaceSectionSurface.xaml'

    foreach ($literal in @(
        '<chrome:WorkspaceNavigationState x:Key="WorkspaceNavigationState" />',
        '<chrome:NavigationSurface State="{StaticResource WorkspaceNavigationState}" />',
        '<chrome:WorkspaceSectionSurface State="{StaticResource WorkspaceNavigationState}" />',
        '<chrome:WorkspaceLayout.LeftContent>',
        '<chrome:WorkspaceLayout.PrimaryContent>'
    )) {
        Assert-ContainsLiteral $MainText $literal 'MainWindow.xaml'
    }

    foreach ($literal in @(
        'x:Name="ProjectsNavigationButton"',
        'x:Name="SessionsNavigationButton"',
        'x:Name="TasksNavigationButton"',
        'Command="{Binding SelectSectionCommand}"',
        'CommandParameter="{x:Static shell:WorkspaceSection.Projects}"',
        'CommandParameter="{x:Static shell:WorkspaceSection.Sessions}"',
        'CommandParameter="{x:Static shell:WorkspaceSection.Tasks}"',
        'AutomationProperties.Name="Projects navigation"',
        'AutomationProperties.Name="Sessions navigation"',
        'AutomationProperties.Name="Tasks navigation"',
        '{DynamicResource FccBrushSelectionBackground}',
        '{DynamicResource FccBrushFocus}'
    )) {
        Assert-ContainsLiteral $NavigationText $literal 'NavigationSurface.xaml'
    }

    foreach ($literal in @(
        'x:Name="SectionTitle"',
        'Text="{Binding SelectedTitle}"',
        'Text="{Binding SelectedDescription}"',
        'x:Name="SectionContentHost"',
        'Content="{Binding SelectedContent}"',
        'AutomationProperties.Name="Selected workspace section content"'
    )) {
        Assert-ContainsLiteral $SectionText $literal 'WorkspaceSectionSurface.xaml'
    }

    foreach ($literal in @(
        'DependencyProperty StateProperty',
        'State ??= new WorkspaceNavigationState();'
    )) {
        Assert-ContainsLiteral $NavigationCodeText $literal 'NavigationSurface.xaml.cs'
        Assert-ContainsLiteral $SectionCodeText $literal 'WorkspaceSectionSurface.xaml.cs'
    }

    foreach ($literal in @(
        'public enum WorkspaceSection',
        'Projects,',
        'Sessions,',
        'Tasks,',
        'INotifyPropertyChanged',
        'ICommand SelectSectionCommand',
        'WorkspaceSection SelectedSection',
        'SelectedTitle',
        'SelectedDescription',
        'SelectedContent',
        'ProjectsContent',
        'SessionsContent',
        'TasksContent',
        'SelectSection(WorkspaceSection section)',
        'Enum.IsDefined(section)',
        'ArgumentOutOfRangeException'
    )) {
        Assert-ContainsLiteral $StateText $literal 'WorkspaceNavigationState.cs'
    }

    foreach ($text in @($NavigationText, $SectionText)) {
        if ($text -match '#[0-9A-Fa-f]{6,8}') {
            throw 'P02-005 surfaces must use semantic theme resources rather than hard-coded colors.'
        }

        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P02-005 surface contains forbidden placeholder text '$placeholder'."
            }
        }
    }

    foreach ($forbidden in @(
        'FCCCodeDesktop.Persistence',
        'System.IO.File',
        'Process.Start',
        'Registry.',
        'Microsoft.Win32',
        'SQLite'
    )) {
        if ($NavigationCodeText.Contains($forbidden) -or
            $SectionCodeText.Contains($forbidden) -or
            $StateText.Contains($forbidden)) {
            throw "P02-005 crossed the presentation/state-only boundary: $forbidden"
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

    throw "Negative navigation-surface fixture was not rejected: $Label"
}

function Invoke-NavigationRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime navigation-surface fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime navigation-surface fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime navigation-surface fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-navigation-surfaces-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'NavigationSurfacesFixture.csproj'
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
        var navigation = layout.LeftContent as NavigationSurface
            ?? throw new InvalidOperationException("NavigationSurface was not composed into the left region.");
        var sectionSurface = layout.PrimaryContent as WorkspaceSectionSurface
            ?? throw new InvalidOperationException("WorkspaceSectionSurface was not composed into the primary region.");

        Assert(ReferenceEquals(navigation.State, sectionSurface.State), "shared production state");
        var state = navigation.State;

        Assert(state.SelectedSection == WorkspaceSection.Projects, "default projects selection");
        Assert(state.SelectedTitle == "Projects", "projects title");
        Assert(state.IsProjectsSelected && !state.IsSessionsSelected && !state.IsTasksSelected, "projects selection flags");

        var projectMarker = new TextBlock { Text = "Project fixture" };
        var sessionMarker = new TextBlock { Text = "Session fixture" };
        var taskMarker = new TextBlock { Text = "Task fixture" };
        state.ProjectsContent = projectMarker;
        state.SessionsContent = sessionMarker;
        state.TasksContent = taskMarker;
        Assert(ReferenceEquals(state.SelectedContent, projectMarker), "project content seam");

        state.SelectSection(WorkspaceSection.Sessions);
        Assert(state.SelectedSection == WorkspaceSection.Sessions, "session selection");
        Assert(state.SelectedTitle == "Sessions", "session title");
        Assert(state.IsSessionsSelected && ReferenceEquals(state.SelectedContent, sessionMarker), "session content seam");

        Assert(state.SelectSectionCommand.CanExecute(WorkspaceSection.Tasks), "task command can execute");
        state.SelectSectionCommand.Execute(WorkspaceSection.Tasks);
        Assert(state.SelectedSection == WorkspaceSection.Tasks, "task command selection");
        Assert(state.SelectedTitle == "Tasks", "task title");
        Assert(state.IsTasksSelected && ReferenceEquals(state.SelectedContent, taskMarker), "task content seam");

        var rejected = false;
        try
        {
            state.SelectSection((WorkspaceSection)999);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejected = true;
        }
        Assert(rejected, "invalid section rejection");
        Assert(state.SelectedSection == WorkspaceSection.Tasks, "state preserved after invalid selection");

        Assert(navigation.FindName("ProjectsNavigationButton") is Button, "projects navigation button");
        Assert(navigation.FindName("SessionsNavigationButton") is Button, "sessions navigation button");
        Assert(navigation.FindName("TasksNavigationButton") is Button, "tasks navigation button");
        Assert(sectionSurface.FindName("SectionContentHost") is ContentControl, "section content host");

        var darkBackground = RequireBrush(navigation.Background, "dark navigation background").Color;
        var themes = new ThemeService(app.Resources);
        themes.Apply(AppearanceTheme.Light);
        navigation.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var lightBackground = RequireBrush(navigation.Background, "light navigation background").Color;
        Assert(lightBackground != darkBackground, "dynamic theme parity");
        themes.Apply(AppearanceTheme.Dark);

        Console.WriteLine("Runtime navigation/projects/sessions/tasks happy/negative/recovery fixture: PASS.");
    }

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush
        ?? throw new InvalidOperationException($"Expected SolidColorBrush for {label}.");

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Navigation-surface assertion failed: {label}");
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime navigation-surface fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$mainPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$navigationPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\NavigationSurface.xaml'
$navigationCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\NavigationSurface.xaml.cs'
$sectionPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceSectionSurface.xaml'
$sectionCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceSectionSurface.xaml.cs'
$statePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\WorkspaceNavigationState.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($mainPath, $navigationPath, $navigationCodePath, $sectionPath, $sectionCodePath, $statePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required navigation-surface path is missing: $path"
    }
}

$mainText = Get-Content -LiteralPath $mainPath -Raw
$navigationText = Get-Content -LiteralPath $navigationPath -Raw
$navigationCodeText = Get-Content -LiteralPath $navigationCodePath -Raw
$sectionText = Get-Content -LiteralPath $sectionPath -Raw
$sectionCodeText = Get-Content -LiteralPath $sectionCodePath -Raw
$stateText = Get-Content -LiteralPath $statePath -Raw

Assert-NavigationContract $mainText $navigationText $navigationCodeText $sectionText $sectionCodeText $stateText
Write-Host 'Static navigation/projects/sessions/tasks validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-NavigationContract ($mainText.Replace('<chrome:WorkspaceNavigationState x:Key="WorkspaceNavigationState" />', '')) $navigationText $navigationCodeText $sectionText $sectionCodeText $stateText
    } 'missing shared production navigation state'

    Assert-ContractRejects {
        Assert-NavigationContract $mainText ($navigationText.Replace('x:Name="TasksNavigationButton"', 'x:Name="RemovedTasksNavigationButton"')) $navigationCodeText $sectionText $sectionCodeText $stateText
    } 'missing tasks navigation control'

    Assert-ContractRejects {
        Assert-NavigationContract $mainText ($navigationText.Replace('{DynamicResource FccBrushSelectionBackground}', '#112233')) $navigationCodeText $sectionText $sectionCodeText $stateText
    } 'hard-coded selected navigation color'

    Assert-ContractRejects {
        Assert-NavigationContract $mainText $navigationText $navigationCodeText $sectionText $sectionCodeText ($stateText.Replace('SelectSection(WorkspaceSection section)', 'RemovedSectionSelection(WorkspaceSection section)'))
    } 'selection state contract removed'

    Assert-NavigationContract $mainText $navigationText $navigationCodeText $sectionText $sectionCodeText $stateText
    Write-Host 'Navigation-surface recovery fixture: PASS.'
    Write-Host 'Deterministic navigation-surface negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-NavigationRuntimeFixture $appProjectPath
}
