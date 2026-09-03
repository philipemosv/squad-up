# ADR-005: Profile-owned initial game and rank catalog

- Status: Accepted
- Date: 2026-09-03
- Decision owners: Squad-Up maintainers

## Context

`plan.md` section 3.4 and `docs/threat-model/data-classification.md` assign
`game_catalog`/`rank_tiers` to the Lobby bounded context, because lobby
matching is the capability that ultimately needs the catalog. Fase 2 item 7
requires a working `PlayerGame` slice — a player selecting a game and a rank
tier for that game — inside `SquadUp.Api`.

`SquadUp.LobbyService.*` currently contains only the resource-based
owner-or-moderator authorization harness from Fase 2 item 6
([ADR-002](ADR-002-process-boundaries-and-dependency-direction.md)); it has no
domain entities, no `DbContext`, and no migrations. Waiting for Lobby's
persistence to exist before Profile can validate a player's rank selection
would block item 7 on unscheduled work, and giving `SquadUp.Api` a live SQL
dependency on a Lobby database would violate the "no cross-context SQL
access" rule already established for this codebase.

The catalog is small, changes rarely, and is explicitly Public data (see the
data-classification inventory), so duplicating it is cheap and does not
create a hidden coupling the way sharing a table or a live cross-context call
would.

## Decision

Seed the initial Dota 2 game and rank-tier catalog inside the `profile`
PostgreSQL schema, owned by `SquadUp.Profile.Infrastructure`
(`games` and `rank_tiers` tables). `PlayerGame.UpsertAsync` validates a
player's `gameId`/`rankTierId` selection against this local copy, and
`GET /catalog/games` / `GET /catalog/games/{gameId}/ranks` serve it as public,
unauthenticated reference data.

When `SquadUp.LobbyService` gains its own domain and persistence, it seeds
its **own** copy of the same catalog rows rather than querying Profile's
schema or the other way around. Each bounded context continues to own the
tables it reads, per [ADR-002](ADR-002-process-boundaries-and-dependency-direction.md).
If catalog drift between the two copies becomes an observed problem, a
follow-up ADR should evaluate a single source of truth (for example, Lobby
publishing a `GameCatalogUpdatedV1` event that Profile consumes) instead of
manual synchronization.

`docs/threat-model/data-classification.md`'s "Game and rank catalog" row is
updated to name Identity/Profile as the current owner, with a forward
reference to this ADR.

## Alternatives considered

### Block Fase 2 item 7 until Lobby's domain exists

Keeps a single conceptual owner from day one, but stalls a scheduled
milestone on unscheduled Lobby work and provides no player-facing value in
the meantime.

### Give `SquadUp.Api` a connection string to a Lobby-owned catalog table

Would let Profile validate against one copy, but requires a live
cross-context SQL dependency, which the architecture explicitly forbids
(ADR-002) because it makes the two contexts' deployments and schemas
implicitly coupled.

### Call a Lobby HTTP endpoint synchronously from Profile

Avoids a shared table, but adds a synchronous availability dependency from a
now-working slice (Profile) onto a context that does not exist yet, for data
that is small, public, and rarely changes. Not justified until Lobby exists
and a real coupling need is measured.

## Consequences

### Positive

- Fase 2 item 7 ships without unscheduled Lobby work.
- `PlayerGame` validation has no live cross-context dependency; Profile stays
  available even if Lobby is down or not yet deployed.
- The catalog stays inside the existing "each context owns its tables" rule.

### Negative

- The catalog will exist in two schemas once Lobby is implemented, with a
  manual process (a migration in each context) to keep both in sync.
- A reviewer must know this ADR exists to understand why catalog ownership in
  code does not match `plan.md` section 3.4 today.

## Enforcement

- `SquadUp.Profile.Infrastructure`'s migrations are the only place the Dota 2
  catalog is seeded until Lobby's own persistence lands.
- Reviews reject any cross-context SQL connection string or `DbContext`
  reaching into another bounded context's schema, catalog included.

## Revisit when

- `SquadUp.LobbyService` gains its own domain and persistence and needs the
  same catalog for matching.
- Catalog drift between Profile's and Lobby's copies is observed in practice.
- A game or rank tier needs to change without a coordinated deploy across both
  contexts.
