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

## Ordering and loss semantics

Each complete line is accepted under one pipeline gate. The assigned monotonic sequence is the observable cross-stream order; operating-system arrival order before acceptance is not claimed. Source-relative order is preserved. Completion is ordered after every accepted entry.

Retained history keeps the newest entries. Entry-count or retained-byte pressure evicts the oldest retained entry and increments eviction counters. An overlong logical line is emitted once with its retained prefix plus exact dropped-character and dropped-byte counts. A full live-delivery queue drops that notification instead of blocking pipe drainage and increments delivery-drop counters. No API claims that evicted, truncated, or undelivered output was preserved.

## Lifecycle

Natural exit and P08-002 cancellation both converge on the existing owned-tree completion path. The public process completion barrier will wait for owned-tree exit, stdout EOF, stderr EOF, final partial-line flush, and output completion. Disposal continues to terminate only the owned job when necessary and then waits for the same drain barrier; it does not leave reader tasks or process handles running.

## Verification plan

Focused tests will cover parser boundaries, source and sequence metadata, limits and accounting, slow consumption, concurrent high-volume streams, fast and nonzero exits, cancellation, completion draining, repeated lifecycle use, disposal, and P08-001/P08-002 regressions. A permanent Windows validator will enforce the static contract, exercise negative policy mutations, run recovery fixtures, and execute the focused runtime suite in canonical Windows CI.
