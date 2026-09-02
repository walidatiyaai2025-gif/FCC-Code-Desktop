# Cloud P00 Claim — FCC Runtime Redaction Hardening

- Branch: `worker/p00-cloud-fcc-redaction-hardening`
- Started from live main: `1fdf0adaf88e4a52759e07fd913e4bb791b5c9ae`
- Date: 2026-09-02
- Scope: fix and regression-test `fcc-runtime` redaction of generic `Authorization: Bearer <opaque-token>` / authorization-header values only
- State: `INTEGRATED`
- Integration: PR #18, merge commit `accc2a0cd1146773b4aa1851b8b8fa55291ffdfe`

## Why this claim exists

The pre-fix `fcc-runtime` redaction pipeline applied an `Authorization` matcher that consumed only the first non-whitespace token. For opaque/JWT-like bearer values that did not match a special prefix such as `sk-`, `Authorization: Bearer <credential>` became `Authorization: [REDACTED] <credential>`, leaving the credential in persisted/sanitized text. The existing self-test used an `sk-...` fixture, so its earlier simple-secret pass masked this defect.

## Completed scope

- authorization header values are now redacted as one complete value up to safe line/header delimiters,
- opaque/JWT-like Bearer and Basic authorization regressions were added,
- direct Bearer redaction and length-preserving masking are covered,
- deterministic fake credentials are asserted absent from persisted self-test output,
- cloud regression evidence is recorded in `RESULT.md`.

This is a narrow P00 secret-hygiene regression fix. It does not change FCC provider behavior, target evidence, session semantics, the unified target-manifest schema, Unity, Blender, task closure criteria, or planning policy. The concurrently active `worker/p00-cloud-target-manifest-schema` lane remains explicitly out of scope.

The claim is terminal and no longer reserves active work.
