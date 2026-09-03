[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RunFixtures,
    [switch]$RequireRuntime
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$XamlNamespace = 'http://schemas.microsoft.com/winfx/2006/xaml'
$RequiredColorKeys = @(
    'FccColorCanvas', 'FccColorSurface', 'FccColorSurfaceRaised', 'FccColorSurfaceSubtle',
    'FccColorTextPrimary', 'FccColorTextSecondary', 'FccColorTextMuted', 'FccColorTextDisabled', 'FccColorTextInverse',
    'FccColorBorder', 'FccColorDivider',
    'FccColorAccent', 'FccColorAccentHover', 'FccColorAccentPressed', 'FccColorAccentForeground',
    'FccColorFocus', 'FccColorSelectionBackground', 'FccColorSelectionForeground',
    'FccColorHoverOverlay', 'FccColorPressedOverlay', 'FccColorDisabledOverlay',
    'FccColorSuccess', 'FccColorSuccessBackground', 'FccColorWarning', 'FccColorWarningBackground',
    'FccColorError', 'FccColorErrorBackground', 'FccColorInfo', 'FccColorInfoBackground'
)
$RequiredBrushKeys = @($RequiredColorKeys | ForEach-Object { $_ -replace '^FccColor', 'FccBrush' })
$OverlayColorKeys = @('FccColorHoverOverlay', 'FccColorPressedOverlay', 'FccColorDisabledOverlay')

function Read-XamlDocument {
    param([string]$Path, [string]$Label)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label is missing: $Path"
    }

    try {
        return [xml](Get-Content -LiteralPath $Path -Raw)
    }
    catch {
        throw "$Label is not valid XML/XAML: $($_.Exception.Message)"
    }
}

function Get-KeyedResources {
    param([xml]$Document, [string]$Label)

    $resources = @{}
    foreach ($node in $Document.SelectNodes('//*')) {
        if ($node -isnot [System.Xml.XmlElement]) {
            continue
        }

        $key = $node.GetAttribute('Key', $XamlNamespace)
        if ([string]::IsNullOrWhiteSpace($key)) {
            continue
        }
        if ($resources.ContainsKey($key)) {
            throw "$Label contains duplicate x:Key '$key'."
        }

        $resources[$key] = $node
    }

    return $resources
}

function ConvertFrom-HexColor {
    param([string]$Value, [string]$Label)

    if ($Value -notmatch '^#(?<hex>[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$') {
        throw "$Label must use #RRGGBB or #AARRGGBB but is '$Value'."
    }

    $hex = $Matches.hex
    if ($hex.Length -eq 6) {
        return @{
            A = 255
            R = [Convert]::ToInt32($hex.Substring(0, 2), 16)
            G = [Convert]::ToInt32($hex.Substring(2, 2), 16)
            B = [Convert]::ToInt32($hex.Substring(4, 2), 16)
        }
    }

    return @{
        A = [Convert]::ToInt32($hex.Substring(0, 2), 16)
        R = [Convert]::ToInt32($hex.Substring(2, 2), 16)
        G = [Convert]::ToInt32($hex.Substring(4, 2), 16)
        B = [Convert]::ToInt32($hex.Substring(6, 2), 16)
    }
}

function Get-RelativeLuminance {
    param([hashtable]$Color)

    $linear = foreach ($channel in @($Color.R, $Color.G, $Color.B)) {
        $normalized = $channel / 255.0
        if ($normalized -le 0.04045) {
            $normalized / 12.92
        }
        else {
            [Math]::Pow(($normalized + 0.055) / 1.055, 2.4)
        }
    }

    return (0.2126 * $linear[0]) + (0.7152 * $linear[1]) + (0.0722 * $linear[2])
}

function Get-ContrastRatio {
    param([hashtable]$Foreground, [hashtable]$Background)

    if ($Foreground.A -ne 255 -or $Background.A -ne 255) {
        throw 'Contrast assertions require opaque colors.'
    }

    $foregroundLuminance = Get-RelativeLuminance $Foreground
    $backgroundLuminance = Get-RelativeLuminance $Background
    $lighter = [Math]::Max($foregroundLuminance, $backgroundLuminance)
    $darker = [Math]::Min($foregroundLuminance, $backgroundLuminance)
    return ($lighter + 0.05) / ($darker + 0.05)
}

function Assert-Contrast {
    param(
        [hashtable]$Resources,
        [string]$ForegroundKey,
        [string]$BackgroundKey,
        [double]$Minimum,
        [string]$Label
    )

    $foreground = ConvertFrom-HexColor $Resources[$ForegroundKey].InnerText.Trim() "$Label $ForegroundKey"
    $background = ConvertFrom-HexColor $Resources[$BackgroundKey].InnerText.Trim() "$Label $BackgroundKey"
    $ratio = Get-ContrastRatio $foreground $background
    if ($ratio -lt $Minimum) {
        throw "$Label contrast $ForegroundKey on $BackgroundKey is $([Math]::Round($ratio, 2)):1; required >= $Minimum`:1."
    }
}

function Assert-ThemeContract {
    param([string]$Path, [string]$ExpectedThemeName)

    $label = "$ExpectedThemeName theme"
    $resources = Get-KeyedResources (Read-XamlDocument $Path $label) $label

    if (-not $resources.ContainsKey('FccThemeName')) {
        throw "$label is missing FccThemeName."
    }
    if ($resources['FccThemeName'].LocalName -ne 'String' -or $resources['FccThemeName'].InnerText.Trim() -ne $ExpectedThemeName) {
        throw "$label must identify itself with FccThemeName='$ExpectedThemeName'."
    }

    foreach ($key in $RequiredColorKeys) {
        if (-not $resources.ContainsKey($key) -or $resources[$key].LocalName -ne 'Color') {
            throw "$label is missing required Color resource '$key'."
        }

        $parsed = ConvertFrom-HexColor $resources[$key].InnerText.Trim() "$label $key"
        if ($OverlayColorKeys -contains $key) {
            if ($parsed.A -le 0 -or $parsed.A -ge 255) {
                throw "$label overlay '$key' must use bounded translucency."
            }
        }
        elseif ($parsed.A -ne 255) {
            throw "$label semantic color '$key' must be opaque."
        }
    }

    foreach ($key in $RequiredBrushKeys) {
        if (-not $resources.ContainsKey($key) -or $resources[$key].LocalName -ne 'SolidColorBrush') {
            throw "$label is missing required SolidColorBrush resource '$key'."
        }

        $expectedColorKey = $key -replace '^FccBrush', 'FccColor'
        $expectedReference = "{StaticResource $expectedColorKey}"
        if ($resources[$key].GetAttribute('Color') -ne $expectedReference) {
            throw "$label brush '$key' must reference '$expectedReference'."
        }
    }

    $allowedKeys = @('FccThemeName') + $RequiredColorKeys + $RequiredBrushKeys
    $unexpectedKeys = @($resources.Keys | Where-Object { $allowedKeys -notcontains $_ })
    if ($unexpectedKeys.Count -gt 0) {
        throw "$label contains uncontracted resources: $($unexpectedKeys -join ', ')."
    }

    Assert-Contrast $resources 'FccColorTextPrimary' 'FccColorCanvas' 4.5 $label
    Assert-Contrast $resources 'FccColorTextSecondary' 'FccColorCanvas' 4.5 $label
    Assert-Contrast $resources 'FccColorAccentForeground' 'FccColorAccent' 4.5 $label
    Assert-Contrast $resources 'FccColorSelectionForeground' 'FccColorSelectionBackground' 4.5 $label
    Assert-Contrast $resources 'FccColorSuccess' 'FccColorSuccessBackground' 4.5 $label
    Assert-Contrast $resources 'FccColorWarning' 'FccColorWarningBackground' 4.5 $label
    Assert-Contrast $resources 'FccColorError' 'FccColorErrorBackground' 4.5 $label
    Assert-Contrast $resources 'FccColorInfo' 'FccColorInfoBackground' 4.5 $label
    Assert-Contrast $resources 'FccColorFocus' 'FccColorCanvas' 3.0 $label

    return $resources
}

function Assert-ThemePairContract {
    param([string]$DarkPath, [string]$LightPath, [string]$AppPath, [string]$ServicePath)

    $darkResources = Assert-ThemeContract $DarkPath 'Dark'
    $lightResources = Assert-ThemeContract $LightPath 'Light'

    $darkKeys = @($darkResources.Keys | Sort-Object)
    $lightKeys = @($lightResources.Keys | Sort-Object)
    if (($darkKeys -join "`n") -ne ($lightKeys -join "`n")) {
        throw 'Dark and light themes must expose identical semantic resource keys.'
    }

    foreach ($key in @('FccColorCanvas', 'FccColorSurface', 'FccColorTextPrimary', 'FccColorTextSecondary', 'FccColorAccent', 'FccColorFocus', 'FccColorSelectionBackground')) {
        if ($darkResources[$key].InnerText.Trim() -eq $lightResources[$key].InnerText.Trim()) {
            throw "Dark/light semantic resource '$key' must not collapse to the same color."
        }
    }

    $darkCanvas = ConvertFrom-HexColor $darkResources['FccColorCanvas'].InnerText.Trim() 'Dark canvas'
    $lightCanvas = ConvertFrom-HexColor $lightResources['FccColorCanvas'].InnerText.Trim() 'Light canvas'
    if ((Get-RelativeLuminance $darkCanvas) -ge 0.12) {
        throw 'Dark canvas luminance is too high for the dark appearance contract.'
    }
    if ((Get-RelativeLuminance $lightCanvas) -le 0.80) {
        throw 'Light canvas luminance is too low for the light appearance contract.'
    }

    $appDocument = Read-XamlDocument $AppPath 'App.xaml'
    $sources = @(
        $appDocument.SelectNodes("//*[local-name()='ResourceDictionary' and @Source]") |
            ForEach-Object { $_.GetAttribute('Source') }
    )
    $tokensIndex = [Array]::IndexOf($sources, 'DesignSystem/DesignTokens.xaml')
    $typographyIndex = [Array]::IndexOf($sources, 'DesignSystem/Typography.xaml')
    $darkIndex = [Array]::IndexOf($sources, 'DesignSystem/Themes/Theme.Dark.xaml')
    $lightIndex = [Array]::IndexOf($sources, 'DesignSystem/Themes/Theme.Light.xaml')

    if ($tokensIndex -lt 0 -or $typographyIndex -lt 0 -or $darkIndex -lt 0) {
        throw 'App.xaml must compose design tokens, typography, and the default dark theme.'
    }
    if (-not ($tokensIndex -lt $typographyIndex -and $typographyIndex -lt $darkIndex)) {
        throw 'App.xaml resource order must be DesignTokens -> Typography -> default Dark theme.'
    }
    if ($lightIndex -ge 0) {
        throw 'App.xaml must load exactly one default appearance theme; light is switched at runtime.'
    }

    if (-not (Test-Path -LiteralPath $ServicePath)) {
        throw "ThemeService is missing: $ServicePath"
    }

    $serviceText = Get-Content -LiteralPath $ServicePath -Raw
    foreach ($requiredLiteral in @(
        '/FCCCodeDesktop.App;component/DesignSystem/Themes/Theme.Dark.xaml',
        '/FCCCodeDesktop.App;component/DesignSystem/Themes/Theme.Light.xaml',
        'public AppearanceTheme? CurrentTheme',
        'public void Apply(AppearanceTheme theme)',
        'public bool TryApply(AppearanceTheme theme, out Exception? error)',
        'var candidate = new ResourceDictionary',
        'ValidateCandidate(candidate, theme);',
        'mergedDictionaries.Insert(insertionIndex, candidate);',
        'mergedDictionaries.Remove(existingTheme);',
        'mergedDictionaries.Remove(candidate);',
        'const string componentMarker = ";component/";',
        'return false;'
    )) {
        if (-not $serviceText.Contains($requiredLiteral)) {
            throw "ThemeService is missing required safe-switch contract text: $requiredLiteral"
        }
    }

    $candidateIndex = $serviceText.IndexOf('var candidate = new ResourceDictionary', [StringComparison]::Ordinal)
    $insertIndex = $serviceText.IndexOf('mergedDictionaries.Insert(insertionIndex, candidate);', [StringComparison]::Ordinal)
    $removeExistingIndex = $serviceText.IndexOf('mergedDictionaries.Remove(existingTheme);', [StringComparison]::Ordinal)
    if (-not ($candidateIndex -lt $insertIndex -and $insertIndex -lt $removeExistingIndex)) {
        throw 'ThemeService must construct and validate the candidate before replacing the current theme.'
    }
}

function Assert-ContractRejects {
    param([scriptblock]$Action, [string]$Label)

    $rejected = $false
    try {
        & $Action
    }
    catch {
        $rejected = $true
        Write-Host "Negative fixture rejected as expected: $Label"
    }

    if (-not $rejected) {
        throw "Negative semantic-theme fixture was not rejected: $Label"
    }
}

function Replace-RequiredLiteral {
    param([string]$Path, [string]$OldValue, [string]$NewValue)

    $text = Get-Content -LiteralPath $Path -Raw
    if (-not $text.Contains($OldValue)) {
        throw "Fixture setup could not find required literal '$OldValue' in $Path"
    }

    Set-Content -LiteralPath $Path -Value ($text.Replace($OldValue, $NewValue)) -Encoding utf8NoBOM
}

function Invoke-RuntimeThemeFixture {
    param([string]$Root, [string]$AppProjectPath)

    if (-not $IsWindows) {
        throw 'Runtime semantic-theme fixture requires Windows/WPF.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet is required for the runtime semantic-theme fixture.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Runtime semantic-theme fixture requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    $fixtureProjectPath = Join-Path $Root 'ThemeRuntimeFixture.csproj'
    $programPath = Join-Path $Root 'Program.cs'
    $escapedProjectReference = [Security.SecurityElement]::Escape($AppProjectPath)

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
    <ProjectReference Include="$escapedProjectReference" />
  </ItemGroup>
</Project>
"@

    $program = @'
using System.Windows;
using FCCCodeDesktop.App.DesignSystem;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var root = new ResourceDictionary();
        root.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/FCCCodeDesktop.App;component/DesignSystem/Themes/Theme.Dark.xaml", UriKind.Relative),
        });

        var service = new ThemeService(root);
        Assert(service.CurrentTheme == AppearanceTheme.Dark, "default dark detection");

        service.Apply(AppearanceTheme.Light);
        Assert(service.CurrentTheme == AppearanceTheme.Light, "dark to light switch");
        Assert(root.MergedDictionaries.Count == 1, "single theme after switch");
        Assert((string)root["FccThemeName"] == "Light", "light resources active");

        service.Apply(AppearanceTheme.Light);
        Assert(root.MergedDictionaries.Count == 1, "idempotent light apply");

        var beforeFailure = service.CurrentTheme;
        var invalidAccepted = service.TryApply((AppearanceTheme)999, out var error);
        Assert(!invalidAccepted, "invalid theme rejected");
        Assert(error is ArgumentOutOfRangeException, "invalid theme classified");
        Assert(service.CurrentTheme == beforeFailure, "failed switch preserves current theme");
        Assert((string)root["FccThemeName"] == "Light", "failed switch preserves resources");

        service.Apply(AppearanceTheme.Dark);
        Assert(service.CurrentTheme == AppearanceTheme.Dark, "light to dark recovery");
        Assert(root.MergedDictionaries.Count == 1, "single theme after recovery");
        Assert((string)root["FccThemeName"] == "Dark", "dark resources restored");

        Console.WriteLine("Runtime semantic-theme happy/negative/recovery fixture: PASS.");
    }

    private static void Assert(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Theme runtime assertion failed: {label}");
        }
    }
}
'@

    Set-Content -LiteralPath $fixtureProjectPath -Value $project -Encoding utf8NoBOM
    Set-Content -LiteralPath $programPath -Value $program -Encoding utf8NoBOM

    & dotnet run --project $fixtureProjectPath -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Runtime semantic-theme fixture failed with exit code $LASTEXITCODE."
    }
}

$appPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\App.xaml'
$appProjectPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\FCCCodeDesktop.App.csproj'
$darkPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\DesignSystem\Themes\Theme.Dark.xaml'
$lightPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\DesignSystem\Themes\Theme.Light.xaml'
$servicePath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\DesignSystem\ThemeService.cs'

Assert-ThemePairContract $darkPath $lightPath $appPath $servicePath
Write-Host 'Static dark/light semantic-theme validation: PASS.'

if ($RunFixtures) {
    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("fccd-semantic-theme-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    $fixtureAppPath = Join-Path $fixtureRoot 'App.xaml'
    $fixtureDarkPath = Join-Path $fixtureRoot 'Theme.Dark.xaml'
    $fixtureLightPath = Join-Path $fixtureRoot 'Theme.Light.xaml'
    $fixtureServicePath = Join-Path $fixtureRoot 'ThemeService.cs'

    function Reset-Fixture {
        Copy-Item -LiteralPath $appPath -Destination $fixtureAppPath -Force
        Copy-Item -LiteralPath $darkPath -Destination $fixtureDarkPath -Force
        Copy-Item -LiteralPath $lightPath -Destination $fixtureLightPath -Force
        Copy-Item -LiteralPath $servicePath -Destination $fixtureServicePath -Force
    }

    try {
        Reset-Fixture
        Replace-RequiredLiteral $fixtureLightPath '<Color x:Key="FccColorBorder">#D5DBE3</Color>' ''
        Assert-ContractRejects { Assert-ThemePairContract $fixtureDarkPath $fixtureLightPath $fixtureAppPath $fixtureServicePath } 'missing semantic resource'

        Reset-Fixture
        Replace-RequiredLiteral $fixtureDarkPath 'Color="{StaticResource FccColorAccent}"' 'Color="{StaticResource FccColorError}"'
        Assert-ContractRejects { Assert-ThemePairContract $fixtureDarkPath $fixtureLightPath $fixtureAppPath $fixtureServicePath } 'brush semantic mismatch'

        Reset-Fixture
        Replace-RequiredLiteral $fixtureLightPath '<Color x:Key="FccColorTextPrimary">#18202A</Color>' '<Color x:Key="FccColorTextPrimary">#A8AFB8</Color>'
        Assert-ContractRejects { Assert-ThemePairContract $fixtureDarkPath $fixtureLightPath $fixtureAppPath $fixtureServicePath } 'insufficient primary text contrast'

        Reset-Fixture
        Replace-RequiredLiteral $fixtureLightPath '<sys:String x:Key="FccThemeName">Light</sys:String>' '<sys:String x:Key="FccThemeName">Dark</sys:String>'
        Assert-ContractRejects { Assert-ThemePairContract $fixtureDarkPath $fixtureLightPath $fixtureAppPath $fixtureServicePath } 'wrong theme identity'

        Reset-Fixture
        Replace-RequiredLiteral $fixtureAppPath '<ResourceDictionary Source="DesignSystem/Themes/Theme.Dark.xaml" />' '<ResourceDictionary Source="DesignSystem/Themes/Theme.Light.xaml" />'
        Assert-ContractRejects { Assert-ThemePairContract $fixtureDarkPath $fixtureLightPath $fixtureAppPath $fixtureServicePath } 'default theme composition regression'

        Reset-Fixture
        Replace-RequiredLiteral $fixtureServicePath 'mergedDictionaries.Remove(candidate);' '// rollback removed'
        Assert-ContractRejects { Assert-ThemePairContract $fixtureDarkPath $fixtureLightPath $fixtureAppPath $fixtureServicePath } 'runtime rollback contract removed'

        Reset-Fixture
        Assert-ThemePairContract $fixtureDarkPath $fixtureLightPath $fixtureAppPath $fixtureServicePath
        Write-Host 'Semantic-theme recovery fixture: PASS.'
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host 'Deterministic semantic-theme negative/recovery fixtures: PASS.'
}

if ($RequireRuntime) {
    $runtimeRoot = Join-Path ([IO.Path]::GetTempPath()) ("fccd-theme-runtime-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runtimeRoot | Out-Null
    try {
        Invoke-RuntimeThemeFixture $runtimeRoot $appProjectPath
    }
    finally {
        Remove-Item -LiteralPath $runtimeRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
