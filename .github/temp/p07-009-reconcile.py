from pathlib import Path

EXPECTED_MAIN = "b534fd7d1d23b1727cc68a7a588d8ab4e5ce5fcb"
EVIDENCE = "evidence/phases/P07/P07_009_INTEGRATED_RECONCILIATION_2026-09-07.md"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


def require(text: str, needle: str, label: str) -> None:
    if needle not in text:
        raise SystemExit(f"{label}: required invariant missing: {needle}")

current_path = Path("CURRENT_PHASE.md")
project_path = Path("PROJECT_CONTROL.md")
ledger_path = Path("docs/TASK_LEDGER.md")

current = current_path.read_text(encoding="utf-8")
project = project_path.read_text(encoding="utf-8")
ledger = ledger_path.read_text(encoding="utf-8")

for text, label in ((current, "CURRENT_PHASE"), (project, "PROJECT_CONTROL")):
    require(text, "CURRENT_PHASE: P07", label)
    require(text, "CURRENT_PHASE_STATE: IN_PROGRESS", label)
    require(text, "PHASE_EXIT_GATE: NOT_RUN", label)
    require(text, "KNOWN_RELEASE_BLOCKERS: 2", label)
    require(text, "VERIFIED_FINAL_COMPLETE: false", label)
    require(text, "OWNER-P04-008-REAL-TARGET;OWNER-P05-EXIT-REAL-TARGET", label)

current = replace_once(
    current,
    "- `FCCD-P07-009` — Dirty/pre-existing-change provenance — PENDING.",
    "- `FCCD-P07-009` — Dirty/pre-existing-change provenance — CLOSED.",
    "CURRENT_PHASE P07-009 row",
)
current = replace_once(
    current,
    "LAST_RECONCILED: 2026-09-06",
    "LAST_RECONCILED: 2026-09-07",
    "CURRENT_PHASE reconciliation date",
)
provenance = f"""## P07-009 integration provenance

- Exact implementation candidate: `2db2276dc920d769c235c8581bd272d6b7b05519` from PR #181 (`worker/fccd-p07-009-dirty-provenance`).
- PR #181 exact-head Windows CI: run `34061234142` / run #419 — SUCCESS.
- PR #181 exact-head P06-007 Workspace Search: run `34061234123` / run #148 — SUCCESS.
- PR #181 exact-head P06-008 Large Workspace Safeguards: run `34061234214` / run #132 — SUCCESS.
- Normal merge commit: `b534fd7d1d23b1727cc68a7a588d8ab4e5ce5fcb`.
- Exact post-merge canonical-main Windows CI: run `34061750164` / run #420 — SUCCESS.
- Exact post-merge P06-007 Workspace Search: run `34061750167` / run #149 — SUCCESS.
- Exact post-merge P06-008 Large Workspace Safeguards: run `34061750177` / run #133 — SUCCESS.
- Integrated evidence: `{EVIDENCE}`.
- Evidence class remains cloud/self-test for conservative read-only dirty/pre-existing-change provenance plus canonical integration provenance; no destructive-operation safeguard closure, conflict-scenario closure, P07 phase closure, P08/P11/P12 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is implied.

"""
current = replace_once(
    current,
    "## P07 cloud activation provenance",
    provenance + "## P07 cloud activation provenance",
    "CURRENT_PHASE provenance insertion point",
)

project = replace_once(
    project,
    "`FCCD-P07-007 — Commit/push`, and `FCCD-P07-008 — History` are CLOSED",
    "`FCCD-P07-007 — Commit/push`, `FCCD-P07-008 — History`, and `FCCD-P07-009 — Dirty/pre-existing-change provenance` are CLOSED",
    "PROJECT_CONTROL closed-task summary",
)
project = replace_once(
    project,
    "`FCCD-P07-009` through `FCCD-P07-011` remain PENDING",
    "`FCCD-P07-010` and `FCCD-P07-011` remain PENDING",
    "PROJECT_CONTROL pending-task summary",
)
project = replace_once(
    project,
    "`evidence/phases/P07/P07_007_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_008_INTEGRATED_RECONCILIATION_2026-09-06.md`.",
    "`evidence/phases/P07/P07_007_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_008_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_009_INTEGRATED_RECONCILIATION_2026-09-07.md`.",
    "PROJECT_CONTROL evidence list",
)

ledger = replace_once(
    ledger,
    "| FCCD-P07-009 | Dirty/pre-existing-change provenance | PENDING |",
    "| FCCD-P07-009 | Dirty/pre-existing-change provenance | CLOSED |",
    "TASK_LEDGER P07-009 row",
)
ledger_paragraph = f"""`FCCD-P07-009` is CLOSED from the conservative read-only dirty/pre-existing-change provenance implementation integrated in PR #181. Exact implementation candidate `2db2276dc920d769c235c8581bd272d6b7b05519` passed Windows CI `34061234142` / #419, P06-007 Workspace Search `34061234123` / #148, and P06-008 Large Workspace Safeguards `34061234214` / #132. PR #181 was normally merged as `b534fd7d1d23b1727cc68a7a588d8ab4e5ce5fcb`; that exact canonical main passed Windows CI `34061750164` / #420, P06-007 Workspace Search `34061750167` / #149, and P06-008 Large Workspace Safeguards `34061750177` / #133. Coverage includes Application-owned read-only `IGitChangeProvenanceService`, dirty-baseline capture/comparison, conservative `PreExistingDirty` versus `CreatedSinceBaseline` classification, resolved pre-existing changes, rename-alias continuity, cross-repository fail-closed comparison, bounded dirty-path materialization, Unicode/Arabic real-Git fixtures, cancellation, and owner-byte preservation. Task evidence: `{EVIDENCE}`. No destructive-operation safeguard closure, conflict integration closure, P07 phase closure, P08/P11/P12 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE` is claimed; P07 remains `IN_PROGRESS`, P07-010 and P07-011 remain PENDING, and the two existing owner-last release blockers remain unchanged.


"""
ledger = replace_once(
    ledger,
    "## P08 — Terminal/process supervision",
    ledger_paragraph + "## P08 — Terminal/process supervision",
    "TASK_LEDGER detailed P07-009 insertion point",
)
marker = "## Current next action"
if ledger.count(marker) != 1:
    raise SystemExit(f"TASK_LEDGER current-next-action marker count was {ledger.count(marker)}")
pre, _ = ledger.split(marker, 1)
ledger = pre + """## Current next action

`CURRENT_PHASE = P07` and P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`. `FCCD-P07-001` through `FCCD-P07-009` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable reconciliation evidence. `FCCD-P07-010` and `FCCD-P07-011` remain PENDING.

P04 remains acceptance-unresolved through `FCCD-P04-008` and its one-to-one queued `OWNER-P04-008-REAL-TARGET` obligation. P05 cloud implementation remains integrated, but its standalone exit observation remains queued as `OWNER-P05-EXIT-REAL-TARGET`. Their phase gates remain `P04=NOT_RUN;P05=NOT_RUN`; owner-last scheduling permits P07 cloud implementation but does not close either deferred acceptance requirement or permit release.

After this P07-009 reconciliation is integrated and exact resulting `main` remains green, re-run the Worker Protocol claim map. Recover/integrate any newly surfaced higher-priority legitimate defect first. Otherwise select the highest-value dependency-valid unclaimed P07 task, nominally `FCCD-P07-010 — Destructive-operation safeguards` if still unclaimed and dependency-valid. Do not advance to P08 until every mandatory P07 task is CLOSED and the P07 phase exit gate is truthfully resolved under canonical governance. Only a genuinely owner-environment-bound residual may be queued under owner-last; do not fabricate target/manual evidence.

P06 is canonically CLOSED with `PHASE_EXIT_GATE=PASS`; closure evidence remains `evidence/phases/P06/CLOSURE.md`.
"""

current_path.write_text(current, encoding="utf-8", newline="\n")
project_path.write_text(project, encoding="utf-8", newline="\n")
ledger_path.write_text(ledger, encoding="utf-8", newline="\n")

evidence = f"""# FCCD-P07-009 — Integrated reconciliation evidence

Date: 2026-09-07  
Phase: P07 — Change review + Git  
Task: `FCCD-P07-009 — Dirty/pre-existing-change provenance`  
Evidence class: cloud/self-test + canonical integration provenance

## Result

`FCCD-P07-009` is CLOSED from the production conservative dirty/pre-existing-change provenance implementation after exact candidate validation, normal merge integration, and exact post-merge canonical-main non-regression validation all completed SUCCESS.

## Implementation provenance

- Implementation PR: #181 — `P07-009: add conservative dirty-change provenance`.
- Branch: `worker/fccd-p07-009-dirty-provenance`.
- Exact implementation candidate: `2db2276dc920d769c235c8581bd272d6b7b05519`.
- Normal merge commit: `b534fd7d1d23b1727cc68a7a588d8ab4e5ce5fcb`.
- Merge parents preserve tested ancestry; no squash/rebase closure is claimed.

## Exact implementation-head gates

- Windows CI #419: `34061234142` — SUCCESS.
- P06-007 Workspace Search #148: `34061234123` — SUCCESS.
- P06-008 Large Workspace Safeguards #132: `34061234214` — SUCCESS.

## Exact post-merge canonical-main gates

- Windows CI #420: `34061750164` — SUCCESS.
- P06-007 Workspace Search #149: `34061750167` — SUCCESS.
- P06-008 Large Workspace Safeguards #133: `34061750177` — SUCCESS.

## Verified cloud boundary

The integrated implementation provides an Application-owned read-only `IGitChangeProvenanceService`; explicit dirty-baseline capture and comparison; conservative `PreExistingDirty` versus `CreatedSinceBaseline` path-lineage classification; reporting of resolved pre-existing changes; rename source/target alias continuity across status-shape changes; cross-repository baseline rejection; bounded dirty-path materialization with fail-closed overflow; cancellation; Unicode/Arabic disposable real-Git fixtures; and preservation of owner bytes. It delegates only to the existing read-only Git status surface and performs no ref, index, work-tree, config, or remote mutation.

## Governance boundary

- P07 remains `IN_PROGRESS` and its exit gate remains `NOT_RUN`.
- `FCCD-P07-010` and `FCCD-P07-011` remain PENDING.
- No P08, P11, P12, or later-phase implementation is authorized by this evidence.
- `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain the unchanged release-blocking owner queue obligations.
- `KNOWN_RELEASE_BLOCKERS=2` and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.
- No owner/manual/target evidence is fabricated or implied.
"""
Path(EVIDENCE).parent.mkdir(parents=True, exist_ok=True)
Path(EVIDENCE).write_text(evidence, encoding="utf-8", newline="\n")

# Self-delete temporary orchestration so it cannot appear in the durable PR diff.
Path(".github/temp/p07-009-reconcile.py").unlink(missing_ok=True)
Path(".github/workflows/temp-p07-009-reconcile.yml").unlink(missing_ok=True)
