# Cloud P00 Claim — Unity Authorization Redaction Hardening

- Branch: `worker/p00-cloud-unity-redaction-hardening`
- Started from live main: `71d9be8f5d69b9b798745e3c57babb37bc46e47e`
- Date: 2026-09-02
- Scope: fix and regression-test non-Bearer Authorization-header leakage in Unity probe persistence sanitization only
- State: `CLOSED` — integrated during owner-authorized branch convergence on 2026-11-06; Unity self-tests pass 21/21, including a complete multiword Basic Authorization value and a following-line preservation regression. This closes only the sanitizer repair, not product/release acceptance.

## Why this claim exists

A cross-probe secret-hygiene audit found that Unity redaction handles direct Bearer credentials before its Authorization matcher, so bearer-form values are protected, but `Authorization: Basic <credential>` still leaks the credential because the Authorization matcher consumes only the first non-whitespace token (`Basic`).

This is a sanitizer-only repair. It does not change Unity project/version detection, editor selection, command builders, process behavior, log classification, tests, build/artifact validation, locking, cancellation, or any target-observed Unity contract. `FCCD-P00-008` external-contract closure is not being redefined by this claim; only deterministic repository-owned evidence sanitization is in scope.
