# FCC Code Desktop — Plan Gap Register

**Status:** CANONICAL  
**Purpose:** record potential missing requirements without allowing ordinary workers to rewrite the canonical plan.

---

## Authority rule

Ordinary implementation workers may identify a possible plan gap, but they must not change phase order, product scope, architecture doctrine, acceptance policy, or mandatory release requirements on their own.

A worker that discovers a genuine suspected omission must add a concise entry here and continue only the work already authorized by the canonical plan when safe to do so.

Canonical plan changes require explicit planning/reconciliation authority and must then be propagated consistently into the relevant canonical documents.

---

## Entry format

```text
PLAN_GAP_ID:
DISCOVERED_BY:
DATE:
CURRENT_PHASE:
RELATED_TASK:
DESCRIPTION:
WHY_CURRENT_PLAN_MAY_BE_INCOMPLETE:
EVIDENCE:
BLOCKS_CURRENT_TASK: yes/no
BLOCKS_PHASE_EXIT: yes/no
SUGGESTED_DESTINATION_IF_APPROVED:
STATUS: OPEN | ACCEPTED | REJECTED | RESOLVED
RESOLUTION_REFERENCE:
```

---

## Open gaps

```text
PLAN_GAP_ID: PG-002-P00-RATE-LIMIT-CLOSURE
DISCOVERED_BY: autonomous cloud P00 convergence audit
DATE: 2026-09-02
CURRENT_PHASE: P00
RELATED_TASK: FCCD-P00-005
DESCRIPTION: P00 requires cancel/interrupt/failure/rate-limit behavior to be captured, while the binding target-validation safety policy forbids intentionally generating provider load merely to force a rate limit. Historical real-target evidence verified provider 503, timeout/cancellation behavior and owned cleanup observations, but no natural HTTP/provider 429 has occurred. PR #9 later hardened the target-relevant process-ownership probe, so FCCD-P00-005 is now independently BLOCKED pending an exact-head Windows rerun of that hardened probe. After the rerun, the separate rate-limit closure question will still remain unless a natural rate-limit event is observed: no canonical planning decision currently defines whether safe NOT_OBSERVED_ON_TARGET plus verified classifier mechanics is sufficient for CLOSED.
WHY_CURRENT_PLAN_MAY_BE_INCOMPLETE: Without an explicit safe closure semantic, FCCD-P00-005 can remain indefinitely below CLOSED based on an external event workers are prohibited from inducing. Conversely, treating NOT_OBSERVED_ON_TARGET as sufficient without planning authority would silently weaken a mandatory P00 outcome. The implementation worker cannot legitimately choose between those policies.
EVIDENCE: docs/EXECUTION_PLAN.md P00 mandatory outcomes require rate-limit behavior to be captured; docs/P00_TARGET_MACHINE_VALIDATION.md forbids intentional load merely to force rate limiting and requires NOT_OBSERVED to remain distinct from PASS; docs/contracts/FCC_FAILURE_CONTRACT.md records deterministic rate-limit classifier mechanics, historical target failure/cancellation evidence, the post-PR #9 exact-head rerun requirement, and RATE_LIMIT = NOT_OBSERVED_ON_TARGET; evidence/phases/P00/fcc-target/TARGET_RECONCILIATION_2026-09-02.md records the historical target boundary.
BLOCKS_CURRENT_TASK: yes
BLOCKS_PHASE_EXIT: yes
SUGGESTED_DESTINATION_IF_APPROVED: P00 planning/reconciliation authority should add an explicit ADR/contract decision defining the safe closure boundary for unforced rate-limit observation and propagate it consistently to EXECUTION_PLAN, FCC_FAILURE_CONTRACT, TASK_LEDGER, and P00 closure criteria.
STATUS: OPEN
RESOLUTION_REFERENCE:
```

---

## Resolved gaps

```text
PLAN_GAP_ID: PG-001-P00-TARGET-EXECUTION
DISCOVERED_BY: planning/reconciliation authority after P00 Worker 1 result
DATE: 2026-09-01
CURRENT_PHASE: P00
RELATED_TASK: FCCD-P00-002, FCCD-P00-003, FCCD-P00-004, FCCD-P00-005, FCCD-P00-007, FCCD-P00-008, FCCD-P00-009
DESCRIPTION: Cloud/remote workers can build contract probes but cannot truthfully observe the owner's actual Windows FCC/fcc-claude, Unity, or Blender installations. Without an explicit local-evidence lane, P00 target-dependent tasks can remain BLOCKED indefinitely even when their probe infrastructure is complete.
WHY_CURRENT_PLAN_MAY_BE_INCOMPLETE: P00 required real target evidence but did not define how remote AI workers hand off deterministic probes to a trusted executor on the target Windows machine and return sanitized evidence to the repository.
EVIDENCE: PR #1 merged valid FCC/CLI probe infrastructure while correctly leaving FCCD-P00-002 and FCCD-P00-007 BLOCKED because the worker host had no target FCC/fcc-claude installation.
BLOCKS_CURRENT_TASK: yes for target-dependent closure
BLOCKS_PHASE_EXIT: yes
SUGGESTED_DESTINATION_IF_APPROVED: P00 target-machine validation supplement
STATUS: RESOLVED
RESOLUTION_REFERENCE: docs/P00_TARGET_MACHINE_VALIDATION.md introduced the mandatory two-lane remote-probe + local-target-validation workflow and unified target runner requirement.
```
