# Cloud P00 Claim — Target Evidence Artifact Integrity

- Branch: `worker/p00-cloud-target-evidence-artifact-integrity`
- Started from live main: `353b36dc4a29c067548183e3fff793bcc5dae459`
- Date: 2026-09-02
- Scope: prevent unified target-evidence summary lanes from reporting PASS/BLOCKED when their mandatory evidence file is missing or unreadable, and add deterministic regression coverage
- State: `COMPLETE_AWAITING_INTEGRATION`
- Result: `evidence/phases/P00/cloud-target-evidence-artifact-integrity/RESULT.md`

## Why this claim exists

`tools/contract-probes/target-evidence-summary.mjs` previously derived top-level lane `status` from the supplied probe exit code even when `readEvidence()` reported `EVIDENCE_FILE_MISSING`, `EVIDENCE_JSON_UNREADABLE`, or no evidence path. A stale/incorrect exit code of `0` could therefore produce `status: PASS` with no parseable mandatory artifact. This violated the binding P00 target-validation requirement that mandatory evidence be present and machine-readable and the project rule that process exit code alone cannot prove success.

The narrow repair and deterministic regression coverage are complete. This claim owns only the summary artifact-integrity guard until its focused integration PR merges or is explicitly abandoned. It does not change FCC/Unity/Blender probe behavior, provider traffic, task closure semantics, target observations, rate-limit policy, or later-phase implementation.
