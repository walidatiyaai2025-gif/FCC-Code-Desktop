# P02 DPI / Resolution Decision

**Status:** Accepted  
**Date:** 2026-09-03  
**Related task:** `FCCD-P02-009`

FCC Code Desktop uses explicit Windows **Per-Monitor V2** DPI awareness while keeping responsive layout thresholds in WPF device-independent pixels (DIPs). WPF remains responsible for DPI scaling; the application does not apply a second manual visual scale transform.

The P02 shell uses deterministic compact, standard, and wide viewport profiles. Responsive collapse is reversible only for panes that the responsive coordinator itself collapsed, preserving a user-collapsed pane when space becomes available again.

This decision establishes the architectural DPI/responsive foundation only. Full visual acceptance across 1366×768, 1920×1080, 4K, and 125/150/175/200% scaling remains owned by later acceptance phases.

**Reason:** Per-monitor awareness is required for correct Windows multi-monitor scaling, and DIP-based breakpoints avoid coupling layout behavior to physical pixel density or duplicating WPF scaling. Reversible forced collapse preserves workspace usability at narrow/short viewports without overwriting user presentation intent.

See `docs/contracts/P02_DPI_RESOLUTION_FOUNDATION.md`.
