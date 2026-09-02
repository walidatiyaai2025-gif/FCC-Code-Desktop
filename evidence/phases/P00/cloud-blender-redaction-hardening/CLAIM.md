# Cloud P00 Claim — Blender Authorization Redaction Hardening

- Branch: `worker/p00-cloud-blender-redaction-hardening`
- Started from live main: `97c6d2e08be711edc569d8f0baeb3d14128429e2`
- Date: 2026-09-02
- Scope: fix and regression-test raw Authorization-header leakage in Blender probe log sanitization only
- State: `INTEGRATED`
- Integration: PR #22, merge commit `b8a9c085147f6fee548d83628e7cecfd48f5dc7b`

## Why this claim exists

`tools/contract-probes/blender/lib.mjs` redacted secret-shaped object keys, `sk-...` values and direct Bearer tokens, but did not redact a raw non-Bearer authorization header. For example, `Authorization: Basic <credential>` was returned unchanged by `redact()`. Blender process stdout/stderr are persisted through this function, so the omission was a concrete secret-hygiene defect even though real Blender execution is still target-unverified.

## Completed scope

- complete Authorization header values are sanitized before Blender probe persistence,
- deterministic Basic and opaque Bearer authorization regressions were added,
- exact branch blobs passed syntax checks and the complete Blender self-test `17/17`,
- evidence is recorded in `RESULT.md`.

This claim does not change Blender discovery, invocation, artifact validation, cancellation, target task state, Unity, FCC, the target runner/manifest, or planning policy.

The claim is terminal and no longer reserves active work.
