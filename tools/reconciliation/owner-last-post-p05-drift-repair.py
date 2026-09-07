from pathlib import Path

phase_path = Path("CURRENT_PHASE.md")
old = "- Canonical owner queue remains exactly `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET`, both unresolved and release-blocking."
new = "- Canonical owner queue now has one unresolved release-blocking item: `OWNER-P04-008-REAL-TARGET`; `OWNER-P05-EXIT-REAL-TARGET` is `PASS_INTEGRATED` and is no longer deferred."

text = phase_path.read_text(encoding="utf-8")
count = text.count(old)
if count != 1:
    raise SystemExit(f"Expected exactly one stale owner-last sentence; found {count}.")
text = text.replace(old, new)
phase_path.write_text(text, encoding="utf-8", newline="\n")

if old in phase_path.read_text(encoding="utf-8"):
    raise SystemExit("Stale owner-last sentence remains after replacement.")

lines = [
    "# Owner-last canonical drift repair — 2026-09-07",
    "",
    "## Classification",
    "",
    "Integration-pending governance reconciliation. No product implementation task is claimed.",
    "",
    "## Live baseline",
    "",
    "- Exact canonical main before repair: `596c43641b8f0706f27b96bfcb92cabb5859df62`.",
    "- `CURRENT_PHASE=P08`; `FCCD-P08-007` and `FCCD-P08-008` remain pending.",
    "- `OWNER-P04-008-REAL-TARGET` is the sole unresolved/deferred owner-last release blocker.",
    "- `OWNER-P05-EXIT-REAL-TARGET` is already `PASS_INTEGRATED` with P05 exit gate `PASS`.",
    "- Exact-main Windows CI run `34157477029` / #543 completed SUCCESS.",
    "- Exact-main Workspace Search run `34157477032` / #272 completed SUCCESS.",
    "- Exact-main Large Workspace Safeguards run `34157477043` / #256 completed SUCCESS.",
    "",
    "## Defect recovered",
    "",
    "The canonical header and owner queue were already reconciled correctly by PR #205, but the historical P08 activation-provenance bullet inside `CURRENT_PHASE.md` still used present-tense text claiming P04 and P05 were both unresolved. That sentence contradicted the live canonical state and could mislead a later worker into treating P05 as deferred again.",
    "",
    "## Repair",
    "",
    "Only the stale present-tense sentence is changed. It now records P04 as the single unresolved owner-last blocker and P05 as `PASS_INTEGRATED`. No P08 task state, phase gate, product source, tests, release status, or owner evidence is changed.",
    "",
    "## Validation",
    "",
    "- `tools/final-acceptance/validate-owner-last-policy.ps1 -RunNegativeFixtures`",
    "- exact assertions for P08 current phase, one deferred owner item, P04-only deferred gate map, and P05 `PASS_INTEGRATED`",
    "- `git diff --check`",
    "",
    "No owner/manual evidence is required for this reconciliation repair.",
]
Path("evidence/governance/OWNER_LAST_POST_P05_CANONICAL_DRIFT_REPAIR_2026-09-07.md").write_text(
    "\n".join(lines) + "\n",
    encoding="utf-8",
    newline="\n",
)
