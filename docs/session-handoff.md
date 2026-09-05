# Session handoff

Read this file first, then verify Git and locate the active catalog block.
Do not load [historical handoffs](session-handoff-history.md) unless a specific
past decision is needed. Keep this file within 60 lines and 4 KB; replace the
current state at each milestone instead of accumulating completed tickets.

## Current state

- Branch: `main`; F4-02 milestone `e742111` pushed to `origin/main`.
- Completed outcome: recruiting search uses server-generated, versioned SHA-256
  keys over normalized bounded filter/cursor/page inputs, with 10–20-second TTL
  jitter and a local post-commit generation invalidation.
- Boundaries: cached pages contain only publishable summaries; cache never
  authorizes or reserves seats. `JoinLobby` always revalidates the persisted
  aggregate. Invalidation is local only; entries on other hosts expire by TTL
  until the later distributed mechanism is deliberately added.
- Verification: locked restore, formatter verification, Release build and full
  suite passed (133 tests); 21 focused Lobby endpoint/persistence tests passed,
  including key/page isolation and post-create/post-join invalidation.
- Current model recommendations were refreshed on 2026-09-05: GPT-5.6 Terra/
  medium for F4-01/02/05, Terra/high for F4-03, Sol/high for F4-04; Gemini 3.8
  Flash at the matching effort is the alternate reviewer.
- Next product step: implement only `F4-03` in a fresh session, reading its
  catalog block, TM-11/TM-12, data classification, `plan.md` §8, and affected
  Lobby host/Infrastructure/integration-test files. Full repository gates apply.

## Relevant limits

- Public Lobby facade currently exposes search/cancellation only; create/join
  forwarding and stale cache reads remain future work. Commands do not retry
  automatically. The local generation does not invalidate other hosts; F4-03
  introduces only the bounded Redis hot-key lease, not stale serving.
- Actual quota savings are unmeasured. Usage percentage is not a token count;
  record model, effort, speed, both quota windows and retries for comparison.

## Next-session prompt

> F4-02 recruiting-search cache keys, TTL/jitter and local invalidation shipped
> at e742111; locked restore, formatter, Release build and 133 tests passed.
> Verify `main` and its separate handoff commit, then implement only F4-03.
> Read its catalog block, TM-11/TM-12, classification and `plan.md` §8; run
> focused contention/expiry/release/outage tests and full repository gates.

## Milestone history

| Milestone commit | Completed outcome | Verification |
| --- | --- | --- |
| `e742111` | F4-02: search cache keys, TTL/jitter and local invalidation | Locked restore, formatter, build and 133 tests passed |

Older evidence is preserved in the archive linked above. After a milestone
push, append its compact row to the archive and retain only the latest row
here; update and push this handoff separately. Do not recurse for that commit.
