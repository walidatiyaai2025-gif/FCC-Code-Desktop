# P00 Cloud Result — Blender Authorization Redaction Hardening

## Scope

Secret-hygiene repair for Blender probe log sanitization only.

`FCCD-P00-009` remains target-blocked: this work does not claim Blender is installed or that real Blender background/Python/render/export behavior was executed.

## Defect

Before the fix, `redact()` in `tools/contract-probes/blender/lib.mjs` handled secret-shaped object keys, `sk-...` values, and direct Bearer credentials, but raw non-Bearer Authorization header strings were not sanitized. A value such as:

```text
Authorization: Basic dXNlcjpwYXNzd29yZA==
```

was returned unchanged and could therefore appear in persisted Blender stdout/stderr evidence.

## Implementation

Added complete-value Authorization header sanitization before persistence:

```text
(Authorization\s*[:=]\s*)[^\r\n,;]+
```

No Blender discovery, CLI argument, fixture, artifact validation, process lifecycle, cancellation, or task-state semantics changed.

The Blender self-test now includes deterministic fake regressions for:

- `Authorization: Basic <credential>`,
- `Authorization: Bearer <opaque/JWT-like credential>`,
- existing direct Bearer handling.

## Exact-blob cloud verification

The exact GitHub branch blobs were reconstructed and executed locally:

```text
lib.mjs blob:       b5f9272d7fcab9e0911138fa12ec271e75772603
self-test.mjs blob: 84a1f832a2f4e00cd41a3b3fe79a5109e32338ec
```

`git hash-object` of the executed files matched those branch blob IDs exactly.

Commands/results:

```text
node --check lib.mjs        PASS
node --check self-test.mjs  PASS
node self-test.mjs          SELF_TEST_VERIFIED 17/17
```

The two new authorization regressions passed along with all existing missing-runtime, argument, artifact, factory-startup, fixture and validation tests.

## Secret scan

Only deterministic fake Basic/JWT-like values exist in regression test source. No live credential, provider token, owner configuration secret, or authorization value was read.

## Target boundary

This is `SELF_TEST_VERIFIED` cloud evidence only. Real Blender target execution remains required for `FCCD-P00-009`.

## Result

`CLOUD_FIX_VERIFIED_AWAITING_INTEGRATION`
