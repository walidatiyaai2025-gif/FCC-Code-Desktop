# FCCD-P00-008 Unity probe-host evidence — 2026-09-01

## Scope

This evidence is for `FCCD-P00-008` only. It records what the remote worker host could and could not prove while implementing the reusable Unity P00 contract probe.

## Repository baseline

- Live `main` observed at worker startup: `e1797650dc93f941f6754f64cf201051d2e98c0c`.
- Worker branch: `worker/p00-unity-contract`.
- No open PR existed when the claim map was built.
- The branch initially matched `main`; `evidence/phases/P00/unity/CLAIM.md` was committed to make ownership durable.

## Worker environment

```text
platform: Linux
architecture: x64
kernel/release: 6.18.35
Node: v22.16.0
PowerShell/pwsh: not found
Unity Editor: not found
Unity Hub: not found
```

This is **not** the owner's Windows target machine.

## Self-tests

Command executed against the authored probe workspace before persistence:

```text
node tools/contract-probes/unity/self-test.mjs --json evidence/phases/P00/unity/SELF_TEST_RESULTS.json
```

Result:

```text
SELF_TEST_VERIFIED 20/20
exit 0
```

Covered project detection, `ProjectVersion.txt` parsing, exact-version resolution, argument arrays with spaces/Unicode/Arabic paths, NUnit XML validation, result/build artifact validation, log classification with unknown-line retention, secret redaction, bounded timeout, explicit cancellation, disposable fixture bootstrap, structured operation manifest, and unified-runner Unity wiring while preserving the Blender ownership boundary.

The persisted self-test source is the durable executable form of these checks. `SELF_TEST_VERIFIED` is not Unity runtime evidence.

## Remote-host Unity probe

Command executed:

```text
node tools/contract-probes/unity/probe.mjs --mode all --json evidence/phases/P00/unity/PROBE_HOST_RESULT.json
```

Result:

```text
BLOCKED_UNITY_NOT_FOUND
exit 2
```

The JSON records Hub/Editor not found and leaves CLI/log/compile/tests/automation/build/locking/cancellation/failure behavior `TARGET_UNVERIFIED`. No fake Unity executable was used and no real Unity operation was claimed.

## Evidence classification

| Area | State |
|---|---|
| Project detector / ProjectVersion parser | `SELF_TEST_VERIFIED` |
| Version comparison / exact-match resolver | `SELF_TEST_VERIFIED` |
| Strong argument arrays | `SELF_TEST_VERIFIED` |
| Spaces / Unicode / Arabic path preservation | `SELF_TEST_VERIFIED` |
| Log parser / unknown-line preservation | `SELF_TEST_VERIFIED` |
| NUnit XML result validator | `SELF_TEST_VERIFIED` |
| JSON/build artifact validation | `SELF_TEST_VERIFIED` |
| Generic owned-process timeout/cancellation | `SELF_TEST_VERIFIED` |
| Secret redaction + persisted-output scan | `SELF_TEST_VERIFIED` |
| Unified runner Unity wiring | `SELF_TEST_VERIFIED` by repository check; PowerShell execution unavailable on this host |
| Windows target Editor/Hub discovery | `TARGET_UNVERIFIED` |
| Unity CLI/log/compile | `TARGET_UNVERIFIED` |
| EditMode / PlayMode | `TARGET_UNVERIFIED` |
| `-executeMethod` | `TARGET_UNVERIFIED` |
| Build/artifact | `TARGET_UNVERIFIED` |
| Same-project collision | `TARGET_UNVERIFIED` |
| Unity-specific cancellation/cleanup | `TARGET_UNVERIFIED` |

## Target validation command

After integration, the owner/local target worker should run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\contract-probes\run-target-validation.ps1
```

Optional `-UnityEditor`, `-UnityHub`, `-UnityProject`, and `-UnityFixtureRoot` values are supported. `-UnityProject` is detection/version-resolution input only; mutations remain in the disposable fixture.

## Blocker

The remote host cannot provide mandatory real Windows/Unity evidence. Therefore the truthful task state is:

```text
FCCD-P00-008 = BLOCKED
```

pending target-machine execution and convergence. This is an evidence-environment blocker, not an implementation failure.
