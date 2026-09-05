# FCC Code Desktop — Streaming Chat Rendering

**Task:** `FCCD-P05-001 — Streaming chat rendering`  
**Phase:** P05  
**Status:** cloud implementation contract

## Scope

P05-001 renders normalized assistant text incrementally in the production Sessions workspace. It consumes only project-owned `AgentRuntimeEvent` semantics and does not parse or expose raw FCC/provider JSON.

The production composition is:

```text
AgentRuntimeEvent
  -> StreamingConversationState
  -> ConversationSurface
  -> WorkspaceNavigationState.SessionsContent
```

## Rendering contract

- `AssistantTextDelta` appends text to the active assistant message in event-sequence order.
- `Completion` marks the active assistant message complete.
- user messages and assistant messages have distinct visible roles.
- the active assistant message exposes a textual `Streaming` state; completion does not rely on color alone.
- non-text runtime events do not leak their `Text`, `PayloadJson`, or raw transport representation into the assistant answer.
- runtime sequence gaps, duplicates, and regressions fail closed rather than silently producing reordered or duplicated chat text.
- the state marshals runtime-event application through its owning WPF dispatcher when called away from the UI thread.
- the conversation surface auto-scrolls to the latest accepted message/update without performing runtime orchestration in code-behind.
- dark/light appearance is inherited exclusively from semantic `FccBrush*` resources.

## Intentional task boundary

P05-001 does **not** claim ownership of:

- structured tool activity rendering (`FCCD-P05-002`),
- composer, attachments, or context (`FCCD-P05-003`),
- session create/history/resume (`FCCD-P05-004`),
- task state machine (`FCCD-P05-005`),
- stop/cancel/retry UX (`FCCD-P05-006`),
- Markdown/code/diff rendering (`FCCD-P05-007`),
- long-history virtualization/performance closure (`FCCD-P05-008`).

The ListBox uses WPF recycling virtualization as a safe baseline, but P05-008 retains ownership of measured long-history performance and any required paging/windowing architecture.

## Validation

Permanent Windows CI runs:

```powershell
.\tools\ui\validate-streaming-conversation.ps1 -RunFixtures -RequireRuntime
```

The validator provides:

- static production-composition checks;
- negative fixtures for missing typed-delta handling, missing sequence enforcement, hard-coded theme color, missing production state, and missing Sessions composition;
- a Windows/WPF executable fixture that verifies ordered incremental deltas, user/assistant distinction, tool-event isolation, completion, duplicate/gap rejection, unknown-event isolation, a second assistant response, production composition, dark/light parity, and reset recovery.

All fixture runtime events are repository-owned synthetic inputs. They verify product mechanics only and are not FCC/provider `REAL_TARGET` evidence.

## Owner-last interaction

P05-001 introduces no new owner-only acceptance requirement. The pre-existing `OWNER-P04-008-REAL-TARGET` queue item remains mandatory and release-blocking. Nothing in this task substitutes for, closes, or weakens that queued real-target obligation.
