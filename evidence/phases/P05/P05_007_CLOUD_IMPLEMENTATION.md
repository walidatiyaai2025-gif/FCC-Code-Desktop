# FCCD-P05-007 — Cloud implementation evidence

**Task:** `FCCD-P05-007 — Markdown/code/diff content rendering`  
**Phase:** P05  
**Evidence class:** `SELF_TEST_ONLY / CLOUD`  
**Status:** `IMPLEMENTED — exact PR-head CI, normal merge, exact-main verification, and integrated reconciliation are still required before CLOSED`

## Recovery provenance

A legitimate task-local implementation was recovered from stale mixed P05 history. The original isolated P05-007 commit was `9d50117e1ba8770d5af9863009216ac23f3cf2c1` (`P05-007: render markdown code and diff content safely`), immediately after the old P05-006 commit and before the old P05-008 commit. Its one-commit diff touched only:

- `ConversationContentParser.cs`;
- `ConversationSurface.xaml`;
- `StreamingConversationState.cs`.

Live main had already integrated and reconciled the independently recovered P05-005 and P05-006 work. The three P05-007 target files had not diverged in a way that conflicted with the isolated P05-007 delta. The P05-007 production semantics were therefore reapplied onto exact live main `6732ed69207260d8372b2f581480dc03ea59d6b7` on the dedicated recovery branch `worker/fccd-p05-007-markdown-code-diff-safe`, without importing the later stale P05-008 implementation.

## Implemented cloud scope

- typed `ConversationContentBlockKind` / `ConversationContentBlock` presentation model;
- deterministic native Markdown subset for paragraphs, ATX headings, and unordered bullets;
- fenced code blocks with bounded language identifiers;
- explicit `diff`/`patch` fenced rendering with header/add/remove/context classification;
- streaming assistant text stays raw and unparsed until completion;
- completed user/assistant/persisted messages expose parsed `ContentBlocks`;
- exact raw/durable message text remains unchanged by rendering;
- native WPF `TextBlock`, `Border`, `ScrollViewer`, and semantic theme resources only;
- no HTML/browser/script execution in the content-rendering path;
- presentation parsing bounded to 1 MiB with a visible truncation notice, without truncating durable message data.

## Permanent cloud validation

Canonical gate:

```powershell
.\tools\ui\validate-conversation-content-rendering.ps1 -RunFixtures -RequireRuntime
```

The gate includes static/negative validation plus an executable Windows/WPF fixture. It verifies:

- paragraph, heading, bullet, code, diff header/add/remove/context projection;
- `+++`/`---` diff headers are not misclassified as add/remove rows;
- safe handling of an unclosed code fence;
- bounded language identifiers;
- 1 MiB rendering bound and visible truncation notice;
- streaming assistant deltas do not trigger structured reparsing;
- completion creates structured blocks while preserving exact raw text;
- persisted completed Markdown/code content restores into structured blocks;
- production `MainWindow` / `ConversationSurface` / `StreamingConversationState` composition;
- semantic resources and absence of executable HTML/browser/process/provider-payload coupling;
- negative fixtures for removed content binding, removed completion parse, removed streaming suppression, removed diff classification, removed rendering bound, and hard-coded colors.

The Windows CI workflow now invokes this validator as a dedicated fail-closed P05-007 step. `tools/ci/validate-windows-ci.ps1` requires that exact invocation and has a negative fixture that rejects its removal.

## Ownership boundaries

- P05-008 retains ownership of conversation virtualization, retained visible-window limits, long-history performance, and load closure.
- P06/P07 retain file/editor/Git mutation and diff-review workflows; P05-007 diff content is presentation-only.
- Provider/runtime contracts remain under P04/P05 runtime orchestration. The renderer consumes normalized message text only.

## Owner-last boundary

No new owner/manual/REAL_TARGET obligation is introduced by these cloud rendering mechanics.

`OWNER-P04-008-REAL-TARGET` remains the sole established queued owner-only requirement, remains `QUEUED`, remains `releaseBlocking=true`, and remains source-linked to unresolved `FCCD-P04-008` with the P04 exit gate `NOT_RUN`.

This evidence does **not** claim a real provider task, real provider formatting behavior, P05 exit-gate PASS, P04 closure, or release eligibility.

## Closure rule

Do not mark `FCCD-P05-007` `CLOSED` from this file alone. Closure requires:

1. exact implementation PR-head Windows CI SUCCESS;
2. normal merge into canonical `main`;
3. exact merge-SHA Windows CI SUCCESS;
4. canonical reconciliation of `CURRENT_PHASE.md`, `docs/TASK_LEDGER.md`, and integrated task evidence;
5. reconciliation PR CI, normal merge, and exact resulting main CI SUCCESS.
