Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$baseSha = 'a2d52fd144a345c4d3aaefed73e46799de6dc69b'
$solutionPath = Join-Path $PWD 'FCCCodeDesktop.sln'

git checkout $baseSha -- FCCCodeDesktop.sln
if ($LASTEXITCODE -ne 0) { throw 'Failed to restore canonical base solution.' }
$text = [IO.File]::ReadAllText($solutionPath).Replace("`r`n", "`n")

$projectBlock = @'
Project("{66A26720-8FB5-11D2-AA7E-00C04F688DDE}") = "tests", "tests", "{0AB3BF05-4346-4AA6-1389-037BE0695223}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "FCCCodeDesktop.Testing", "tests\FCCCodeDesktop.Testing\FCCCodeDesktop.Testing.csproj", "{6433EEF5-263A-4E38-AAB1-BB2E67EC114F}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "FCCCodeDesktop.UnitTests", "tests\FCCCodeDesktop.UnitTests\FCCCodeDesktop.UnitTests.csproj", "{269C3259-7F9A-4DB1-B474-534FCBFC2779}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "FCCCodeDesktop.IntegrationTests", "tests\FCCCodeDesktop.IntegrationTests\FCCCodeDesktop.IntegrationTests.csproj", "{86983556-6FCD-468C-A6DE-77097534C8EC}"
EndProject
'@
$projectMarker = "EndProject`nGlobal"
if (-not $text.Contains($projectMarker)) { throw 'Could not locate solution project/global boundary.' }
$text = $text.Replace($projectMarker, "EndProject`n$projectBlock" + 'Global')

$testConfigs = @'
		{6433EEF5-263A-4E38-AAB1-BB2E67EC114F}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{6433EEF5-263A-4E38-AAB1-BB2E67EC114F}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{6433EEF5-263A-4E38-AAB1-BB2E67EC114F}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{6433EEF5-263A-4E38-AAB1-BB2E67EC114F}.Release|Any CPU.Build.0 = Release|Any CPU
		{269C3259-7F9A-4DB1-B474-534FCBFC2779}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{269C3259-7F9A-4DB1-B474-534FCBFC2779}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{269C3259-7F9A-4DB1-B474-534FCBFC2779}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{269C3259-7F9A-4DB1-B474-534FCBFC2779}.Release|Any CPU.Build.0 = Release|Any CPU
		{86983556-6FCD-468C-A6DE-77097534C8EC}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{86983556-6FCD-468C-A6DE-77097534C8EC}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{86983556-6FCD-468C-A6DE-77097534C8EC}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{86983556-6FCD-468C-A6DE-77097534C8EC}.Release|Any CPU.Build.0 = Release|Any CPU
'@
$configMatch = [regex]::Match($text, '(?ms)\tGlobalSection\(ProjectConfigurationPlatforms\) = postSolution\n.*?\tEndGlobalSection')
if (-not $configMatch.Success) { throw 'Could not locate ProjectConfigurationPlatforms section.' }
$configEnd = $configMatch.Index + $configMatch.Length - "`tEndGlobalSection".Length
$text = $text.Insert($configEnd, $testConfigs)

$testNested = "`t`t{6433EEF5-263A-4E38-AAB1-BB2E67EC114F} = {0AB3BF05-4346-4AA6-1389-037BE0695223}`n`t`t{269C3259-7F9A-4DB1-B474-534FCBFC2779} = {0AB3BF05-4346-4AA6-1389-037BE0695223}`n`t`t{86983556-6FCD-468C-A6DE-77097534C8EC} = {0AB3BF05-4346-4AA6-1389-037BE0695223}`n"
$nestedMatch = [regex]::Match($text, '(?ms)\tGlobalSection\(NestedProjects\) = preSolution\n.*?\tEndGlobalSection')
if (-not $nestedMatch.Success) { throw 'Could not locate NestedProjects section.' }
$nestedEnd = $nestedMatch.Index + $nestedMatch.Length - "`tEndGlobalSection".Length
$text = $text.Insert($nestedEnd, $testNested)

if ($text -match 'Debug\|x64|Debug\|x86|Release\|x64|Release\|x86') { throw 'Unexpected x64/x86 solution configuration introduced.' }
foreach ($required in @('"src", "src"','"tests", "tests"','FCCCodeDesktop.Testing.csproj','FCCCodeDesktop.UnitTests.csproj','FCCCodeDesktop.IntegrationTests.csproj')) {
    if (-not $text.Contains($required)) { throw "Missing required solution entry: $required" }
}
$srcNestedCount = ([regex]::Matches($text, '= \{62D5EF03-65C2-51DC-B8C4-28D4D1D66754\}')).Count
if ($srcNestedCount -ne 16) { throw "Expected 16 preserved src nesting entries, found $srcNestedCount." }
$testsNestedCount = ([regex]::Matches($text, '= \{0AB3BF05-4346-4AA6-1389-037BE0695223\}')).Count
if ($testsNestedCount -ne 3) { throw "Expected 3 tests nesting entries, found $testsNestedCount." }

[IO.File]::WriteAllText($solutionPath, $text, [Text.UTF8Encoding]::new($false))

$listed = dotnet sln .\FCCCodeDesktop.sln list | Out-String
if ($LASTEXITCODE -ne 0) { throw 'Solution list failed.' }
foreach ($project in @('FCCCodeDesktop.Testing.csproj','FCCCodeDesktop.UnitTests.csproj','FCCCodeDesktop.IntegrationTests.csproj')) {
    if ($listed -notmatch [regex]::Escape($project)) { throw "Missing $project from solution list." }
}
$diff = git diff -- FCCCodeDesktop.sln | Out-String
if ($diff -match 'Debug\|x64|Debug\|x86|Release\|x64|Release\|x86') { throw 'Platform configuration churn remains in solution diff.' }
if ($diff -match '(?m)^-.*= \{62D5EF03-65C2-51DC-B8C4-28D4D1D66754\}') { throw 'Existing src nesting was removed.' }
git diff --check
if ($LASTEXITCODE -ne 0) { throw 'Solution diff hygiene failed.' }
Write-Host 'Minimal solution reconstruction: PASS.'
