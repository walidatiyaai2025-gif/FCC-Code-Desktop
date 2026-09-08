from pathlib import Path

current = Path('CURRENT_PHASE.md')
ledger = Path('docs/TASK_LEDGER.md')
evidence = Path('evidence/phases/P09/P09_003_INTEGRATED_RECONCILIATION_2026-09-08.md')
workflow = Path('.github/workflows/p09-003-reconciliation-helper.yml')
self_path = Path('tools/reconciliation/reconcile-p09-003-integrated.py')

cur = current.read_text(encoding='utf-8')
led = ledger.read_text(encoding='utf-8')

cur_pending = '- `FCCD-P09-003` — Structured invocation/result contracts — PENDING.'
cur_closed = '- `FCCD-P09-003` — Structured invocation/result contracts — CLOSED.'
led_pending = '| FCCD-P09-003 | Structured invocation/result contracts | PENDING |'
led_closed = '| FCCD-P09-003 | Structured invocation/result contracts | CLOSED |'

if cur.count(cur_pending) != 1 or cur.count(cur_closed) != 0:
    raise SystemExit('CURRENT_PHASE P09-003 state guard failed')
if led.count(led_pending) != 1 or led.count(led_closed) != 0:
    raise SystemExit('TASK_LEDGER P09-003 state guard failed')

cur = cur.replace(cur_pending, cur_closed, 1)
led = led.replace(led_pending, led_closed, 1)

cur_anchor = '## P08 cloud task inventory\n'
cur_provenance = '''## P09-003 integration provenance

- Task: `FCCD-P09-003 — Structured invocation/result contracts` — `CLOSED` in this reconciliation candidate.
- Implementation PR: #218 (`worker/fccd-p09-003-structured-invocation-result-contracts`).
- Exact accepted implementation candidate: `f50233efb761e9144d5e229a9aeab1710852a532`.
- Exact implementation-head Windows CI: run `34176981924` — SUCCESS.
- Exact implementation-head P06-007 Workspace Search: run `34176981927` — SUCCESS.
- Exact implementation-head P06-008 Large Workspace Safeguards: run `34176981931` — SUCCESS.
- Normal implementation merge / accepted implementation main: `0db4d89ecec7ab6a489f7af1ba51634815987997`.
- Exact implementation-main Windows CI: run `34177513172` — SUCCESS.
- Exact implementation-main P06-007 Workspace Search: run `34177513192` — SUCCESS.
- Exact implementation-main P06-008 Large Workspace Safeguards: run `34177513156` — SUCCESS.
- Pre-reconciliation canonical main: `3c0bf0520a78df54c9fcb62cf44199615d0ee5ec`; its Git tree `6959578bf463877f42ccf54e8118fdfa52466d02` is byte-identical to the accepted implementation merge after immediate repair/removal of a transient reconciliation-bootstrap artifact.
- Exact pre-reconciliation current-main Windows CI: run `34178135579` — SUCCESS.
- Exact pre-reconciliation current-main P06-007 Workspace Search: run `34178135582` — SUCCESS.
- Exact pre-reconciliation current-main P06-008 Large Workspace Safeguards: run `34178135615` — SUCCESS.
- Integrated evidence: `evidence/phases/P09/P09_003_INTEGRATED_RECONCILIATION_2026-09-08.md`.
- No owner-only evidence is required or added. P09 remains `IN_PROGRESS`; `FCCD-P09-004` through `FCCD-P09-008` remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P10 and later implementation remain prohibited; `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `VERIFIED_FINAL_COMPLETE=false`.

'''
if cur.count(cur_anchor) != 1 or '## P09-003 integration provenance' in cur:
    raise SystemExit('CURRENT_PHASE provenance anchor guard failed')
cur = cur.replace(cur_anchor, cur_provenance + cur_anchor, 1)

led_anchor = '## P10 — Unity first-class adapter\n'
led_provenance = '''`FCCD-P09-003` is CLOSED from the provider-neutral structured invocation/result contracts integrated in PR #218. Exact accepted implementation candidate `f50233efb761e9144d5e229a9aeab1710852a532` passed Windows CI `34176981924`, P06-007 Workspace Search `34176981927`, and P06-008 Large Workspace Safeguards `34176981931`. PR #218 was normally merged as `0db4d89ecec7ab6a489f7af1ba51634815987997`; that exact implementation main passed Windows CI `34177513172`, Workspace Search `34177513192`, and Large Workspace Safeguards `34177513156`. Pre-reconciliation canonical main `3c0bf0520a78df54c9fcb62cf44199615d0ee5ec` has the same Git tree `6959578bf463877f42ccf54e8118fdfa52466d02` as the accepted implementation merge after immediate removal of a transient reconciliation-bootstrap artifact, and passed Windows CI `34178135579`, Workspace Search `34178135582`, and Large Workspace Safeguards `34178135615`. Coverage includes immutable ordered hostile argv, fully-qualified working-directory semantics, immutable case-insensitive environment overlays with malformed/NUL/duplicate rejection, and typed terminal result/status events without shell-command concatenation. Task evidence: `evidence/phases/P09/P09_003_INTEGRATED_RECONCILIATION_2026-09-08.md`. No owner-only evidence is required. P09 remains `IN_PROGRESS`; P09-004 through P09-008 remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P10 and later implementation remain prohibited; `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item; `VERIFIED_FINAL_COMPLETE=false`.

'''
if led.count(led_anchor) != 1 or '`FCCD-P09-003` is CLOSED from the provider-neutral structured invocation/result contracts' in led:
    raise SystemExit('TASK_LEDGER provenance anchor guard failed')
led = led.replace(led_anchor, led_provenance + led_anchor, 1)

# Fail closed if this reconciliation accidentally changes later task states.
for task in range(4, 9):
    needle = f'| FCCD-P09-{task:03d} |'
    if needle not in led:
        raise SystemExit(f'missing expected P09-{task:03d} ledger row')

current.write_text(cur, encoding='utf-8', newline='')
ledger.write_text(led, encoding='utf-8', newline='')
evidence.parent.mkdir(parents=True, exist_ok=True)
evidence.write_text('''# FCCD-P09-003 — Integrated Reconciliation Evidence

Date: 2026-09-08
Task: `FCCD-P09-003 — Structured invocation/result contracts`
Classification: CLOUD / HOSTED-WINDOWS / INTEGRATED
Canonical task result: CLOSED after normal implementation integration and exact-main validation.

## Live-state recovery

The convergence request carried a future `P16` hint, but canonical `CURRENT_PHASE=P09`. There were no open pull requests and P09-003 had legitimate implementation already normally merged in PR #218 while its canonical task row remained PENDING. Recovery/reconciliation therefore took priority over any new P09 claim and all P10/P16 implementation.

Pre-reconciliation canonical main: `3c0bf0520a78df54c9fcb62cf44199615d0ee5ec`.

## Implementation

- Implementation PR: #218 — `P09-003: structured invocation and result contracts`.
- Branch: `worker/fccd-p09-003-structured-invocation-result-contracts`.
- Exact accepted implementation candidate: `f50233efb761e9144d5e229a9aeab1710852a532`.
- Production contract additions are in `src/FCCCodeDesktop.Tools/ExternalToolContracts.cs` with focused tests in `tests/FCCCodeDesktop.UnitTests/StructuredToolContractsTests.cs`.
- `StructuredToolInvocation` snapshots ordered arguments without shell-string concatenation, preserves spaces/quotes/metacharacters/empty values/Unicode, requires a fully-qualified working directory, and snapshots an immutable case-insensitive environment overlay.
- Fail-closed validation rejects null/NUL arguments, malformed operation tokens, unsafe or ambiguous environment names/values, case-insensitive duplicate environment keys, and relative working directories without touching the filesystem.
- Provider-neutral `ToolResultStatus`, extensible `ToolResult`, and terminal `ToolResultEvent` provide typed completion semantics without coupling core orchestration to a provider.
- Scope does not take P09-004 locking, P09-005 artifact manifests, P09-006 diagnostics, P09-007 CLI/process adapter implementation, or P09-008 protocol seams.

## Exact implementation-head validation

On `f50233efb761e9144d5e229a9aeab1710852a532`:

- Windows CI `34176981924` — SUCCESS.
- P06-007 Workspace Search `34176981927` — SUCCESS.
- P06-008 Large Workspace Safeguards `34176981931` — SUCCESS.

## Normal implementation integration

PR #218 was normally merged as `0db4d89ecec7ab6a489f7af1ba51634815987997`, preserving the tested implementation head as a merge parent. No squash, rebase, force-push, or fabricated evidence is claimed.

## Exact implementation-main validation

On `0db4d89ecec7ab6a489f7af1ba51634815987997`:

- Windows CI `34177513172` — SUCCESS.
- P06-007 Workspace Search `34177513192` — SUCCESS.
- P06-008 Large Workspace Safeguards `34177513156` — SUCCESS.

## Current-main repair equivalence and validation

A transient one-character reconciliation-bootstrap artifact was accidentally committed after the implementation merge and immediately removed with a normal repair commit. Current pre-reconciliation main `3c0bf0520a78df54c9fcb62cf44199615d0ee5ec` therefore has the exact same Git tree `6959578bf463877f42ccf54e8118fdfa52466d02` as the accepted implementation merge; no product/configuration/document bytes from that transient artifact remain.

The repaired exact current main independently passed:

- Windows CI `34178135579` — SUCCESS.
- P06-007 Workspace Search `34178135582` — SUCCESS.
- P06-008 Large Workspace Safeguards `34178135615` — SUCCESS.

## Owner-last classification

P09-003 has no genuine owner-machine/manual/provider/Unity/Blender acceptance requirement. Its acceptance is fully cloud/hosted-Windows verifiable, so no queue item is added. `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item and is unchanged.

## Reconciliation boundary

This reconciliation closes only `FCCD-P09-003`. P09 remains `IN_PROGRESS`; P09-004 through P09-008 remain PENDING; `PHASE_EXIT_GATE=NOT_RUN`; P10 and later phases remain prohibited until sequential P09 convergence completes; `VERIFIED_FINAL_COMPLETE=false`.

The reconciliation PR must itself pass exact-head CI, be normally merged, and the resulting exact canonical main must remain green before this task closure is treated as the durable endpoint.
''', encoding='utf-8', newline='')

# The helper and its one-shot workflow must not survive in the final candidate.
if workflow.exists():
    workflow.unlink()
if self_path.exists():
    self_path.unlink()
