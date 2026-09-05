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

function Assert-CommandPaletteContract {
    param(
        [string]$MainText,
        [string]$MainCodeText,
        [string]$PaletteText,
        [string]$PaletteCodeText,
        [string]$PaletteStateText
    )

    Assert-ValidXaml $MainText 'MainWindow.xaml'
    Assert-ValidXaml $PaletteText 'CommandPalette.xaml'

    foreach ($literal in @(
        '<chrome:CommandPaletteState x:Key="CommandPaletteState" />',
        '<chrome:CommandPalette x:Name="CommandPaletteHost"',
        'Grid.RowSpan="2"',
        'Panel.ZIndex="100"',
        'State="{StaticResource CommandPaletteState}"'
    )) {
        Assert-ContainsLiteral $MainText $literal 'MainWindow.xaml'
    }

    foreach ($literal in @(
        'ConfigureShellCommandFramework();',
        'RequireResource<CommandPaletteState>("CommandPaletteState")',
        'paletteState.RegisterCommand(',
        '"workspace.projects"',
        '"workspace.sessions"',
        '"workspace.tasks"',
        '"workspace.toggleBottomPanel"',
        '"Ctrl+J"',
        'new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)',
        'new KeyGesture(Key.F1)',
        'new KeyGesture(Key.J, ModifierKeys.Control)'
    )) {
        Assert-ContainsLiteral $MainCodeText $literal 'MainWindow.xaml.cs'
    }

    foreach ($literal in @(
        'x:Name="PaletteChrome"',
        'x:Name="SearchBox"',
        'Text="{Binding State.FilterText, ElementName=Root, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"',
        'AutomationProperties.Name="Command palette search"',
        'x:Name="CommandList"',
        'ItemsSource="{Binding State.FilteredCommands, ElementName=Root}"',
        'SelectedIndex="{Binding State.SelectedIndex, ElementName=Root, Mode=TwoWay}"',
        'AutomationProperties.Name="Command results"',
        'Text="No matching commands"',
        'Ctrl+Shift+P · F1',
        'Up/Down navigate · Enter run · Esc close',
        '{DynamicResource FccBrushSurfaceRaised}',
        '{DynamicResource FccBrushSelectionBackground}',
        '{DynamicResource FccBrushFocus}'
    )) {
        Assert-ContainsLiteral $PaletteText $literal 'CommandPalette.xaml'
    }

    foreach ($literal in @(
        'DependencyProperty StateProperty',
        'State ??= new CommandPaletteState();',
        'state.PropertyChanged += OnStatePropertyChanged',
        'state.PropertyChanged -= OnStatePropertyChanged',
        'Keyboard.FocusedElement',
        'SearchBox.Focus();',
        'Keyboard.Focus(focusTarget)',
        'case Key.Down:',
        'State.MoveSelection(1);',
        'case Key.Up:',
        'State.MoveSelection(-1);',
        'case Key.Enter:',
        'State.ExecuteSelected();',
        'case Key.Escape:',
        'State.Close();',
        'OnCommandDoubleClick'
    )) {
        Assert-ContainsLiteral $PaletteCodeText $literal 'CommandPalette.xaml.cs'
    }

    foreach ($literal in @(
        'public sealed class ShellCommandDescriptor',
        'public sealed class CommandPaletteState : INotifyPropertyChanged',
        'ReadOnlyObservableCollection<ShellCommandDescriptor>',
        'IReadOnlyList<ShellCommandDescriptor> RegisteredCommands',
        'ICommand OpenCommand',
        'ICommand DismissCommand',
        'ICommand MoveSelectionCommand',
        'ICommand ExecuteSelectedCommand',
        'bool IsOpen',
        'string FilterText',
        'int SelectedIndex',
        'ShellCommandDescriptor? SelectedCommand',
        'bool HasMatches',
        'RegisterCommand(ShellCommandDescriptor descriptor)',
        'UnregisterCommand(string id)',
        'StringComparison.OrdinalIgnoreCase',
        'already registered',
        'public void Open()',
        'public void Close()',
        'public bool MoveSelection(int offset)',
        'public bool ExecuteSelected()',
        'Math.Clamp',
        'ArgumentOutOfRangeException'
    )) {
        Assert-ContainsLiteral $PaletteStateText $literal 'CommandPaletteState.cs'
    }

    foreach ($text in @($MainText, $PaletteText)) {
        if ($text -match '#[0-9A-Fa-f]{6,8}') {
            throw 'P02-007 surfaces must use semantic theme resources rather than hard-coded colors.'
        }

        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P02-007 surface contains forbidden placeholder text '$placeholder'."
            }
        }
    }

    $shellFrameworkText = Get-CSharpMethodBlock \
        $MainCodeText \
        'private void ConfigureShellCommandFramework()' \
        'MainWindow.ConfigureShellCommandFramework'

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
        if ($shellFrameworkText.Contains($forbidden) -or
            $PaletteCodeText.Contains($forbidden) -or
            $PaletteStateText.Contains($forbidden)) {
            throw "P02-007 crossed the shell-framework-only boundary: $forbidden"
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

    throw "Negative command-palette fixture was not rejected: $Label"
}

function Invoke-CommandPaletteRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime command-palette fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime command-palette fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime command-palette fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-command-palette-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $projectPath = Join-Path $fixtureRoot 'CommandPaletteFixture.csproj'
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

        var palette = window.FindName("CommandPaletteHost") as CommandPalette
            ?? throw new InvalidOperationException("CommandPaletteHost was not created.");
        var paletteState = window.Resources["CommandPaletteState"] as CommandPaletteState
            ?? throw new InvalidOperationException("Shared CommandPaletteState was not created.");
        var navigationState = window.Resources["WorkspaceNavigationState"] as WorkspaceNavigationState
            ?? throw new InvalidOperationException("Shared WorkspaceNavigationState was not created.");
        var layoutState = window.Resources["WorkspaceLayoutState"] as WorkspaceLayoutState
            ?? throw new InvalidOperationException("Shared WorkspaceLayoutState was not created.");

        Assert(ReferenceEquals(palette.State, paletteState), "shared production palette state");
        Assert(paletteState.RegisteredCommands.Count == 4, "registered built-in shell commands");
        Assert(paletteState.FilteredCommands.Count == 4, "initial command list");
        Assert(!paletteState.IsOpen, "palette closed by default");

        var openBinding = RequireKeyBinding(
            window,
            Key.P,
            ModifierKeys.Control | ModifierKeys.Shift,
            "Ctrl+Shift+P");
        Assert(ReferenceEquals(openBinding.Command, paletteState.OpenCommand), "palette shortcut command");

        var f1Binding = RequireKeyBinding(window, Key.F1, ModifierKeys.None, "F1");
        Assert(ReferenceEquals(f1Binding.Command, paletteState.OpenCommand), "F1 palette shortcut command");

        var panelBinding = RequireKeyBinding(window, Key.J, ModifierKeys.Control, "Ctrl+J");
        Assert(ReferenceEquals(panelBinding.Command, layoutState.ToggleBottomPanelCommand), "bottom panel shortcut command");

        openBinding.Command.Execute(openBinding.CommandParameter);
        palette.Dispatcher.Invoke(() => { }, DispatcherPriority.Input);
        Assert(paletteState.IsOpen, "palette opens from global shortcut command");
        Assert(palette.Visibility == Visibility.Visible, "palette visibility follows open state");

        paletteState.FilterText = "SeSsIoNs";
        Assert(paletteState.FilteredCommands.Count == 1, "case-insensitive filtering");
        Assert(paletteState.SelectedCommand?.Id == "workspace.sessions", "filtered command selection");
        Assert(paletteState.ExecuteSelected(), "selected command execution");
        Assert(navigationState.SelectedSection == WorkspaceSection.Sessions, "registered command routes to shell state");
        Assert(!paletteState.IsOpen, "successful execution closes palette");

        paletteState.Open();
        paletteState.FilterText = "no-command-matches-this";
        Assert(!paletteState.HasMatches, "empty result state");
        Assert(paletteState.SelectedIndex == -1, "empty result selection");
        Assert(!paletteState.ExecuteSelected(), "empty result execution rejected");
        Assert(paletteState.IsOpen, "failed execution preserves palette");
        paletteState.Close();

        paletteState.Open();
        Assert(paletteState.MoveSelection(-1), "move selection upward");
        Assert(paletteState.SelectedIndex == paletteState.FilteredCommands.Count - 1, "selection wraps upward");
        Assert(paletteState.MoveSelection(1), "move selection downward");
        Assert(paletteState.SelectedIndex == 0, "selection wraps downward");
        paletteState.Close();

        var duplicateRejected = false;
        try
        {
            paletteState.RegisterCommand(paletteState.RegisteredCommands[0]);
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        Assert(duplicateRejected, "duplicate command id rejection");
        Assert(paletteState.RegisteredCommands.Count == 4, "duplicate rejection preserves registry");

        var invalidMoveRejected = false;
        try
        {
            paletteState.MoveSelection(0);
        }
        catch (ArgumentOutOfRangeException)
        {
            invalidMoveRejected = true;
        }
        Assert(invalidMoveRejected, "zero selection movement rejection");

        var temporary = new FixtureCommand();
        paletteState.RegisterCommand(
            new ShellCommandDescriptor(
                "fixture.temporary",
                "Temporary Fixture",
                "Fixture",
                null,
                temporary));
        Assert(paletteState.RegisteredCommands.Count == 5, "extension registration seam");
        Assert(paletteState.UnregisterCommand("fixture.temporary"), "extension unregistration seam");
        Assert(paletteState.RegisteredCommands.Count == 4, "unregistration recovery");
        Assert(!paletteState.UnregisterCommand("fixture.missing"), "missing unregistration is safe");

        var wasCollapsed = layoutState.IsBottomPanelCollapsed;
        panelBinding.Command.Execute(panelBinding.CommandParameter);
        Assert(layoutState.IsBottomPanelCollapsed != wasCollapsed, "Ctrl+J toggles existing bottom panel state");
        panelBinding.Command.Execute(panelBinding.CommandParameter);
        Assert(layoutState.IsBottomPanelCollapsed == wasCollapsed, "Ctrl+J toggle recovery");

        var paletteChrome = palette.FindName("PaletteChrome") as Border
            ?? throw new InvalidOperationException("PaletteChrome was not created.");
        var darkBackground = RequireBrush(paletteChrome.Background, "dark palette background").Color;
        var themes = new ThemeService(app.Resources);
        themes.Apply(AppearanceTheme.Light);
        palette.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var lightBackground = RequireBrush(paletteChrome.Background, "light palette background").Color;
        Assert(lightBackground != darkBackground, "dynamic theme parity");
        themes.Apply(AppearanceTheme.Dark);

        Console.WriteLine("Runtime command-palette happy/negative/recovery fixture: PASS.");
    }

    private static KeyBinding RequireKeyBinding(
        Window window,
        Key key,
        ModifierKeys modifiers,
        string label)
    {
        return window.InputBindings
            .OfType<KeyBinding>()
            .SingleOrDefault(
                binding => binding.Gesture is KeyGesture gesture &&
                           gesture.Key == key &&
                           gesture.Modifiers == modifiers)
            ?? throw new InvalidOperationException($"Missing expected key binding: {label}");
    }

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush
        ?? throw new InvalidOperationException($"Expected SolidColorBrush for {label}.");

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Command-palette assertion failed: {label}");
        }
    }

    private sealed class FixtureCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }
}
'@

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime command-palette fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$mainPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$mainCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml.cs'
$palettePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\CommandPalette.xaml'
$paletteCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\CommandPalette.xaml.cs'
$paletteStatePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Shell\CommandPaletteState.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($mainPath, $mainCodePath, $palettePath, $paletteCodePath, $paletteStatePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required command-palette path is missing: $path"
    }
}

$mainText = Get-Content -LiteralPath $mainPath -Raw
$mainCodeText = Get-Content -LiteralPath $mainCodePath -Raw
$paletteText = Get-Content -LiteralPath $palettePath -Raw
$paletteCodeText = Get-Content -LiteralPath $paletteCodePath -Raw
$paletteStateText = Get-Content -LiteralPath $paletteStatePath -Raw

Assert-CommandPaletteContract $mainText $mainCodeText $paletteText $paletteCodeText $paletteStateText
Write-Host 'Static command-palette and keyboard framework validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        Assert-CommandPaletteContract ($mainText.Replace('<chrome:CommandPalette x:Name="CommandPaletteHost"', '<chrome:RemovedCommandPalette x:Name="CommandPaletteHost"')) $mainCodeText $paletteText $paletteCodeText $paletteStateText
    } 'missing production command palette composition'

    Assert-ContractRejects {
        Assert-CommandPaletteContract $mainText ($mainCodeText.Replace('new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)', 'new KeyGesture(Key.P, ModifierKeys.Control)')) $paletteText $paletteCodeText $paletteStateText
    } 'weakened primary command palette shortcut'

    Assert-ContractRejects {
        Assert-CommandPaletteContract $mainText $mainCodeText ($paletteText.Replace('{DynamicResource FccBrushSurfaceRaised}', '#112233')) $paletteCodeText $paletteStateText
    } 'hard-coded palette color'

    Assert-ContractRejects {
        Assert-CommandPaletteContract $mainText $mainCodeText $paletteText ($paletteCodeText.Replace('case Key.Escape:', 'case Key.Tab:')) $paletteStateText
    } 'escape dismissal removed'

    Assert-ContractRejects {
        Assert-CommandPaletteContract $mainText $mainCodeText $paletteText $paletteCodeText ($paletteStateText.Replace('already registered', 'duplicate accepted'))
    } 'duplicate command guard removed'

    Assert-ContractRejects {
        Assert-CommandPaletteContract $mainText $mainCodeText $paletteText $paletteCodeText ($paletteStateText.Replace('StringComparison.OrdinalIgnoreCase', 'StringComparison.Ordinal'))
    } 'case-insensitive filtering removed'

    Assert-ContractRejects {
        $leakedMainCodeText = $mainCodeText.Replace(
            'var paletteState = RequireResource<CommandPaletteState>("CommandPaletteState");',
            "// FCCCodeDesktop.Persistence`n        var paletteState = RequireResource<CommandPaletteState>(`"CommandPaletteState`");")
        Assert-CommandPaletteContract $mainText $leakedMainCodeText $paletteText $paletteCodeText $paletteStateText
    } 'persistence leaked into shell command framework'

    Assert-CommandPaletteContract $mainText $mainCodeText $paletteText $paletteCodeText $paletteStateText
    Write-Host 'Command-palette recovery fixture: PASS.'
    Write-Host 'Deterministic command-palette negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-CommandPaletteRuntimeFixture $appProjectPath
}
