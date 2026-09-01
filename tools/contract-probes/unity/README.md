# FCCD-P00-008 Unity contract probe

This directory contains **P00 contract-probe infrastructure only**. It is not the production P10 Unity adapter.

- `lib.mjs` — project/version parsing, discovery, deterministic argument-array builders, owned-process execution, log/test/artifact validation, redaction, disposable fixture bootstrap.
- `probe.mjs` — real Unity-host orchestration and structured evidence capture.
- `self-test.mjs` — deterministic `SELF_TEST_VERIFIED` checks that do not claim Unity behavior.

## Safety

- A supplied `--project` is detected and version-resolved only. It is never injected with scripts, compile errors, tests, scenes or builds.
- Mutating operations run only in a generated disposable fixture under temp or an explicit `--fixture-root`.
- Unity is launched as executable + argument array with `shell: false`; no raw command-string concatenation.
- Cancellation tracks the owned PID and never kills processes by executable name.
- Test/build/result artifacts are validated independently from process exit code.
- Persisted JSON is secret-redacted.

## Self-test

```powershell
node tools/contract-probes/unity/self-test.mjs
```

A passing run ends with `SELF_TEST_VERIFIED 20/20`. This is not target Unity evidence.

## Discovery

```powershell
node tools/contract-probes/unity/probe.mjs --mode discovery `
  --json evidence/phases/P00/target/unity-discovery.json
```

Optional explicit paths:

```powershell
node tools/contract-probes/unity/probe.mjs --mode discovery `
  --unity "C:\Program Files\Unity\Hub\Editor\6000.0.65f1\Editor\Unity.exe" `
  --hub "C:\Program Files\Unity Hub\Unity Hub.exe" `
  --json evidence/phases/P00/target/unity-discovery.json
```

## Existing-project detection without mutation

```powershell
node tools/contract-probes/unity/probe.mjs --mode project `
  --project "C:\path with spaces\My Unity Project" `
  --json evidence/phases/P00/target/unity-project.json
```

The resolver selects an Editor only when its observed version exactly matches `ProjectSettings/ProjectVersion.txt`. Missing required versions are reported explicitly; the probe never upgrades a user's project.

## Full target validation

Use the canonical P00 runner:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\contract-probes\run-target-validation.ps1
```

Optional Unity overrides:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\contract-probes\run-target-validation.ps1 `
  -UnityEditor "C:\Program Files\Unity\Hub\Editor\6000.0.65f1\Editor\Unity.exe" `
  -UnityProject "C:\path\to\project"
```

`-UnityProject` remains non-destructive input; compile/test/build/concurrency/cancellation probes always use a separate disposable fixture.

Exit codes:

- `0`: requested real Unity contract evidence completed.
- `1`: observed contract/probe failure.
- `2`: Unity absent or mandatory target evidence incomplete.
- `64`: usage error.

If Unity is absent, the truthful result is `BLOCKED_UNITY_NOT_FOUND`.

## Target operations

When a usable Editor exists, the probe attempts real evidence for discovery/version selection; disposable project creation/detection; batch/headless launch with dedicated log; positive and controlled-negative compile; EditMode and PlayMode NUnit XML results; static `-executeMethod`; Windows x64 build plus artifact validation; same-project collision characterization; timeout/cancellation and owned-tree cleanup.

Anything not actually proven remains `TARGET_UNVERIFIED`/`BLOCKED`; self-test fixtures never become fake Unity evidence.
