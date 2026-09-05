# Session create, history, and resume

`FCCD-P05-004` turns the durable P03 conversation records into the P05 session UX contract. It does not execute FCC/provider work and it does not own the later task lifecycle state machine.

## Production boundary

The session workspace is project-scoped and backed by the existing `IConversationStateStore` / `SqliteConversationStateStore` contract. A project must already exist in persistence before it can become the active session owner. P06 owns add/open/recent project workflows; P05-004 exposes `MainWindow.ActivateProjectSessionsAsync(...)` as the explicit seam that that project workflow can call.

The desktop application initializes the canonical SQLite store under the current user's LocalApplicationData directory:

```text
FCC Code Desktop/State/fcc-code-desktop.db
```

No demo project is synthesized at startup.

## Session lifecycle

For an active persisted project, the workspace supports:

- deterministic session history ordered by most recent `UpdatedUtc`;
- durable local session creation;
- selection/resume of a session only when its `ProjectId` matches the active project;
- loading durable user/assistant messages in persisted sequence order;
- preserving and displaying a bound FCC runtime session identifier when one has been recorded;
- durable user-message append before the P05 composer acknowledges a submission;
- recreating the session state/store and reopening the same durable history after process restart.

A session with no runtime session ID is truthfully a local session. `BindActiveRuntimeSessionAsync(...)` / `BindRuntimeSessionAsync(...)` stores the normalized runtime session ID for later runtime resume. This is a binding seam only; P05-004 does not start an agent run or claim that a provider session was resumed.

## Conversation restore

`StreamingConversationState.LoadPersistedMessages(...)` replaces the visible conversation with completed durable messages, validates strictly increasing persisted message sequences and supported user/assistant roles, and resets transient streaming/tool activity state. This prevents stale runtime events from being presented as part of a reopened durable conversation.

## Failure behavior

- orphan/non-persisted projects fail closed;
- cross-project session resume fails closed;
- malformed or non-increasing durable message history fails closed;
- message persistence is serialized so concurrent UI submissions cannot allocate the same sequence;
- when an active durable session exists, the composer persists its user message before adding it to the visible conversation; persistence failure rejects the composer submission instead of presenting an unpersisted message as durable;
- startup storage initialization errors are surfaced to the local user and do not fabricate session availability.

## Ownership boundaries

P05-004 does **not** own:

- project add/open/recent UX — P06;
- FCC/provider invocation or task execution — P05-005 and the runtime layer;
- stop/cancel/retry UX — P05-006;
- Markdown/code/diff rendering — P05-007;
- conversation virtualization/performance closure — P05-008.

It also makes no new provider-backed P04 acceptance claim. `OWNER-P04-008-REAL-TARGET` remains the sole currently queued owner-only runtime obligation and remains release-blocking.

## Permanent validation

The canonical Windows baseline executes:

```powershell
.\tools\ui\validate-session-workspace.ps1 -RunFixtures -RequireRuntime
```

The validator enforces the persistence/session boundary statically, runs negative fixtures for cross-project resume, message ordering, runtime-ID persistence, session history composition and semantic colors, and executes a Windows/WPF + temporary SQLite fixture that creates sessions, binds a runtime ID, persists messages, recreates state, resumes durable history, rejects cross-project resume, renders the production surface and verifies dark/light semantic theme behavior.
