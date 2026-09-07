# FCC Code Desktop — Final Owner Acceptance Queue

**Status:** CANONICAL  
**Policy:** `docs/OWNER_LAST_EXECUTION_POLICY.md`  
**Purpose:** durable registry of genuine owner-machine/manual/environment-bound checks whose cloud preparation is complete enough to defer execution without waiving acceptance

## Rules

- A `QUEUED` item is a mandatory unresolved release blocker.
- `QUEUED` is not a substitute for `PASS`, `VERIFIED`, task `CLOSED`, or phase/release closure.
- Only genuinely environment-bound work may appear here.
- Code defects, failed CI, missing implementation/tests, security defects, data-integrity defects, or repairable repository problems are never owner-only.
- Queue sources may be unresolved mandatory tasks or an explicit phase-exit requirement whose cloud prerequisites are fully integrated and whose only remaining evidence is genuinely environment-bound.
- A phase-gate queue item never converts that phase gate to `PASS`; its gate remains truthfully unresolved until genuine evidence is reviewed and integrated.
- The tracked command/manual procedure must fail closed and must not manufacture evidence.
- Successful execution still requires evidence review, canonical integration, and source-task/phase-gate acceptance reconciliation before state becomes `PASS_INTEGRATED`.
- P22 and `VERIFIED_FINAL_COMPLETE=true` are prohibited while any required item remains `QUEUED`.
- Future eligible items are appended when their cloud prerequisites are actually complete; do not pre-defer unfinished future implementation.

## Current queue

### OWNER-P04-008-REAL-TARGET

`FCCD-P04-008 — Runtime contract suite` is cloud-complete but requires a fresh authoritative run on the owner's real Windows FCC/`fcc-claude`/provider environment. GitHub-hosted CI deliberately uses a controlled fake executable and is only `SELF_TEST_ONLY`; it cannot truthfully prove the provider-backed runtime contract. The tracked P04 target runner already enforces Windows, repository identity, exact HEAD, clean source inputs, .NET SDK `10.0.400`, REAL_TARGET classification, required runtime scenarios, and non-induced rate-limit evidence.

The genuine run must be performed only when the final-owner lane is intentionally executed. If it exposes a product defect, the source work is repaired and rerun; the queue item remains unresolved until real PASS evidence is integrated.

### OWNER-P05-EXIT-REAL-TARGET — PASS_INTEGRATED

The owner completed the P05 real-target phase-exit interaction on exact repository SHA `60b6ef491e9dde3ca195b377a2ce07442452a6ce`: a provider-backed task executed in the conversation surface, structured runtime activity was observed, Stop and Retry succeeded, the application closed/reopened, and the same session resumed with durable prior state. Sanitized machine-readable evidence is stored at `evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json` and reconciliation review is stored at `evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md`.

The tested SHA passed exact-main Windows CI `34092682076`, Workspace Search `34092682081`, and Large Workspace Safeguards `34092682122`. Later history-only repository commits before reconciliation retained the same Git tree `221ebaf238346bdcc91cdc2fe2a1524637e312f1`, so no source/configuration/packaging bytes changed. The P05 queue item is therefore reconciled as `PASS_INTEGRATED`; it is no longer an unresolved release blocker. P04 remains unresolved and release-blocking.

<!-- OWNER_ACCEPTANCE_QUEUE_JSON_BEGIN -->
```json
{
  "schemaVersion": 1,
  "items": [
    {
      "id": "OWNER-P04-008-REAL-TARGET",
      "sourceKind": "TASK",
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
    },
    {
      "id": "OWNER-P05-EXIT-REAL-TARGET",
      "sourceKind": "PHASE_GATE",
      "sourceRequirement": "P05_EXIT_GATE",
      "sourcePhase": "P05",
      "classification": "REAL_TARGET",
      "state": "PASS_INTEGRATED",
      "whyOwnerOnly": "The P05 exit criterion requires genuine interactive execution in the owner's Windows FCC Code Desktop with the installed fcc-claude/FCC/provider environment, followed by close/reopen and durable session resume; cloud CI can prove deterministic mechanics only.",
      "cloudEvidence": "evidence/phases/P05/P05_PHASE_EXIT_CLOUD_COMPLETE_OWNER_TARGET_REQUIRED_2026-09-05.md",
      "command": ".\\tools\\ui\\run-p05-phase-exit-owner-validation.ps1",
      "prerequisites": [
        "Owner Windows target",
        "Git and PowerShell 7 available on PATH",
        ".NET SDK 10.0.400",
        "Working installed fcc-claude/FCC/provider configuration",
        "Exact intended canonical candidate HEAD",
        "Ability to launch FCC Code Desktop and use a disposable project/session",
        "Clean source/config worktree except declared evidence outputs"
      ],
      "expectedEvidencePath": "evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json",
      "passCriteria": "Evidence is genuine REAL_TARGET for exact HEAD; overallStatus=PASS; a provider-backed task completes in the conversation surface; streamed output and structured activity are observed; stop then retry succeeds; the application closes and reopens; the same session resumes with prior durable conversation/task state intact; evidence is sanitized and records no prompt/provider content or credentials.",
      "reconciliationRule": "Review and integrate the genuine exact-head evidence, then reconcile the P05 exit gate. Any failed observation is product/recovery work. The runner never marks P05 CLOSED, never changes PHASE_EXIT_GATE, and never changes queue state.",
      "releaseBlocking": true,
      "integratedEvidence": "evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md"
    }
  ]
}
```
<!-- OWNER_ACCEPTANCE_QUEUE_JSON_END -->

## Final reconciliation rule

After each genuine run, a convergence worker must verify the generated evidence, exact tested SHA, sanitization, ancestry/applicability, and source acceptance criteria. Only then may it update the queue item to `PASS_INTEGRATED` and reconcile the corresponding source task or phase-gate acceptance state. A code/config/packaging change after exact-candidate validation invalidates affected evidence according to `docs/RELEASE_POLICY.md`.
