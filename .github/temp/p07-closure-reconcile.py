from pathlib import Path
import re
import subprocess

CANDIDATE = "7561dd88b16531403a9f8f5667db17801105687f"
EXACT_GATE_RUN = "34068796895"
EXACT_GATE_JOB = "101582228434"


def replace_exact(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected exact fragment once, found {count}: {old[:100]!r}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8")


def replace_regex(path: str, pattern: str, replacement: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    new_text, count = re.subn(pattern, replacement, text, count=1, flags=re.S | re.M)
    if count != 1:
        raise RuntimeError(f"{path}: expected regex once, found {count}: {pattern}")
    p.write_text(new_text, encoding="utf-8")


# CURRENT_PHASE.md — closure state only; CURRENT_PHASE deliberately stays P07.
replace_exact("CURRENT_PHASE.md", "CURRENT_PHASE_STATE: IN_PROGRESS", "CURRENT_PHASE_STATE: CLOSED")
replace_exact("CURRENT_PHASE.md", "PHASE_EXIT_GATE: NOT_RUN", "PHASE_EXIT_GATE: PASS")
replace_exact(
    "CURRENT_PHASE.md",
    "P07 is now the sole legal cloud implementation/convergence phase. Only dependency-valid, unclaimed P07 work may begin. P08 and later implementation remain prohibited until P07 is truthfully closed with its exit gate resolved under canonical governance.",
    "P07 is canonically CLOSED in this closure state. `FCCD-P07-001` through `FCCD-P07-011` are normally integrated and exact-main verified, and dedicated exact-candidate phase-exit run `34068796895` passed on immutable product candidate `7561dd88b16531403a9f8f5667db17801105687f`. Canonical closure evidence is `evidence/phases/P07/CLOSURE.md`.\n\n`CURRENT_PHASE` deliberately remains `P07` after closure. P08 is not active yet. A separate governance transition may activate `CURRENT_PHASE=P08` only after this closure state is normally merged and the resulting exact canonical `main` remains green. No P08 or later implementation is authorized inside this closure state."
)
replace_exact(
    "CURRENT_PHASE.md",
    "- Exactly one cloud implementation/convergence phase is active: P07.",
    "- P07 is CLOSED and retained as the current closure checkpoint until a separate, validated transition activates P08; no later-phase implementation is authorized yet."
)
replace_regex(
    "CURRENT_PHASE.md",
    r"## Next legitimate action after this reconciliation is integrated\n\nNormally merge this P06 closure state/evidence.*?VERIFIED_FINAL_COMPLETE=false`\.\s*$",
    """## P07 phase-exit provenance

- Exact immutable product candidate: `7561dd88b16531403a9f8f5667db17801105687f`.
- Exact candidate pre-closure Windows CI: run `34068325212` / #431 — SUCCESS.
- Exact candidate pre-closure P06-007 Workspace Search: run `34068325218` / #160 — SUCCESS.
- Exact candidate pre-closure P06-008 Large Workspace Safeguards: run `34068325246` / #144 — SUCCESS.
- Dedicated exact-candidate P07 phase-exit gate: run `34068796895` / job `101582228434` — SUCCESS.
- Gate coverage: complete Windows baseline plus explicit `FCCCodeDesktop.UnitTests.Git*` acceptance suite, pre-closure state guards, exact-SHA assertion, diff hygiene, and final clean-worktree assertion.
- Canonical closure evidence: `evidence/phases/P07/CLOSURE.md`.
- P07 phase state: `CLOSED`; `PHASE_EXIT_GATE=PASS`; phase-local blockers/regressions: none.
- No P07 owner-only acceptance item was created; the canonical owner queue remains exactly the two pre-existing P04/P05 release blockers.

## Next legitimate action after this closure is integrated

Normally merge this P07 closure state/evidence and require the resulting exact canonical `main` to remain green. Only then may a separate governance transition activate P08 as the next sequential cloud implementation phase. Do not implement P08, P13, or any later phase inside the P07 closure change. Preserve `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` as unresolved release blockers and keep `VERIFIED_FINAL_COMPLETE=false`.
"""
)

# PROJECT_CONTROL.md — same closure checkpoint, no P08 activation.
replace_exact("PROJECT_CONTROL.md", "CURRENT_PHASE_STATE: IN_PROGRESS", "CURRENT_PHASE_STATE: CLOSED")
replace_exact("PROJECT_CONTROL.md", "PHASE_EXIT_GATE: NOT_RUN", "PHASE_EXIT_GATE: PASS")
replace_regex(
    "PROJECT_CONTROL.md",
    r"P07 — Change review \+ Git — is the single active cloud implementation/convergence phase\..*?P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes\.\n\n---\n\n## 3\.",
    """P07 — Change review + Git — is canonically CLOSED in this closure state. `FCCD-P07-001` through `FCCD-P07-011` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable task reconciliation. Exact immutable phase candidate `7561dd88b16531403a9f8f5667db17801105687f` passed pre-closure Windows CI `34068325212` / #431, Workspace Search `34068325218` / #160, and Large Workspace Safeguards `34068325246` / #144, then dedicated P07 phase-exit run `34068796895` / job `101582228434` completed SUCCESS with the full Windows baseline, explicit Git acceptance suite, exact-SHA/diff-hygiene guards, and a clean worktree. Closure evidence is `evidence/phases/P07/CLOSURE.md`. `CURRENT_PHASE` deliberately remains P07 until this closure change is normally integrated and the resulting exact canonical `main` remains green; only then may a separate governance transition activate P08. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes.

---

## 3."""
)

# TASK_LEDGER.md — tasks were already CLOSED; record phase gate truth and replace stale next action.
replace_exact(
    "docs/TASK_LEDGER.md",
    "\n## P08 — Terminal/process supervision\n",
    "\nP07 is canonically CLOSED at the phase level on immutable candidate `7561dd88b16531403a9f8f5667db17801105687f`. Dedicated exact-candidate exit-gate run `34068796895` / job `101582228434` completed SUCCESS after pre-closure guards, the complete Windows baseline, explicit P07 Git acceptance tests, and exact-SHA/clean-worktree verification. Canonical evidence is `evidence/phases/P07/CLOSURE.md`. P08 is only the authorized next phase and is not active until a separate governance transition is normally integrated and exact-main verified.\n\n## P08 — Terminal/process supervision\n"
)
replace_regex(
    "docs/TASK_LEDGER.md",
    r"## Current next action\n.*\Z",
    """## Current next action

`CURRENT_PHASE = P07` is now a **CLOSED closure checkpoint** with `PHASE_EXIT_GATE=PASS`. All `FCCD-P07-001` through `FCCD-P07-011` rows are CLOSED, exact immutable candidate `7561dd88b16531403a9f8f5667db17801105687f` was green on the permanent pre-closure runs, and dedicated phase-exit run `34068796895` / job `101582228434` passed the full Windows baseline plus explicit P07 Git acceptance and clean exact-SHA checks.

P04 remains acceptance-unresolved through `FCCD-P04-008` and its one-to-one queued `OWNER-P04-008-REAL-TARGET` obligation. P05 cloud implementation remains integrated, but its standalone exit observation remains queued as `OWNER-P05-EXIT-REAL-TARGET`. Their gates remain `P04=NOT_RUN;P05=NOT_RUN`; owner-last scheduling does not waive either obligation or permit release.

The next legal action after this P07 closure is normally merged and the resulting exact canonical `main` remains green is a **separate governance transition activating P08**. Do not implement P08 inside the closure change, do not skip to P13 or any later phase, and do not fabricate owner/manual evidence.
"""
)

closure = f"""# P07 Phase Closure — Change review + Git

```text
PHASE: P07
PHASE_NAME: Change review + Git
CANDIDATE_SHA: {CANDIDATE}
DATE: 2026-09-07
EXIT_GATE: PASS
KNOWN_BLOCKERS: 0
KNOWN_REGRESSIONS: 0
MANDATORY_TASKS: 11/11 CLOSED
EXACT_GATE_RUN: {EXACT_GATE_RUN}
EXACT_GATE_JOB: {EXACT_GATE_JOB}
PRE_CLOSURE_MAIN_WINDOWS_CI_RUN: 34068325212
PRE_CLOSURE_WORKSPACE_SEARCH_RUN: 34068325218
PRE_CLOSURE_LARGE_WORKSPACE_RUN: 34068325246
OWNER_PENDING_P07: NONE
GLOBAL_RELEASE_BLOCKERS: 2
VERIFIED_FINAL_COMPLETE: false
```

## 1. Mandatory task reconciliation

All eleven mandatory P07 task rows were canonically `CLOSED` before this phase gate ran. Their durable integration records are retained under `evidence/phases/P07/P07_001_INTEGRATED_RECONCILIATION_2026-09-06.md` through `P07_011_INTEGRATED_RECONCILIATION_2026-09-07.md`.

The integrated P07 surface covers repository detection, status/changed files, bounded diff review, explicit stage/unstage, safe local branch create/checkout, bounded fetch/clean-tree fast-forward pull, commit/non-force push, history, dirty/pre-existing-change provenance, fail-closed destructive-command safeguards, and cross-service conflict/error workflows.

No new product implementation was added by this exit gate.

## 2. Exact-candidate automated verification

Dedicated validation-only branch `worker-b/p07-exit-gate` ran workflow `P07 Exit Gate Exact Validation`. The workflow commit was not treated as product code: its first action explicitly checked out immutable canonical candidate `{CANDIDATE}` before validation.

Authoritative gate:
- run `{EXACT_GATE_RUN}` / job `{EXACT_GATE_JOB}` — **SUCCESS**.
- GitHub-hosted Microsoft Windows Server 2025.
- .NET SDK exactly `10.0.400`.

The gate completed:

```text
pre-closure canonical-state guards
RESULT: PASS

.\\tools\\ci\\run-windows-ci.ps1
RESULT: PASS

dotnet test tests/FCCCodeDesktop.UnitTests/FCCCodeDesktop.UnitTests.csproj \\
  --configuration Release --no-build --no-restore \\
  --filter "FullyQualifiedName~FCCCodeDesktop.UnitTests.Git"
RESULT: PASS

git diff --check
git diff --cached --check
final clean-worktree and exact-SHA assertions
RESULT: PASS
```

Before the dedicated gate, the same exact candidate was already green on canonical main:
- Windows CI `34068325212` / #431 — SUCCESS.
- P06-007 Workspace Search `34068325218` / #160 — SUCCESS.
- P06-008 Large Workspace Safeguards `34068325246` / #144 — SUCCESS.

## 3. Exit-criterion acceptance

The canonical P07 exit criterion is: standard Git workflows and defined conflict/dirty-tree scenarios pass without silently destroying owner work.

The exact candidate proves this through the integrated `GitIntegrationConflictScenarioTests` and the rest of the Git test surface:

1. **Clean standard workflow:** clean pull → explicit stage → commit → non-force push; local and remote heads converge and status finishes clean.
2. **Dirty checkout refusal:** a conflicting owner modification causes checkout refusal; the current branch and exact owner bytes remain unchanged; provenance remains `PreExistingDirty`.
3. **Real merge conflict:** an intentional disposable content conflict remains visibly `Unmerged`/conflicted; inspection preserves conflict bytes; destructive reset/clean/forced checkout/discard command shapes remain denied.
4. **Diverged remote refusal:** fast-forward-only pull and non-force push both refuse divergence without silently moving the local or remote head.

The broader P07 suite also covers Unicode/Arabic/space-containing refs and paths, bounded timeouts/cancellation, explicit-path index mutation, safe branch-name validation, clean-tree pull requirements, push rejection, history bounds, rename provenance, and fail-closed mutation-command classification.

## 4. Safety and integrity conclusion

No phase-exit scenario silently discarded owner work. Read-only surfaces remain non-mutating. Index mutation remains explicit-path only. Branch checkout never forces/discards changes. Pull remains clean-tree and fast-forward-only. Push remains non-force. Destructive command shapes remain fail-closed. Conflict and divergence states remain visible and typed.

The dedicated gate ended with a clean worktree and unchanged exact candidate SHA.

No force push, squash, rebase, reset/clean bypass, or safety-check weakening was used to obtain this PASS.

## 5. Owner-last classification

No P07 phase-exit requirement remains genuinely owner-only. P07's acceptance criterion is fully exercised with disposable real Git repositories/bare remotes on Windows CI.

The canonical final-owner queue is unchanged and still contains only the two earlier release-blocking obligations:
- `OWNER-P04-008-REAL-TARGET`
- `OWNER-P05-EXIT-REAL-TARGET`

Those obligations remain `QUEUED`; their source task/gate remains unresolved; `P04=NOT_RUN`, `P05=NOT_RUN`, `KNOWN_RELEASE_BLOCKERS=2`, and `VERIFIED_FINAL_COMPLETE=false` remain truthful. This P07 PASS does not waive or satisfy either item.

## 6. Known defects and regressions

```text
KNOWN_P07_PHASE_LOCAL_DEFECTS: NONE
EARLIER_CLOUD_REGRESSIONS: NONE
```

The final exact candidate passed the permanent Windows baseline before the dedicated gate, and the dedicated gate reran that baseline plus the explicit P07 Git acceptance subset. No repairable P07 defect remains known.

## 7. Exit decision

```text
ALL_P07_MANDATORY_TASKS_CLOSED: true
P07_EXACT_HEAD_GATE_PASS: true
P07_CANDIDATE_MAIN_GREEN: true
P07_KNOWN_PHASE_BLOCKERS: 0
P07_KNOWN_REGRESSIONS: 0
P07_OWNER_EVIDENCE_QUEUED: 0
EXIT_GATE: PASS
P07_PHASE_STATE: CLOSED
AUTHORIZED_NEXT_PHASE: P08
P08_IMPLEMENTATION_IN_THIS_CLOSURE: NONE
P13_IMPLEMENTATION_IN_THIS_CLOSURE: NONE
VERIFIED_FINAL_COMPLETE: false
```

P07 is therefore truthfully closed on exact candidate `{CANDIDATE}`. This closure record **does not activate P08** and does not authorize P13. After this closure/control-state change is normally merged and the resulting exact canonical `main` remains green, a separate governance transition may activate P08 as the next sequential cloud implementation phase while preserving both unresolved owner-last release blockers.
"""

closure_path = Path("evidence/phases/P07/CLOSURE.md")
if closure_path.exists():
    raise RuntimeError("P07 CLOSURE.md unexpectedly already exists")
closure_path.write_text(closure, encoding="utf-8")

# Remove temporary orchestration before durable commit.
Path(".github/temp/p07-closure-reconcile.py").unlink(missing_ok=False)
Path(".github/workflows/p07-closure-reconcile.yml").unlink(missing_ok=False)

# Durable scope guard.
allowed = {
    "CURRENT_PHASE.md",
    "PROJECT_CONTROL.md",
    "docs/TASK_LEDGER.md",
    "evidence/phases/P07/CLOSURE.md",
}
status = subprocess.check_output(["git", "status", "--porcelain=v1"], text=True, encoding="utf-8")
changed = set()
for line in status.splitlines():
    path = line[3:]
    if " -> " in path:
        path = path.split(" -> ", 1)[1]
    changed.add(path.replace("\\", "/"))
if changed != allowed:
    raise RuntimeError(f"Unexpected durable scope. expected={sorted(allowed)} actual={sorted(changed)}")

subprocess.run(["git", "config", "user.name", "fccd-owner-last-worker"], check=True)
subprocess.run(["git", "config", "user.email", "fccd-owner-last-worker@users.noreply.github.com"], check=True)
subprocess.run(["git", "add", "CURRENT_PHASE.md", "PROJECT_CONTROL.md", "docs/TASK_LEDGER.md", "evidence/phases/P07/CLOSURE.md", ".github/temp/p07-closure-reconcile.py", ".github/workflows/p07-closure-reconcile.yml"], check=True)
subprocess.run(["git", "diff", "--cached", "--check"], check=True)
subprocess.run(["git", "commit", "-m", "P07: reconcile exact phase closure"], check=True)
subprocess.run(["git", "push", "origin", "HEAD:closure/p07-phase-exit"], check=True)
