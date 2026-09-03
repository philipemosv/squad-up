# Profile database migrations

The `SquadUp.Profile` bounded context exclusively owns the PostgreSQL
`profile` schema. `SquadUp.Api` is its only application reader and writer; no
other context may query these tables directly.

## Initial migration

The initial migration is expand-only: it creates a new schema, four tables
(`games`, `rank_tiers`, `player_profiles`, `player_games`), their constraints
and indexes, plus the EF migration history table. It does not alter or read
an existing context's objects.

`player_profiles` uses the PostgreSQL `xmin` system column as an optimistic
concurrency token. `PUT /me/profile` requires callers to echo back the
`version` value from the last read; a stale or missing version is rejected
before any row is touched.

The composite foreign key `fk_player_games_rank_tiers_game_id_rank_tier_id`
(`game_id`, `rank_tier_id`) references `rank_tiers` (`game_id`, `tier_id`),
so the database itself rejects a `player_games` row whose rank tier belongs
to a different game than the one selected.

The schema stores these data classes:

| Data | Classification | Handling |
| --- | --- | --- |
| Player id (local user id), nickname, time zone, profile status | Confidential | Identity/Profile-only access; nickname and time zone are only ever returned to the owning player |
| Player's game, rank tier and region selection, verification timestamp | Confidential | Identity/Profile-only access; never joined across bounded contexts |
| Game and rank tier catalog (id, name, ordinal) | Public | Read-only reference data served at `/catalog/games` and `/catalog/games/{gameId}/ranks` without authentication |

## Catalog ownership (provisional)

`plan.md` and `docs/threat-model/data-classification.md` originally listed the
game/rank catalog as data owned by the Lobby bounded context. Lobby has no
domain or persistence yet (`SquadUp.LobbyService.*` currently contains only
the resource-based authorization harness), so this milestone seeds the
initial Dota 2 catalog inside the `profile` schema instead, so that
`PlayerGame` rows have a real catalog to validate against without a
cross-context join. [ADR-005](../adr/ADR-005-profile-owned-catalog-seed.md)
records this decision and the compatibility path for when Lobby's own
persistence lands.

## Second migration: seed Dota 2 catalog

The second migration is additive data only: it inserts one `games` row
(`dota2`) and eight `rank_tiers` rows (Herald through Immortal, ordinals 1-8).
It does not alter table shape and carries no destructive `Down` risk beyond
deleting the rows it inserted.

## Compatibility and rollback

- Both migrations are expand-only. No existing table is altered, and no other
  context's schema is read or written.
- The new API requires the expanded schema before profile operations are
  enabled. Normal application startup never runs migrations.
- Rollback deploys the earlier application and leaves the additive schema in
  place. Removing the schema requires a separate reviewed migration, explicit
  human approval, and confirmation that no data must be retained.
- The initial migration takes locks only on newly created objects. The seed
  migration only inserts rows into tables it created in the same expand
  phase, so no production table experiences contention from this change.

Generate review artifacts from the repository root with the pinned local tool:

```bash
dotnet tool restore
dotnet ef migrations has-pending-model-changes \
  --context ProfileDbContext \
  --project src/Api/SquadUp.Profile.Infrastructure \
  --startup-project src/Api/SquadUp.Api
dotnet ef migrations script --idempotent \
  --context ProfileDbContext \
  --project src/Api/SquadUp.Profile.Infrastructure \
  --startup-project src/Api/SquadUp.Api \
  --output docs/database/migrations/profile/001_initial_profile.sql \
  0 20260903191038_InitialProfile
dotnet ef migrations script --idempotent \
  --context ProfileDbContext \
  --project src/Api/SquadUp.Profile.Infrastructure \
  --startup-project src/Api/SquadUp.Api \
  --output docs/database/migrations/profile/002_seed_dota2_catalog.sql \
  20260903191038_InitialProfile 20260903191310_SeedDota2Catalog
```

Supply `ConnectionStrings:ProfileDatabase` through User Secrets locally or a
secret provider in deployment. Never place the connection string in the
command, repository, generated SQL, logs, or artifacts.

Pinned dependencies used by this slice are EF Core `10.0.11` (MIT), Npgsql EF
Core provider `10.0.3` (PostgreSQL license), and the local `dotnet-ef` tool
`10.0.11` (MIT) — the same versions already pinned for the identity slice.
