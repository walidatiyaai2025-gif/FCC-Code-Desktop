# Cloud P00 Claim — Unified Target Manifest Schema

- Branch: `worker/p00-cloud-target-manifest-schema`
- Started from live main: `1fdf0adaf88e4a52759e07fd913e4bb791b5c9ae`
- Date: 2026-09-02
- Scope: narrow cloud-actionable evidence-schema defect in `tools/contract-probes/run-target-validation.ps1`
- State: `IN_PROGRESS`

The binding P00 target-validation contract requires the unified manifest to identify discovered tool versions, exact PASS/FAIL/BLOCKED reasons, artifact paths, and whether results were observed on the actual target machine. The current runner records only generic step status/exit code/path with an empty note. This claim owns only that manifest-completeness defect and associated deterministic regression coverage/documentation. It does not claim or manufacture Windows/provider/Blender target evidence and does not change P00 closure semantics.
