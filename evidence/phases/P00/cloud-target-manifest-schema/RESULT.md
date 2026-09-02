# Cloud P00 Result — Unified Target Manifest Schema

- Branch: `worker/p00-cloud-target-manifest-schema`
- Started from live main: `1fdf0adaf88e4a52759e07fd913e4bb791b5c9ae`
- Date: 2026-09-02
- Scope: binding unified target-manifest completeness only
- Result: `COMPLETE_AWAITING_INTEGRATION`

## Defect

The binding P00 target-validation contract requires the unified manifest to identify discovered tool versions, exact PASS/FAIL/BLOCKED reasons, artifact paths, sanitized error summaries, and whether each result was actually observed on the authoritative target. The previous runner recorded generic step status/exit code/path and an empty note, so the one-command target artifact did not fully satisfy its own documented evidence schema.

## Implementation

- Added `tools/contract-probes/target-evidence-summary.mjs`.
  - Keeps probe exit codes authoritative for PASS/BLOCKED/FAIL.
  - Preserves `NOT_INSTALLED`, `TARGET_UNVERIFIED`, and `NOT_OBSERVED_ON_TARGET` distinctions instead of promoting them to PASS.
  - Surfaces bounded discovered versions for FCC/Claude plus Node, Git, .NET SDK, Python, PowerShell, Unity Editors, and Blender when present.
  - Records controlled lane reasons/classifications without copying raw provider output.
  - Requires explicit `--authoritative-target` plus Windows-host evidence before setting `executedOnAuthoritativeTarget=true`.
- Added `tools/contract-probes/target-evidence-summary-self-test.mjs`.
- Updated `tools/contract-probes/run-target-validation.ps1`.
  - Runs the new summary self-test.
  - Preserves native probe stdout while reading authoritative lane exit codes from the already-recorded step result.
  - Writes `evidence/phases/P00/target/P00_TARGET_CONTRACT_SUMMARY.json`.
  - Upgrades `P00_TARGET_EVIDENCE.json` to schema version 2 and embeds the compact contract summary.
  - Adds the contract summary to `P00_TARGET_EVIDENCE.md`.
- Extended `tools/contract-probes/target-runner-self-test.mjs` with static integration guards and execution of the new summary self-test.

## Cloud tests

Executed from the cloud/Linux worker environment:

```text
node --check tools/contract-probes/target-evidence-summary.mjs
node --check tools/contract-probes/target-evidence-summary-self-test.mjs
node --check tools/contract-probes/target-runner-self-test.mjs
```

All syntax checks passed.

`node tools/contract-probes/target-evidence-summary-self-test.mjs`:

```json
{
  "status": "SELF_TEST_VERIFIED",
  "schemaVersion": 2,
  "assertions": 14,
  "cliInvocation": "PASS",
  "unicodeSpacePath": "PASS",
  "targetEvidenceClaimed": false
}
```

The self-test exercises the actual summarizer CLI with a fixture repository path containing spaces and Arabic text, verifies repo-relative artifact paths, version extraction, controlled missing-evidence failure, target-authorization boundaries, and Blender `NOT_INSTALLED` semantics.

`node tools/contract-probes/target-runner-self-test.mjs`:

```json
{
  "status": "SELF_TEST_VERIFIED",
  "staticPolicyMarkers": "PASS",
  "targetEvidenceSummary": "PASS",
  "gitPathspecMechanics": {
    "cleanAccepted": true,
    "targetEvidenceModifiedAccepted": true,
    "targetEvidenceUntrackedAccepted": true,
    "siblingEvidenceBlocked": true,
    "trackedSourceBlocked": true,
    "untrackedSourceBlocked": true,
    "spacePathBlocked": true
  },
  "targetEvidenceClaimed": false
}
```

## Regression review

During review, assigning `Invoke-NodeStep` directly to an exit-code variable was rejected because PowerShell can collect native stdout together with the function return value. The runner keeps the existing `[void](Invoke-NodeStep ...)` behavior and then reads the recorded step's integer `exitCode`, preventing stdout/exit-code coercion regressions.

Existing exact-head/rerun-safety dirty-tree semantics remain covered by `target-runner-self-test.mjs`.

## Secret scan

A targeted credential-shape scan over the four changed executable/test files returned:

```text
SECRET_SCAN_PASS
```

No real credential/provider secret was introduced into the implementation or fixtures.

## Environment boundary

PowerShell is not installed in this cloud worker environment. Therefore the modified PowerShell runner was not represented as executed on the authoritative Windows target. Static integration/regression checks and Node self-tests are cloud evidence only.

No provider success, session resume, real Blender automation, or Windows process behavior is claimed by this result.

## Canonical task impact

This hardening fixes a closure-readiness defect in the unified target runner but does not independently satisfy any target-dependent P00 task acceptance gate. Canonical task states therefore remain truthful pending authoritative target execution and the existing planning decision:

- `FCCD-P00-004` remains target/provider blocked.
- `FCCD-P00-005` remains blocked on exact-head Windows rerun plus `PG-002-P00-RATE-LIMIT-CLOSURE` unless a natural rate-limit event is observed.
- `FCCD-P00-007` remains target/provider blocked.
- `FCCD-P00-009` remains target Blender blocked.
- `FCCD-P00-006` and `FCCD-P00-010` remain implemented pending P00 exit-gate convergence.

## Next authoritative action

After integration, execute the canonical one-command Windows target runner from an exact clean merged head. The resulting schema-version-2 manifest should make the remaining target blockers and evidence states explicit without manufacturing 429 traffic or other target-only evidence.
