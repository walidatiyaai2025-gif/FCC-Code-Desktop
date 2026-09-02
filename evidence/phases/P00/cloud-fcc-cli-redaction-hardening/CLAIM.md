# Cloud P00 Claim — FCC CLI Probe Redaction Hardening

- Branch: `worker/p00-cloud-fcc-cli-redaction-hardening`
- Started from live main: `d140944bee81ce2f55ba9a4c35b305660297cc6d`
- Date: 2026-09-02
- Scope: fix and regression-test opaque authorization-header leakage in `tools/contract-probes/fcc/probe.mjs` only
- State: `COMPLETE_AWAITING_INTEGRATION`

## Why this claim exists

After the independent `fcc-runtime` redaction repair merged, a cross-probe consistency audit found the same defect still present in the older FCC discovery/CLI probe: its `Authorization` pattern consumed only the first non-whitespace token. Therefore `Authorization: Bearer <opaque-credential>` could become `Authorization: [REDACTED] <opaque-credential>` unless the credential separately matched a special prefix pattern.

## Completed scope

- the FCC CLI/discovery probe now redacts the complete authorization value up to safe line/header delimiters,
- the pre-fix opaque bearer leak was reproduced,
- direct post-fix redaction mechanics pass,
- the canonical FCC self-test now includes an end-to-end stdout/stderr/event persistence fixture for opaque Bearer and Basic credentials,
- cloud evidence and the exact execution limitation are recorded in `RESULT.md`.

This claim is independent from the concurrently active `worker/p00-cloud-target-manifest-schema` lane. It does not touch the unified runner/manifest files, target evidence, session semantics, Unity, Blender, task states, or planning policy.
