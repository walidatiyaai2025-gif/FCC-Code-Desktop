from pathlib import Path

current = Path("CURRENT_PHASE.md")
ledger = Path("docs/TASK_LEDGER.md")
evidence = Path("evidence/phases/P09/P09_004_INTEGRATED_RECONCILIATION_2026-09-08.md")
workflow = Path(".github/workflows/p09-004-reconcile-bootstrap.yml")
helper = Path(".github/p09-004-reconcile-bootstrap.py")

current_text = current.read_text(encoding="utf-8")
ledger_text = ledger.read_text(encoding="utf-8")

current_old = "- `FCCD-P09-004` — Tool resource locking — PENDING."
current_new = "- `FCCD-P09-004` — Tool resource locking — CLOSED."
ledger_old = "| FCCD-P09-004 | Tool resource locking | PENDING |"
ledger_new = "| FCCD-P09-004 | Tool resource locking | CLOSED |"
marker = "<!-- FCCD-P09-004-INTEGRATED-CLOSURE -->"

if current_text.count(current_old) != 1:
    raise SystemExit(f"Expected exactly one CURRENT_PHASE P09-004 PENDING row, found {current_text.count(current_old)}")
if ledger_text.count(ledger_old) != 1:
    raise SystemExit(f"Expected exactly one TASK_LEDGER P09-004 PENDING row, found {ledger_text.count(ledger_old)}")
if marker in current_text:
    raise SystemExit("P09-004 provenance marker already exists; refusing duplicate reconciliation")
if evidence.exists():
    raise SystemExit("P09-004 evidence file already exists; refusing overwrite")

current_text = current_text.replace(current_old, current_new, 1)
ledger_text = ledger_text.replace(ledger_old, ledger_new, 1)

provenance = """

<!-- FCCD-P09-004-INTEGRATED-CLOSURE -->
## P09-004 integration provenance

- Task: `FCCD-P09-004 — Tool resource locking` — `CLOSED` in this reconciliation candidate.
- Implementation PR: #220 (`worker/fccd-p09-004-tool-resource-locking`).
- Exact accepted implementation candidate: `c9c9b2af6df94ce9de897fdbcd7b9340821e5c00`.
- Exact implementation-head Windows CI: run `34181523680` / #602 — SUCCESS.
- Exact implementation-head P06-007 Workspace Search: run `34181523686` / #331 — SUCCESS.
- Exact implementation-head P06-008 Large Workspace Safeguards: run `34181523684` / #315 — SUCCESS.
- Normal implementation merge / accepted implementation main: `c0958e04c363cec3fa389baaabb9d3ddc4c8b49a`.
- Accepted implementation head and normal merge have the same Git tree `ab6987d6051c20ed1b69149346991d5e7999010d`.
- Exact implementation-main Windows CI: run `34182264507` / #603 — SUCCESS.
- Exact implementation-main P06-007 Workspace Search: run `34182264471` / #332 — SUCCESS.
- Exact implementation-main P06-008 Large Workspace Safeguards: run `34182264516` / #316 — SUCCESS.
- Integrated evidence: `evidence/phases/P09/P09_004_INTEGRATED_RECONCILIATION_2026-09-08.md`.
- No owner-only evidence is required or added. P09 remains `IN_PROGRESS`; `FCCD-P09-005` through `FCCD-P09-008` remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P10 and later implementation remain prohibited; `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `VERIFIED_FINAL_COMPLETE=false`.
"""

evidence_text = """# FCCD-P09-004 — Integrated Reconciliation Evidence

Date: 2026-09-08
Task: `FCCD-P09-004 — Tool resource locking`
Classification: CLOUD / HOSTED-WINDOWS / INTEGRATED
Canonical task result: CLOSED after normal implementation integration, exact-main validation, and this reconciliation.

## Live-state recovery

The scheduling request carried future P17 hints, but canonical `CURRENT_PHASE=P09`. A legitimate in-flight P09-004 branch/PR already existed and had cloud-repairable CI failures, so recovery-first governance required completing `FCCD-P09-004` rather than starting P17 or a second P09 task. No competing P09-004 reconciliation branch or open reconciliation PR existed before this closure branch was created.

Pre-reconciliation canonical main: `c0958e04c363cec3fa389baaabb9d3ddc4c8b49a`.

## Implementation

- Implementation PR: #220 — `P09-004: add external tool resource locking`.
- Branch: `worker/fccd-p09-004-tool-resource-locking`.
- Exact accepted implementation candidate: `c9c9b2af6df94ce9de897fdbcd7b9340821e5c00`.
- Product implementation: `src/FCCCodeDesktop.Tools/ToolResourceLocking.cs`.
- Focused tests: `tests/FCCCodeDesktop.UnitTests/ToolResourceLockingTests.cs`.
- `ToolResourceLockKey` provides validated, provider-neutral resource identities.
- `IExternalToolResourceLockProvider` requires adapters to explicitly declare invocation locks; the coordinator fails closed when the declaration contract is absent or invalid.
- `ToolResourceLockManager` provides case-insensitive keyed exclusivity, deterministic sorted multi-key acquisition, duplicate-key collapse, concurrency for unrelated resources, cancellation-safe partial-acquisition rollback, and reference-cleaned semaphore ownership.
- `ToolResourceLockLease` supports deterministic idempotent synchronous/asynchronous release.
- Tests cover invalid keys, same-key serialization, independent-resource concurrency, reversed/duplicate multi-key requests, cancellation recovery, partial-acquisition rollback, explicit adapter declarations, and empty lock sets.
- Scope deliberately excludes P09-005 artifact validation, P09-006 diagnostics, P09-007 CLI/process primitives, P09-008 protocol seams, and all P10/P11 adapter implementation.

## Cloud repair history

Early PR heads exposed only cloud-repairable quality defects and were not accepted: missing final newlines stopped format verification, analyzer `CA1859` required a concrete private return type, and analyzer `CA1861` required a shared static expected-array fixture. Each defect was repaired on the same task/PR and the superseded failing heads are not closure evidence.

## Exact implementation-head validation

On `c9c9b2af6df94ce9de897fdbcd7b9340821e5c00`:

- Windows CI `34181523680` / #602 — SUCCESS.
- P06-007 Workspace Search `34181523686` / #331 — SUCCESS.
- P06-008 Large Workspace Safeguards `34181523684` / #315 — SUCCESS.

## Normal implementation integration

PR #220 was normally merged as `c0958e04c363cec3fa389baaabb9d3ddc4c8b49a`, preserving the tested implementation head as a merge parent. No squash, rebase, force-push, or fabricated evidence is claimed. The accepted implementation head and merge commit share Git tree `ab6987d6051c20ed1b69149346991d5e7999010d`, so the integrated product bytes are identical to the accepted head.

## Exact implementation-main validation

On exact canonical main `c0958e04c363cec3fa389baaabb9d3ddc4c8b49a`:

- Windows CI `34182264507` / #603 — SUCCESS, including Release baseline and all permanent Windows validators.
- P06-007 Workspace Search `34182264471` / #332 — SUCCESS.
- P06-008 Large Workspace Safeguards `34182264516` / #316 — SUCCESS.

## Owner-last classification

P09-004 has no genuine owner-machine/manual/provider/Unity/Blender acceptance requirement. Its locking semantics are fully cloud/hosted-Windows verifiable, so no owner queue item is added. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item and is unchanged.

## Reconciliation boundary

This reconciliation closes only `FCCD-P09-004`. P09 remains `IN_PROGRESS`; P09-005 through P09-008 remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P10 and later phases remain prohibited until sequential P09 convergence completes; `VERIFIED_FINAL_COMPLETE=false`.

The reconciliation PR itself must pass exact-head CI, be normally merged, and the resulting exact canonical main must remain green before this task closure is treated as the durable endpoint.
"""

current.write_text(current_text.rstrip("\n") + provenance + "\n", encoding="utf-8", newline="\n")
ledger.write_text(ledger_text, encoding="utf-8", newline="\n")
evidence.parent.mkdir(parents=True, exist_ok=True)
evidence.write_text(evidence_text, encoding="utf-8", newline="\n")
workflow.unlink()
helper.unlink()
