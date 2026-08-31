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

None at repository initialization.
