# FCCD-P05-002 — Integrated Reconciliation

**Task:** `FCCD-P05-002 — Structured tool activity timeline`  
**Phase:** P05  
**Evidence class:** `SELF_TEST_ONLY / CLOUD + CANONICAL INTEGRATION PROVENANCE`  
**Status:** `CLOSED` after canonical reconciliation is merged and exact resulting `main` remains green

## Implementation

The production conversation state now consumes normalized `ToolStarted`, `ToolProgress`, and `ToolResult` runtime events and projects them into structured tool activity rows. Normal correlation updates one activity row; result receipt is represented neutrally without inventing success semantics. Unmatched progress/results remain visible rather than being silently discarded, correlation reuse preserves prior history, and the presentation never reads raw `PayloadJson`.

`ConversationSurface` renders the structured activity in a bounded semantic-theme timeline and keeps the newest activity visible through the existing WPF dispatcher path. P05-001 assistant streaming remains independent and ordered through the same contiguous runtime sequence guard.

Permanent validation is registered in the canonical Windows baseline through:

```powershell
.\tools\ui\validate-tool-activity-timeline.ps1 -RunFixtures -RequireRuntime
```

The validator covers static contract enforcement, negative/recovery fixtures, correlated start/progress/result behavior, unmatched-event preservation, correlation reuse, assistant/tool separation, raw-payload non-rendering, production WPF composition, dark/light theme parity, and reset recovery.

## Exact implementation candidate

- Candidate: `d17643560b2ec8e36f24b052ab0ee322a6b0a4c5`
- PR: #122 — `P05-002: structured tool activity timeline`
- Exact PR-head Windows CI: run `33942370655` / run #179 — `SUCCESS`
- Release build: `0` warnings / `0` errors
- Unit tests: `24/24` PASS
- Integration tests: `37/37` PASS
- Static tool-activity timeline validator: PASS
- Tool-activity negative/recovery fixtures: PASS
- Executable Windows/WPF tool-activity happy/negative/recovery fixture: PASS
- Complete permanent Windows baseline: PASS

## Canonical integration

PR #122 was normally merged without squash/rebase as:

`94d639ba0d4f2afe4e28054152b15df04e33f76a`

Exact post-merge canonical-main Windows CI:

- run `33942655208` / run #180 — `SUCCESS`

That run revalidated the same production build/test/validator baseline on the exact merge SHA before task reconciliation.

## Owner-last boundary

This evidence does **not** claim real FCC/provider execution and does not create a new owner-only obligation. The existing `OWNER-P04-008-REAL-TARGET` queue item remains `QUEUED`, genuine, unresolved, and release-blocking. P04 remains acceptance-unresolved with its exit gate `NOT_RUN`.

## Reconciliation

When this reconciliation commit is normally merged and the exact resulting canonical `main` Windows CI is green:

- `FCCD-P05-002` is `CLOSED`;
- P05 remains `IN_PROGRESS` and `PHASE_EXIT_GATE=NOT_RUN`;
- P05-003 through P05-008 remain unresolved cloud tasks;
- the next dependency-valid P05 task is `FCCD-P05-003 — Composer/attachments/context`, subject to a fresh live claim/recovery check.
