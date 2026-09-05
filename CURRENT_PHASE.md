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
KNOWN_RELEASE_BLOCKERS: 2
VERIFIED_FINAL_COMPLETE: false
OWNER_LAST_MODE: ACTIVE
DEFERRED_OWNER_ACCEPTANCE_COUNT: 2
DEFERRED_OWNER_ACCEPTANCE_ITEMS: OWNER-P04-008-REAL-TARGET;OWNER-P05-EXIT-REAL-TARGET
DEFERRED_PHASE_GATES: P04=NOT_RUN;P05=NOT_RUN
LAST_RECONCILED: 2026-09-05
```

## Active scheduling rule

`CURRENT_PHASE` now means the single phase authorized for **cloud-actionable implementation** while the owner-authorized scheduling amendment in `docs/OWNER_LAST_EXECUTION_POLICY.md` is active. Workers must read that policy and `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` before selecting work.

P05 remains the sole legal cloud implementation/convergence phase. `FCCD-P05-001` through `FCCD-P05-008` are normally integrated and exact-main verified. There is no remaining unclaimed P05 implementation task. P05 phase-exit cloud convergence is now prepared: the strongest deterministic cloud evidence is integrated on the convergence candidate, exact canonical-main Windows CI at baseline `47fabb4aa9ea7e29d7526374ed6120d76c4e16d4` succeeded in run `33986684958`, and a fail-closed exact-head owner runner is tracked at `tools/ui/run-p05-phase-exit-owner-validation.ps1`.

The P05 exit criterion still requires genuine owner Windows/FCC/provider interaction: a real task in the application conversation surface, structured execution, stop/retry, close/reopen, and durable session resume. That requirement is queued as `OWNER-P05-EXIT-REAL-TARGET`, remains `releaseBlocking=true`, and does **not** convert `PHASE_EXIT_GATE` to `PASS`. P05 therefore remains `IN_PROGRESS / NOT_RUN` until a separate legal transition is integrated under owner-last governance.

This is **not** a P04 closure and does not weaken P04 acceptance. `FCCD-P04-008 — Runtime contract suite` remains unresolved in `docs/TASK_LEDGER.md`; the P04 exit gate remains `NOT_RUN`; no P04 `CLOSURE.md` PASS is claimed by this scheduling transition. Its fresh owner-Windows/provider `REAL_TARGET` obligation is durably queued as `OWNER-P04-008-REAL-TARGET`, remains `releaseBlocking=true`, and must later be genuinely executed, reviewed, integrated, and reconciled.

The pre-owner-last P04 handoff correctly prohibited P05 under the earlier phase-lock model. That scheduling statement is superseded only by the owner-authorized owner-last policy and the activation evidence at `evidence/governance/OWNER_LAST_P05_CLOUD_ACTIVATION_2026-09-05.md`. All P04 functional/acceptance requirements remain unchanged.

## Owner-last invariants

- Exactly one cloud implementation/convergence phase remains active: P05.
- Earlier unresolved task work is permitted only when every such task is one-to-one represented by a valid `QUEUED`, environment-bound, `releaseBlocking=true` entry in `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md`.
- A phase-exit requirement may be queued only when all cloud-actionable implementation/tests/CI are complete, the remaining evidence is genuinely environment-bound, and the phase gate remains truthfully unresolved rather than being represented as PASS.
- Code defects, failed CI, missing tests/implementation, security/data-integrity defects, and repairable repository problems are never deferrable.
- A queued source task is not `CLOSED`; a queued phase-gate requirement is not converted to `PASS`.
- Any later-discovered regression in P04/P05 or any failed final-owner run regains repair priority immediately.
- `KNOWN_RELEASE_BLOCKERS` must never be lower than the number of unresolved release-blocking owner queue items.
- P22 cannot become the current cloud implementation phase while any required owner queue item remains `QUEUED`.
- `VERIFIED_FINAL_COMPLETE` remains false until canonical P22 closure after every mandatory task/gate/acceptance row and every owner queue obligation genuinely passes on the required exact candidate.

## Deferred owner acceptance

### OWNER-P04-008-REAL-TARGET

- Source kind: task.
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

### OWNER-P05-EXIT-REAL-TARGET

- Source kind: phase gate.
- Source requirement: `P05_EXIT_GATE`.
- Source phase: P05.
- P05 task rows: 8/8 CLOSED.
- P05 exit gate: `NOT_RUN`.
- Classification: `REAL_TARGET`.
- Reason: requires the owner Windows FCC Code Desktop application plus installed `fcc-claude`/FCC/provider environment and an actual close/reopen persistence interaction; GitHub-hosted CI proves only deterministic mechanics.
- Cloud convergence evidence: `evidence/phases/P05/P05_PHASE_EXIT_CLOUD_COMPLETE_OWNER_TARGET_REQUIRED_2026-09-05.md`.
- Tracked owner runner: `tools/ui/run-p05-phase-exit-owner-validation.ps1`.
- Expected evidence: `evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json`.
- Release status: blocking until genuine exact-head PASS evidence is reviewed, integrated and reconciled.

## P05 cloud task inventory

- `FCCD-P05-001` — Streaming chat rendering — CLOSED.
- `FCCD-P05-002` — Structured tool activity timeline — CLOSED.
- `FCCD-P05-003` — Composer/attachments/context — CLOSED.
- `FCCD-P05-004` — Session create/history/resume — CLOSED.
- `FCCD-P05-005` — Explicit task state machine — CLOSED.
- `FCCD-P05-006` — Stop/cancel/retry UX — CLOSED.
- `FCCD-P05-007` — Markdown/code/diff content rendering — CLOSED.
- `FCCD-P05-008` — Conversation virtualization/performance — CLOSED.

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

## P05-003 integration provenance

- Exact implementation candidate: `3cbfc00a79ce7f7826bb442939c9c0d29ae8036e`.
- PR #124 exact-head Windows CI: run `33944648152` / run #186 — SUCCESS.
- Normal merge commit: `f00a579358405e8197a5b78ecbe64501743c2101`.
- Exact post-merge canonical-main Windows CI: run `33944933157` / run #187 — SUCCESS.
- Integrated evidence: `evidence/phases/P05/P05_003_INTEGRATED_RECONCILIATION_2026-09-05.md`.
- Evidence class remains cloud/self-test for composer/attachment/context mechanics; no provider-backed P04 acceptance is implied.

## P05-004 integration provenance

- Exact implementation candidate: `12bb212bc5fc5455045efd4d08c01cb56a62bbb7`.
- PR #126 exact-head Windows CI: run `33948793781` / run #202 — SUCCESS.
- Normal merge commit: `2988eb449570cfcf9fc62d2198fe209c8c9b9371`.
- Exact post-merge canonical-main Windows CI: run `33949094044` / run #203 — SUCCESS.
- Integrated evidence: `evidence/phases/P05/P05_004_INTEGRATED_RECONCILIATION_2026-09-05.md`.
- Evidence class remains cloud/self-test for session persistence/resume mechanics; no provider-backed P04 acceptance is implied.

## P05-005 integration provenance

- Exact implementation candidate: `cb7edc6909235a275949b6e184ceabb2a8340859`.
- PR #132 exact-head Windows CI: run `33953673037` / run #217 — SUCCESS.
- Normal merge commit: `7ee9feab02a5691246452d4e472d110cd420e443`.
- Exact post-merge canonical-main Windows CI: run `33953912542` / run #218 — SUCCESS.
- Integrated evidence: `evidence/phases/P05/P05_005_INTEGRATED_RECONCILIATION_2026-09-05.md`.
- Evidence class remains cloud/self-test for task lifecycle/persistence/cleanup mechanics; no provider-backed P04 acceptance is implied.

## P05-006 integration provenance

- Exact implementation candidate: `7c49d2e6009acb7f1e3dcceec57ad88e690fd34c`.
- PR #134 exact-head Windows CI: run `33955670600` / run #221 — SUCCESS.
- Normal merge commit: `18ecb7e0aa11200043454911c0b994291d296df3`.
- Exact post-merge canonical-main Windows CI: run `33956024415` / run #222 — SUCCESS.
- Integrated evidence: `evidence/phases/P05/P05_006_INTEGRATED_RECONCILIATION_2026-09-05.md`.
- Evidence class remains cloud/self-test for stop/cancel/retry lifecycle and persistence mechanics; no provider-backed P04 acceptance is implied.

## P05-007 integration provenance

- Exact repaired implementation candidate: `903e7276337dd90c029d284dbd1bb386acc44574`.
- PR #136 exact-head Windows CI: run `33982214968` / run #226 — SUCCESS.
- Normal merge commit: `e4a0a401872a36713b1e71113aa91b2dbe56bb9c`.
- Exact post-merge canonical-main Windows CI: run `33982452443` / run #227 — SUCCESS.
- Integrated evidence: `evidence/phases/P05/P05_007_INTEGRATED_RECONCILIATION_2026-09-05.md`.
- Evidence class remains cloud/self-test for native Markdown/code/diff rendering mechanics; no provider-backed P04 acceptance or P05 phase closure is implied.

## P05-008 integration provenance

- Exact repaired implementation candidate: `a81f1ec86e0c05498cfd86ed3cafd91d0fd5b124`.
- PR #138 exact-head Windows CI: run `33985390212` / run #235 — SUCCESS.
- Normal merge commit: `237dad3b69e8b4cc2314dc13351d30136a996e1f`.
- Exact post-merge canonical-main Windows CI: run `33985710844` / run #236 — SUCCESS.
- Integrated evidence: `evidence/phases/P05/P05_008_INTEGRATED_RECONCILIATION_2026-09-05.md`.
- Evidence class remains cloud/self-test for conversation virtualization, progressive parsing, bounded rendering, and viewport/tail-follow behavior; no provider-backed P04 acceptance, P05 phase exit-gate PASS, or release eligibility is implied.

## P05 phase-exit cloud convergence provenance

- Canonical cloud baseline before this convergence unit: `47fabb4aa9ea7e29d7526374ed6120d76c4e16d4`.
- Exact baseline Windows CI: run `33986684958` / run #238 — SUCCESS.
- Cloud convergence evidence: `evidence/phases/P05/P05_PHASE_EXIT_CLOUD_COMPLETE_OWNER_TARGET_REQUIRED_2026-09-05.md`.
- Owner runner: `tools/ui/run-p05-phase-exit-owner-validation.ps1`.
- Queue item: `OWNER-P05-EXIT-REAL-TARGET` — QUEUED / release blocking.
- P05 exit gate remains `NOT_RUN`; no `evidence/phases/P05/CLOSURE.md` PASS is claimed.

## Activation provenance

- Owner-last bootstrap base before PR #117: `2e76d7f6a44bf120e16efab21a01df9784cd8380`.
- Owner-last mechanism PR #117 candidate: `cf2def8d93a5cbaf161cd836985d1e6c9ed57fce`.
- Owner-last mechanism normal merge: `cfe43774b7c43605e119cb6b94f34b29694612f2`.
- Exact post-merge canonical-main Windows CI: run `33937019700` / run #167 — SUCCESS.
- Open PRs at activation recovery: none.
- Open issues at activation recovery: none.
- P05 branch/claim found before this governance repair: none.

## Next legitimate action after this reconciliation is integrated

Re-fetch live main, queue, open PRs/branches/issues, and exact-head CI. If this P05 phase-exit convergence is integrated and green with both owner obligations truthfully queued, the next legal unit is a **separate owner-last P05 → P06 cloud-phase transition**. That transition must preserve `P04=NOT_RUN` and `P05=NOT_RUN`, preserve both release-blocking queue items, keep `VERIFIED_FINAL_COMPLETE=false`, and activate only P06. P07 remains future work behind P06.