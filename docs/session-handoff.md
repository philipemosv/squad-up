# Session handoff

Read this file first, then verify Git and locate the active catalog block.
Do not load [historical handoffs](session-handoff-history.md) unless a specific
past decision is needed. Keep this file within 60 lines and 4 KB; replace the
current state at each milestone instead of accumulating completed tickets.

## Current state

- Branch: `main`; workflow milestone `83fe785` pushed to `origin/main`.
  This document is its separate handoff update; verify Git again.
- Last functional milestone: `c7cc5fa`, F3-09-01, graceful API-to-Lobby
  degradation. Search/cancellation failures become sanitized 503 Problem
  Details; failed cancellation never returns false success.
- Functional verification at that commit: locked restore, format verification,
  Release CI build and all 131 tests passed (83 API integration tests).
  These are historical results, not validation of subsequent edits.
- Completed ticket: `DEV-WF-01`, workflow cost reduction; see the block in
  [implementation-catalog.md](implementation-catalog.md). Compact handoff,
  archived history and the approved documentation checks shipped in `83fe785`.
- Scope: compact startup context, archive historical detail, avoid repeated
  planning and measure usage. Security controls and required CI are unchanged.
- Milestone checks: diff/links/consistency, handoff limits and archive comparison
  passed. Earlier restore/format/Release build passed; suite: 130 passed, 1 failed in
  `InvalidLobbyServiceNameStopsHostStartup`; see DEV-WF-01 evidence.
- Documentation-only exception approved/applied on 2026-09-05: diff, changed
  links and consistency checks; no .NET rerun. Full gates for executable changes.
- Product next step: slice F4 distributed cache into two to five vertical
  tickets with `$squad-up-to-tickets`; register IDs, then execute only the first
  ticket in a fresh session. No F4 implementation has started.

## Relevant limits

- Public Lobby facade currently exposes search/cancellation only; create/join
  forwarding and stale cache reads remain future work. Commands do not retry
  automatically. Other subsystem limits live in their catalog/ADR evidence.
- Actual quota savings are unmeasured. Usage percentage is not a token count;
  record model, effort, speed, both quota windows and retries for comparison.

## Next-session prompt

> DEV-WF-01 shipped at 83fe785; documentation checks passed. Earlier full suite:
> 130 passes and one unresolved initialization-test failure (see catalog).
> Verify main and its separate handoff commit. Slice F4 using squad-up-to-tickets,
> register IDs, then execute its first ticket in a fresh session. Read only this
> handoff and the relevant catalog block; do not rerun .NET for documentation.

## Milestone history

| Milestone commit | Completed outcome | Verification |
| --- | --- | --- |
| `83fe785` | DEV-WF-01: compact context and documentation-only checks | Diff, links, consistency, archive and size checks passed |

Older evidence is preserved in the archive linked above. After a milestone
push, append its compact row to the archive and retain only the latest row
here; update and push this handoff separately. Do not recurse for that commit.
