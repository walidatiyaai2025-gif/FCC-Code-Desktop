# Structured Tool Activity Timeline

## Scope

`FCCD-P05-002` projects normalized runtime tool events into a structured conversation-side timeline. It builds on the P05-001 conversation state and does not parse provider/FCC payloads directly.

## Input boundary

The presentation consumes only `AgentRuntimeEvent` values from `FCCCodeDesktop.Runtime`:

- `ToolStarted`
- `ToolProgress`
- `ToolResult`

Tool identity/progress/result text and correlation IDs are supplied by the already-established runtime normalization layer. `PayloadJson` is deliberately outside the P05-002 presentation boundary.

## Correlation model

- A `ToolStarted` event creates a new activity row.
- A non-empty `CorrelationId` maps later `ToolProgress` and `ToolResult` events to the latest active row for that correlation.
- A correlated progress event updates the existing row rather than creating duplicates.
- A correlated result updates the existing row to the neutral `ResultReceived` state. The UI does not infer success from arbitrary result text.
- A later start that reuses a correlation ID creates a new row and becomes the active mapping; prior completed history remains unchanged.
- An unmatched progress or result event remains visible as a generic standalone activity instead of being silently discarded.

The shared `StreamingConversationState` retains the existing contiguous runtime-sequence guard, so conversation text and tool activity observe one ordered normalized event stream.

## Presentation

`ConversationSurface` contains a bounded vertical tool timeline below the conversation. It appears only when tool activity exists and uses semantic theme resources exclusively. Rows expose:

- tool name or safe generic label;
- `Running` / `Result` status;
- last update time;
- normalized progress text when present;
- normalized result text when present.

The surface subscribes to the tool collection and uses the WPF dispatcher to keep the latest activity visible. It preserves dark/light theme parity and the existing P05-001 conversation behavior.

## Safety and ownership boundaries

P05-002 does not:

- execute FCC, `fcc-claude`, shell commands, or processes;
- inspect raw provider payload JSON;
- claim provider-backed target evidence;
- implement composer/attachments/context (P05-003);
- implement session create/history/resume (P05-004);
- define the task lifecycle state machine (P05-005);
- implement stop/cancel/retry UX (P05-006);
- implement Markdown/code/diff rendering (P05-007);
- claim long-history performance/virtualization closure (P05-008).

## Permanent verification

The canonical Windows CI baseline executes:

```powershell
.\tools\ui\validate-tool-activity-timeline.ps1 -RunFixtures -RequireRuntime
```

The validator contains static contract checks, negative/recovery fixtures, and an executable WPF fixture covering correlated start/progress/result behavior, unmatched-event preservation, correlation reuse, assistant/tool separation, raw-payload non-rendering, runtime composition, theme parity, and reset recovery.
