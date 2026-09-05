# FCCD-P05-003 — Integrated Reconciliation

**Task:** `FCCD-P05-003 — Composer/attachments/context`  
**Phase:** P05  
**Evidence class:** `SELF_TEST_ONLY / CLOUD + CANONICAL INTEGRATION PROVENANCE`  
**Status:** `CLOSED` after this reconciliation is normally merged and the exact resulting `main` remains green

## Implementation

The production conversation surface now has a bounded composer state with immutable submission snapshots, monotonic submission identity, visible validation, metadata-only file attachments, typed context references, removable attachment/context chips, multiline input, and `Ctrl+Enter` submission. Attachment validation enforces existence, count, per-file size and case-insensitive deduplication without reading attachment contents. Context references are typed and bounded independently.

Submission currently projects the accepted user text into the conversation state only. It does not invent runtime dispatch, session lifecycle, task lifecycle, provider payload semantics, or file-content loading; those remain owned by later P05/P06 tasks.

Permanent validation is registered in the canonical Windows baseline through:

```powershell
.\tools\ui\validate-conversation-composer.ps1 -RunFixtures -RequireRuntime
```

The validator covers static contract enforcement, negative/recovery fixtures, caps and deduplication, missing-handler fail-closed behavior, immutable submission identity/acknowledgement, production WPF composition, semantic dark/light parity, accepted-draft clearing, user-message projection, programmatic length rejection, and explicit prevention of file-content/process/runtime coupling.

## Repair history

The implementation was repaired through cloud CI rather than deferred:

- run #183 exposed missing `System.IO` imports in `ComposerState.cs`;
- run #184 exposed the same explicit dependency in `ConversationComposer.xaml.cs`;
- run #185 passed the production Release build with 0 warnings/0 errors, unit 24/24 and integration 37/37, then exposed a missing `System.IO` import only inside the disposable generated WPF fixture;
- the fixture was repaired without weakening any production behavior or assertion.

No failed CI, build defect, or fixture defect was classified as owner-only.

## Exact implementation candidate

- Candidate: `3cbfc00a79ce7f7826bb442939c9c0d29ae8036e`
- PR: #124 — `P05-003: composer attachments and context foundation`
- Exact PR-head Windows CI: run `33944648152` / run #186 — `SUCCESS`
- Release build: `0` warnings / `0` errors
- Unit tests: `24/24` PASS
- Integration tests: `37/37` PASS
- Static conversation-composer validator: PASS
- Conversation-composer negative/recovery fixtures: PASS
- Executable Windows/WPF conversation-composer happy/negative/recovery fixture: PASS
- Complete permanent Windows baseline: PASS

## Canonical integration

PR #124 was normally merged without squash/rebase as:

`f00a579358405e8197a5b78ecbe64501743c2101`

Exact post-merge canonical-main Windows CI:

- run `33944933157` / run #187 — `SUCCESS`

That run revalidated the production Release build, all unit/integration lanes, owner-last governance, all earlier permanent validators, the P05-003 static/negative/recovery suite, the executable WPF composer fixture, and the complete Windows baseline on the exact merge SHA before ledger closure.

## Owner-last boundary

This evidence does **not** claim real FCC/provider execution and creates no new owner-only obligation. The existing `OWNER-P04-008-REAL-TARGET` queue item remains `QUEUED`, genuine, unresolved, and `releaseBlocking=true`. `FCCD-P04-008` remains unresolved and the P04 exit gate remains `NOT_RUN`.

## Reconciliation

When this reconciliation is normally merged and the exact resulting canonical `main` Windows CI remains green:

- `FCCD-P05-003` is `CLOSED`;
- P05 remains `IN_PROGRESS` and `PHASE_EXIT_GATE=NOT_RUN`;
- P05-004 through P05-008 remain unresolved cloud tasks;
- the next dependency-valid P05 task is `FCCD-P05-004 — Session create/history/resume`, subject to a fresh live claim/recovery check.
