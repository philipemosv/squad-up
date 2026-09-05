# Session handoff

Read this file first, then verify Git and locate the active catalog block.
Do not load [historical handoffs](session-handoff-history.md) unless a specific
past decision is needed. Keep this file within 60 lines and 4 KB; replace the
current state at each milestone instead of accumulating completed tickets.

## Current state

- Branch: `main`; F4 ticket-map milestone `2694e65` pushed to `origin/main`.
- Completed outcome: F4 cache distributed was sliced into five ordered vertical
  tickets in [implementation-catalog.md](implementation-catalog.md), from
  HybridCache/Redis L2 composition through measurement. No F4 code started.
- Boundaries: cache stores minimized read projections only; it neither authorizes
  nor reserves seats. `JoinLobby` revalidates persisted aggregate state even if
  a search is stale. Redis/lease failure always bypasses or limits reads, never
  blocks writes or changes correctness.
- Documentation-only verification: `git diff --check`, local-link/anchor check
  and full diff consistency review passed. No .NET gates ran by the approved
  documentation-only exception. Previous functional suite is historical only.
- Current model recommendations were refreshed on 2026-09-05: GPT-5.6 Terra/
  medium for F4-01/02/05, Terra/high for F4-03, Sol/high for F4-04; Gemini 3.8
  Flash at the matching effort is the alternate reviewer.
- Next product step: implement only `F4-01` in a fresh session, reading its
  catalog block, TM-11/TM-12, data classification, `plan.md` §8, and affected
  Lobby host/Infrastructure/integration-test files. Full repository gates apply.

## Relevant limits

- Public Lobby facade currently exposes search/cancellation only; create/join
  forwarding and stale cache reads remain future work. Commands do not retry
  automatically. Other subsystem limits live in their catalog/ADR evidence.
- Actual quota savings are unmeasured. Usage percentage is not a token count;
  record model, effort, speed, both quota windows and retries for comparison.

## Next-session prompt

> F4 ticket map shipped at 2694e65; documentation checks passed, no F4 code
> started. Verify `main` and its separate handoff commit, then implement only
> F4-01. Read this handoff, its catalog block, TM-11/TM-12, classification and
> `plan.md` §8; run focused tests and full repository gates before its milestone.

## Milestone history

| Milestone commit | Completed outcome | Verification |
| --- | --- | --- |
| `2694e65` | F4 ticket map: five cache vertical slices | Diff, links and consistency checks passed |

Older evidence is preserved in the archive linked above. After a milestone
push, append its compact row to the archive and retain only the latest row
here; update and push this handoff separately. Do not recurse for that commit.
