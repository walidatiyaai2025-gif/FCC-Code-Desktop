# Conversation Composer, Attachments, and Context

## Scope

`FCCD-P05-003` establishes the production conversation composer and a typed submission boundary for message text, file attachments, and context references. Its own component remains independent of session creation/resume, task-state execution, runtime dispatch, Markdown rendering, and long-history virtualization; later P05 work consumes its typed submission contract through the application composition boundary.

## Composer contract

`ComposerState` owns the mutable draft and emits an immutable `ComposerSubmission` snapshot when a submission is requested. A submission contains:

- normalized visible message text;
- attachment metadata snapshots (`FullPath`, display name, size);
- typed context snapshots (`Project`, `File`, `Selection`, or `Reference`);
- a monotonically increasing submission identity;
- a UTC creation timestamp.

The pending submission identity must be acknowledged exactly through `AcceptSubmission` or `RejectSubmission`. A stale or mismatched acknowledgement fails closed.

## Safety limits

The composer enforces these bounded inputs:

- maximum draft length: 12,000 characters;
- maximum attachments: 8;
- maximum attachment size: 25 MiB each;
- maximum context references: 12;
- duplicate attachment paths and duplicate typed context references are rejected case-insensitively.

P05-003 stores file references and metadata only. It does **not** read attachment contents, parse arbitrary files, execute files, start processes, or extend the FCC/provider wire contract. The later safe-file/project layers own content access and workspace-policy enforcement.

## Presentation

`ConversationComposer` is composed under the existing conversation/tool timeline and provides:

- multiline message entry;
- `Ctrl+Enter` submit shortcut while plain Enter remains multiline;
- multi-file attachment picker;
- file-context picker;
- removable attachment/context chips;
- clear action;
- character count and visible validation feedback;
- semantic dark/light resources and keyboard focus states.

The P05-003 component contract still emits immutable submissions and requires exact acknowledgement. With P05-005 integrated into the production application boundary, `MainWindow` now preflights the active persisted session, runtime availability, and task-state eligibility before persisting/projecting the user message and starting the task. A failed downstream preflight rejects the same submission identity and preserves the draft/attachments/context for correction or retry; it does not fabricate a sent user message. This downstream orchestration does not add runtime coupling to `ComposerState` itself.

## Ownership boundaries

P05-003 itself does not implement:

- session create/history/resume — `FCCD-P05-004`;
- explicit task-state/runtime-dispatch lifecycle — `FCCD-P05-005`;
- stop/cancel/retry UX — `FCCD-P05-006`;
- Markdown/code/diff rendering — `FCCD-P05-007`;
- long-history virtualization/performance closure — `FCCD-P05-008`;
- project/file content loading and safe file service — P06.

The typed `ComposerSubmission` contract remains the seam used by later orchestration without provider-specific payload parsing in presentation code.

## Permanent verification

The canonical Windows CI baseline executes:

```powershell
.\tools\ui\validate-conversation-composer.ps1 -RunFixtures -RequireRuntime
```

The validator covers static contract invariants, negative/recovery fixtures, executable WPF composition, missing-handler fail-closed behavior, missing/duplicate file rejection, typed context deduplication, immutable snapshot emission, exact accept/reject acknowledgement, downstream task-preflight rejection without false message projection, rejected-draft preservation, accepted-draft clearing, programmatic length rejection, and dark/light theme parity.
