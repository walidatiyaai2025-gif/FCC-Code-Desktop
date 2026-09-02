# Cloud P00 Claim — FCC Runtime Redaction Hardening

- Branch: `worker/p00-cloud-fcc-redaction-hardening`
- Started from live main: `1fdf0adaf88e4a52759e07fd913e4bb791b5c9ae`
- Date: 2026-09-02
- Scope: fix and regression-test `fcc-runtime` redaction of generic `Authorization: Bearer <opaque-token>` / authorization-header values only
- State: `IN_PROGRESS`

## Why this claim exists

The current `fcc-runtime` redaction pipeline applies the `Authorization` prefix matcher before the generic `Bearer` matcher. For opaque/JWT-like bearer values that do not match a special prefix such as `sk-`, the `Authorization` matcher replaces only the word `Bearer`, leaving the credential value in persisted/sanitized text. The existing self-test uses an `sk-...` fixture, so its earlier simple-secret pass masks this ordering defect.

This is a narrow P00 secret-hygiene regression fix. It does not change FCC provider behavior, target evidence, session semantics, the unified target-manifest schema, Unity, Blender, task closure criteria, or planning policy. The concurrently active `worker/p00-cloud-target-manifest-schema` lane is explicitly out of scope.
