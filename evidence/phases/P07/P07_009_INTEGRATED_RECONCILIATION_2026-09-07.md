# FCCD-P07-009 — Integrated reconciliation evidence

Date: 2026-09-07  
Phase: P07 — Change review + Git  
Task: `FCCD-P07-009 — Dirty/pre-existing-change provenance`  
Evidence class: cloud/self-test + canonical integration provenance

## Result

`FCCD-P07-009` is CLOSED from the production conservative dirty/pre-existing-change provenance implementation after exact candidate validation, normal merge integration, and exact post-merge canonical-main non-regression validation all completed SUCCESS.

## Implementation provenance

- Implementation PR: #181 — `P07-009: add conservative dirty-change provenance`.
- Branch: `worker/fccd-p07-009-dirty-provenance`.
- Exact implementation candidate: `2db2276dc920d769c235c8581bd272d6b7b05519`.
- Normal merge commit: `b534fd7d1d23b1727cc68a7a588d8ab4e5ce5fcb`.
- Merge parents preserve tested ancestry; no squash/rebase closure is claimed.

## Exact implementation-head gates

- Windows CI #419: `34061234142` — SUCCESS.
- P06-007 Workspace Search #148: `34061234123` — SUCCESS.
- P06-008 Large Workspace Safeguards #132: `34061234214` — SUCCESS.

## Exact post-merge canonical-main gates

- Windows CI #420: `34061750164` — SUCCESS.
- P06-007 Workspace Search #149: `34061750167` — SUCCESS.
- P06-008 Large Workspace Safeguards #133: `34061750177` — SUCCESS.

## Verified cloud boundary

The integrated implementation provides an Application-owned read-only `IGitChangeProvenanceService`; explicit dirty-baseline capture and comparison; conservative `PreExistingDirty` versus `CreatedSinceBaseline` path-lineage classification; reporting of resolved pre-existing changes; rename source/target alias continuity across status-shape changes; cross-repository baseline rejection; bounded dirty-path materialization with fail-closed overflow; cancellation; Unicode/Arabic disposable real-Git fixtures; and preservation of owner bytes. It delegates only to the existing read-only Git status surface and performs no ref, index, work-tree, config, or remote mutation.

## Governance boundary

- P07 remains `IN_PROGRESS` and its exit gate remains `NOT_RUN`.
- `FCCD-P07-010` and `FCCD-P07-011` remain PENDING.
- No P08, P11, P12, or later-phase implementation is authorized by this evidence.
- `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET` remain the unchanged release-blocking owner queue obligations.
- `KNOWN_RELEASE_BLOCKERS=2` and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.
- No owner/manual/target evidence is fabricated or implied.
