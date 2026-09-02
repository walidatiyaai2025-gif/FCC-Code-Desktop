# P00 Cloud Result — FCC CLI Probe Redaction Hardening

## Scope

Cross-probe secret-hygiene repair for `tools/contract-probes/fcc/probe.mjs` and its deterministic self-test only.

No provider traffic was generated. No authoritative Windows target evidence is claimed. The concurrently active target-manifest worker lane was not modified.

## Defect reproduced on pre-fix mechanics

The FCC discovery/CLI probe had an independent copy of the same unsafe authorization matcher previously fixed in `fcc-runtime`:

```text
(Authorization\s*[:=]\s*)([^\s,;]+)
```

With an opaque credential that does not match the `sk-...` or GitHub-token special patterns, the exact pre-fix replacement mechanics produced:

```text
INPUT:  Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.signature
OUTPUT: Authorization: [REDACTED] eyJhbGciOiJIUzI1NiJ9.payload.signature
LEAKED: true
```

The older FCC self-test exercised missing-runtime behavior and secret-named environment variables, but did not send a raw opaque Authorization header through stdout/stderr/event persistence.

## Implementation

The FCC probe authorization matcher now redacts the complete authorization value up to CR/LF/comma/semicolon:

```text
(Authorization\s*[:=]\s*)([^\r\n,;]+)
```

No invocation, failure-classification, discovery, cancellation, or provider semantics changed.

The existing `fcc/self-test.mjs` now also creates a disposable Node fixture that emits deterministic fake values through the probe's real streaming/persistence path:

- `Authorization: Bearer <opaque/JWT-like fake>` on stdout,
- `Authorization: Basic <fake base64>` on stderr,
- three workspace cases,
- the cancellation case,
- persisted JSON stdout/stderr/events.

It requires the persisted result to contain neither fake credential and requires a `[REDACTED]` marker to be observed in captured output.

## Cloud verification

### Pre-fix regression

PASS: the old exact pattern/replacement mechanics reproduced the opaque bearer leak.

### Post-fix redaction mechanics

PASS / `SELF_TEST_VERIFIED` using the exact new pattern/replacement mechanics:

```json
{
  "status": "SELF_TEST_VERIFIED",
  "outputs": [
    "Authorization: [REDACTED]",
    "Bearer [REDACTED]",
    "Authorization: [REDACTED]"
  ]
}
```

### Updated self-test source

- `node --check` passed for the modified self-test source reconstructed from the branch content.
- GitHub branch blob `cddc219add5bcc2791cfe6f2f7a295f5a0f315cc` contains the end-to-end opaque Bearer/Basic persistence assertions.
- GitHub branch probe blob `c625e68a59625e26dd77f7504ec9024ef56132e3` contains the complete-value Authorization matcher.

### Execution limitation

The full repository-owned `fcc/self-test.mjs` could not be executed against an exact local branch checkout in this cloud shell because this execution environment cannot resolve/download the GitHub checkout. This is recorded as a cloud execution limitation, not converted into a PASS. The changed security mechanics themselves were executed directly, and the full self-test remains the canonical regression command for environments with a checkout.

No Windows/FCC target behavior is inferred from these cloud checks.

## Regression / overlap check

- target-manifest/runner files: untouched,
- `fcc-runtime` files: untouched by this branch,
- session/resume logic: untouched,
- process ownership/cancellation implementation: untouched,
- Unity/Blender: untouched,
- task states / closure policy: untouched.

## Secret scan

Only deterministic fake regression credentials are present in test source/evidence. No live token, API key, provider credential, or owner configuration value was read or written by this work.

## Result

`CLOUD_FIX_IMPLEMENTED_AND_DIRECTLY_VERIFIED_AWAITING_INTEGRATION`
