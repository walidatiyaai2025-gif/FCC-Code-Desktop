from pathlib import Path

BASE = "ac54e739019e7264db5de3f9b26b700735924bc1"


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"guard failed for {path}: expected exactly one match, got {count}: {old[:120]!r}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8")

# Fast checkpoint: close only P08-001 and add exact integration/repair provenance.
replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P08-001` — Process supervisor with owned process-tree tracking — PENDING.",
    "- `FCCD-P08-001` — Process supervisor with owned process-tree tracking — CLOSED.",
)
marker = "## P07 cloud task inventory\n"
cp = Path("CURRENT_PHASE.md")
text = cp.read_text(encoding="utf-8")
if text.count(marker) != 1 or "## P08-001 integration provenance" in text:
    raise SystemExit("CURRENT_PHASE provenance insertion guard failed")
provenance = """## P08-001 integration provenance

- P08 activation: PR #188, normal merge `36cd7984c87e3ef9e627d0bf424b414f2237f374`.
- Exact implementation candidate: `5915ce7f21d8b487346acf7334b34bd4523a215a` from PR #189 (`worker/fccd-p08-001-process-supervisor`).
- PR #189 exact-head Windows CI: run `34072739503` / #438 — SUCCESS.
- PR #189 exact-head P06-007 Workspace Search: run `34072739496` / #167 — SUCCESS.
- PR #189 exact-head P06-008 Large Workspace Safeguards: run `34072739498` / #151 — SUCCESS.
- Initial normal merge: `d0df56e60ec62e05db793184c5bc0d53b7c65d9b`.
- Exact post-merge validation exposed a real lifecycle race: `ISupervisedProcess.Completion` could publish before the supervisor removed the owned entry from its active registry. Workspace Search run `34073251587` / #168 failed; the task was not reconciled or closed at that point.
- Exact repair candidate: `e3d6ecdc14f01be5460ca1656d6f6ba2b6535460` from PR #190 (`repair/p08-001-post-merge-workspace-lock`).
- PR #190 exact-head Windows CI: run `34074218833` / #446 — SUCCESS.
- PR #190 exact-head P06-007 Workspace Search: run `34074218827` / #175 — SUCCESS.
- PR #190 exact-head P06-008 Large Workspace Safeguards: run `34074218830` / #159 — SUCCESS.
- Repair normal merge / accepted canonical implementation: `ac54e739019e7264db5de3f9b26b700735924bc1`.
- Exact accepted-main Windows CI: run `34074668199` / #447 — SUCCESS.
- Exact accepted-main P06-007 Workspace Search: run `34074668196` / #176 — SUCCESS.
- Exact accepted-main P06-008 Large Workspace Safeguards: run `34074668191` / #160 — SUCCESS.
- Integrated evidence: `evidence/phases/P08/P08_001_INTEGRATED_RECONCILIATION_2026-09-07.md`.
- Evidence is cloud/Windows-CI process-supervision evidence only. No owner-only evidence is added, P08 remains IN_PROGRESS, P08-002..008 remain PENDING, P09+ remain prohibited, and `VERIFIED_FINAL_COMPLETE` remains false.

"""
cp.write_text(text.replace(marker, provenance + marker, 1), encoding="utf-8")

# Project control: replace only the stale current-P08 inventory sentence.
replace_once(
    "PROJECT_CONTROL.md",
    "P08 — Terminal/process supervision — is now the single active cloud implementation/convergence phase. Its eight mandatory ledger tasks remain PENDING at activation. Workers must select dependency-valid unclaimed P08 work while preserving owned-process boundaries, bounded output, cancellation escalation, interactive terminal safety, and owner work.",
    "P08 — Terminal/process supervision — is now the single active cloud implementation/convergence phase. `FCCD-P08-001 — Process supervisor with owned process-tree tracking` is CLOSED after implementation PR #189, post-merge regression repair PR #190, and exact accepted-main Windows CI `34074668199`, Workspace Search `34074668196`, and Large Workspace Safeguards `34074668191` all completed SUCCESS on `ac54e739019e7264db5de3f9b26b700735924bc1`. `FCCD-P08-002` through `FCCD-P08-008` remain PENDING. Workers must select dependency-valid unclaimed P08 work while preserving owned-process boundaries, bounded output, cancellation escalation, interactive terminal safety, and owner work.",
)

# Ledger task row + integrated evidence narrative.
replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P08-001 | Process supervisor with owned process-tree tracking | PENDING |",
    "| FCCD-P08-001 | Process supervisor with owned process-tree tracking | CLOSED |",
)
ledger = Path("docs/TASK_LEDGER.md")
text = ledger.read_text(encoding="utf-8")
p09_marker = "## P09 — External Tool Gateway\n"
if text.count(p09_marker) != 1 or "P08_001_INTEGRATED_RECONCILIATION_2026-09-07.md" in text:
    raise SystemExit("TASK_LEDGER P08-001 evidence insertion guard failed")
ledger_note = """`FCCD-P08-001` is CLOSED from the owned process-tree supervisor integrated in PR #189 and the mandatory post-merge lifecycle-race repair integrated in PR #190. Exact implementation candidate `5915ce7f21d8b487346acf7334b34bd4523a215a` passed Windows CI `34072739503` / #438, Workspace Search `34072739496` / #167, and Large Workspace Safeguards `34072739498` / #151. Initial normal merge `d0df56e60ec62e05db793184c5bc0d53b7c65d9b` exposed the Completion/active-registry race (including Workspace Search `34073251587` / #168 FAILURE), so no closure was claimed. Repair candidate `e3d6ecdc14f01be5460ca1656d6f6ba2b6535460` passed Windows CI `34074218833` / #446, Workspace Search `34074218827` / #175, and Large Workspace Safeguards `34074218830` / #159; PR #190 normally merged as accepted main `ac54e739019e7264db5de3f9b26b700735924bc1`, which then passed exact-main Windows CI `34074668199` / #447, Workspace Search `34074668196` / #176, and Large Workspace Safeguards `34074668191` / #160. Task evidence: `evidence/phases/P08/P08_001_INTEGRATED_RECONCILIATION_2026-09-07.md`. P08 remains IN_PROGRESS; P08-002..008 remain PENDING; no P09/P13 implementation or new owner-only obligation is claimed.\n\n"""
text = text.replace(p09_marker, ledger_note + p09_marker, 1)

next_marker = "## Current next action\n"
if text.count(next_marker) != 1:
    raise SystemExit("TASK_LEDGER current-next-action marker guard failed")
prefix, old_tail = text.split(next_marker, 1)
if "CURRENT_PHASE = P07" not in old_tail or "separate governance transition activating P08" not in old_tail:
    raise SystemExit("TASK_LEDGER stale next-action guard failed")
new_tail = """## Current next action

`CURRENT_PHASE = P08` is `IN_PROGRESS`. `FCCD-P08-001 — Process supervisor with owned process-tree tracking` is CLOSED and exact accepted-main green on `ac54e739019e7264db5de3f9b26b700735924bc1` after the post-merge lifecycle-race repair. `FCCD-P08-002` through `FCCD-P08-008` remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`.

P04 remains acceptance-unresolved through `FCCD-P04-008` and its one-to-one queued `OWNER-P04-008-REAL-TARGET` obligation. P05 cloud implementation remains integrated, but its standalone exit observation remains queued as `OWNER-P05-EXIT-REAL-TARGET`. Their gates remain `P04=NOT_RUN;P05=NOT_RUN`; owner-last scheduling does not waive either obligation or permit release.

The next legal cloud action is to re-read live claims and recover any legitimate integration-pending P08 work first; otherwise select the highest-value dependency-valid unclaimed P08 task, nominally `FCCD-P08-002 — Graceful→forced cancellation escalation` if it remains unclaimed. Do not skip to P09, P13, or any later phase and do not fabricate owner/manual evidence.
"""
ledger.write_text(prefix + new_tail, encoding="utf-8")

# Dedicated immutable-style reconciliation evidence.
evidence = Path("evidence/phases/P08/P08_001_INTEGRATED_RECONCILIATION_2026-09-07.md")
evidence.parent.mkdir(parents=True, exist_ok=True)
if evidence.exists():
    raise SystemExit("evidence file already exists")
evidence.write_text("""# P08-001 Integrated Reconciliation — 2026-09-07

## Classification

- Task: `FCCD-P08-001 — Process supervisor with owned process-tree tracking`
- Phase: `P08 — Terminal/process supervision`
- Final task state after this reconciliation: `CLOSED`
- Evidence class: cloud / Windows CI + canonical integration provenance
- Owner-only evidence required for this task: none

## Implementation

P08 was activated by PR #188, normal merge `36cd7984c87e3ef9e627d0bf424b414f2237f374`.

PR #189 implemented Runtime-owned `IProcessSupervisor` / `ISupervisedProcess` contracts, one private Windows Job Object per launched tree with `KILL_ON_JOB_CLOSE`, active-process ownership snapshots, full owned-tree completion semantics, bounded non-shell launch arguments/environment, and an owned-handle-only forced-tree termination primitive. Real Windows fixtures cover descendant cleanup and preservation of an unrelated unowned sentinel.

Exact implementation candidate: `5915ce7f21d8b487346acf7334b34bd4523a215a`.

PR #189 exact-head permanent gates:
- Windows CI `34072739503` / #438 — SUCCESS
- P06-007 Workspace Search `34072739496` / #167 — SUCCESS
- P06-008 Large Workspace Safeguards `34072739498` / #151 — SUCCESS

PR #189 normally merged as `d0df56e60ec62e05db793184c5bc0d53b7c65d9b`.

## Post-merge regression and repair

Exact-main repeated Windows validation exposed a real lifecycle race: `ISupervisedProcess.Completion` could publish before the supervisor removed the owned process from its active registry. Workspace Search `34073251587` / #168 failed. This was treated as a cloud-repairable product/test defect; P08-001 was not reconciled CLOSED at that point.

PR #190 repaired `ObserveTreeExitAsync` so the owned entry is removed from `_active` before successful or failed completion is published. The forced-tree fixture also moved the controlled process CWD to `Environment.SystemDirectory` while retaining descendant-PID evidence in the disposable fixture directory, removing irrelevant Windows directory-handle coupling without weakening descendant-termination or unowned-sentinel assertions.

Exact repair candidate: `e3d6ecdc14f01be5460ca1656d6f6ba2b6535460`.

PR #190 exact-head permanent gates:
- Windows CI `34074218833` / #446 — SUCCESS
- P06-007 Workspace Search `34074218827` / #175 — SUCCESS
- P06-008 Large Workspace Safeguards `34074218830` / #159 — SUCCESS

PR #190 normally merged as `ac54e739019e7264db5de3f9b26b700735924bc1`.

Exact accepted-main permanent gates:
- Windows CI `34074668199` / #447 — SUCCESS
- P06-007 Workspace Search `34074668196` / #176 — SUCCESS
- P06-008 Large Workspace Safeguards `34074668191` / #160 — SUCCESS

## Reconciliation boundary

This evidence closes only `FCCD-P08-001`. P08 remains `IN_PROGRESS`, `PHASE_EXIT_GATE=NOT_RUN`, and `FCCD-P08-002` through `FCCD-P08-008` remain PENDING. P09 and later implementation remain prohibited until P08 truthfully closes.

The canonical final-owner queue is unchanged: `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain `QUEUED`, release-blocking obligations. No real-target/manual evidence is fabricated or reclassified, and `VERIFIED_FINAL_COMPLETE=false` remains mandatory.
""", encoding="utf-8")

# Owner-last invariants must remain present and unchanged in state.
owner = Path("docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md").read_text(encoding="utf-8")
for token in ["OWNER-P04-008-REAL-TARGET", "OWNER-P05-EXIT-REAL-TARGET", '"releaseBlocking": true']:
    if token not in owner:
        raise SystemExit(f"owner queue invariant missing: {token}")

print("P08-001 guarded reconciliation prepared")
