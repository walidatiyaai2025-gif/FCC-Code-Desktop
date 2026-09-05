# FCC Code Desktop — Final Owner Acceptance Queue

**Status:** CANONICAL  
**Policy:** `docs/OWNER_LAST_EXECUTION_POLICY.md`  
**Purpose:** durable registry of genuine owner-machine/manual/environment-bound checks whose cloud preparation is complete enough to defer execution without waiving acceptance

## Rules

- A `QUEUED` item is a mandatory unresolved release blocker.
- `QUEUED` is not a substitute for `PASS`, `VERIFIED`, task `CLOSED`, or phase/release closure.
- Only genuinely environment-bound work may appear here.
- Code defects, failed CI, missing implementation/tests, security defects, data-integrity defects, or repairable repository problems are never owner-only.
- The tracked command/manual procedure must fail closed and must not manufacture evidence.
- Successful execution still requires evidence review, canonical integration, and source-task/acceptance reconciliation before state becomes `PASS_INTEGRATED`.
- P22 and `VERIFIED_FINAL_COMPLETE=true` are prohibited while any required item remains `QUEUED`.
- Future eligible items are appended when their cloud prerequisites are actually complete; do not pre-defer unfinished future implementation.

## Current queue

### OWNER-P04-008-REAL-TARGET

`FCCD-P04-008 — Runtime contract suite` is cloud-complete but requires a fresh authoritative run on the owner's real Windows FCC/`fcc-claude`/provider environment. GitHub-hosted CI deliberately uses a controlled fake executable and is only `SELF_TEST_ONLY`; it cannot truthfully prove the provider-backed runtime contract. The tracked P04 target runner already enforces Windows, repository identity, exact HEAD, clean source inputs, .NET SDK `10.0.400`, REAL_TARGET classification, required runtime scenarios, and non-induced rate-limit evidence.

The genuine run must be performed only when the final-owner lane is intentionally executed. If it exposes a product defect, the source work is repaired and rerun; the queue item remains unresolved until real PASS evidence is integrated.

<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->
```json
{
  "schemaVersion": 1,
  "items": [
    {
      "id": "OWNER-P04-008-REAL-TARGET",
      "sourceTask": "FCCD-P04-008",
      "sourcePhase": "P04",
      "classification": "REAL_TARGET",
      "state": "QUEUED",
      "whyOwnerOnly": "Requires the owner's installed Windows fcc-claude/FCC/provider environment; cloud CI uses a controlled fake executable and provides SELF_TEST_ONLY evidence only.",
      "cloudEvidence": "evidence/phases/P04/P04_008_CLOUD_COMPLETE_TARGET_VALIDATION_REQUIRED_2026-09-04.md",
      "command": ".\\tools\\runtime\\run-p04-runtime-target-validation.ps1",
      "prerequisites": [
        "Owner Windows target",
        "Git available on PATH",
        ".NET SDK 10.0.400",
        "Working installed fcc-claude/FCC/provider configuration",
        "Exact intended canonical candidate HEAD",
        "Clean source worktree except declared evidence outputs"
      ],
      "expectedEvidencePath": "evidence/phases/P04/runtime-contract/P04_RUNTIME_TARGET_EVIDENCE.json",
      "passCriteria": "Evidence is genuine REAL_TARGET for exact HEAD; overallStatus=PASS; all required structured success/stream/session, resume, invalid-session failure, cancellation, and fallback scenarios PASS; rateLimitObservation=NOT_INDUCED; evidence is sanitized.",
      "reconciliationRule": "Review and integrate genuine evidence, then reconcile FCCD-P04-008 and its P04 acceptance obligation. Any failed scenario is product/recovery work, not an owner waiver. The runner never auto-closes the task or queue item.",
      "releaseBlocking": true
    }
  ]
}
```
<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->

## Final reconciliation rule

After each genuine run, a convergence worker must verify the generated evidence, exact tested SHA, sanitization, ancestry/applicability, and source acceptance criteria. Only then may it update the queue item to `PASS_INTEGRATED` and reconcile the corresponding task/phase/acceptance state. A code/config/packaging change after exact-candidate validation invalidates affected evidence according to `docs/RELEASE_POLICY.md`.