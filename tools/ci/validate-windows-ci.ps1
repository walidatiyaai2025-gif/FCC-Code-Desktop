[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$RequireDotNet
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

function Assert-CiPolicy {
    param(
        [string]$WorkflowText,
        [string]$RunnerText
    )

    foreach ($requiredWorkflowText in @(
        'push:',
        'pull_request:',
        '- main',
        'contents: read',
        'runs-on: windows-2025',
        'timeout-minutes: 30',
        'uses: actions/checkout@v7',
        'uses: actions/setup-dotnet@v6',
        'dotnet-version: 10.0.400',
        '.\tools\ci\validate-windows-ci.ps1 -RequireDotNet',
        '.\tools\ci\run-windows-ci.ps1'
    )) {
        Assert-ContainsLiteral $WorkflowText $requiredWorkflowText 'Windows CI workflow'
    }

    if ($WorkflowText.Contains('contents: write')) {
        throw 'Windows CI must not request repository write permission.'
    }
    if ($WorkflowText -match 'runs-on:\s*(ubuntu|macos)') {
        throw 'Canonical P01 CI must not move the Release baseline off Windows.'
    }
    if ($WorkflowText.Contains('continue-on-error: true')) {
        throw 'Windows CI must not downgrade baseline failures with continue-on-error.'
    }

    foreach ($requiredRunnerText in @(
        "if (-not `$IsWindows)",
        "'10.0.400'",
        'dotnet restore $solutionPath --locked-mode --nologo',
        'dotnet format $solutionPath --verify-no-changes --no-restore',
        'dotnet build $solutionPath -c Release --no-restore --nologo',
        '.\tools\testing\run-tests.ps1 -Suite all -Configuration Release -NoRestore -NoBuild',
        '.\tools\build\validate-build-metadata.ps1 -RequireDotNet',
        '.\tools\dependencies\validate-dependency-policy.ps1 -RequireDotNet',
        '.\tools\quality\validate-quality-policy.ps1 -RequireDotNet',
        '.\tools\testing\validate-test-infrastructure.ps1 -RequireDotNet',
        '.\tools\runtime\validate-fcc-environment-discovery.ps1 -RunFixtures -RequireRuntime',
        '.\tools\runtime\validate-fcc-runtime-health-compatibility.ps1 -RunFixtures -RequireRuntime',
        '.\tools\runtime\validate-fcc-structured-runtime.ps1 -RunFixtures -RequireRuntime',
        '.\tools\runtime\validate-fcc-runtime-event-normalization.ps1 -RunFixtures -RequireRuntime',
        '.\tools\runtime\validate-fcc-cli-fallback-runtime.ps1 -RunFixtures -RequireRuntime',
        '.\tools\ui\validate-design-system.ps1 -RunFixtures',
        '.\tools\ui\validate-semantic-themes.ps1 -RunFixtures -RequireRuntime',
        '.\tools\ui\validate-app-chrome.ps1 -RunFixtures -RequireRuntime',
        '.\tools\ui\validate-workspace-layout.ps1 -RunFixtures -RequireRuntime',
        '.\tools\ui\validate-navigation-surfaces.ps1 -RunFixtures -RequireRuntime',
        '.\tools\ui\validate-bottom-tool-panel.ps1 -RunFixtures -RequireRuntime',
        '.\tools\ui\validate-command-palette.ps1 -RunFixtures -RequireRuntime',
        '.\tools\ui\validate-common-states.ps1 -RunFixtures -RequireRuntime',
        '.\tools\ui\validate-dpi-layout.ps1 -RunFixtures -RequireRuntime'
    )) {
        Assert-ContainsLiteral $RunnerText $requiredRunnerText 'Windows CI runner'
    }

    if ($RunnerText.Contains('-p:RestoreLockedMode=false') -or $RunnerText.Contains('--force-evaluate')) {
        throw 'Canonical CI must not regenerate or bypass committed dependency locks.'
    }
}

function Assert-PolicyRejects {
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
    }

    if (-not $rejected) {
        throw "Negative CI policy fixture was not rejected: $Label"
    }
}

$workflowPath = Join-Path $RepositoryRoot '.github\workflows\windows-ci.yml'
$runnerPath = Join-Path $RepositoryRoot 'tools\ci\run-windows-ci.ps1'

foreach ($requiredPath in @($workflowPath, $runnerPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required CI path is missing: $requiredPath"
    }
}

$workflowText = Get-Content -LiteralPath $workflowPath -Raw
$runnerText = Get-Content -LiteralPath $runnerPath -Raw
Assert-CiPolicy $workflowText $runnerText

Assert-PolicyRejects { Assert-CiPolicy ($workflowText.Replace('windows-2025', 'ubuntu-latest')) $runnerText } 'non-Windows runner'
Assert-PolicyRejects { Assert-CiPolicy ($workflowText.Replace('dotnet-version: 10.0.400', 'dotnet-version: 10.0.401')) $runnerText } 'wrong SDK'
Assert-PolicyRejects { Assert-CiPolicy ($workflowText.Replace('contents: read', 'contents: write')) $runnerText } 'write permissions'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('--locked-mode', '')) } 'unlocked restore'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('-c Release', '-c Debug')) } 'non-Release build'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('-Suite all', '-Suite unit')) } 'incomplete test lane'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\build\validate-build-metadata.ps1 -RequireDotNet', '')) } 'missing build metadata validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\quality\validate-quality-policy.ps1 -RequireDotNet', '.\tools\quality\validate-quality-policy.ps1')) } 'weakened quality validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\runtime\validate-fcc-environment-discovery.ps1 -RunFixtures -RequireRuntime', '')) } 'missing FCC environment-discovery validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\runtime\validate-fcc-runtime-health-compatibility.ps1 -RunFixtures -RequireRuntime', '')) } 'missing FCC runtime health/version compatibility validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\runtime\validate-fcc-structured-runtime.ps1 -RunFixtures -RequireRuntime', '')) } 'missing FCC structured-runtime validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\runtime\validate-fcc-runtime-event-normalization.ps1 -RunFixtures -RequireRuntime', '')) } 'missing FCC runtime event-normalization validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\runtime\validate-fcc-cli-fallback-runtime.ps1 -RunFixtures -RequireRuntime', '')) } 'missing FCC CLI fallback-runtime validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\ui\validate-design-system.ps1 -RunFixtures', '')) } 'missing design-system validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\ui\validate-semantic-themes.ps1 -RunFixtures -RequireRuntime', '')) } 'missing semantic-theme validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\ui\validate-app-chrome.ps1 -RunFixtures -RequireRuntime', '')) } 'missing app-chrome validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\ui\validate-workspace-layout.ps1 -RunFixtures -RequireRuntime', '')) } 'missing workspace-layout validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\ui\validate-navigation-surfaces.ps1 -RunFixtures -RequireRuntime', '')) } 'missing navigation-surface validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\ui\validate-bottom-tool-panel.ps1 -RunFixtures -RequireRuntime', '')) } 'missing bottom-tool-panel validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\ui\validate-command-palette.ps1 -RunFixtures -RequireRuntime', '')) } 'missing command-palette validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\ui\validate-common-states.ps1 -RunFixtures -RequireRuntime', '')) } 'missing common-state validation'
Assert-PolicyRejects { Assert-CiPolicy $workflowText ($runnerText.Replace('.\tools\ui\validate-dpi-layout.ps1 -RunFixtures -RequireRuntime', '')) } 'missing DPI/resolution layout validation'

Write-Host 'Static Windows CI policy validation: PASS.'
Write-Host 'Negative fixtures verified runner, SDK, permissions, locked restore, Release build, complete tests, build metadata, quality, FCC environment discovery, FCC runtime health/version compatibility, FCC structured runtime, FCC runtime event normalization, FCC CLI fallback runtime, design-system, semantic-theme, app-chrome, workspace-layout, navigation-surface, bottom-tool-panel, command-palette, common-state, and DPI/resolution layout enforcement.'

if ($RequireDotNet) {
    if (-not $IsWindows) {
        throw 'Executable CI contract validation requires Windows.'
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw 'dotnet was required but is not available on PATH.'
    }
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if (-not $pwsh) {
        throw 'pwsh was required but is not available on PATH.'
    }

    $sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.400') {
        throw "Expected .NET SDK 10.0.400 but resolved '$sdkVersion'."
    }

    Write-Host 'Executable Windows CI prerequisites: PASS.'
}
