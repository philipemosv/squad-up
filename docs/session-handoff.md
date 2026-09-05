# Session handoff

Read this file first, then verify Git and locate the active catalog block.
Do not load [historical handoffs](session-handoff-history.md) unless a specific
past decision is needed. Keep this file within 60 lines and 4 KB; replace the
current state at each milestone instead of accumulating completed tickets.

## Current state

- Branch: `main`; F4-03 milestone `09c2da1` pushed to `origin/main`.
- Completed outcome: recruiting search coordinates replicas with a two-second
  Redis hot-key lease using `SET NX PX`, random ownership tokens and
  compare-and-delete release.
- Boundaries: the lease wraps cache-aside only; it is not a Redis–PostgreSQL
  transaction and never authorizes, reserves capacity or makes `JoinLobby`
  exclusive. Contention or Redis loss waits briefly, then retries L2/bypasses.
- Verification: locked restore, formatter verification, Release build and the
  complete suite passed (135 tests); 23 focused Lobby integration tests include
  contention, expiry, old-token release and Redis-outage proofs.
- Current model recommendations were refreshed on 2026-09-05: GPT-5.6 Terra/
  medium for F4-01/02/05, Terra/high for F4-03, Sol/high for F4-04; Gemini 3.8
  Flash at the matching effort is the alternate reviewer.
- Next product step: implement only `F4-04` in a fresh session, reading its
  catalog block, TM-11/TM-12, data classification, `plan.md` §8, and affected
  Lobby host/Infrastructure/integration-test files. Full repository gates apply.

## Relevant limits

- Public Lobby facade currently exposes search/cancellation only; create/join
  forwarding and stale cache reads remain future work. Commands do not retry
  automatically. The local generation does not invalidate other hosts; F4-04
  alone may add stale serving and its bounded PostgreSQL fallback.
- Actual quota savings are unmeasured. Usage percentage is not a token count;
  record model, effort, speed, both quota windows and retries for comparison.

## Next-session prompt

> F4-03 Redis lease for the recruiting-search hot key shipped at `09c2da1`;
> locked restore, formatter, Release build and 135 tests passed. Verify `main`
> and its separate handoff commit, then implement only F4-04. Read its catalog
> block, TM-11/TM-12, classification and `plan.md` §8; run the stale, outage,
> bounded-fallback and join-correctness proofs plus full repository gates.

## Milestone history

| Milestone commit | Completed outcome | Verification |
| --- | --- | --- |
| `09c2da1` | F4-03: Redis hot-key lease for recruiting search | Locked restore, formatter, build and 135 tests passed |

Older evidence is preserved in the archive linked above. After a milestone
push, append its compact row to the archive and retain only the latest row
here; update and push this handoff separately. Do not recurse for that commit.
