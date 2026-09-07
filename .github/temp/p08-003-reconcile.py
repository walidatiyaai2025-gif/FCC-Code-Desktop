from pathlib import Path
import subprocess
import sys

BASE = "eae2134666fe659fd26bf065b89d8fd661c9b0da"
EVIDENCE = Path("evidence/phases/P08/P08_003_INTEGRATED_RECONCILIATION_2026-09-07.md")
HELPER = Path(".github/temp/p08-003-reconcile.py")
WORKFLOW = Path(".github/workflows/temp-p08-003-reconcile.yml")
ALLOWED = {
    "CURRENT_PHASE.md",
    "PROJECT_CONTROL.md",
    "docs/TASK_LEDGER.md",
    str(EVIDENCE).replace("\\", "/"),
}

def git(*args):
    return subprocess.check_output(["git", *args], text=True, encoding="utf-8").strip()

def replace_once(text, old, new, label):
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{label}: expected exactly 1 match, found {count}")
    return text.replace(old, new, 1)

def read(path):
    return Path(path).read_text(encoding="utf-8")

def write(path, text):
    Path(path).write_text(text, encoding="utf-8", newline="\n")

if git("rev-parse", "HEAD^") != BASE:
    # The workflow commit is expected to sit directly on a branch whose ancestry starts at BASE;
    # helper/workflow setup commits may make HEAD^ not BASE, so verify merge-base instead.
    if git("merge-base", "HEAD", BASE) != BASE:
        raise RuntimeError("reconciliation branch is not descended from expected exact main")

# CURRENT_PHASE.md
path = "CURRENT_PHASE.md"
text = read(path)
text = replace_once(
    text,
    "- `FCCD-P08-003` — Bounded streaming log pipeline — PENDING.",
    "- `FCCD-P08-003` — Bounded streaming log pipeline — CLOSED.",
    "CURRENT_PHASE P08-003 inventory row",
)
old_pending = "`FCCD-P08-003`, `FCCD-P08-004`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING."
if old_pending in text:
    text = replace_once(text, old_pending, "`FCCD-P08-004`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING.", "CURRENT_PHASE pending narrative")
marker = "## P08-005 integration provenance"
if marker not in text:
    raise RuntimeError("CURRENT_PHASE P08-005 provenance marker missing")
if "## P08-003 integration provenance" in text:
    raise RuntimeError("CURRENT_PHASE already contains P08-003 provenance")
provenance = """## P08-003 integration provenance

- Task: `FCCD-P08-003 — Bounded streaming log pipeline` — CLOSED.
- Implementation PR: #195 (`codex/fccd-p08-003-bounded-streaming-logs`).
- Exact implementation candidate: `9ffabc1c85a70ebea9d6bbbc0c2478bdffae3136`.
- Exact implementation-head Windows CI: run `34090112839` / #493 — SUCCESS.
- Exact implementation-head P06-007 Workspace Search: run `34090112868` / #222 — SUCCESS.
- Exact implementation-head P06-008 Large Workspace Safeguards: run `34090112831` / #206 — SUCCESS.
- Exact implementation-head P08-003 Bounded Process Output: run `34090112927` / #2 — SUCCESS.
- Normal implementation merge / accepted main: `eae2134666fe659fd26bf065b89d8fd661c9b0da`.
- Exact accepted-main Windows CI: run `34090854609` / #494 — SUCCESS.
- Exact accepted-main P06-007 Workspace Search: run `34090854605` / #223 — SUCCESS.
- Exact accepted-main P06-008 Large Workspace Safeguards: run `34090854583` / #207 — SUCCESS.
- Exact accepted-main P08-003 Bounded Process Output: run `34090854589` / #3 — SUCCESS.
- Integrated evidence: `evidence/phases/P08/P08_003_INTEGRATED_RECONCILIATION_2026-09-07.md`.
- Evidence is cloud/hosted-Windows bounded-process-output evidence only. P08 remains IN_PROGRESS; P08-004, P08-007, and P08-008 remain PENDING; P09/P14 and later phases remain prohibited. The owner queue is unchanged and `VERIFIED_FINAL_COMPLETE` remains false.

"""
text = text.replace(marker, provenance + marker, 1)
write(path, text)

# PROJECT_CONTROL.md
path = "PROJECT_CONTROL.md"
text = read(path)
old = "`FCCD-P08-003`, `FCCD-P08-004`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING."
new = "`FCCD-P08-003 — Bounded streaming log pipeline` is CLOSED after implementation PR #195 exact candidate `9ffabc1c85a70ebea9d6bbbc0c2478bdffae3136` passed Windows CI `34090112839`, Workspace Search `34090112868`, Large Workspace Safeguards `34090112831`, and dedicated P08-003 gate `34090112927`; PR #195 was normally merged as `eae2134666fe659fd26bf065b89d8fd661c9b0da`, whose exact accepted-main Windows CI `34090854609`, Workspace Search `34090854605`, Large Workspace Safeguards `34090854583`, and dedicated P08-003 gate `34090854589` all completed SUCCESS. `FCCD-P08-004`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING."
text = replace_once(text, old, new, "PROJECT_CONTROL P08 pending narrative")
write(path, text)

# TASK_LEDGER.md
path = "docs/TASK_LEDGER.md"
text = read(path)
text = replace_once(
    text,
    "| FCCD-P08-003 | Bounded streaming log pipeline | PENDING |",
    "| FCCD-P08-003 | Bounded streaming log pipeline | CLOSED |",
    "TASK_LEDGER P08-003 row",
)
start = text.find("## Current next action")
if start < 0:
    raise RuntimeError("TASK_LEDGER Current next action heading missing")
body_start = text.find("\n\n", start)
if body_start < 0:
    raise RuntimeError("TASK_LEDGER Current next action body missing")
body_start += 2
anchor = "\n\nP04 remains acceptance-unresolved"
end = text.find(anchor, body_start)
if end < 0:
    raise RuntimeError("TASK_LEDGER P04 anchor missing")
new_body = """`CURRENT_PHASE = P08` is `IN_PROGRESS`. `FCCD-P08-001 — Process supervisor with owned process-tree tracking`, `FCCD-P08-002 — Graceful→forced cancellation escalation`, `FCCD-P08-003 — Bounded streaming log pipeline`, `FCCD-P08-005 — PowerShell/CMD profiles`, and `FCCD-P08-006 — Optional Git Bash/WSL detection` are CLOSED. P08-003 is accepted only after implementation PR #195 exact candidate `9ffabc1c85a70ebea9d6bbbc0c2478bdffae3136` passed Windows CI `34090112839`, Workspace Search `34090112868`, Large Workspace Safeguards `34090112831`, and dedicated P08-003 gate `34090112927`; PR #195 was normally merged as `eae2134666fe659fd26bf065b89d8fd661c9b0da`, whose exact-main Windows CI `34090854609`, Workspace Search `34090854605`, Large Workspace Safeguards `34090854583`, and dedicated P08-003 gate `34090854589` all completed SUCCESS. `FCCD-P08-004`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`. The next legal cloud action is to recover/integrate legitimate active P08 work first — currently PR #196 for P08-004 if it remains live — otherwise re-read claims and select the highest-value dependency-valid unclaimed P08 task. Do not steal active P08 work, do not skip to P09/P14 or any later phase, and do not fabricate owner/manual evidence."""
text = text[:body_start] + new_body + text[end:]
write(path, text)

# Evidence
if EVIDENCE.exists():
    raise RuntimeError("P08-003 evidence file already exists")
EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
EVIDENCE.write_text("""# FCCD-P08-003 — Integrated Reconciliation Evidence

Date: 2026-09-07
Task: `FCCD-P08-003 — Bounded streaming log pipeline`
Classification: CLOUD / HOSTED-WINDOWS / INTEGRATED
Canonical task result: CLOSED after normal integration and exact-main permanent validation.

## Implementation

- Implementation PR: #195 — `P08-003: bounded streaming log pipeline`.
- Branch: `codex/fccd-p08-003-bounded-streaming-logs`.
- Exact accepted implementation candidate: `9ffabc1c85a70ebea9d6bbbc0c2478bdffae3136`.
- Durable scope provides bounded asynchronous stdout/stderr capture, immutable correlated output entries and snapshots, deterministic line framing, finite retained-history and delivery bounds, truthful truncation/eviction/delivery-drop accounting, stream EOF/final-line drain barriers, and stress/recovery coverage.
- ADR-024 defines bounded newest-history plus independently bounded best-effort live delivery so UI consumption never controls child-pipe drainage.
- Recovery added the missing permanent P08-003 validator/workflow. Its dedicated gate exposed a scheduler-dependent stress assertion; the fixture was repaired to prove exact dual-stream acceptance/source completion without assuming the bounded newest-history slice retains both sources. Production semantics and bounds were unchanged.

## Exact implementation-head validation

On `9ffabc1c85a70ebea9d6bbbc0c2478bdffae3136`:

- Windows CI run `34090112839` / #493 — SUCCESS.
- P06-007 Workspace Search run `34090112868` / #222 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34090112831` / #206 — SUCCESS.
- P08-003 Bounded Process Output run `34090112927` / #2 — SUCCESS.

## Normal integration

PR #195 was merged with the normal merge method. Accepted canonical implementation merge:

`eae2134666fe659fd26bf065b89d8fd661c9b0da`

The merge preserves tested candidate `9ffabc1c85a70ebea9d6bbbc0c2478bdffae3136` as its second parent; no squash/rebase/force-push is claimed.

## Exact accepted-main validation

On exact canonical main `eae2134666fe659fd26bf065b89d8fd661c9b0da`:

- Windows CI run `34090854609` / #494 — SUCCESS.
- P06-007 Workspace Search run `34090854605` / #223 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34090854583` / #207 — SUCCESS.
- P08-003 Bounded Process Output run `34090854589` / #3 — SUCCESS.

No task-local cloud regression remains known after these permanent gates.

## Reconciliation consistency

The canonical P08 inventory now records P08-003 CLOSED. P08 remains `IN_PROGRESS`; only P08-004, P08-007, and P08-008 remain pending, and the P08 exit gate remains `NOT_RUN`.

This reconciliation also repairs stale next-action/narrative text that still described already-integrated P08 tasks as pending.

## Scope / owner-last boundary

This evidence closes only `FCCD-P08-003`.

It does not close P08, does not authorize P09/P14 or any later phase, does not take ownership of active P08-004 PR #196, and creates no target/manual obligation. The canonical owner queue remains unchanged with exactly `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET`, both release-blocking. `VERIFIED_FINAL_COMPLETE` remains false.
""", encoding="utf-8", newline="\n")

# Delete temporary orchestration before durable commit.
HELPER.unlink()
WORKFLOW.unlink()

subprocess.run(["git", "add", "-A"], check=True)
changed = {p.replace("\\", "/") for p in git("diff", "--cached", "--name-only").splitlines() if p.strip()}
if changed != ALLOWED:
    raise RuntimeError(f"unexpected durable reconciliation scope: {sorted(changed)}")

# Semantic guards after mutation.
if "- `FCCD-P08-003` — Bounded streaming log pipeline — CLOSED." not in read("CURRENT_PHASE.md"):
    raise RuntimeError("CURRENT_PHASE closure guard failed")
if "| FCCD-P08-003 | Bounded streaming log pipeline | CLOSED |" not in read("docs/TASK_LEDGER.md"):
    raise RuntimeError("TASK_LEDGER closure guard failed")
for owner_item in ("OWNER-P04-008-REAL-TARGET", "OWNER-P05-EXIT-REAL-TARGET"):
    if owner_item not in read("CURRENT_PHASE.md"):
        raise RuntimeError(f"owner queue invariant missing: {owner_item}")
if "VERIFIED_FINAL_COMPLETE: false" not in read("CURRENT_PHASE.md"):
    raise RuntimeError("final-complete invariant changed")

subprocess.run(["git", "config", "user.name", "github-actions[bot]"], check=True)
subprocess.run(["git", "config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com"], check=True)
subprocess.run(["git", "commit", "-m", "FCCD-P08-003: reconcile integrated bounded output closure"], check=True)
subprocess.run(["git", "push", "origin", "HEAD:reconcile/fccd-p08-003-integrated-closure"], check=True)
print("P08-003 guarded reconciliation committed and pushed successfully")
