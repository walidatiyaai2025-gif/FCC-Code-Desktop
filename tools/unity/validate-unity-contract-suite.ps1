[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'P10-013 Unity contract suite must run on Windows.'
}

$validators = @(
    'validate-unity-project-detection.ps1',
    'validate-unity-editor-resolution.ps1',
    'validate-unity-cli-command-builder.ps1',
    'validate-unity-resource-locking.ps1',
    'validate-unity-log-capture.ps1',
    'validate-unity-compile.ps1',
    'validate-unity-editmode-tests.ps1',
    'validate-unity-playmode-tests.ps1',
    'validate-unity-editor-automation.ps1',
    'validate-unity-build-target.ps1',
    'validate-unity-structured-events.ps1',
    'validate-unity-recovery.ps1'
)

$requiredContracts = @(
    'UnityProjectDetection.cs',
    'UnityEditorResolution.cs',
    'UnityCliCommandBuilder.cs',
    'UnityResourceLocking.cs',
    'UnityLogCapture.cs',
    'UnityCompileValidation.cs',
    'UnityEditModeTestValidation.cs',
    'UnityPlayModeTestValidation.cs',
    'UnityEditorAutomationInvocation.cs',
    'UnityEditorAutomationValidation.cs',
    'UnityBuildTargetInvocation.cs',
    'UnityBuildTargetValidation.cs',
    'UnityStructuredUiEvents.cs',
    'UnityOperationRecovery.cs'
)

$unitySourceRoot = Join-Path $RepositoryRoot 'src\FCCCodeDesktop.Tools.Unity'
foreach ($contract in $requiredContracts) {
    $path = Join-Path $unitySourceRoot $contract
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "P10-013 required Unity contract is missing: $contract"
    }
}

$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($validator in $validators) {
    if (-not $seen.Add($validator)) {
        throw "P10-013 validator list contains a duplicate: $validator"
    }

    $validatorPath = Join-Path $PSScriptRoot $validator
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw "P10-013 required validator is missing: $validatorPath"
    }
}

$sdkVersion = (& dotnet --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "P10-013 SDK version check failed with exit code $LASTEXITCODE."
}
if ($sdkVersion -ne '10.0.400') {
    throw "P10-013 requires .NET SDK 10.0.400 but resolved '$sdkVersion'."
}

Push-Location $RepositoryRoot
try {
    foreach ($validator in $validators) {
        Write-Host "P10-013 stage: $validator"
        & (Join-Path $PSScriptRoot $validator) -RepositoryRoot $RepositoryRoot
        if ($LASTEXITCODE -ne 0) {
            throw "P10-013 validator '$validator' returned exit code $LASTEXITCODE."
        }
    }

    Write-Host "P10-013 Unity contract suite: PASS ($($validators.Count) validators)."
}
finally {
    Pop-Location
}
