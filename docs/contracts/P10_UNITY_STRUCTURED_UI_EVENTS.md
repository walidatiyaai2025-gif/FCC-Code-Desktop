# P10 Unity Structured UI Event Contract

## Purpose

`FCCD-P10-011` defines the bounded presentation/activity projection for Unity adapter activity. It consumes the already integrated P10 log, Editor-automation, and build-validation results; it does not create another process runner, parser, or provider protocol.

## Event identity and ordering

Every event carries a non-empty operation correlation GUID and a positive sequence number. Consumers must order events by the product-owned sequence within the operation; wall-clock timestamps are intentionally not required for correctness.

## Event kinds

The public contract exposes diagnostic, progress, completed, failed, cancelled, and indeterminate kinds together with info/warning/error severity. Stable machine-readable codes use only ASCII letters, digits, `.`, `-`, and `_` and are bounded to 128 characters.

## Message and metadata safety

Messages are Unicode-preserving, NUL-sanitized, and bounded to 4096 UTF-16 characters with explicit truncation disclosure. The event model never contains raw argv, command strings, environment variables, stdout/stderr buffers, result-file paths, project paths, or arbitrary provider payloads.

Build completion may expose only bounded presentation-safe artifact provenance already produced by P10-010: byte length and SHA-256. No local artifact path is projected.

## Source projections

- `UnityLogEntry` becomes a diagnostic event with the P10-005 classified severity and original bounded text.
- Editor-automation validation becomes completed/failed/cancelled/indeterminate according to the fail-closed P10-009 result.
- Build-target validation becomes completed/failed/cancelled/indeterminate according to P10-010 and may include byte/hash provenance.
- Callers may emit explicit progress events only with a normalized stage and a finite percentage from 0 through 100.

The projector cannot convert a failed, cancelled, or indeterminate terminal result into a success event.

## Cloud acceptance

Hosted Windows CI builds the production assembly with warnings-as-errors and executes deterministic positive/negative fixtures for Unicode preservation, severity mapping, correlation/sequence preservation, bounded messages, NUL replacement, progress validation, unsafe code rejection, automation cancellation, and build artifact provenance. The fixture does not pretend to launch Unity or fabricate target evidence.
