from pathlib import Path

EXPECTED_MAIN = "5dae18f48f7519a7d67fbdf56ccbdb3ef1a8ae9d"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


ledger_path = Path("docs/TASK_LEDGER.md")
ledger = ledger_path.read_text(encoding="utf-8")
ledger = replace_once(
    ledger,
    "| FCCD-P10-001 | Unity project/version detector | PENDING |",
    "| FCCD-P10-001 | Unity project/version detector | CLOSED |",
    "TASK_LEDGER P10-001 row",
)
ledger = replace_once(
    ledger,
    "Concurrent P10-003 implementation merge `165708a542faee753149fedc19c787297a75f38d` is preserved with its canonical row still PENDING; P10-001 is likewise unchanged.",
    "At the P10-002 reconciliation checkpoint, concurrent P10-003 implementation merge `165708a542faee753149fedc19c787297a75f38d` and P10-001 were still unreconciled. Subsequent canonical reconciliations closed P10-003 and P10-001; this sentence is retained as historical P10-002 provenance only.",
    "TASK_LEDGER stale P10-002 provenance",
)
p10_001_ledger = """`FCCD-P10-001` is CLOSED from the read-only Unity project/version detector recovered and integrated in PR #237. Exact accepted implementation candidate `4fb5fbbc0d106230a48d5f1d8922671ccc62bb9d` passed P10-001 Unity Project Detection `34208819369`, Windows CI `34208819312`, P06-007 Workspace Search `34208819367`, and P06-008 Large Workspace Safeguards `34208819342`. PR #237 was normally merged as `5dae18f48f7519a7d67fbdf56ccbdb3ef1a8ae9d`; that exact implementation main passed P10-001 Unity Project Detection `34209538818`, Windows CI `34209538838`, Workspace Search `34209538858`, and Large Workspace Safeguards `34209538774`. Coverage includes Unity project markers, canonical `ProjectVersion.txt` parsing, incomplete/missing/non-Unity roots, Unicode/Arabic/space paths, cancellation/invalid-input handling, deterministic fixtures, and locked dependencies without launching Unity or mutating a project. The initial exact-head analyzer failure `CA1861` was repaired by reusing static line separators rather than suppressing analyzers or weakening CI. Task evidence: `evidence/phases/P10/P10_001_INTEGRATED_RECONCILIATION_2026-09-08.md`. No owner-only evidence is required or queued. P10 remains `IN_PROGRESS`; P10-004 through P10-013 remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P11+ remain prohibited; `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `VERIFIED_FINAL_COMPLETE=false`.

"""
ledger = replace_once(
    ledger,
    "`FCCD-P10-002` is CLOSED from the exact-version Unity install/Hub editor resolver integrated in PR #232.",
    p10_001_ledger + "`FCCD-P10-002` is CLOSED from the exact-version Unity install/Hub editor resolver integrated in PR #232.",
    "TASK_LEDGER P10-001 provenance insertion",
)
old_next = """`CURRENT_PHASE = P09` is canonically `CLOSED` in this closure state. `FCCD-P09-001` through `FCCD-P09-008` are CLOSED, exact candidate Windows CI `34195027792`, Workspace Search `34195027743`, Large Workspace Safeguards `34195027756`, and dedicated P09 exit run `34195027724` are `SUCCESS`, and `PHASE_EXIT_GATE=PASS`. Canonical phase evidence is `evidence/phases/P09/CLOSURE.md`.

Normally merge this P09 closure state/evidence and require the resulting exact canonical `main` to remain green. Only then may a separate governance transition activate `CURRENT_PHASE=P10`. Do not implement P10 or any later phase inside this closure change. Preserve `OWNER-P04-008-REAL-TARGET` as the sole unresolved release-blocking owner item, keep P05 `PASS_INTEGRATED`, keep `P04=NOT_RUN`, and keep `VERIFIED_FINAL_COMPLETE=false`.
"""
new_next = """`CURRENT_PHASE = P10` is the sole active cloud implementation/convergence phase. `FCCD-P10-001` through `FCCD-P10-003` are CLOSED after normal implementation integration, exact implementation-main validation, and durable task reconciliation; `FCCD-P10-004` through `FCCD-P10-013` remain PENDING. After this P10-001 reconciliation itself passes exact-head CI, normal merge, and exact-main verification, the next legal cloud action is a fresh live claim/concurrency sweep and then `FCCD-P10-004 — Unity process/project resource locking` only if it remains unclaimed and dependency-valid. P11 and later implementation remain prohibited until P10 is truthfully closed under canonical governance. Preserve `OWNER-P04-008-REAL-TARGET` as the sole unresolved release-blocking owner item, keep `P04=NOT_RUN`, and keep `VERIFIED_FINAL_COMPLETE=false`.
"""
ledger = replace_once(ledger, old_next, new_next, "TASK_LEDGER current next action")
ledger_path.write_bytes(ledger.encode("utf-8"))

current_path = Path("CURRENT_PHASE.md")
current = current_path.read_text(encoding="utf-8")
current = replace_once(
    current,
    "- `FCCD-P10-001` — Unity project/version detector — PENDING.",
    "- `FCCD-P10-001` — Unity project/version detector — CLOSED.",
    "CURRENT_PHASE P10-001 inventory",
)
current = replace_once(
    current,
    "Concurrent P10-003 implementation was normally merged as `165708a542faee753149fedc19c787297a75f38d`; its canonical row remains PENDING and is not modified here. P10-001 also remains independently owned/PENDING.",
    "At the P10-002 reconciliation checkpoint, concurrent P10-003 implementation `165708a542faee753149fedc19c787297a75f38d` and P10-001 were still unreconciled. Subsequent canonical reconciliations closed P10-003 and P10-001; this sentence is retained as historical P10-002 provenance only.",
    "CURRENT_PHASE stale P10-002 provenance",
)
p10_001_current = """## P10-001 integration provenance

- Task: `FCCD-P10-001 — Unity project/version detector` — `CLOSED` in this reconciliation candidate.
- Recovery boundary: legitimate stale detector work predated integrated P10-002/P10-003; it was recovered onto exact then-current main rather than duplicated or force-pushed.
- Implementation PR: #237 (`recovery/fccd-p10-001-unity-project-detector`).
- Exact accepted implementation candidate: `4fb5fbbc0d106230a48d5f1d8922671ccc62bb9d`.
- Exact implementation-head P10-001 Unity Project Detection: run `34208819369` — SUCCESS.
- Exact implementation-head Windows CI: run `34208819312` — SUCCESS.
- Exact implementation-head P06-007 Workspace Search: run `34208819367` — SUCCESS.
- Exact implementation-head P06-008 Large Workspace Safeguards: run `34208819342` — SUCCESS.
- Normal implementation merge / accepted implementation main: `5dae18f48f7519a7d67fbdf56ccbdb3ef1a8ae9d`.
- Exact implementation-main P10-001 Unity Project Detection: run `34209538818` — SUCCESS.
- Exact implementation-main Windows CI: run `34209538838` — SUCCESS.
- Exact implementation-main P06-007 Workspace Search: run `34209538858` — SUCCESS.
- Exact implementation-main P06-008 Large Workspace Safeguards: run `34209538774` — SUCCESS.
- Initial analyzer `CA1861` was repaired without suppression by reusing static line separators; all repaired exact-head and exact-main gates are green.
- Integrated evidence: `evidence/phases/P10/P10_001_INTEGRATED_RECONCILIATION_2026-09-08.md`.
- No owner-only evidence is required or added. P10 remains `IN_PROGRESS`; P10-004 through P10-013 remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P11+ remain prohibited; `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `VERIFIED_FINAL_COMPLETE=false`.

"""
current = replace_once(
    current,
    "## P10-002 integration provenance\n",
    p10_001_current + "## P10-002 integration provenance\n",
    "CURRENT_PHASE P10-001 provenance insertion",
)
current_path.write_bytes(current.encode("utf-8"))

evidence = f"""# FCCD-P10-001 — Integrated Reconciliation Evidence

Date: 2026-09-08
Task: `FCCD-P10-001 — Unity project/version detector`
Classification: CLOUD / HOSTED-WINDOWS / INTEGRATED
Canonical task result: CLOSED after normal implementation integration, exact-main validation, and this reconciliation.

## Live-state recovery

Canonical `CURRENT_PHASE=P10`; the supplied P19 slot hint is future and non-authoritative. The implementation was already normally merged while the canonical P10-001 row remained PENDING. A fresh convergence sweep found no open PR, no P10-004 branch/claim, and no competing P10-001 reconciliation branch, so stale integrated-state reconciliation had priority over new implementation.

Pre-reconciliation canonical main: `{EXPECTED_MAIN}`.

## Implementation and repair

- Recovered legitimate stale detector work from `worker/fccd-p10-001-unity-project-detector` onto then-exact current main rather than duplicating it.
- Recovery/implementation PR: #237 — `P10-001: integrate recovered Unity project/version detector`.
- Branch: `recovery/fccd-p10-001-unity-project-detector`.
- Exact accepted implementation candidate: `4fb5fbbc0d106230a48d5f1d8922671ccc62bb9d`.
- Adds read-only Unity project marker/version detection and canonical `ProjectSettings/ProjectVersion.txt` parsing.
- Covers complete/incomplete/missing/non-Unity projects, missing roots, invalid input, cancellation, Unicode/Arabic/space-containing paths, deterministic fixtures, and locked dependencies.
- Does not launch Unity, mutate project bytes, or claim later P10 process/log/compile/test/build ownership.
- The first recovery CI exposed analyzer `CA1861`; the defect was repaired by reusing static line separators rather than adding a suppression or weakening analyzers.

## Exact implementation-head validation

On `4fb5fbbc0d106230a48d5f1d8922671ccc62bb9d`:

- P10-001 Unity Project Detection `34208819369` — SUCCESS.
- Windows CI `34208819312` — SUCCESS.
- P06-007 Workspace Search `34208819367` — SUCCESS.
- P06-008 Large Workspace Safeguards `34208819342` — SUCCESS.

## Normal implementation integration

PR #237 was normally merged as `{EXPECTED_MAIN}`, preserving accepted implementation head `4fb5fbbc0d106230a48d5f1d8922671ccc62bb9d` as a merge parent. No squash, rebase, force-push, or fabricated evidence is claimed.

## Exact implementation-main validation

On exact canonical main `{EXPECTED_MAIN}`:

- P10-001 Unity Project Detection `34209538818` — SUCCESS.
- Windows CI `34209538838` — SUCCESS.
- P06-007 Workspace Search `34209538858` — SUCCESS.
- P06-008 Large Workspace Safeguards `34209538774` — SUCCESS.

All four exact-main checks completed SUCCESS before this canonical reconciliation was created.

## Documentation repair

The reconciliation corrects the P10-001 row from PENDING to CLOSED and records the accepted implementation/main evidence. It also repairs stale P10-002 historical prose that still described P10-003 and P10-001 as currently PENDING, and refreshes the ledger's stale current-next-action paragraph from the already-closed P09 transition to the active P10 sequence. No product implementation outside P10-001 is changed.

## Owner-last classification

P10-001 has no genuine owner-machine/manual/provider/Unity-runtime acceptance requirement. Its project/version detection semantics are deterministic and fully hosted-Windows verifiable. No owner queue item is added. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item and remains QUEUED.

## Reconciliation boundary

This reconciliation closes only `FCCD-P10-001`. P10 remains `IN_PROGRESS`; P10-002 and P10-003 remain CLOSED; P10-004 through P10-013 remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P11 and later phases remain prohibited; `VERIFIED_FINAL_COMPLETE=false`.

The reconciliation candidate itself must pass exact-head CI, be normally merged, and the resulting exact canonical main must remain green before this task closure is treated as the durable endpoint.
"""
evidence_path = Path("evidence/phases/P10/P10_001_INTEGRATED_RECONCILIATION_2026-09-08.md")
if evidence_path.exists():
    raise SystemExit("P10-001 reconciliation evidence unexpectedly already exists")
evidence_path.write_bytes(evidence.encode("utf-8"))
