# Bounded Process Output Pipeline

**Task:** `FCCD-P08-003`
**Status:** IN PROGRESS
**Start main SHA:** `4f80433830684966405c7d76aea50583ae4df75b`
**Current integrated baseline:** `3735aed19237c58ee7aab2bd554a46257a55af6c`

## Scope and ownership

P08-003 extends the P08-001 owned-process supervisor with bounded asynchronous stdout and stderr capture. It preserves P08-001 job-object ownership and P08-002 graceful-to-forced cancellation semantics. It does not own ConPTY hosting, shell profile discovery, interactive terminal UI, final P08 safety convergence, or either owner-last acceptance item.

The runtime assembly owns process stream mechanics. Consumers receive WPF-independent immutable output entries, snapshots, and statistics through the supervised-process contract. UI rendering speed never controls whether the child process pipes are drained.

## Contract direction

The implementation will expose:

- a validated `ProcessOutputPolicy` containing every memory and delivery limit;
- immutable entries carrying ownership/process correlation, stdout or stderr identity, a global sequence, a UTC timestamp, text, and explicit per-entry truncation metadata;
- a bounded latest-history snapshot with aggregate accepted, retained, evicted, truncated, and delivery-drop counters;
- a bounded asynchronous live-delivery stream that may drop notifications for a slow consumer while preserving truthful counters and the independently queryable retained snapshot;
- one output completion barrier that closes only after both redirected streams reach EOF and final partial lines have been emitted.

The process supervisor will redirect and concurrently drain both streams with UTF-8 replacement fallback. Fixed-size read and partial-line buffers prevent `ReadToEnd`, unbounded `StringBuilder`, or unbounded list growth. CRLF, LF, lone CR, split delimiters, final unterminated lines, Unicode, Arabic, emoji, malformed bytes, and empty streams have explicit deterministic behavior.

## Policy

Every launch uses one immutable validated `ProcessOutputPolicy`. A caller may supply it through `ProcessOutputOptions`; otherwise the finite defaults apply.

| Limit | Default | Hard maximum |
|---|---:|---:|
| Retained entries | 4,096 | 100,000 |
| Retained UTF-8 payload bytes | 4 MiB | 128 MiB |
| Entry characters | 16,384 | 1,048,576 |
| Entry UTF-8 bytes | 64 KiB | 4 MiB |
| Partial-line characters per source | 16,384 | 1,048,576 |
| Pending live-delivery entries | 512 | 16,384 |
| Reader-buffer characters per source | 4,096 | 65,536 |

The entry-byte limit cannot exceed retained bytes, and entry characters cannot exceed the partial-line buffer. Pending live payload is therefore also bounded by pending entries multiplied by the per-entry byte limit. Values outside the hard bounds and contradictory combinations fail before a process is started.

## Ordering and loss semantics

Each complete line is accepted under one pipeline gate. The assigned monotonic sequence is the observable cross-stream order; operating-system arrival order before acceptance is not claimed. Source-relative order is preserved. Completion is ordered after every accepted entry.

Retained history keeps the newest entries. Entry-count or retained-byte pressure evicts the oldest retained entry and increments exact entry and retained-payload-byte counters. An overlong logical line is emitted once with its retained prefix plus an exact dropped-character count. A full live-delivery queue drops that notification instead of blocking pipe drainage and increments exact entry and retained-payload-byte counters. No API claims that evicted, truncated, or undelivered output was preserved.

`IProcessOutput.ReadEntriesAsync` is a single-consumer best-effort notification stream. Consumers that stop or fall behind re-synchronize from `GetSnapshot()` and compare monotonic sequences and statistics. Snapshot allocation is bounded by the retained-entry policy. The pipeline invokes no WPF dispatcher and never waits for a renderer.

## Lifecycle

Natural exit and P08-002 cancellation both converge on the existing owned-tree completion path. The public process completion barrier will wait for owned-tree exit, stdout EOF, stderr EOF, final partial-line flush, and output completion. Disposal continues to terminate only the owned job when necessary and then waits for the same drain barrier; it does not leave reader tasks or process handles running.

`ProcessOutputStreamState.ReadFailed` reports an operating-system stream-read failure without retaining exception stacks or replaying output into diagnostics. Malformed UTF-8 bytes are not a stream-read failure: the configured decoder emits Unicode replacement characters and continues draining.

## Verification plan

Focused tests will cover parser boundaries, source and sequence metadata, limits and accounting, slow consumption, concurrent high-volume streams, fast and nonzero exits, cancellation, completion draining, repeated lifecycle use, disposal, and P08-001/P08-002 regressions. A permanent Windows validator will enforce the static contract, exercise negative policy mutations, run recovery fixtures, and execute the focused runtime suite in canonical Windows CI.
