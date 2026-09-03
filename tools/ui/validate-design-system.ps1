[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RunFixtures
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$XamlNamespace = 'http://schemas.microsoft.com/winfx/2006/xaml'

function Read-XamlDocument {
    param(
        [string]$Path,
        [string]$Label
    )

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
    param(
        [xml]$Document,
        [string]$Label
    )

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

function Assert-ResourceValue {
    param(
        [hashtable]$Resources,
        [string]$Key,
        [string]$ExpectedType,
        [string]$ExpectedValue,
        [string]$Label
    )

    if (-not $Resources.ContainsKey($Key)) {
        throw "$Label is missing required resource '$Key'."
    }

    $resource = $Resources[$Key]
    if ($resource.LocalName -ne $ExpectedType) {
        throw "$Label resource '$Key' must be '$ExpectedType' but is '$($resource.LocalName)'."
    }

    $actualValue = $resource.InnerText.Trim()
    if ($actualValue -ne $ExpectedValue) {
        throw "$Label resource '$Key' must be '$ExpectedValue' but is '$actualValue'."
    }
}

function Assert-StyleContract {
    param(
        [hashtable]$Resources,
        [string]$StyleKey,
        [bool]$RequireBaseStyle
    )

    if (-not $Resources.ContainsKey($StyleKey)) {
        throw "Typography is missing required style '$StyleKey'."
    }

    $style = $Resources[$StyleKey]
    if ($style.LocalName -ne 'Style') {
        throw "Typography resource '$StyleKey' must be a Style."
    }

    if ($style.GetAttribute('TargetType') -ne '{x:Type TextBlock}') {
        throw "Typography style '$StyleKey' must target TextBlock."
    }

    if ($RequireBaseStyle -and $style.GetAttribute('BasedOn') -ne '{StaticResource FccTextBase}') {
        throw "Typography style '$StyleKey' must be based on FccTextBase."
    }
}

function Assert-StyleSetter {
    param(
        [hashtable]$Resources,
        [string]$StyleKey,
        [string]$Property,
        [string]$ExpectedValue
    )

    $style = $Resources[$StyleKey]
    $matches = @(
        $style.SelectNodes("./*[local-name()='Setter']") |
            Where-Object { $_.GetAttribute('Property') -eq $Property }
    )

    if ($matches.Count -ne 1) {
        throw "Typography style '$StyleKey' must contain exactly one '$Property' setter."
    }

    $actualValue = $matches[0].GetAttribute('Value')
    if ($actualValue -ne $ExpectedValue) {
        throw "Typography style '$StyleKey' setter '$Property' must be '$ExpectedValue' but is '$actualValue'."
    }
}

function Test-DesignSystemContract {
    param(
        [string]$AppPath,
        [string]$TokensPath,
        [string]$TypographyPath
    )

    $appDocument = Read-XamlDocument $AppPath 'App.xaml'
    $tokensDocument = Read-XamlDocument $TokensPath 'DesignTokens.xaml'
    $typographyDocument = Read-XamlDocument $TypographyPath 'Typography.xaml'

    $tokenResources = Get-KeyedResources $tokensDocument 'DesignTokens.xaml'
    $typographyResources = Get-KeyedResources $typographyDocument 'Typography.xaml'

    foreach ($key in $tokenResources.Keys) {
        if ($typographyResources.ContainsKey($key)) {
            throw "Design-system resource key '$key' is duplicated across token dictionaries."
        }
    }

    $sources = @(
        $appDocument.SelectNodes("//*[local-name()='ResourceDictionary' and @Source]") |
            ForEach-Object { $_.GetAttribute('Source') }
    )

    $tokensIndex = -1
    $typographyIndex = -1
    for ($index = 0; $index -lt $sources.Count; $index++) {
        if ($sources[$index] -eq 'DesignSystem/DesignTokens.xaml') {
            $tokensIndex = $index
        }
        if ($sources[$index] -eq 'DesignSystem/Typography.xaml') {
            $typographyIndex = $index
        }
    }

    if ($tokensIndex -lt 0 -or $typographyIndex -lt 0) {
        throw 'App.xaml must merge DesignTokens.xaml and Typography.xaml.'
    }
    if ($tokensIndex -ge $typographyIndex) {
        throw 'App.xaml must merge DesignTokens.xaml before Typography.xaml.'
    }

    $expectedTokenValues = [ordered]@{
        FccSpace0 = @('Double', '0')
        FccSpace2 = @('Double', '2')
        FccSpace4 = @('Double', '4')
        FccSpace6 = @('Double', '6')
        FccSpace8 = @('Double', '8')
        FccSpace12 = @('Double', '12')
        FccSpace16 = @('Double', '16')
        FccSpace20 = @('Double', '20')
        FccSpace24 = @('Double', '24')
        FccSpace32 = @('Double', '32')
        FccSpace40 = @('Double', '40')
        FccSpace48 = @('Double', '48')
        FccInset2 = @('Thickness', '2')
        FccInset4 = @('Thickness', '4')
        FccInset6 = @('Thickness', '6')
        FccInset8 = @('Thickness', '8')
        FccInset12 = @('Thickness', '12')
        FccInset16 = @('Thickness', '16')
        FccInset20 = @('Thickness', '20')
        FccInset24 = @('Thickness', '24')
        FccRadiusNone = @('CornerRadius', '0')
        FccRadiusSmall = @('CornerRadius', '4')
        FccRadiusMedium = @('CornerRadius', '6')
        FccRadiusLarge = @('CornerRadius', '10')
        FccRadiusXLarge = @('CornerRadius', '14')
        FccStrokeThin = @('Double', '1')
        FccFocusRingThickness = @('Thickness', '2')
        FccControlHeightCompact = @('Double', '28')
        FccControlHeightStandard = @('Double', '32')
        FccControlHeightComfortable = @('Double', '36')
        FccIconSizeSmall = @('Double', '14')
        FccIconSizeMedium = @('Double', '16')
        FccIconSizeLarge = @('Double', '20')
    }

    foreach ($entry in $expectedTokenValues.GetEnumerator()) {
        Assert-ResourceValue $tokenResources $entry.Key $entry.Value[0] $entry.Value[1] 'DesignTokens.xaml'
    }

    $expectedTypographyValues = [ordered]@{
        FccFontFamilyInterface = @('FontFamily', 'Segoe UI')
        FccFontFamilyCode = @('FontFamily', 'Consolas')
        FccFontSizeDisplay = @('Double', '22')
        FccFontSizeSection = @('Double', '15')
        FccFontSizeBody = @('Double', '13')
        FccFontSizeMetadata = @('Double', '12')
        FccFontSizeStatus = @('Double', '11')
        FccFontSizeCode = @('Double', '13')
        FccLineHeightDisplay = @('Double', '30')
        FccLineHeightSection = @('Double', '22')
        FccLineHeightBody = @('Double', '19')
        FccLineHeightMetadata = @('Double', '18')
        FccLineHeightStatus = @('Double', '16')
        FccLineHeightCode = @('Double', '19')
    }

    foreach ($entry in $expectedTypographyValues.GetEnumerator()) {
        Assert-ResourceValue $typographyResources $entry.Key $entry.Value[0] $entry.Value[1] 'Typography.xaml'
    }

    $tokensText = Get-Content -LiteralPath $TokensPath -Raw
    $typographyText = Get-Content -LiteralPath $TypographyPath -Raw
    $themeLeakPattern = '(?i)<\s*(Color|SolidColorBrush|LinearGradientBrush|RadialGradientBrush|GradientStop)\b|#[0-9a-f]{6,8}\b'
    if ($tokensText -match $themeLeakPattern -or $typographyText -match $themeLeakPattern) {
        throw 'P02-001 design resources must remain theme-neutral; color/brush values belong to P02-002.'
    }

    if ($typographyText -match '(?i)\.(ttf|otf|woff2?)\b|pack://application:,,,/.*fonts') {
        throw 'Typography must not depend on a bundled or external font asset in P02-001.'
    }

    foreach ($styleKey in @('FccTextBase', 'FccTextDisplay', 'FccTextSection', 'FccTextBody', 'FccTextMetadata', 'FccTextStatus', 'FccTextCode')) {
        Assert-StyleContract $typographyResources $styleKey ($styleKey -ne 'FccTextBase')
    }

    Assert-StyleSetter $typographyResources 'FccTextBase' 'FontFamily' '{StaticResource FccFontFamilyInterface}'
    Assert-StyleSetter $typographyResources 'FccTextBase' 'FontSize' '{StaticResource FccFontSizeBody}'
    Assert-StyleSetter $typographyResources 'FccTextBase' 'LineHeight' '{StaticResource FccLineHeightBody}'
    Assert-StyleSetter $typographyResources 'FccTextBase' 'FontWeight' 'Normal'
    Assert-StyleSetter $typographyResources 'FccTextBase' 'TextOptions.TextFormattingMode' 'Display'

    Assert-StyleSetter $typographyResources 'FccTextDisplay' 'FontSize' '{StaticResource FccFontSizeDisplay}'
    Assert-StyleSetter $typographyResources 'FccTextDisplay' 'LineHeight' '{StaticResource FccLineHeightDisplay}'
    Assert-StyleSetter $typographyResources 'FccTextDisplay' 'FontWeight' 'SemiBold'

    Assert-StyleSetter $typographyResources 'FccTextSection' 'FontSize' '{StaticResource FccFontSizeSection}'
    Assert-StyleSetter $typographyResources 'FccTextSection' 'LineHeight' '{StaticResource FccLineHeightSection}'
    Assert-StyleSetter $typographyResources 'FccTextSection' 'FontWeight' 'SemiBold'

    Assert-StyleSetter $typographyResources 'FccTextMetadata' 'FontSize' '{StaticResource FccFontSizeMetadata}'
    Assert-StyleSetter $typographyResources 'FccTextMetadata' 'LineHeight' '{StaticResource FccLineHeightMetadata}'

    Assert-StyleSetter $typographyResources 'FccTextStatus' 'FontSize' '{StaticResource FccFontSizeStatus}'
    Assert-StyleSetter $typographyResources 'FccTextStatus' 'LineHeight' '{StaticResource FccLineHeightStatus}'
    Assert-StyleSetter $typographyResources 'FccTextStatus' 'FontWeight' 'SemiBold'

    Assert-StyleSetter $typographyResources 'FccTextCode' 'FontFamily' '{StaticResource FccFontFamilyCode}'
    Assert-StyleSetter $typographyResources 'FccTextCode' 'FontSize' '{StaticResource FccFontSizeCode}'
    Assert-StyleSetter $typographyResources 'FccTextCode' 'LineHeight' '{StaticResource FccLineHeightCode}'

    $displaySize = [double]::Parse($typographyResources['FccFontSizeDisplay'].InnerText, [Globalization.CultureInfo]::InvariantCulture)
    if ($displaySize -gt 24) {
        throw 'Workbench display typography must remain compact; marketing-scale typography is not allowed in P02.'
    }

    foreach ($role in @('Display', 'Section', 'Body', 'Metadata', 'Status', 'Code')) {
        $fontSize = [double]::Parse($typographyResources["FccFontSize$role"].InnerText, [Globalization.CultureInfo]::InvariantCulture)
        $lineHeight = [double]::Parse($typographyResources["FccLineHeight$role"].InnerText, [Globalization.CultureInfo]::InvariantCulture)
        if ($lineHeight -le $fontSize) {
            throw "Typography role '$role' must have line height greater than font size."
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
        throw "Negative design-system fixture was not rejected: $Label"
    }
}

function Replace-RequiredLiteral {
    param(
        [string]$Path,
        [string]$OldValue,
        [string]$NewValue
    )

    $text = Get-Content -LiteralPath $Path -Raw
    if (-not $text.Contains($OldValue)) {
        throw "Fixture setup could not find required literal '$OldValue' in $Path"
    }

    Set-Content -LiteralPath $Path -Value ($text.Replace($OldValue, $NewValue)) -Encoding utf8NoBOM
}

$appPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\App.xaml'
$tokensPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\DesignSystem\DesignTokens.xaml'
$typographyPath = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.App\DesignSystem\Typography.xaml'

Test-DesignSystemContract $appPath $tokensPath $typographyPath
Write-Host 'Static design-token and typography validation: PASS.'

if ($RunFixtures) {
    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("fccd-design-system-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null

    $fixtureAppPath = Join-Path $fixtureRoot 'App.xaml'
    $fixtureTokensPath = Join-Path $fixtureRoot 'DesignTokens.xaml'
    $fixtureTypographyPath = Join-Path $fixtureRoot 'Typography.xaml'

    function Reset-Fixture {
        Copy-Item -LiteralPath $appPath -Destination $fixtureAppPath -Force
        Copy-Item -LiteralPath $tokensPath -Destination $fixtureTokensPath -Force
        Copy-Item -LiteralPath $typographyPath -Destination $fixtureTypographyPath -Force
    }

    try {
        Reset-Fixture
        Replace-RequiredLiteral $fixtureTokensPath '<sys:Double x:Key="FccSpace8">8</sys:Double>' ''
        Assert-ContractRejects { Test-DesignSystemContract $fixtureAppPath $fixtureTokensPath $fixtureTypographyPath } 'missing spacing token'

        Reset-Fixture
        Replace-RequiredLiteral $fixtureTokensPath '</ResourceDictionary>' "    <SolidColorBrush x:Key=`"FixtureBrush`" Color=`"#FFFFFFFF`" />`n</ResourceDictionary>"
        Assert-ContractRejects { Test-DesignSystemContract $fixtureAppPath $fixtureTokensPath $fixtureTypographyPath } 'theme color leakage into P02-001'

        Reset-Fixture
        Replace-RequiredLiteral $fixtureAppPath 'DesignSystem/DesignTokens.xaml' '__FCC_SWAP__'
        Replace-RequiredLiteral $fixtureAppPath 'DesignSystem/Typography.xaml' 'DesignSystem/DesignTokens.xaml'
        Replace-RequiredLiteral $fixtureAppPath '__FCC_SWAP__' 'DesignSystem/Typography.xaml'
        Assert-ContractRejects { Test-DesignSystemContract $fixtureAppPath $fixtureTokensPath $fixtureTypographyPath } 'resource dictionary merge order regression'

        Reset-Fixture
        Replace-RequiredLiteral $fixtureTypographyPath '{StaticResource FccFontSizeDisplay}' '99'
        Assert-ContractRejects { Test-DesignSystemContract $fixtureAppPath $fixtureTokensPath $fixtureTypographyPath } 'hard-coded display font size'

        Reset-Fixture
        Replace-RequiredLiteral $fixtureTokensPath '</ResourceDictionary>' "    <sys:Double x:Key=`"FccSpace8`">9</sys:Double>`n</ResourceDictionary>"
        Assert-ContractRejects { Test-DesignSystemContract $fixtureAppPath $fixtureTokensPath $fixtureTypographyPath } 'duplicate token key'

        Reset-Fixture
        Test-DesignSystemContract $fixtureAppPath $fixtureTokensPath $fixtureTypographyPath
        Write-Host 'Design-system recovery fixture: PASS.'
    }
    finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host 'Deterministic design-system negative/recovery fixtures: PASS.'
}
