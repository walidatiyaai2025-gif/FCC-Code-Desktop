from pathlib import Path

implementation_head = "391f9caf8cd53cc810ca02012def35d7815b937a"
implementation_merge = "f889b901ebc9fda362813c18827585551775e877"
evidence_path = "evidence/phases/P07/P07_011_INTEGRATED_RECONCILIATION_2026-09-07.md"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)

path = Path("CURRENT_PHASE.md")
text = path.read_text(encoding="utf-8")
text = replace_once(text, "- `FCCD-P07-011` — Git integration tests/conflict scenarios — PENDING.", "- `FCCD-P07-011` — Git integration tests/conflict scenarios — CLOSED.", "CURRENT_PHASE P07-011 row")
marker = "## P07 cloud activation provenance"
if text.count(marker) != 1:
    raise SystemExit("CURRENT_PHASE activation marker mismatch")
provenance = f"""## P07-011 integration provenance

- Exact implementation candidate: `{implementation_head}` from PR #185 (`worker-b/fccd-p07-011-git-integration-conflicts`).
- PR #185 exact-head Windows CI: run `34066314053` / run #428 — SUCCESS.
- PR #185 exact-head P06-007 Workspace Search: run `34066314086` / run #157 — SUCCESS.
- PR #185 exact-head P06-008 Large Workspace Safeguards: run `34066314047` / run #141 — SUCCESS.
- Normal merge commit: `{implementation_merge}`.
- Exact post-merge canonical-main Windows CI: run `34066787222` / run #429 — SUCCESS.
- Exact post-merge P06-007 Workspace Search: run `34066787177` / run #158 — SUCCESS.
- Exact post-merge P06-008 Large Workspace Safeguards: run `34066787145` / run #142 — SUCCESS.
- Integrated evidence: `{evidence_path}`.
- Evidence class is cloud/self-test plus canonical integration provenance for final P07 Git workflow/conflict acceptance. All mandatory P07 task rows are now CLOSED, but `PHASE_EXIT_GATE` remains `NOT_RUN` until a separate canonical phase-exit decision is executed and integrated. No P08/P12 authorization or release-readiness claim is implied.

"""
text = text.replace(marker, provenance + marker, 1)
path.write_text(text, encoding="utf-8", newline="\n")

path = Path("PROJECT_CONTROL.md")
text = path.read_text(encoding="utf-8")
text = replace_once(text, "`FCCD-P07-011` remains PENDING; workers must select dependency-valid unclaimed P07 work and preserve user changes, conflict safety, and destructive-operation safeguards.", "`FCCD-P07-011 — Git integration tests/conflict scenarios` is also CLOSED after exact PR-head validation, normal merge integration, and exact post-merge canonical-main validation. All mandatory P07 task implementations are now integrated, while P07 remains `IN_PROGRESS` until its phase exit gate is truthfully executed and reconciled.", "PROJECT_CONTROL P07-011 summary")
text = replace_once(text, "`evidence/phases/P07/P07_010_INTEGRATED_RECONCILIATION_2026-09-07.md`. P08 and later implementation remain prohibited", "`evidence/phases/P07/P07_010_INTEGRATED_RECONCILIATION_2026-09-07.md`, and `evidence/phases/P07/P07_011_INTEGRATED_RECONCILIATION_2026-09-07.md`. P08 and later implementation remain prohibited", "PROJECT_CONTROL evidence list")
path.write_text(text, encoding="utf-8", newline="\n")

path = Path("docs/TASK_LEDGER.md")
text = path.read_text(encoding="utf-8")
text = replace_once(text, "| FCCD-P07-011 | Git integration tests/conflict scenarios | PENDING |", "| FCCD-P07-011 | Git integration tests/conflict scenarios | CLOSED |", "TASK_LEDGER P07-011 row")
p08 = "## P08 — Terminal/process supervision"
if text.count(p08) != 1:
    raise SystemExit("TASK_LEDGER P08 marker mismatch")
task_note = f"""`FCCD-P07-011` is CLOSED from the final real disposable-Git integration/conflict acceptance suite integrated in PR #185. Exact implementation candidate `{implementation_head}` passed Windows CI `34066314053` / #428, P06-007 Workspace Search `34066314086` / #157, and P06-008 Large Workspace Safeguards `34066314047` / #141. PR #185 was normally merged as `{implementation_merge}`; that exact canonical main passed Windows CI `34066787222` / #429, P06-007 Workspace Search `34066787177` / #158, and P06-008 Large Workspace Safeguards `34066787145` / #142. Coverage includes clean pull→stage→commit→push flow, dirty checkout refusal preserving exact owner bytes and pre-existing-change provenance, a genuine disposable merge conflict with typed visibility and fail-closed destructive-command policy, and diverged pull/push refusal preserving both local and remote heads. Task evidence: `{evidence_path}`. No P08/P12 implementation, new owner-only obligation, P07 phase-gate PASS, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is claimed.

"""
text = text.replace(p08, task_note + p08, 1)
idx = text.find("## Current next action")
if idx < 0:
    raise SystemExit("TASK_LEDGER current-action marker missing")
replacement = """## Current next action

`CURRENT_PHASE = P07` and P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`. `FCCD-P07-001` through `FCCD-P07-011` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable task reconciliation evidence.

P04 remains acceptance-unresolved through `FCCD-P04-008` and its one-to-one queued `OWNER-P04-008-REAL-TARGET` obligation. P05 cloud implementation remains integrated, but its standalone exit observation remains queued as `OWNER-P05-EXIT-REAL-TARGET`. Their gates remain `P04=NOT_RUN;P05=NOT_RUN`; owner-last scheduling does not waive either obligation or permit release.

The next legal P07 action is phase-exit convergence: validate the canonical P07 exit criterion on an exact candidate, repair any cloud-actionable defect, integrate truthful `evidence/phases/P07/CLOSURE.md` only if the gate passes, and only then activate P08 if governance permits. Do not skip directly to P08/P12 and do not fabricate owner/manual evidence.

P06 is canonically CLOSED with `PHASE_EXIT_GATE=PASS`; closure evidence remains `evidence/phases/P06/CLOSURE.md`.
"""
text = text[:idx] + replacement
path.write_text(text, encoding="utf-8", newline="\n")

evidence = Path(evidence_path)
evidence.parent.mkdir(parents=True, exist_ok=True)
evidence.write_text(f"""# FCCD-P07-011 Integrated Reconciliation — 2026-09-07

## Decision

`FCCD-P07-011 — Git integration tests/conflict scenarios` is **CLOSED** as a cloud-actionable task. Its final Git workflow/conflict fixture is normally integrated and exact-main verified. All mandatory P07 task rows are now CLOSED; P07 itself remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN` pending separate phase-exit convergence.

## Production integration

Accepted candidate: `{implementation_head}` from PR #185 (`worker-b/fccd-p07-011-git-integration-conflicts`). PR #185 added only the final disposable-Git cross-service acceptance fixture and scoped documentation; it introduced no new mutation primitive.

The fixture proves clean pull → stage → commit → push workflow; dirty checkout refusal preserving exact owner bytes and pre-existing-change provenance; a genuine disposable merge conflict with typed conflict visibility while destructive-command safety remains fail-closed; and diverged pull/push refusal preserving both local and remote heads.

Exact PR-head gates on `{implementation_head}`:
- Windows CI `34066314053` / #428 — SUCCESS.
- P06-007 Workspace Search `34066314086` / #157 — SUCCESS.
- P06-008 Large Workspace Safeguards `34066314047` / #141 — SUCCESS.

PR #185 was normally merged without squash/rebase as `{implementation_merge}`.

Exact post-merge canonical-main gates on `{implementation_merge}`:
- Windows CI `34066787222` / #429 — SUCCESS.
- P06-007 Workspace Search `34066787177` / #158 — SUCCESS.
- P06-008 Large Workspace Safeguards `34066787145` / #142 — SUCCESS.

No task-local cloud defect or exact-main regression remains known.

## Owner-last boundary

P07-011 requires no new owner-only evidence. The canonical queue remains exactly `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET`, both unresolved and release-blocking. `KNOWN_RELEASE_BLOCKERS=2`, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.

## Remaining phase state

- `CURRENT_PHASE=P07`.
- `CURRENT_PHASE_STATE=IN_PROGRESS`.
- `FCCD-P07-001` through `FCCD-P07-011` are CLOSED.
- `PHASE_EXIT_GATE=NOT_RUN` until separate exact-candidate phase-exit convergence truthfully passes.
- P08 and later phases remain prohibited until that gate is integrated under canonical governance.

## Next legal cloud action

Run P07 phase-exit convergence against an exact candidate using the strongest available cloud evidence for standard Git workflows plus conflict/dirty-tree safety. Repair any failure. Only a truthful PASS may produce `evidence/phases/P07/CLOSURE.md` and authorize sequential activation of P08; no P12 jump is permitted.
""", encoding="utf-8", newline="\n")
