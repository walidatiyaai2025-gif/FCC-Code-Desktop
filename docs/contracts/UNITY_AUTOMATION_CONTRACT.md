# Unity Automation Contract — P00 probe baseline

**Task:** `FCCD-P00-008`  
**Phase:** P00  
**Status:** reusable probe infrastructure implemented; real target-machine Unity execution still required.  
**Boundary:** this contract informs future P10 design but does not implement the P10 Unity adapter.

## Evidence states

Target-sensitive claims use the canonical states `VERIFIED_ON_TARGET`, `VERIFIED_ON_AVAILABLE_UNITY_HOST`, `SELF_TEST_VERIFIED`, `TARGET_UNVERIFIED`, `NOT_OBSERVED`, `UNSUPPORTED`, or `UNKNOWN`.

The worker host used for this implementation had no Unity Editor/Hub executable and was not the owner's Windows target machine. Therefore repository-owned parsing/building/validation/process mechanics may be `SELF_TEST_VERIFIED`, while Unity runtime behavior remains `TARGET_UNVERIFIED` until the unified target lane executes on a real supported Windows/Unity host.

## Documentation baseline

Supported syntax was researched from Unity documentation rather than inferred from memory alone:

- Unity 6 Editor command-line arguments: <https://docs.unity3d.com/6000.0/Documentation/Manual/EditorCommandLineArguments.html>
- Unity 6 command-line build guidance: <https://docs.unity3d.com/6000.0/Documentation/Manual/build-command-line.html>
- Unity Test Framework command-line reference: <https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/reference-command-line.html>

Unity documentation establishes syntax expectations; task closure still requires real target observation where P00 acceptance requires it.

## Discovery

### Hub

The probe checks explicit `--hub`, `UNITY_HUB_PATH`, `PATH`, and common Program Files/LocalAppData locations. A common Hub-managed Editor root is also considered, but no single filesystem source is treated as universally authoritative because Hub install locations can change.

**State:** discovery logic `SELF_TEST_VERIFIED`; real Windows Hub discovery `TARGET_UNVERIFIED`.

### Editor

The probe checks explicit `--unity`, `UNITY_EDITOR_PATH` / `UNITY_PATH`, `PATH`, the common Hub root `...\Unity\Hub\Editor\<version>\Editor\Unity.exe`, and secondary legacy/common Program Files locations. Existing candidates are version-probed with `Unity.exe -version` where possible; folder version names are only fallback hints. On Windows the probe also attempts to identify the executable PE architecture.

**State:** resolver logic `SELF_TEST_VERIFIED`; target discovery `TARGET_UNVERIFIED`.

## Project detection

A directory is classified without launching Unity. The detector inspects:

```text
Assets/
Packages/
ProjectSettings/
ProjectSettings/ProjectVersion.txt
```

`ProjectVersion.txt` is parsed for `m_EditorVersion` and `m_EditorVersionWithRevision`. Structured output contains project root, marker presence, required version, revision/hash when exposed, validity, and explicit invalid/incomplete/read-failure classifications.

**State:** `SELF_TEST_VERIFIED`, including spaces, Unicode, and Arabic paths.

## Version resolution

The probe exposes the required conceptual values:

```text
PROJECT_REQUIRED_VERSION
INSTALLED_MATCHING_VERSION
INSTALLED_OTHER_VERSIONS
SELECTED_EDITOR
COMPATIBILITY_STATUS
```

For a supplied user project, Editor selection is intentionally conservative: only an exact observed version match is selected. Missing exact versions return `REQUIRED_VERSION_NOT_INSTALLED`; the probe never silently opens a user project in another Editor and never upgrades it. A generated disposable fixture may use the newest discovered Editor because it has no pre-existing compatibility requirement.

**State:** exact-match resolver `SELF_TEST_VERIFIED`; broader compatibility boundaries remain target/convergence evidence.

## Argument and path model

Every process launch uses a validated executable plus ordered argument array with shell expansion disabled. Manual quoting is not embedded in individual path arguments. Self-tests preserve space-containing, Unicode, and Arabic path values as single arguments.

**State:** `SELF_TEST_VERIFIED`.

## CLI baseline

The target lane uses documented arguments as applicable:

```text
-batchmode
-nographics
-projectPath <path>
-logFile <path>
-timestamps
-executeMethod <Class.Method>
-quit
-version
```

The dedicated `-logFile` is required evidence because console output is not treated as the complete Unity diagnostic stream.

**State:** construction `SELF_TEST_VERIFIED`; launch/exit/log behavior `TARGET_UNVERIFIED`.

## Disposable fixture safety

All mutations occur only below a generated temporary fixture with a name intentionally containing spaces and Arabic/Unicode text. A supplied `--project` is detection/version-resolution input only and is never used for injected scripts, deliberate compile failures, test assemblies, scene creation, builds, concurrency collisions, or cancellation experiments.

The full target lane creates a disposable project with `-createProject`, injects repository-owned fixture sources, and removes it after the run unless `--keep-fixture` is explicitly requested for diagnostics.

**State:** filesystem bootstrap `SELF_TEST_VERIFIED`; Unity project creation `TARGET_UNVERIFIED`.

## Logging

The probe captures child stdout, child stderr, and the explicit Unity log file. It retains every captured non-empty line, including unknown lines, while tagging recognized compiler errors/warnings, project-lock messages, exceptions, build failures, and test failures. Unknown lines are preserved for later reconciliation.

**State:** capture/parser `SELF_TEST_VERIFIED`; real Unity log shapes `TARGET_UNVERIFIED`.

## Compile contract

Positive compile health requires a disposable project-owned static Editor method to execute and emit structured JSON. Classification distinguishes `COMPILE_PASS`, `COMPILE_ERROR`, `UNITY_STARTUP_FAILURE`, `PROJECT_OPEN_FAILURE`, `TIMEOUT`, `CANCELLED`, and `UNKNOWN_FAILURE`.

The target sequence also injects one intentionally invalid C# file into the disposable project, requires compiler-error evidence and absence of the success marker, removes the bad file, then verifies compile recovery. Exit code alone cannot produce `COMPILE_PASS`.

**State:** classifier/fixtures `SELF_TEST_VERIFIED`; real Unity compile behavior `TARGET_UNVERIFIED`.

## EditMode and PlayMode tests

The target argument model uses:

```text
-runTests
-testPlatform EditMode|PlayMode
-testResults <results.xml>
```

with project path, batch/headless operation, timestamps, and a dedicated log. Unity Test Framework documentation defines the result file as NUnit XML. The validator requires the file to exist, be non-empty, expose an expected structured root, contain a plausible test total, run a nonzero number of deterministic fixture tests, and surface failed/skipped/inconclusive counts. `exit 0 + missing test artifact` is failure/incomplete evidence.

PlayMode uses a deterministic `UnityTest` that yields one frame then passes. If the target cannot execute required PlayMode behavior, the step remains `BLOCKED`/`TARGET_UNVERIFIED`.

**State:** argument/XML validation `SELF_TEST_VERIFIED`; actual EditMode/PlayMode execution `TARGET_UNVERIFIED`.

## Editor automation / executeMethod

The disposable Editor script exposes static harmless methods to emit compile/automation markers, throw a known expected exception, hold the fixture open for lock/cancel characterization, and perform a minimal build. Extra arguments are read through `Environment.GetCommandLineArgs()`.

Success requires a structured result artifact; the negative method must expose nonzero exit and/or exception diagnostics rather than relying on assumptions.

**State:** fixture and command builder `SELF_TEST_VERIFIED`; real `-executeMethod` semantics `TARGET_UNVERIFIED`.

## Build contract

The fixture uses `BuildPipeline.BuildPlayer` with `StandaloneWindows64`; the command also includes `-buildTarget StandaloneWindows64`. A build is PASS only when:

```text
process completes acceptably
AND build-result JSON reports success/Succeeded
AND expected executable exists
AND expected executable is non-empty
```

The result artifact records errors, warnings, total size, and output path where available. A zero exit with missing output is an artifact-validation failure.

**State:** command/result/artifact validators `SELF_TEST_VERIFIED`; real Windows build `TARGET_UNVERIFIED`.

## Project lock / concurrency

Unity documentation says batch mode cannot open a project while another Editor has the same project open, but P00 does not freeze an assumed exact diagnostic shape. The empirical probe uses only the disposable fixture: one owned process writes a ready marker and holds the project; a second batch process targets the same fixture; PASS requires clear lock/collision diagnostics and no second success artifact. Ambiguous behavior remains `BLOCKED`/`TARGET_UNVERIFIED`.

**State:** orchestration logic `SELF_TEST_VERIFIED`; target lock files/messages/collision behavior `TARGET_UNVERIFIED`.

## Cancellation and process ownership

Each owned process records its PID. Cancellation is bounded: request termination of the owned process, wait, then force the owned tree by root PID if still necessary. On Windows escalation uses `taskkill /PID <pid> /T /F`; the probe never kills by `Unity.exe` process name and therefore does not intentionally affect unrelated instances.

Self-tests use owned Node fixture processes to verify timeout/cancel mechanics without representing them as Unity behavior.

**State:** generic ownership/cancellation `SELF_TEST_VERIFIED`; Unity-specific termination/post-cancel project state `TARGET_UNVERIFIED`.

## Crash/interruption observability

For each operation the manifest can capture dedicated log, process lifecycle, structured result/test/build artifact, artifact size/hash, timeout/cancellation, and final classification. P00 does not implement P15 recovery; target evidence must determine later lock/temp/project-state/log reconciliation needs.

## Operation manifest

Where applicable each operation records:

```text
operation ID
Unity version
executable path
project path
sanitized argument array
start/end/duration
PID
exit code
timeout/cancellation
log path
test-result path
build-output path
produced artifacts with size/hash
final classification
evidence state
```

Persisted output is recursively redacted. Self-tests inject fake API keys, bearer tokens, and token-shaped values and verify they are absent from persisted output.

**State:** `SELF_TEST_VERIFIED`.

## Unified target runner

`tools/contract-probes/run-target-validation.ps1` now invokes the Unity self-test and real Unity target probe. The Unity lane is considered integrated even when its target step truthfully exits 2/BLOCKED because Unity is absent. The separate Blender hook remains owned by `FCCD-P00-009`; this worker did not implement Blender behavior.

The global runner still cannot PASS until every required lane is integrated and every mandatory step passes.

**State:** Unity runner wiring `SELF_TEST_VERIFIED`; PowerShell execution on the owner's target Windows machine `TARGET_UNVERIFIED` from this host.

## Known limitations

1. This remote host did not contain the owner's Windows Unity environment.
2. No single Hub metadata/install location is assumed globally authoritative.
3. Exact-version matching is deliberately conservative for existing projects; broader compatible-version policy belongs to evidence-driven convergence/future implementation.
4. The deterministic test fixture requires a Unity Test Framework capability usable by the installed Editor/project; absence must be reported, not hidden.
5. The P00 build fixture validates Windows x64 output only.
6. `-nographics` is used only for nonvisual probe operations; graphics/GI workflows are not inferred from this evidence.
7. Project lock file names/locations remain unfrozen until empirical evidence exists.

## Closure rule

`FCCD-P00-008` remains `BLOCKED` until sufficient real target evidence proves the mandatory Unity discovery/version/project/CLI/log/compile/EditMode/PlayMode/Editor automation/build/artifact/cancellation/failure/concurrency contract. Reusable probe implementation plus self-tests is worker success, not P00 task closure.
