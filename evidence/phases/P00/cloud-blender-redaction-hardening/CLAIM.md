# Cloud P00 Claim — Blender Authorization Redaction Hardening

- Branch: `worker/p00-cloud-blender-redaction-hardening`
- Started from live main: `97c6d2e08be711edc569d8f0baeb3d14128429e2`
- Date: 2026-09-02
- Scope: fix and regression-test raw Authorization-header leakage in Blender probe log sanitization only
- State: `IN_PROGRESS`

## Why this claim exists

`tools/contract-probes/blender/lib.mjs` redacts secret-shaped object keys, `sk-...` values and direct Bearer tokens, but it does not redact a raw non-Bearer authorization header. For example, `Authorization: Basic <credential>` is returned unchanged by `redact()`. Blender process stdout/stderr are persisted through this function, so the omission is a concrete secret-hygiene defect even though real Blender execution is still target-unverified.

This claim does not change Blender discovery, invocation, artifact validation, cancellation, target task state, Unity, FCC, the target runner/manifest, or planning policy.
