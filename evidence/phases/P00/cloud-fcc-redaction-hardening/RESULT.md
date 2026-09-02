# P00 Cloud Result — FCC Runtime Redaction Hardening

## Scope

Narrow cloud-actionable secret-hygiene defect in `tools/contract-probes/fcc-runtime/common.mjs` plus regression coverage in `self-test.mjs`.

No target-machine evidence is claimed. No provider traffic was generated. No session, CLI fallback, Unity, Blender, target-manifest schema, or task-state semantics were changed.

## Defect reproduced before fix

The pre-fix prefix order/patterns were reproduced with a generic opaque/JWT-like bearer credential that intentionally does not match the earlier `sk-...` / GitHub-token simple patterns:

```text
INPUT:  Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.signature
OUTPUT: Authorization: [REDACTED] eyJhbGciOiJIUzI1NiJ9.payload.signature
LEAKED: true
```

Root cause: the `Authorization` matcher consumed only the first non-whitespace token (`Bearer`). The generic bearer value remained after that substitution, and the later `Bearer` matcher could no longer see the removed scheme word. The existing regression used an `sk-...` value, which was already removed by the earlier simple-secret matcher and therefore did not expose this defect.

## Implementation

The `Authorization` prefix matcher now treats the complete authorization header value up to CR/LF/comma/semicolon as the secret payload:

```text
(Authorization\s*[:=]\s*)([^\r\n,;]+)
```

Both `redactString` and `maskSecretsPreserveLength` share this pattern table, so the fix applies consistently to sanitized text and length-preserving raw-frame masking.

`self-test.mjs` now covers:

- existing `sk-...` authorization fixture,
- opaque/JWT-like `Authorization: Bearer <value>`,
- direct `Bearer <value>`,
- `Authorization: Basic <value>`,
- length preservation for an opaque authorization header,
- persisted-output assertions for all added deterministic fake credentials.

## Cloud verification

Direct regression with the exact new pattern/function mechanics:

```json
{
  "status": "SELF_TEST_VERIFIED",
  "opaqueBearerLeak": false,
  "basicAuthorizationLeak": false,
  "preserveLength": true
}
```

GitHub branch inspection confirms the updated `common.mjs` blob contains the complete-value authorization matcher and the updated `self-test.mjs` blob contains the new opaque/Bearer/Basic regression assertions.

This execution host cannot produce authoritative Windows FCC behavior. These checks are `SELF_TEST_VERIFIED` only.

## Regression / scope check

- no change to failure classification categories,
- no change to process ownership/cancellation logic,
- no change to FCC invocation arguments,
- no change to target runner or active target-manifest-schema worker files,
- no change to Unity or Blender probes,
- no task promotion or closure claim.

## Secret scan

The changed source contains only deterministic fake regression values (`sk-FAKE...`, a fabricated JWT-like string, and fabricated Basic base64 text). No real credential, authorization header value, API key, or provider secret was introduced.

## Result

`CLOUD_FIX_VERIFIED_AWAITING_INTEGRATION`
