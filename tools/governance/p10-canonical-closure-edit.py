from pathlib import Path
import re

CANDIDATE = "de3187f435ff5b248e39a68bc8a9c20d4b5b8675"
DATE = "2026-09-09"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise SystemExit(f"Missing expected {label}: {old!r}")
    return text.replace(old, new, 1)


ledger = Path("docs/TASK_LEDGER.md").read_text(encoding="utf-8")
rows = re.findall(r"^\|\s*(FCCD-P10-\d{3})\s*\|[^|]*\|\s*([A-Z_]+)\s*\|", ledger, re.MULTILINE)
if len(rows) != 13:
    raise SystemExit(f"Expected 13 P10 task rows, found {len(rows)}: {rows}")
not_closed = [(task, state) for task, state in rows if state != "CLOSED"]
if not_closed:
    raise SystemExit(f"P10 closure denied; non-CLOSED rows: {not_closed}")

current_path = Path("CURRENT_PHASE.md")
current = current_path.read_text(encoding="utf-8")
current = replace_once(current, "CURRENT_PHASE_STATE: IN_PROGRESS", "CURRENT_PHASE_STATE: CLOSED", "CURRENT_PHASE state")
current = replace_once(current, "PHASE_EXIT_GATE: NOT_RUN", "PHASE_EXIT_GATE: PASS", "CURRENT_PHASE gate")
current = replace_once(current, "LAST_RECONCILED: 2026-09-08", "LAST_RECONCILED: 2026-09-09", "CURRENT_PHASE date")
current = replace_once(
    current,
    "P10 — Unity first-class adapter — is now the sole legal cloud implementation/convergence phase. Only dependency-valid, unclaimed P10 work may begin. P11 and later implementation remain prohibited until P10 is truthfully closed with its exit gate resolved under canonical governance.",
    "P10 — Unity first-class adapter — is canonically CLOSED. All thirteen mandatory P10 tasks are CLOSED, exact-main P10 Unity Adapter Exit run `34312651614` passed on accepted candidate `de3187f435ff5b248e39a68bc8a9c20d4b5b8675`, and the exact same main passed Windows CI `34312651653`, Workspace Search `34312651640`, Large Workspace Safeguards `34312651707`, and P09 regression gate `34312651670`. Canonical closure evidence is `evidence/phases/P10/CLOSURE.md`. P11 remains inactive until a separate governance transition is normally integrated and exact-main verified.",
    "P10 active paragraph",
)
current = replace_once(
    current,
    "- Exactly one cloud implementation/convergence phase is active: P10.",
    "- P10 is CLOSED and retained as the current closure checkpoint until a separate validated transition activates P11; no P11 or later implementation is authorized by this closure commit.",
    "P10 active invariant",
)
for task, title in [
    ("FCCD-P10-011", "Unity structured UI events"),
    ("FCCD-P10-012", "Unity cancellation/recovery"),
    ("FCCD-P10-013", "Unity contract fixture/suite"),
]:
    current = replace_once(
        current,
        f"- `{task}` — {title} — PENDING.",
        f"- `{task}` — {title} — CLOSED.",
        task,
    )
checkpoint = f"""

## P10 canonical closure checkpoint — {DATE}

- `FCCD-P10-001` through `FCCD-P10-013`: CLOSED.
- Accepted pre-closure canonical main: `{CANDIDATE}`.
- P10 Unity Adapter Exit `34312651614`: SUCCESS.
- Windows CI `34312651653`: SUCCESS.
- Workspace Search `34312651640`: SUCCESS.
- Large Workspace Safeguards `34312651707`: SUCCESS.
- P09 External Tool Gateway Exit regression `34312651670`: SUCCESS.
- `PHASE_EXIT_GATE=PASS`; `CURRENT_PHASE_STATE=CLOSED`.
- P11 is the authorized next phase but is not activated by this closure.
- `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `P04=NOT_RUN`; `VERIFIED_FINAL_COMPLETE=false`.

Earlier P10 task-specific provenance text that describes P10 as `IN_PROGRESS`, later P10 rows as `PENDING`, or `PHASE_EXIT_GATE=NOT_RUN` is historical task-time provenance. This checkpoint and the top canonical status block supersede those historical scheduling statements.
"""
if "## P10 canonical closure checkpoint — 2026-09-09" not in current:
    current = current.rstrip() + checkpoint + "\n"
current_path.write_text(current, encoding="utf-8")

project_path = Path("PROJECT_CONTROL.md")
project = project_path.read_text(encoding="utf-8")
project = replace_once(project, "CURRENT_PHASE_STATE: IN_PROGRESS", "CURRENT_PHASE_STATE: CLOSED", "PROJECT_CONTROL state")
project = replace_once(project, "PHASE_EXIT_GATE: NOT_RUN", "PHASE_EXIT_GATE: PASS", "PROJECT_CONTROL gate")
project_checkpoint = f"""

## P10 canonical closure checkpoint — {DATE}

P10 — Unity first-class adapter — is canonically CLOSED. All thirteen mandatory P10 task rows are CLOSED. Accepted canonical candidate `{CANDIDATE}` passed dedicated P10 Unity Adapter Exit run `34312651614`, Windows CI `34312651653`, Workspace Search `34312651640`, Large Workspace Safeguards `34312651707`, and P09 External Tool Gateway Exit regression run `34312651670`, all on that exact SHA. Canonical closure evidence is `evidence/phases/P10/CLOSURE.md`.

This closure does not activate P11. A separate governance transition is required before P11 implementation. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item, `P04=NOT_RUN`, `KNOWN_RELEASE_BLOCKERS=1`, and `VERIFIED_FINAL_COMPLETE=false`.
"""
if "## P10 canonical closure checkpoint — 2026-09-09" not in project:
    project = project.rstrip() + project_checkpoint + "\n"
project_path.write_text(project, encoding="utf-8")

closure_path = Path("evidence/phases/P10/CLOSURE.md")
if closure_path.exists():
    raise SystemExit("P10 CLOSURE.md already exists; refusing duplicate closure")
rows_md = "\n".join(f"| FCCD-P10-{i:03d} | CLOSED |" for i in range(1, 14))
closure = f"""# P10 Phase Closure — Unity first-class adapter

```text
PHASE: P10
PHASE_NAME: Unity first-class adapter
CANDIDATE_SHA: {CANDIDATE}
DATE: {DATE}
EXIT_GATE: PASS
KNOWN_BLOCKERS: 0
KNOWN_REGRESSIONS: 0
```

## 1. Mandatory task reconciliation

| Task ID | Final state |
|---|---|
{rows_md}

All thirteen mandatory P10 tasks were CLOSED before this phase-exit decision. The final P10-011 through P10-013 reconciliation was normally integrated by PR #259 before this closure decision.

## 2. Exact candidate verification

Accepted canonical main `{CANDIDATE}` passed all required cloud gates on the same exact SHA:

```text
P10 Unity Adapter Exit
RESULT: PASS — run 34312651614 / #2

Windows CI
RESULT: PASS — run 34312651653 / #722

P06-007 Workspace Search
RESULT: PASS — run 34312651640 / #451

P06-008 Large Workspace Safeguards
RESULT: PASS — run 34312651707 / #435

P09 External Tool Gateway Exit regression
RESULT: PASS — run 34312651670 / #41
```

The dedicated P10 exit workflow checked out the exact candidate SHA, required a clean source tree, ran the complete Windows Release baseline, ran the aggregate P10 Unity contract suite, and verified the source tree remained clean afterward.

## 3. P10 contract coverage

The aggregate P10 contract suite covers Unity project/version detection, installed Editor/Hub resolution, strongly typed Unity CLI construction, same-project/process resource locking, dedicated Unity log capture/parsing, compile validation, EditMode tests, PlayMode tests, project-owned Editor automation, build target/artifact validation, structured Unity UI events, and cancellation/recovery.

The suite is fail-closed: delegated validator failure fails the aggregate suite; stale, malformed, mismatched, or missing evidence cannot become success; same-project locking and process ownership constraints cannot be bypassed; and no real Unity/provider success is fabricated by deterministic cloud fixtures.

## 4. Runtime/environment verification

P10 closure is based on deterministic product-owned Windows contract acceptance. No owner-only P10 item is required or queued. This closure does not claim real external Unity Editor/provider success beyond the repository-defined deterministic acceptance boundary.

## 5. Safety and recovery

P10 recovery preserves exact operation/project/process-run identity, rejects foreign or mismatched process ownership, forbids kill-by-name cleanup, blocks unsafe overlapping retry, rejects stale or wrong-correlation terminal evidence, and requires a fresh invocation identity after safe lease release when retry is permitted.

## 6. Known defects and regressions

```text
KNOWN_PHASE_LOCAL_DEFECTS: NONE
EARLIER_PHASE_REGRESSIONS: NONE
```

The exact candidate passed the P09 phase regression gate in addition to Windows/workspace regression gates.

## 7. Exit decision

```text
ALL_P10_MANDATORY_TASKS_CLOSED: true
P10_EXACT_GATE_PASS: true
P10_CANDIDATE_MAIN_GREEN: true
P10_KNOWN_PHASE_BLOCKERS: 0
P10_KNOWN_REGRESSIONS: 0
EXIT_GATE: PASS
P10_PHASE_STATE: CLOSED
AUTHORIZED_NEXT_PHASE: P11
P11_IMPLEMENTATION_IN_THIS_CLOSURE: NONE
OWNER_PENDING_P10: NONE
GLOBAL_RELEASE_BLOCKERS: 1
VERIFIED_FINAL_COMPLETE: false
```

`OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item. P10 closure does not activate P11; a separate governance transition is allowed only after this closure is normally merged and exact canonical `main` remains green.
"""
closure_path.write_text(closure, encoding="utf-8")
