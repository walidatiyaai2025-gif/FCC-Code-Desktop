from pathlib import Path
import subprocess

current_path = Path("CURRENT_PHASE.md")
ledger_path = Path("docs/TASK_LEDGER.md")
evidence_path = Path("evidence/phases/P09/P09_005_INTEGRATED_RECONCILIATION_2026-09-08.md")
workflow_path = Path(".github/workflows/p09-005-reconcile-bootstrap.yml")
helper_path = Path(".github/p09-005-reconcile.py")

current = current_path.read_text(encoding="utf-8")
old_current = "- `FCCD-P09-005` — Artifact manifest/validation framework — PENDING."
new_current = "- `FCCD-P09-005` — Artifact manifest/validation framework — CLOSED."
if current.count(old_current) != 1:
    raise SystemExit(f"CURRENT_PHASE row count mismatch: {current.count(old_current)}")
if "<!-- FCCD-P09-005-INTEGRATED-CLOSURE -->" in current:
    raise SystemExit("P09-005 provenance already exists")
current = current.replace(old_current, new_current, 1)
current += """

<!-- FCCD-P09-005-INTEGRATED-CLOSURE -->
## P09-005 integration provenance

- Task: `FCCD-P09-005 — Artifact manifest/validation framework` — `CLOSED` in this reconciliation candidate.
- Implementation PR: #222 (`worker/fccd-p09-005-artifact-manifest-validation`).
- Exact accepted implementation candidate: `b0381ad11381a958eb6ef588fde057630e246be5`.
- Exact implementation-head Windows CI: run `34184899985` / #608 — SUCCESS.
- Exact implementation-head P06-007 Workspace Search: run `34184899988` / #337 — SUCCESS.
- Exact implementation-head P06-008 Large Workspace Safeguards: run `34184900036` / #321 — SUCCESS.
- Normal implementation merge / accepted implementation main: `82a251d9c957e418818a2b85755905410a0a5eab`.
- Accepted implementation head and normal merge have the same Git tree `5d1ce66c1f476a894dc33c26780ea2d397fed0ba`.
- Exact implementation-main Windows CI: run `34185404436` / #609 — SUCCESS.
- Exact implementation-main P06-007 Workspace Search: run `34185404429` / #338 — SUCCESS.
- Exact implementation-main P06-008 Large Workspace Safeguards: run `34185404427` / #322 — SUCCESS.
- Integrated evidence: `evidence/phases/P09/P09_005_INTEGRATED_RECONCILIATION_2026-09-08.md`.
- No owner-only evidence is required or added. P09 remains `IN_PROGRESS`; `FCCD-P09-006` through `FCCD-P09-008` remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P10 and later implementation remain prohibited; `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `VERIFIED_FINAL_COMPLETE=false`.
"""
current_path.write_text(current.rstrip("\n") + "\n", encoding="utf-8")

ledger = ledger_path.read_text(encoding="utf-8")
old_ledger = "| FCCD-P09-005 | Artifact manifest/validation framework | PENDING |"
new_ledger = "| FCCD-P09-005 | Artifact manifest/validation framework | CLOSED |"
if ledger.count(old_ledger) != 1:
    raise SystemExit(f"TASK_LEDGER row count mismatch: {ledger.count(old_ledger)}")
ledger_path.write_text(ledger.replace(old_ledger, new_ledger, 1), encoding="utf-8")

if evidence_path.exists():
    raise SystemExit("P09-005 evidence already exists")
evidence_path.parent.mkdir(parents=True, exist_ok=True)
evidence_path.write_text("""# FCCD-P09-005 — Integrated Reconciliation Evidence

Date: 2026-09-08
Task: `FCCD-P09-005 — Artifact manifest/validation framework`
Classification: CLOUD / HOSTED-WINDOWS / INTEGRATED
Canonical task result: CLOSED after normal implementation integration, exact-main validation, and this reconciliation.

## Live-state recovery

The scheduling request carried a future P17-005 hint, but canonical `CURRENT_PHASE=P09`. Live state showed P09-004 already CLOSED and P09-005 implementation already normally merged while its canonical row remained PENDING. Recovery-first governance therefore required reconciling that integrated P09-005 work before selecting any new task. No competing P09-005 reconciliation PR or branch existed when this closure branch was created.

Pre-reconciliation canonical main: `82a251d9c957e418818a2b85755905410a0a5eab`.

## Implementation

- Implementation PR: #222 — `P09-005: add artifact manifest validation framework`.
- Branch: `worker/fccd-p09-005-artifact-manifest-validation`.
- Exact accepted implementation candidate: `b0381ad11381a958eb6ef588fde057630e246be5`.
- Product implementation: `src/FCCCodeDesktop.Tools/ToolArtifactValidation.cs`.
- Focused tests: `tests/FCCCodeDesktop.UnitTests/ToolArtifactValidationTests.cs`.
- The provider-neutral manifest snapshots artifact declarations beneath a fully-qualified output root and rejects rooted/traversal/NUL/duplicate-id/duplicate-path declarations before filesystem access.
- File and directory artifacts support optional non-empty requirements; file declarations may include normalized expected SHA-256 values.
- Validation records actual file length/SHA-256 and classifies missing, type mismatch, empty, hash mismatch, unsafe path, and unreadable outcomes.
- Containment checks and reparse-point refusal fail closed instead of following unsafe output paths.
- Cancellation propagates through validation, and `ToolArtifactValidationEvent` carries structured validation evidence through the existing external-tool event seam.
- Tests cover manifest immutability and invalid declarations, valid file/directory evidence, independent failure classifications, cancellation, unsafe reparse behavior, and empty manifests.
- Scope excludes P09-006 diagnostics, P09-007 CLI/process primitives, P09-008 protocol seams, and all P10/P11 adapter implementation.

## Exact implementation-head validation

On `b0381ad11381a958eb6ef588fde057630e246be5`:

- Windows CI `34184899985` / #608 — SUCCESS.
- P06-007 Workspace Search `34184899988` / #337 — SUCCESS.
- P06-008 Large Workspace Safeguards `34184900036` / #321 — SUCCESS.

## Normal implementation integration

PR #222 was normally merged as `82a251d9c957e418818a2b85755905410a0a5eab`, preserving the accepted implementation head as a merge parent. No squash, rebase, force-push, or fabricated evidence is claimed. The accepted head and merge commit share Git tree `5d1ce66c1f476a894dc33c26780ea2d397fed0ba`, so the integrated product bytes are identical to the exact tested head.

## Exact implementation-main validation

On exact canonical main `82a251d9c957e418818a2b85755905410a0a5eab`:

- Windows CI `34185404436` / #609 — SUCCESS, including the complete Release baseline and permanent Windows validators.
- P06-007 Workspace Search `34185404429` / #338 — SUCCESS.
- P06-008 Large Workspace Safeguards `34185404427` / #322 — SUCCESS.

## Owner-last classification

P09-005 has no genuine owner-machine/manual/provider/Unity/Blender acceptance requirement. Its manifest and filesystem-validation semantics are fully cloud/hosted-Windows verifiable, so no owner queue item is added. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item and is unchanged.

## Reconciliation boundary

This reconciliation closes only `FCCD-P09-005`. P09 remains `IN_PROGRESS`; P09-006 through P09-008 remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P10 and later phases remain prohibited until sequential P09 convergence completes; `VERIFIED_FINAL_COMPLETE=false`.

The reconciliation PR itself must pass exact-head CI, be normally merged, and the resulting exact canonical main must remain green before this task closure is treated as the durable endpoint.
""", encoding="utf-8")

workflow_path.unlink(missing_ok=False)
helper_path.unlink(missing_ok=False)

subprocess.run(["git", "diff", "--check"], check=True)
changed = set(subprocess.check_output(["git", "diff", "--name-only"]).decode().splitlines())
expected = {
    "CURRENT_PHASE.md",
    "docs/TASK_LEDGER.md",
    "evidence/phases/P09/P09_005_INTEGRATED_RECONCILIATION_2026-09-08.md",
    ".github/workflows/p09-005-reconcile-bootstrap.yml",
    ".github/p09-005-reconcile.py",
}
if changed != expected:
    raise SystemExit(f"unexpected working diff: {sorted(changed)}")
