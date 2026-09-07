from pathlib import Path

SOURCE_SHA = "e94f241b75ab7119bbb45f48872d24b78c5f9007"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


current_path = Path("CURRENT_PHASE.md")
current = current_path.read_text(encoding="utf-8")
current = replace_once(
    current,
    """CURRENT_PHASE: P07
CURRENT_PHASE_NAME: Change review + Git
CURRENT_PHASE_STATE: CLOSED
NEXT_PHASE: P08
PHASE_EXIT_GATE: PASS""",
    """CURRENT_PHASE: P08
CURRENT_PHASE_NAME: Terminal/process supervision
CURRENT_PHASE_STATE: IN_PROGRESS
NEXT_PHASE: P09
PHASE_EXIT_GATE: NOT_RUN""",
    "CURRENT_PHASE status block",
)
current = replace_once(
    current,
    """P07 is canonically CLOSED in this closure state. `FCCD-P07-001` through `FCCD-P07-011` are normally integrated and exact-main verified, and dedicated exact-candidate phase-exit run `34068796895` passed on immutable product candidate `7561dd88b16531403a9f8f5667db17801105687f`. Canonical closure evidence is `evidence/phases/P07/CLOSURE.md`.

`CURRENT_PHASE` deliberately remains `P07` after closure. P08 is not active yet. A separate governance transition may activate `CURRENT_PHASE=P08` only after this closure state is normally merged and the resulting exact canonical `main` remains green. No P08 or later implementation is authorized inside this closure state.""",
    """P07 is canonically CLOSED. `FCCD-P07-001` through `FCCD-P07-011` are normally integrated and exact-main verified, dedicated exact-candidate phase-exit run `34068796895` passed on immutable product candidate `7561dd88b16531403a9f8f5667db17801105687f`, closure PR #187 was normally merged as `e94f241b75ab7119bbb45f48872d24b78c5f9007`, and exact post-closure Windows CI `34069973813`, Workspace Search `34069973830`, and Large Workspace Safeguards `34069973823` all completed SUCCESS. Canonical closure evidence is `evidence/phases/P07/CLOSURE.md`.

P08 — Terminal/process supervision — is now the sole legal cloud implementation/convergence phase. Only dependency-valid, unclaimed P08 work may begin. P09 and later implementation remain prohibited until P08 is truthfully closed with its exit gate resolved under canonical governance.""",
    "P07 closure/P08 activation paragraph",
)
current = replace_once(
    current,
    "- P07 is CLOSED and retained as the current closure checkpoint until a separate, validated transition activates P08; no later-phase implementation is authorized yet.",
    "- Exactly one cloud implementation/convergence phase is active: P08.",
    "owner-last active-phase invariant",
)
current = replace_once(
    current,
    "## P07 cloud task inventory",
    """## P08 cloud task inventory

- `FCCD-P08-001` — Process supervisor with owned process-tree tracking — PENDING.
- `FCCD-P08-002` — Graceful→forced cancellation escalation — PENDING.
- `FCCD-P08-003` — Bounded streaming log pipeline — PENDING.
- `FCCD-P08-004` — ConPTY terminal host — PENDING.
- `FCCD-P08-005` — PowerShell/CMD profiles — PENDING.
- `FCCD-P08-006` — Optional Git Bash/WSL detection — PENDING.
- `FCCD-P08-007` — Interactive terminal UX — PENDING.
- `FCCD-P08-008` — Process/terminal safety tests — PENDING.

## P08 cloud activation provenance

- Source closed-phase canonical main: `e94f241b75ab7119bbb45f48872d24b78c5f9007`.
- P07 closure integration: PR #187, normal merge.
- Dedicated P07 exact-candidate phase-exit gate: run `34068796895` — SUCCESS.
- Exact post-closure main Windows CI: run `34069973813` / #433 — SUCCESS.
- Exact post-closure main P06-007 Workspace Search: run `34069973830` / #162 — SUCCESS.
- Exact post-closure main P06-008 Large Workspace Safeguards: run `34069973823` / #146 — SUCCESS.
- Pre-activation live claim scan: no open PR and no P08 branch/claim was present.
- Canonical owner queue remains exactly `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET`, both unresolved and release-blocking.
- `VERIFIED_FINAL_COMPLETE` remains `false`; P22 remains prohibited while any required owner queue item is unresolved.
- This is scheduling/governance activation only; no P08 product implementation is included.

## P07 cloud task inventory""",
    "P08 activation inventory insertion",
)
current_path.write_text(current, encoding="utf-8", newline="\n")

control_path = Path("PROJECT_CONTROL.md")
control = control_path.read_text(encoding="utf-8")
control = replace_once(
    control,
    """CURRENT_PHASE: P07
CURRENT_PHASE_NAME: Change review + Git
CURRENT_PHASE_STATE: CLOSED
NEXT_PHASE: P08
PHASE_EXIT_GATE: PASS""",
    """CURRENT_PHASE: P08
CURRENT_PHASE_NAME: Terminal/process supervision
CURRENT_PHASE_STATE: IN_PROGRESS
NEXT_PHASE: P09
PHASE_EXIT_GATE: NOT_RUN""",
    "PROJECT_CONTROL status block",
)
control = replace_once(
    control,
    """P07 — Change review + Git — is canonically CLOSED in this closure state. `FCCD-P07-001` through `FCCD-P07-011` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable task reconciliation. Exact immutable phase candidate `7561dd88b16531403a9f8f5667db17801105687f` passed pre-closure Windows CI `34068325212` / #431, Workspace Search `34068325218` / #160, and Large Workspace Safeguards `34068325246` / #144, then dedicated P07 phase-exit run `34068796895` / job `101582228434` completed SUCCESS with the full Windows baseline, explicit Git acceptance suite, exact-SHA/diff-hygiene guards, and a clean worktree. Closure evidence is `evidence/phases/P07/CLOSURE.md`. `CURRENT_PHASE` deliberately remains P07 until this closure change is normally integrated and the resulting exact canonical `main` remains green; only then may a separate governance transition activate P08. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes.""",
    """P07 — Change review + Git — is canonically CLOSED: `FCCD-P07-001` through `FCCD-P07-011` are CLOSED after exact PR-head validation, normal merge integration, exact post-merge canonical-main validation, and durable task reconciliation. Exact immutable phase candidate `7561dd88b16531403a9f8f5667db17801105687f` passed pre-closure Windows CI `34068325212` / #431, Workspace Search `34068325218` / #160, and Large Workspace Safeguards `34068325246` / #144; dedicated P07 phase-exit run `34068796895` / job `101582228434` completed SUCCESS; closure PR #187 was normally merged as `e94f241b75ab7119bbb45f48872d24b78c5f9007`; and exact post-closure Windows CI `34069973813` / #433, Workspace Search `34069973830` / #162, and Large Workspace Safeguards `34069973823` / #146 all completed SUCCESS. Closure evidence is `evidence/phases/P07/CLOSURE.md`.

P08 — Terminal/process supervision — is now the single active cloud implementation/convergence phase. Its eight mandatory ledger tasks remain PENDING at activation. Workers must select dependency-valid unclaimed P08 work while preserving owned-process boundaries, bounded output, cancellation escalation, interactive terminal safety, and owner work. P09 and later implementation remain prohibited until P08 is truthfully closed. The two earlier owner-last queue obligations remain unresolved/release-blocking, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false`. P22 and `VERIFIED_FINAL_COMPLETE=true` remain impossible until all queued owner evidence is genuinely executed, reviewed, integrated, and reconciled and every normal mandatory release gate passes.""",
    "PROJECT_CONTROL P07/P08 paragraph",
)
control_path.write_text(control, encoding="utf-8", newline="\n")

evidence = Path("evidence/governance/OWNER_LAST_P08_CLOUD_ACTIVATION_2026-09-07.md")
evidence.parent.mkdir(parents=True, exist_ok=True)
evidence.write_text("""# Owner-Last P08 Cloud Activation — 2026-09-07

```text
SOURCE_MAIN_SHA: e94f241b75ab7119bbb45f48872d24b78c5f9007
SOURCE_PHASE: P07
SOURCE_PHASE_STATE: CLOSED
SOURCE_PHASE_EXIT_GATE: PASS
ACTIVATED_PHASE: P08
ACTIVATED_PHASE_NAME: Terminal/process supervision
ACTIVATED_PHASE_STATE: IN_PROGRESS
NEXT_PHASE: P09
ACTIVATED_PHASE_EXIT_GATE: NOT_RUN
KNOWN_PHASE_BLOCKERS: 0
KNOWN_RELEASE_BLOCKERS: 2
OWNER_LAST_MODE: ACTIVE
VERIFIED_FINAL_COMPLETE: false
```

## Eligibility

P07 is canonically closed and integrated. PR #187 was normally merged as `e94f241b75ab7119bbb45f48872d24b78c5f9007` after dedicated exact-candidate P07 phase-exit gate `34068796895` passed on immutable product candidate `7561dd88b16531403a9f8f5667db17801105687f`.

Exact resulting canonical main gates all passed:

- Windows CI `34069973813` / #433 — SUCCESS.
- P06-007 Workspace Search `34069973830` / #162 — SUCCESS.
- P06-008 Large Workspace Safeguards `34069973823` / #146 — SUCCESS.

No P07 cloud-actionable defect or P07 owner-only residual remains.

## Owner-last preservation

The canonical final-owner queue remains unchanged:

- `OWNER-P04-008-REAL-TARGET`
- `OWNER-P05-EXIT-REAL-TARGET`

Both remain unresolved and `releaseBlocking=true`; their source task/gate states are not converted to PASS. `P04=NOT_RUN`, `P05=NOT_RUN`, `VERIFIED_FINAL_COMPLETE=false`, and P22 remains unavailable while required owner evidence is queued.

## Concurrency / claim check

Immediately before the first transition write, canonical main was exactly `e94f241b75ab7119bbb45f48872d24b78c5f9007`, no pull request was open, and no P08 branch/claim existed. This transition therefore does not steal or duplicate active P08 implementation work.

## Activated boundary

P08 — Terminal/process supervision — is the sole active cloud implementation/convergence phase. Its mandatory task inventory remains PENDING:

- `FCCD-P08-001` — Process supervisor with owned process-tree tracking.
- `FCCD-P08-002` — Graceful→forced cancellation escalation.
- `FCCD-P08-003` — Bounded streaming log pipeline.
- `FCCD-P08-004` — ConPTY terminal host.
- `FCCD-P08-005` — PowerShell/CMD profiles.
- `FCCD-P08-006` — Optional Git Bash/WSL detection.
- `FCCD-P08-007` — Interactive terminal UX.
- `FCCD-P08-008` — Process/terminal safety tests.

P09 and later implementation remain prohibited until P08 closes truthfully with `PHASE_EXIT_GATE=PASS`. This activation includes no P08 product implementation and creates no new owner-only obligation.
""", encoding="utf-8", newline="\n")

# Self-delete temporary orchestration files so the durable branch diff is governance-only.
for temp in [Path(".github/temp/p08-activation.py"), Path(".github/workflows/temp-p08-activation.yml")]:
    if temp.exists():
        temp.unlink()
