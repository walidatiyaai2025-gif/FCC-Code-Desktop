from pathlib import Path
import subprocess

BASE = "4ca55a93d0636e4ce9d72e74178e3536f02ed859"
BRANCH = "reconcile/fccd-p07-006-integrated-closure"
SCRIPT = Path(".github/temp/p07-006-reconcile.py")
WORKFLOW = Path(".github/workflows/temp-p07-006-reconcile.yml")
EVIDENCE = Path("evidence/phases/P07/P07_006_INTEGRATED_RECONCILIATION_2026-09-06.md")
ALLOWED_FINAL = {
    "CURRENT_PHASE.md",
    "PROJECT_CONTROL.md",
    "docs/TASK_LEDGER.md",
    str(EVIDENCE).replace("\\", "/"),
}


def run(*args, check=True):
    result = subprocess.run(args, check=check, text=True, capture_output=True)
    return result.stdout.strip()


def replace_once(path, old, new):
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected exactly one match, found {count}: {old[:120]!r}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8", newline="\n")


head = run("git", "rev-parse", "HEAD")
run("git", "merge-base", "--is-ancestor", BASE, head)
changed_before = {x for x in run("git", "diff", "--name-only", f"{BASE}...{head}").splitlines() if x}
helper_only = {str(SCRIPT).replace("\\", "/"), str(WORKFLOW).replace("\\", "/")}
if not changed_before.issubset(helper_only):
    raise RuntimeError(f"Unexpected pre-reconciliation branch drift: {sorted(changed_before - helper_only)}")

replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P07-006` — Fetch/pull — PENDING.",
    "- `FCCD-P07-006` — Fetch/pull — CLOSED.",
)

p07_006_provenance = """## P07-006 integration provenance

- Exact implementation candidate: `1fa59f6d6ac3a422e013c8119b9208b68b1e34c0` from PR #175 (`worker-b/fccd-p07-006-fetch-pull`).
- PR #175 exact-head Windows CI: run `34053021240` / run #407 — SUCCESS.
- PR #175 exact-head P06-007 Workspace Search: run `34053021234` / run #136 — SUCCESS.
- PR #175 exact-head P06-008 Large Workspace Safeguards: run `34053021316` / run #120 — SUCCESS.
- Normal merge commit: `4ca55a93d0636e4ce9d72e74178e3536f02ed859`.
- Exact post-merge canonical-main Windows CI: run `34053539796` / run #408 — SUCCESS.
- Exact post-merge P06-007 Workspace Search: run `34053539859` / run #137 — SUCCESS.
- Exact post-merge P06-008 Large Workspace Safeguards: run `34053539834` / run #121 — SUCCESS.
- Integrated evidence: `evidence/phases/P07/P07_006_INTEGRATED_RECONCILIATION_2026-09-06.md`.
- Evidence class remains cloud/self-test for bounded Git fetch and clean-tree fast-forward pull plus canonical integration provenance. No commit/push, history, destructive-operation safeguard closure, P07 phase closure, P08/P11 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is implied.

"""
replace_once(
    "CURRENT_PHASE.md",
    "## P07 cloud activation provenance\n",
    p07_006_provenance + "## P07 cloud activation provenance\n",
)

replace_once(
    "PROJECT_CONTROL.md",
    "`FCCD-P07-004 — Stage/unstage`, and `FCCD-P07-005 — Branch create/checkout` are CLOSED",
    "`FCCD-P07-004 — Stage/unstage`, `FCCD-P07-005 — Branch create/checkout`, and `FCCD-P07-006 — Fetch/pull` are CLOSED",
)
replace_once(
    "PROJECT_CONTROL.md",
    "`FCCD-P07-006` through `FCCD-P07-011` remain PENDING",
    "`FCCD-P07-007` through `FCCD-P07-011` remain PENDING",
)
replace_once(
    "PROJECT_CONTROL.md",
    "`evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_005_INTEGRATED_RECONCILIATION_2026-09-06.md`.",
    "`evidence/phases/P07/P07_004_INTEGRATED_RECONCILIATION_2026-09-06.md`, `evidence/phases/P07/P07_005_INTEGRATED_RECONCILIATION_2026-09-06.md`, and `evidence/phases/P07/P07_006_INTEGRATED_RECONCILIATION_2026-09-06.md`.",
)

replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P07-006 | Fetch/pull | PENDING |",
    "| FCCD-P07-006 | Fetch/pull | CLOSED |",
)

ledger_p07_006 = """`FCCD-P07-006` is CLOSED from the production bounded remote synchronization implementation integrated in PR #175. Exact implementation candidate `1fa59f6d6ac3a422e013c8119b9208b68b1e34c0` passed Windows CI `34053021240` / #407, P06-007 Workspace Search `34053021234` / #136, and P06-008 Large Workspace Safeguards `34053021316` / #120. PR #175 was normally merged as `4ca55a93d0636e4ce9d72e74178e3536f02ed859`; that exact canonical main passed Windows CI `34053539796` / #408, P06-007 Workspace Search `34053539859` / #137, and P06-008 Large Workspace Safeguards `34053539834` / #121. Coverage includes Application-owned `IGitRemoteService`, bounded non-interactive UTF-8 fetch, local-HEAD preservation verification, clean attached-HEAD fast-forward-only pull via explicit fetch plus `git merge --ff-only FETCH_HEAD`, dirty-tree and detached-HEAD refusal, non-fast-forward divergence refusal, concurrent state-drift checks, missing/invalid target handling, local bare-remote real-Git fixtures, timeout/cancellation, and owned-process-tree cleanup. No reset, clean, forced checkout, autostash, rebase, merge-commit fallback, commit, push, or conflict auto-resolution is introduced. Task evidence: `evidence/phases/P07/P07_006_INTEGRATED_RECONCILIATION_2026-09-06.md`. No commit/push, history, dirty-change provenance, destructive-operation safeguard closure, P07 phase closure, P08/P11 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE` is claimed; P07 remains `IN_PROGRESS`, P07-007 through P07-011 remain PENDING, and the two existing owner-last queue blockers remain unchanged.

"""
replace_once(
    "docs/TASK_LEDGER.md",
    "## P08 — Terminal/process supervision\n",
    ledger_p07_006 + "## P08 — Terminal/process supervision\n",
)
replace_once(
    "docs/TASK_LEDGER.md",
    "`FCCD-P07-001` through `FCCD-P07-005` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable reconciliation evidence. `FCCD-P07-006` through `FCCD-P07-011` remain PENDING.",
    "`FCCD-P07-001` through `FCCD-P07-006` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable reconciliation evidence. `FCCD-P07-007` through `FCCD-P07-011` remain PENDING.",
)
replace_once(
    "docs/TASK_LEDGER.md",
    "After this P07-005 reconciliation is integrated and exact resulting `main` remains green",
    "After this P07-006 reconciliation is integrated and exact resulting `main` remains green",
)
replace_once(
    "docs/TASK_LEDGER.md",
    "nominally `FCCD-P07-006 — Fetch/pull`",
    "nominally `FCCD-P07-007 — Commit/push`",
)

if EVIDENCE.exists():
    raise RuntimeError(f"Evidence path already exists: {EVIDENCE}")
EVIDENCE.parent.mkdir(parents=True, exist_ok=True)
EVIDENCE.write_text(
    """# FCCD-P07-006 — Integrated reconciliation

Date: 2026-09-06
Task: `FCCD-P07-006 — Fetch/pull`
Canonical disposition: `CLOSED`
Evidence class: cloud/self-test + canonical integration provenance

## Accepted implementation

- Implementation PR: #175 — `P07-006: add safe fetch and fast-forward pull`.
- Implementation branch: `worker-b/fccd-p07-006-fetch-pull`.
- Exact accepted implementation candidate: `1fa59f6d6ac3a422e013c8119b9208b68b1e34c0`.
- Normal merge commit: `4ca55a93d0636e4ce9d72e74178e3536f02ed859`.
- Merge ancestry preserves previous canonical main `f9eea40f288cffa7c40ff9fb2e2fa64dfa1fee99` and tested implementation head `1fa59f6d6ac3a422e013c8119b9208b68b1e34c0`; no squash/rebase is claimed.

## Exact implementation-head validation

- Windows CI run `34053021240` / #407 — `SUCCESS`.
- P06-007 Workspace Search run `34053021234` / #136 — `SUCCESS`.
- P06-008 Large Workspace Safeguards run `34053021316` / #120 — `SUCCESS`.

## Exact post-merge canonical-main validation

All permanent gates were rerun against exact merge SHA `4ca55a93d0636e4ce9d72e74178e3536f02ed859`:

- Windows CI run `34053539796` / #408 — `SUCCESS`.
- P06-007 Workspace Search run `34053539859` / #137 — `SUCCESS`.
- P06-008 Large Workspace Safeguards run `34053539834` / #121 — `SUCCESS`.

## Implemented safety boundary

The integrated `IGitRemoteService` provides bounded local Git remote synchronization while preserving owner work:

- fetch is explicit, non-interactive and verifies local `HEAD` does not move;
- pull requires an attached `HEAD` plus clean index/work tree;
- pull fetches the explicit remote branch, proves fast-forward ancestry, and performs only `git merge --ff-only FETCH_HEAD`;
- dirty trees, detached `HEAD`, divergence, concurrent branch/HEAD/work-tree drift, missing/invalid targets and remote failures return typed refusal/failure results;
- there is no reset, clean, force checkout, autostash, rebase, merge-commit fallback, commit, push or conflict auto-resolution;
- disposable real-Git fixtures use a local bare remote, so no external network/provider/owner-machine evidence is required for this task.

## Governance reconciliation

- `FCCD-P07-006` is CLOSED only after exact implementation-head validation, normal merge integration and exact post-merge canonical-main validation all succeeded.
- P07 remains `IN_PROGRESS` with `PHASE_EXIT_GATE=NOT_RUN`.
- `FCCD-P07-007` through `FCCD-P07-011` remain PENDING.
- P08 and later implementation, including P11 Blender tasks, remain prohibited until P07 is truthfully closed.
- No new owner-only acceptance item is introduced.
- Existing release-blocking owner queue items remain exactly `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET`.
- `KNOWN_RELEASE_BLOCKERS=2`; `VERIFIED_FINAL_COMPLETE=false`.
""",
    encoding="utf-8",
    newline="\n",
)

# Remove temporary orchestration before forming the durable candidate.
SCRIPT.unlink(missing_ok=True)
WORKFLOW.unlink(missing_ok=True)

subprocess.run(["git", "config", "user.name", "fcc-cloud-worker"], check=True)
subprocess.run(["git", "config", "user.email", "fcc-cloud-worker@users.noreply.github.com"], check=True)
subprocess.run(["git", "add", "-A"], check=True)
final_changed = {x for x in run("git", "diff", "--cached", "--name-only", BASE).splitlines() if x}
if final_changed != ALLOWED_FINAL:
    raise RuntimeError(f"Final reconciliation scope mismatch. Expected {sorted(ALLOWED_FINAL)}, got {sorted(final_changed)}")

# Guard owner-last invariants in the durable candidate.
for path in ("CURRENT_PHASE.md", "PROJECT_CONTROL.md"):
    text = Path(path).read_text(encoding="utf-8")
    for required in (
        "CURRENT_PHASE: P07",
        "CURRENT_PHASE_STATE: IN_PROGRESS",
        "PHASE_EXIT_GATE: NOT_RUN",
        "KNOWN_RELEASE_BLOCKERS: 2",
        "VERIFIED_FINAL_COMPLETE: false",
        "OWNER-P04-008-REAL-TARGET;OWNER-P05-EXIT-REAL-TARGET",
    ):
        if required not in text:
            raise RuntimeError(f"{path}: owner-last invariant missing after patch: {required}")

subprocess.run(["git", "commit", "-m", "FCCD-P07-006: reconcile integrated fetch/pull closure"], check=True)
subprocess.run(["git", "push", "origin", f"HEAD:{BRANCH}"], check=True)
