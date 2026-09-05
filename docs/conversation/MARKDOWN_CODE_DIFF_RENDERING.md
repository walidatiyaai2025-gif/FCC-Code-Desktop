# P05-007 — Markdown, code, and diff content rendering

## Scope

`FCCD-P05-007` adds safe native WPF presentation for completed conversation messages without changing the durable message text or introducing provider/runtime parsing into the UI layer.

The production path is:

```text
normalized assistant/user message text
    -> ConversationMessageState
    -> ConversationContentParser (completed messages only)
    -> typed ConversationContentBlock list
    -> ConversationSurface native WPF templates
```

## Supported presentation subset

The deterministic first-party parser recognizes:

- paragraphs;
- Markdown ATX headings (`#` through `######`);
- unordered list items beginning with `- ` or `* `;
- fenced code blocks using triple backticks;
- bounded optional fence language identifiers;
- `diff` and `patch` fenced blocks with explicit header/add/remove/context line classification.

Unrecognized Markdown syntax remains visible as literal text. The product does not execute embedded HTML, script, links, browser content, or provider payloads as part of this rendering path.

## Streaming behavior

Streaming assistant deltas remain plain raw text while `IsStreaming=true`. The renderer intentionally does not reparse Markdown/code/diff on every token. When a completion event arrives, the exact accumulated text is parsed once into `ContentBlocks`, the raw durable text remains unchanged, and the completed native WPF rendering replaces the streaming text presentation.

Persisted completed messages are parsed when restored so reopened sessions have the same content rendering behavior.

## Code and diff safety

Code and diff blocks use native `TextBlock`/`ScrollViewer` WPF primitives and the existing semantic design resources. Code does not execute. Diff content is presentation-only in P05-007; it does not stage, apply, edit, or mutate files/Git state.

Diff classification is ordered so `+++` and `---` file markers are headers rather than additions/removals. `@@`, `diff `, and `index ` lines are also headers. Remaining `+` and `-` lines become added/removed blocks and all other lines are context.

## Rendering bounds

`ConversationContentParser.MaxRenderedSourceCharacters` is 1 MiB. Only the presentation parse is bounded. If a message exceeds that limit, a visible notice is appended to the rendered block list while the full raw/durable message remains unchanged. Fence language identifiers are also bounded to 32 characters.

Long-conversation windowing, retained UI history size, virtualization thresholds, and performance/load closure remain owned by `FCCD-P05-008`.

## Permanent validation

Run:

```powershell
.\tools\ui\validate-conversation-content-rendering.ps1 -RunFixtures -RequireRuntime
```

The gate verifies parser behavior, streaming-versus-completed projection, persisted-message restoration, production WPF composition, semantic resources, rendering limits, safe diff classification, and negative fixtures that reject removal of required safeguards. The canonical Windows workflow and CI-policy validator permanently require this gate.

## Evidence boundary

The executable fixture is `SELF_TEST_ONLY / CLOUD`. It uses controlled normalized events and local WPF state. It is not evidence of a real FCC/provider run, real provider response formatting, or P05 phase-exit acceptance. The existing `OWNER-P04-008-REAL-TARGET` obligation remains unchanged and release-blocking.
