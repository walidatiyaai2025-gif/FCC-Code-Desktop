# Cloud P00 Claim — FCC CLI Probe Redaction Hardening

- Branch: `worker/p00-cloud-fcc-cli-redaction-hardening`
- Started from live main: `d140944bee81ce2f55ba9a4c35b305660297cc6d`
- Date: 2026-09-02
- Scope: fix and regression-test opaque authorization-header leakage in `tools/contract-probes/fcc/probe.mjs` only
- State: `IN_PROGRESS`

## Why this claim exists

After the independent `fcc-runtime` redaction repair merged, a cross-probe consistency audit found the same defect still present in the older FCC discovery/CLI probe: its `Authorization` pattern consumes only the first non-whitespace token. Therefore `Authorization: Bearer <opaque-credential>` can become `Authorization: [REDACTED] <opaque-credential>` unless the credential separately matches a special prefix pattern.

This claim is independent from the concurrently active `worker/p00-cloud-target-manifest-schema` lane. It does not touch the unified runner/manifest files, target evidence, session semantics, Unity, Blender, task states, or planning policy.
