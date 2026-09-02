# Cloud P00 Result — Target Evidence Artifact Integrity

- Branch: `worker/p00-cloud-target-evidence-artifact-integrity`
- Started from live main: `353b36dc4a29c067548183e3fff793bcc5dae459`
- Date: 2026-09-02
- Scope: unified target-evidence summary artifact-integrity guard only
- Result: `COMPLETE_AWAITING_INTEGRATION`

## Defect

The summary previously derived each lane's top-level `status` from the supplied probe exit code even when the mandatory evidence path was absent, the file did not exist, or JSON could not be parsed. With a stale or incorrect exit code of `0`, a lane could therefore report `PASS` despite having no machine-readable evidence artifact. That contradicts the P00 target-validation contract and the project rule that exit code alone is not success.

## Implementation

- Added one fail-closed guard in `tools/contract-probes/target-evidence-summary.mjs`.
- `EVIDENCE_PATH_NOT_SUPPLIED`, `EVIDENCE_FILE_MISSING`, and `EVIDENCE_JSON_UNREADABLE` now force:
  - `status: FAIL`
  - `resultState: FAIL`
  - the exact evidence-read state as `reason` / `errorSummary`
  - no authoritative-target execution claim
  - no target-behavior observation claim
- Readable evidence retains the existing exit-code / `NOT_INSTALLED` / `NOT_OBSERVED_ON_TARGET` semantics unchanged.
- No probe, provider, Unity, Blender, rate-limit, or task-closure behavior changed.

## Tests

The changed source/test files were reconstructed locally from the exact branch blobs and verified by Git blob SHA before execution:

- `target-evidence-summary.mjs` blob: `c3c38f812292ee5b7586e4c3773878e4478e744b`
- `target-evidence-summary-self-test.mjs` blob: `7c353b7174ad49ec70604c863b065a58f3c48963`
- unchanged `target-runner-self-test.mjs` blob: `f1eddd29eb7a6cccd05d373bd15dd2ae2e145d33`
- unchanged `run-target-validation.ps1` blob: `6b8f2953c97e2a3d3411a6920f31f65b69de60b7`

Executed in the cloud/Linux worker environment:

```text
node --check tools/contract-probes/target-evidence-summary.mjs
node --check tools/contract-probes/target-evidence-summary-self-test.mjs
node tools/contract-probes/target-evidence-summary-self-test.mjs
```

Result:

```json
{
  "status": "SELF_TEST_VERIFIED",
  "schemaVersion": 2,
  "assertions": 20,
  "cliInvocation": "PASS",
  "unicodeSpacePath": "PASS",
  "missingPathFailClosed": "PASS",
  "missingEvidenceFailClosed": "PASS",
  "unreadableEvidenceFailClosed": "PASS",
  "targetEvidenceClaimed": false
}
```

Also executed the affected runner regression:

```text
FCC_P00_REPO_ROOT=<local exact-source fixture> node tools/contract-probes/target-runner-self-test.mjs
```

Result: `SELF_TEST_VERIFIED`; static runner policy, target-evidence summary integration, and all exact-head/rerun-safety pathspec mechanics remained PASS.

## Secret scan

A targeted credential-shape scan over the changed executable/test blobs returned:

```text
SECRET_SCAN_PASS
```

No real credential/provider secret was introduced.

## Environment boundary

These are cloud/self-test results only. No Windows target run, provider success, session resume, CLI successful completion, Blender execution, or natural rate-limit event is claimed.

## Canonical task impact

This fixes a closure-readiness defect in the unified target evidence layer but does not independently close a target-dependent task. Canonical states remain truthful:

- `FCCD-P00-004` — BLOCKED on provider-backed target session/resume completion.
- `FCCD-P00-005` — BLOCKED on exact-head target rerun plus `PG-002-P00-RATE-LIMIT-CLOSURE` unless a natural rate-limit event is observed.
- `FCCD-P00-007` — BLOCKED on provider-backed successful CLI fallback completion.
- `FCCD-P00-009` — BLOCKED on real Blender target execution.
- `FCCD-P00-006` and `FCCD-P00-010` — IMPLEMENTED pending P00 convergence/exit gate.

## Next authoritative action

After integration, run the canonical one-command Windows target validation from an exact clean merged head. Missing/unreadable mandatory evidence can no longer be masked by a zero probe exit code in the compact contract summary.
