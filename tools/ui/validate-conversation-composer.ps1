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

function Assert-ComposerContract {
    param(
        [string]$ComposerStateText,
        [string]$ComposerXamlText,
        [string]$ComposerCodeText,
        [string]$ConversationSurfaceText,
        [string]$ConversationSurfaceCodeText,
        [string]$MainWindowText,
        [string]$MainWindowCodeText
    )

    Assert-ValidXaml $ComposerXamlText 'ConversationComposer.xaml'
    Assert-ValidXaml $ConversationSurfaceText 'ConversationSurface.xaml'
    Assert-ValidXaml $MainWindowText 'MainWindow.xaml'

    foreach ($literal in @(
        'public const int MaxDraftLength = 12_000',
        'public const int MaxAttachments = 8',
        'public const int MaxContextReferences = 12',
        'public const long MaxAttachmentBytes = 25L * 1024L * 1024L',
        'public sealed record ComposerSubmission(',
        'IReadOnlyList<ComposerAttachmentSnapshot> Attachments',
        'IReadOnlyList<ComposerContextSnapshot> ContextReferences',
        'public bool TryAddAttachment(string path)',
        'Path.GetFullPath(path.Trim())',
        'File.Exists(fullPath)',
        'string.Equals(item.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)',
        'or InvalidOperationException',
        'public bool TryAddContextReference(ComposerContextKind kind, string reference, string label)',
        'public ComposerSubmission CreateSubmission()',
        'public bool RequestSubmission()',
        'SubmissionRequested.Invoke',
        'public void AcceptSubmission(long submissionId)',
        'public void RejectSubmission(long submissionId, string message)',
        'Composer submission identity does not match the pending submission.'
    )) {
        Assert-ContainsLiteral $ComposerStateText $literal 'ComposerState.cs'
    }

    foreach ($literal in @(
        'x:Name="ComposerTextBox"',
        'Text="{Binding DraftText, UpdateSourceTrigger=PropertyChanged}"',
        'MaxLength="12000"',
        'x:Name="AttachmentItems"',
        'ItemsSource="{Binding Attachments}"',
        'x:Name="ContextItems"',
        'ItemsSource="{Binding ContextReferences}"',
        'x:Name="AttachFilesButton"',
        'x:Name="AddContextButton"',
        'x:Name="SubmitComposerButton"',
        'Content="Add message"',
        'Command="{Binding SubmitCommand}"',
        'AutomationProperties.Name="Conversation composer"',
        '{DynamicResource FccBrushSurface}',
        '{DynamicResource FccBrushSurfaceRaised}',
        '{DynamicResource FccBrushBorder}',
        '{DynamicResource FccBrushAccent}',
        '{DynamicResource FccBrushError}'
    )) {
        Assert-ContainsLiteral $ComposerXamlText $literal 'ConversationComposer.xaml'
    }

    foreach ($literal in @(
        'OpenFileDialog',
        'Multiselect = true',
        'State.TryAddAttachment(path)',
        'State.TryAddContextReference(',
        'ComposerContextKind.File',
        'e.Key != Key.Enter',
        'ModifierKeys.Control',
        'State.SubmitCommand.Execute(null)'
    )) {
        Assert-ContainsLiteral $ComposerCodeText $literal 'ConversationComposer.xaml.cs'
    }

    foreach ($literal in @(
        'x:Name="ConversationComposerHost"',
        'State="{Binding Composer, ElementName=Root}"'
    )) {
        Assert-ContainsLiteral $ConversationSurfaceText $literal 'ConversationSurface.xaml'
    }

    foreach ($literal in @(
        'public static readonly DependencyProperty ComposerProperty',
        'typeof(ComposerState)',
        'public ComposerState Composer',
        'Composer ??= new ComposerState()'
    )) {
        Assert-ContainsLiteral $ConversationSurfaceCodeText $literal 'ConversationSurface.xaml.cs'
    }

    foreach ($literal in @(
        '<conversation:ComposerState x:Key="ComposerState" />',
        'Composer="{StaticResource ComposerState}"'
    )) {
        Assert-ContainsLiteral $MainWindowText $literal 'MainWindow.xaml'
    }

    foreach ($literal in @(
        'composerState.SubmissionRequested += OnComposerSubmissionRequested',
        'taskState.ValidateCanStart()',
        'conversationState.AddUserMessage(e.Submission.Text)',
        'composerState.AcceptSubmission(e.Submission.SubmissionId)',
        'composerState.RejectSubmission(e.Submission.SubmissionId, exception.Message)'
    )) {
        Assert-ContainsLiteral $MainWindowCodeText $literal 'MainWindow.xaml.cs'
    }

    foreach ($text in @($ComposerStateText, $ComposerXamlText, $ComposerCodeText, $ConversationSurfaceText, $ConversationSurfaceCodeText, $MainWindowText, $MainWindowCodeText)) {
        foreach ($placeholder in @('TODO', 'FIXME', 'Coming soon', 'Placeholder')) {
            if ($text.IndexOf($placeholder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "P05-003 contains forbidden placeholder text '$placeholder'."
            }
        }
    }

    if ($ComposerXamlText -match '#[0-9A-Fa-f]{6,8}') {
        throw 'P05-003 composer must consume semantic theme resources instead of hard-coded colors.'
    }

    foreach ($forbidden in @(
        'File.ReadAllText',
        'File.ReadAllBytes',
        'StreamReader',
        'IAgentRuntime',
        'AgentRuntimeRequest',
        'PayloadJson',
        'System.Diagnostics.Process',
        'Process.Start',
        'fcc-claude'
    )) {
        if ($ComposerStateText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $ComposerCodeText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase) -or
            $ComposerXamlText.Contains($forbidden, [StringComparison]::OrdinalIgnoreCase)) {
            throw "P05-003 crossed the composer presentation/submission boundary: $forbidden"
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

    throw "Negative conversation-composer fixture was not rejected: $Label"
}

function Invoke-ComposerRuntimeFixture {
    param([string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime conversation-composer fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime conversation-composer fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime conversation-composer fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('fccd-composer-' + [Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $fixtureRoot -Force)

    try {
        $fixtureFile = Join-Path $fixtureRoot 'fixture context.txt'
        Set-Content -LiteralPath $fixtureFile -Value 'composer fixture content' -Encoding utf8NoBOM

        $projectPath = Join-Path $fixtureRoot 'ComposerFixture.csproj'
        $programPath = Join-Path $fixtureRoot 'Program.cs'
        $projectReference = [Security.SecurityElement]::Escape($AppProjectPath)
        $fixturePathLiteral = $fixtureFile.Replace('"', '""')

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

        $programTemplate = @'
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FCCCodeDesktop.App;
using FCCCodeDesktop.App.Conversation;
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

        var composer = window.Resources["ComposerState"] as ComposerState
            ?? throw new InvalidOperationException("ComposerState production resource is missing.");
        var conversation = window.Resources["StreamingConversationState"] as StreamingConversationState
            ?? throw new InvalidOperationException("StreamingConversationState production resource is missing.");
        var surface = window.Resources["ConversationSurface"] as ConversationSurface
            ?? throw new InvalidOperationException("ConversationSurface production resource is missing.");
        var navigation = window.Resources["WorkspaceNavigationState"] as WorkspaceNavigationState
            ?? throw new InvalidOperationException("WorkspaceNavigationState production resource is missing.");

        Assert(ReferenceEquals(surface.Composer, composer), "shared composer composition");
        Assert(!composer.CanSubmit && !composer.HasDraftContent, "initial composer state");

        var detached = new ComposerState { DraftText = "orphan" };
        Assert(!detached.RequestSubmission(), "submission fails closed without a handler");
        Assert(detached.HasValidationMessage, "missing-handler validation is visible");

        var accepted = new ComposerState();
        Assert(!accepted.TryAddAttachment(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing")), "missing attachment rejected");
        Assert(accepted.TryAddAttachment(@"__FIXTURE_PATH__"), "attachment accepted");
        Assert(!accepted.TryAddAttachment(@"__FIXTURE_PATH__"), "duplicate attachment rejected");
        Assert(accepted.HasValidationMessage, "duplicate attachment produces visible validation");
        Assert(accepted.Attachments.Count == 1 && accepted.Attachments[0].SizeBytes > 0, "attachment metadata retained without content read");
        Assert(accepted.TryAddContextReference(ComposerContextKind.File, @"__FIXTURE_PATH__", "fixture context.txt"), "context accepted");
        Assert(!accepted.TryAddContextReference(ComposerContextKind.File, @"__FIXTURE_PATH__", "duplicate"), "duplicate context rejected");
        Assert(accepted.ContextReferences.Count == 1, "context deduplicated");

        ComposerSubmission? captured = null;
        accepted.SubmissionRequested += (_, args) =>
        {
            captured = args.Submission;
            accepted.AcceptSubmission(args.Submission.SubmissionId);
        };
        accepted.DraftText = "  inspect this fixture safely  ";
        Assert(accepted.RequestSubmission(), "standalone submission emitted");
        var submission = captured ?? throw new InvalidOperationException("Immutable composer submission was not emitted.");
        Assert(submission.Text == "inspect this fixture safely", "submission text normalized");
        Assert(submission.Attachments.Count == 1 && submission.Attachments[0].FullPath == @"__FIXTURE_PATH__", "attachment snapshot emitted");
        Assert(submission.ContextReferences.Count == 1 && submission.ContextReferences[0].Kind == ComposerContextKind.File, "context snapshot emitted");
        Assert(!accepted.HasDraftContent && accepted.Attachments.Count == 0 && accepted.ContextReferences.Count == 0, "exact acknowledgement clears accepted submission");
        Assert(!accepted.CanSubmit, "empty accepted composer cannot submit again");

        Assert(composer.TryAddAttachment(@"__FIXTURE_PATH__"), "production attachment accepted");
        Assert(composer.TryAddContextReference(ComposerContextKind.File, @"__FIXTURE_PATH__", "fixture context.txt"), "production context accepted");
        composer.DraftText = "production preflight";
        Assert(composer.CanSubmit, "visible production text enables submit");

        navigation.SelectSection(WorkspaceSection.Sessions);
        window.Show();
        PumpUntil(() => window.IsLoaded, "production window loaded");

        var composerControl = surface.FindName("ConversationComposerHost") as ConversationComposer
            ?? throw new InvalidOperationException("Production conversation composer host is missing.");
        Assert(composerControl.FindName("ComposerTextBox") is TextBox, "composer textbox rendered");
        Assert(composerControl.FindName("AttachmentItems") is ItemsControl attachmentItems && attachmentItems.Items.Count == 1, "attachment chip rendered");
        Assert(composerControl.FindName("ContextItems") is ItemsControl contextItems && contextItems.Items.Count == 1, "context chip rendered");
        Assert(composerControl.FindName("SubmitComposerButton") is Button submitButton && submitButton.IsEnabled, "submit button enabled");

        var composerTextBox = (TextBox)composerControl.FindName("ComposerTextBox")!;
        var darkBackground = RequireBrush(composerTextBox.Background, "dark composer background").Color;
        var themes = new ThemeService(app.Resources);
        themes.Apply(AppearanceTheme.Light);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        var lightBackground = RequireBrush(composerTextBox.Background, "light composer background").Color;
        Assert(lightBackground != darkBackground, "dynamic theme parity");
        themes.Apply(AppearanceTheme.Dark);

        composer.SubmitCommand.Execute(null);
        PumpUntil(() => composer.HasValidationMessage && composer.CanSubmit, "production preflight rejection settles");
        Assert(conversation.Messages.Count == 0, "failed execution preflight does not add a user message");
        Assert(composer.HasDraftContent && composer.Attachments.Count == 1 && composer.ContextReferences.Count == 1, "rejected production submission preserves draft context");

        composer.Clear();
        Assert(!composer.HasDraftContent, "rejected submission can be cleared after settling");

        var tooLongRejected = false;
        try
        {
            composer.DraftText = new string('x', ComposerState.MaxDraftLength + 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            tooLongRejected = true;
        }
        Assert(tooLongRejected, "programmatic over-limit draft rejected");

        window.Close();
        Console.WriteLine("Runtime conversation-composer happy/negative/recovery/downstream-preflight fixture: PASS.");
    }

    private static void PumpUntil(Func<bool> condition, string label)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        Assert(condition(), label);
    }

    private static SolidColorBrush RequireBrush(Brush? brush, string label) =>
        brush as SolidColorBrush
        ?? throw new InvalidOperationException($"Expected SolidColorBrush for {label}.");

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Conversation-composer assertion failed: {label}");
        }
    }
}
'@
        $program = $programTemplate.Replace('__FIXTURE_PATH__', $fixturePathLiteral)

        Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8NoBOM
        Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

        & dotnet run --project $projectPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Runtime conversation-composer fixture failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$composerStatePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ComposerState.cs'
$composerXamlPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationComposer.xaml'
$composerCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationComposer.xaml.cs'
$conversationSurfacePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationSurface.xaml'
$conversationSurfaceCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\Conversation\ConversationSurface.xaml.cs'
$mainWindowPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml'
$mainWindowCodePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\MainWindow.xaml.cs'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'

foreach ($path in @($composerStatePath, $composerXamlPath, $composerCodePath, $conversationSurfacePath, $conversationSurfaceCodePath, $mainWindowPath, $mainWindowCodePath, $appProjectPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required conversation-composer path is missing: $path"
    }
}

$composerStateText = Get-Content -LiteralPath $composerStatePath -Raw
$composerXamlText = Get-Content -LiteralPath $composerXamlPath -Raw
$composerCodeText = Get-Content -LiteralPath $composerCodePath -Raw
$conversationSurfaceText = Get-Content -LiteralPath $conversationSurfacePath -Raw
$conversationSurfaceCodeText = Get-Content -LiteralPath $conversationSurfaceCodePath -Raw
$mainWindowText = Get-Content -LiteralPath $mainWindowPath -Raw
$mainWindowCodeText = Get-Content -LiteralPath $mainWindowCodePath -Raw

$contractArguments = @(
    $composerStateText,
    $composerXamlText,
    $composerCodeText,
    $conversationSurfaceText,
    $conversationSurfaceCodeText,
    $mainWindowText,
    $mainWindowCodeText
)
Assert-ComposerContract @contractArguments
Write-Host 'Static conversation-composer validation: PASS.'

if ($RunFixtures) {
    Assert-ContractRejects {
        $mutated = $composerStateText.Replace('public const int MaxAttachments = 8', 'public const int RemovedAttachmentLimit = 8')
        Assert-ComposerContract $mutated $composerXamlText $composerCodeText $conversationSurfaceText $conversationSurfaceCodeText $mainWindowText $mainWindowCodeText
    } 'attachment limit removed'

    Assert-ContractRejects {
        $mutated = $composerStateText.Replace('string.Equals(item.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)', 'false')
        Assert-ComposerContract $mutated $composerXamlText $composerCodeText $conversationSurfaceText $conversationSurfaceCodeText $mainWindowText $mainWindowCodeText
    } 'attachment deduplication removed'

    Assert-ContractRejects {
        $mutated = $composerXamlText.Replace('x:Name="ComposerTextBox"', 'x:Name="RemovedComposerTextBox"')
        Assert-ComposerContract $composerStateText $mutated $composerCodeText $conversationSurfaceText $conversationSurfaceCodeText $mainWindowText $mainWindowCodeText
    } 'composer textbox removed'

    Assert-ContractRejects {
        $mutated = $conversationSurfaceText.Replace('State="{Binding Composer, ElementName=Root}"', '')
        Assert-ComposerContract $composerStateText $composerXamlText $composerCodeText $mutated $conversationSurfaceCodeText $mainWindowText $mainWindowCodeText
    } 'shared composer state binding removed'

    Assert-ContractRejects {
        $mutated = $mainWindowCodeText.Replace('taskState.ValidateCanStart()', '/* removed task preflight */')
        Assert-ComposerContract $composerStateText $composerXamlText $composerCodeText $conversationSurfaceText $conversationSurfaceCodeText $mainWindowText $mutated
    } 'downstream task preflight removed'

    Assert-ContractRejects {
        $mutated = $mainWindowCodeText.Replace('composerState.AcceptSubmission(e.Submission.SubmissionId)', 'composerState.Clear()')
        Assert-ComposerContract $composerStateText $composerXamlText $composerCodeText $conversationSurfaceText $conversationSurfaceCodeText $mainWindowText $mutated
    } 'submission identity acknowledgement removed'

    Assert-ContractRejects {
        $mutated = $mainWindowCodeText.Replace('composerState.RejectSubmission(e.Submission.SubmissionId, exception.Message)', 'composerState.Clear()')
        Assert-ComposerContract $composerStateText $composerXamlText $composerCodeText $conversationSurfaceText $conversationSurfaceCodeText $mainWindowText $mutated
    } 'submission rejection identity path removed'

    Assert-ContractRejects {
        $mutated = $composerXamlText.Replace('{DynamicResource FccBrushSurfaceRaised}', '#112233')
        Assert-ComposerContract $composerStateText $mutated $composerCodeText $conversationSurfaceText $conversationSurfaceCodeText $mainWindowText $mainWindowCodeText
    } 'hard-coded composer color'

    Assert-ComposerContract @contractArguments
    Write-Host 'Conversation-composer recovery fixture: PASS.'
    Write-Host 'Deterministic conversation-composer negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    Invoke-ComposerRuntimeFixture $appProjectPath
}
