# FCC Code Desktop — Current Phase

This file is the fastest canonical resume checkpoint. It must be updated only when durable live project state changes.

```text
PROJECT_ID: FCC_CODE_DESKTOP
TARGET_RELEASE: 1.0.0
CURRENT_PHASE: P05
CURRENT_PHASE_NAME: Conversation + session + task experience
CURRENT_PHASE_STATE: IN_PROGRESS
NEXT_PHASE: P06
PHASE_EXIT_GATE: NOT_RUN
KNOWN_PHASE_BLOCKERS: 0
KNOWN_RELEASE_BLOCKERS: 1
VERIFIED_FINAL_COMPLETE: false
OWNER_LAST_MODE: ACTIVE
DEFERRED_OWNER_ACCEPTANCE_COUNT: 1
DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET
DEFERRED_PHASE_GATES: P04=NOT_RUN
LAST_RECONCILED: 2026-09-05
```

## Active scheduling rule

`CURRENT_PHASE` now means the single phase authorized for **cloud-actionable implementation** while the owner-authorized scheduling amendment in `docs/OWNER_LAST_EXECUTION_POLICY.md` is active. Workers must read that policy and `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` before selecting work.

P05 is the sole legal cloud implementation phase. `FCCD-P05-001 — Streaming chat rendering` and `FCCD-P05-002 — Structured tool activity timeline` have been normally integrated and exact-main verified; subject to the normal live recovery/ownership check in `docs/WORKER_PROTOCOL.md`, the earliest unclaimed mandatory task is now `FCCD-P05-003 — Composer/attachments/context`.

This is **not** a P04 closure and does not weaken P04 acceptance. `FCCD-P04-008 — Runtime contract suite` remains unresolved in `docs/TASK_LEDGER.md`; the P04 exit gate remains `NOT_RUN`; no P04 `CLOSURE.md` PASS is claimed by this scheduling transition. Its fresh owner-Windows/provider `REAL_TARGET` obligation is durably queued as `OWNER-P04-008-REAL-TARGET`, remains `releaseBlocking=true`, and must later be genuinely executed, reviewed, integrated, and reconciled.

The pre-owner-last P04 handoff correctly prohibited P05 under the earlier phase-lock model. That scheduling statement is superseded only by the owner-authorized owner-last policy and the activation evidence at `evidence/governance/OWNER_LAST_P05_CLOUD_ACTIVATION_2026-09-05.md`. All P04 functional/acceptance requirements remain unchanged.

## Owner-last invariants

- Exactly one cloud implementation phase remains active: P05.
- Earlier unresolved work is permitted only when every such task is one-to-one represented by a valid `QUEUED`, environment-bound, `releaseBlocking=true` entry in `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md`.
- Code defects, failed CI, missing tests/implementation, security/data-integrity defects, and repairable repository problems are never deferrable.
- A queued source task is not `CLOSED`; its phase exit gate is not converted to `PASS`.
- Any later-discovered regression in P04 or any failed final-owner run regains repair priority immediately.
- `KNOWN_RELEASE_BLOCKERS` must never be lower than the number of unresolved release-blocking owner queue items.
- P22 cannot become the current cloud implementation phase while any required owner queue item remains `QUEUED`.
- `VERIFIED_FINAL_COMPLETE` remains false until canonical P22 closure after every mandatory task/gate/acceptance row and every owner queue obligation genuinely passes on the required exact candidate.

## Deferred owner acceptance

### OWNER-P04-008-REAL-TARGET

- Source task: `FCCD-P04-008`.
- Source phase: P04.
- Source task state: unresolved / not CLOSED.
- P04 exit gate: `NOT_RUN`.
- Classification: `REAL_TARGET`.
- Reason: requires the owner's installed Windows `fcc-claude`/FCC/provider environment; GitHub-hosted CI proves only deterministic `SELF_TEST_ONLY` mechanics.
- Cloud implementation evidence: `evidence/phases/P04/P04_008_CLOUD_COMPLETE_TARGET_VALIDATION_REQUIRED_2026-09-04.md`.
- Canonical queue: `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md`.
- Final execution runner: `tools/final-acceptance/run-final-owner-acceptance.ps1`.
- Release status: blocking until genuine PASS evidence is integrated and reconciled.

## P05 cloud task inventory

- `FCCD-P05-001` — Streaming chat rendering — CLOSED.
- `FCCD-P05-002` — Structured tool activity timeline — CLOSED.
- `FCCD-P05-003` — Composer/attachments/context — PENDING.
- `FCCD-P05-004` — Session create/history/resume — PENDING.
- `FCCD-P05-005` — Explicit task state machine — PENDING.
- `FCCD-P05-006` — Stop/cancel/retry UX — PENDING.
- `FCCD-P05-007` — Markdown/code/diff content rendering — PENDING.
- `FCCD-P05-008` — Conversation virtualization/performance — PENDING.

## P05-001 integration provenance

- Exact implementation candidate: `b261a511222dfa79b77172b0fd390345b6af10c6`.
- PR #120 exact-head Windows CI: run `33940749591` / run #175 — SUCCESS.
- Normal merge commit: `994c2cb91fbd22bd622b27cfb1041774eaafafd0`.
- Exact post-merge canonical-main Windows CI: run `33941044692` / run #176 — SUCCESS.
- Integrated evidence: `evidence/phases/P05/P05_001_INTEGRATED_RECONCILIATION_2026-09-05.md`.
- Evidence class remains cloud/self-test for UI mechanics; no provider-backed P04 acceptance is implied.

## P05-002 integration provenance

- Exact implementation candidate: `d17643560b2ec8e36f24b052ab0ee322a6b0a4c5`.
- PR #122 exact-head Windows CI: run `33942370655` / run #179 — SUCCESS.
- Normal merge commit: `94d639ba0d4f2afe4e28054152b15df04e33f76a`.
- Exact post-merge canonical-main Windows CI: run `33942655208` / run #180 — SUCCESS.
- Integrated evidence: `evidence/phases/P05/P05_002_INTEGRATED_RECONCILIATION_2026-09-05.md`.
- Evidence class remains cloud/self-test for structured timeline mechanics; no provider-backed P04 acceptance is implied.

## Activation provenance

- Owner-last bootstrap base before PR #117: `2e76d7f6a44bf120e16efab21a01df9784cd8380`.
- Owner-last mechanism PR #117 candidate: `cf2def8d93a5cbaf161cd836985d1e6c9ed57fce`.
- Owner-last mechanism normal merge: `cfe43774b7c43605e119cb6b94f34b29694612f2`.
- Exact post-merge canonical-main Windows CI: run `33937019700` / run #167 — SUCCESS.
- Open PRs at activation recovery: none.
- Open issues at activation recovery: none.
- P05 branch/claim found before this governance repair: none.

## Next legitimate action after this reconciliation is integrated

Re-fetch live main, queue, open PRs/branches/issues, and CI. If no Priority 1–4 repair/recovery/integration work exists, select exactly one unclaimed P05 task beginning with the earliest dependency-valid row, `FCCD-P05-003 — Composer/attachments/context`, and execute it normally. Do not execute queued owner acceptance until the final-owner lane is intentionally reached, do not treat that deferral as release PASS, and do not advance to P06 until P05's normal cloud implementation and gate requirements are genuinely satisfied.
