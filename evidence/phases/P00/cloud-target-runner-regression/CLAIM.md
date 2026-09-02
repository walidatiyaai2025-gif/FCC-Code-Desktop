# Cloud P00 Claim — Target Runner Regression Coverage

- Branch: `worker/p00-cloud-runner-regression-tests`
- Started from live main: `0b12242f122b0c6b69a703436c24b617708ca3f3`
- Date: 2026-09-02
- Scope: repository-owned deterministic regression tests for `tools/contract-probes/run-target-validation.ps1` exact-head/rerun-safety policy only
- State: `COMPLETE_AWAITING_INTEGRATION`

This claim is intentionally narrower than any P00 task. It does not change task closure semantics, does not produce target-machine evidence, and does not alter provider/Unity/Blender contracts. The goal is to preserve the already-merged PR #6/#13 provenance and rerun-safety guarantees with repeatable cloud-executable regression coverage.

Implementation and cloud self-test evidence are recorded in `RESULT.md`. The target runner itself was intentionally left unchanged so prior authoritative target evidence is not invalidated by behavior changes from this hardening item.
