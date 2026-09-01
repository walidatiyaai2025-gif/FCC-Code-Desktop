# FCCD-P00-009 probe-host evidence — 2026-09-02

## Result

`FCCD-P00-009 = BLOCKED`

Reusable Blender P00 infrastructure is implemented and integrated into the unified runner. Deterministic self-tests pass `15/15`.

The owner's current Windows host was searched through `PATH` and common Program Files/LocalAppData locations. No `blender.exe` was found. The real target probe therefore returned `BLOCKED_BLENDER_NOT_FOUND` and did not create synthetic target evidence.

Implemented target operations:

- executable discovery and observed version
- background/factory-startup launch
- Python scene/object/material creation
- `.blend` save and header validation
- PNG render and signature validation
- OBJ export and geometry validation
- structured result validation
- controlled Python failure
- owned-process cancellation
- Unicode/Arabic disposable paths and bounded cleanup
- recursive credential redaction

Evidence: `evidence/phases/P00/target/blender-contract.json`.

External requirement: install or provide access to a supported Blender executable, then rerun the canonical unified target validation command. No owner implementation decision is required.
