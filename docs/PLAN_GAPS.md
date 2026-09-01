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

None.

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
