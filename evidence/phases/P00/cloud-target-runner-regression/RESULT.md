# P00 Cloud Target-Runner Regression Evidence

- Date: `2026-09-02`
- Start main SHA: `0b12242f122b0c6b69a703436c24b617708ca3f3`
- Branch: `worker/p00-cloud-runner-regression-tests`
- Tested implementation commit: `b0ca0e1ad4fc95e88c8e7db374807ec204f4b11f`
- Tested script blob: `32c232499257d65c02d5e39d694a0a6b6fb62dbe`
- Script SHA-256: `389b5817551ac5957f5ebddf033a6e3ad8648cda378d6ec8878f0ffb777b4421`
- Evidence class: `SELF_TEST_VERIFIED`
- Target-machine evidence claimed: `false`

## Purpose

Preserve the already-canonical PR #6/#13 exact-head and rerun-safety semantics with a repository-owned deterministic regression test. This does not change the target runner policy or any provider/Unity/Blender contract.

## Static runner invariants covered

The self-test requires the canonical runner source to retain markers for:

- Windows-only authoritative target execution,
- Git and Node prerequisite checks,
- repository identity validation,
- the exact target-evidence exclusion pathspec,
- rejection of dirty executable/source/configuration inputs,
- FCC discovery/CLI lane,
- FCC streaming/session/failure lane,
- Unity lane,
- Blender lane.

## Git pathspec mechanics exercised

The isolated disposable Git fixture verified:

- clean worktree: `PASS` / accepted,
- modified file under `evidence/phases/P00/target/**`: `PASS` / excluded,
- untracked nested file under `evidence/phases/P00/target/**`: `PASS` / excluded,
- changed sibling evidence outside the target-output subtree: detected/blocking,
- changed tracked probe/source input: detected/blocking,
- untracked probe/source input: detected/blocking,
- untracked source/config path containing spaces: detected/blocking,
- fixture cleanup: completed.

## Commands/results

```text
node --check tools/contract-probes/target-runner-self-test.mjs
PASS

node tools/contract-probes/target-runner-self-test.mjs
SELF_TEST_VERIFIED (isolated fixture mechanics)
```

The cloud runtime did not have a connector-mounted repository checkout, so the exact uploaded test script was executed against an isolated fixture carrying the canonical runner policy markers and exact Git pathspec. The locally executed script's Git blob hash exactly matched the uploaded branch blob `32c232499257d65c02d5e39d694a0a6b6fb62dbe`.

## Secret scan

Credential-shaped pattern scan of the new self-test source produced no matches. The fixture uses only the reserved non-deliverable address `selftest@example.invalid` and contains no tokens, credentials, or authorization values.

## Regression boundary

`tools/contract-probes/run-target-validation.ps1` itself is not modified by this work. Therefore this change adds regression coverage without changing the behavior that produced prior authoritative target evidence.
