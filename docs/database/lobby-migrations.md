# Lobby database migrations

The `SquadUp.LobbyService` bounded context exclusively owns the PostgreSQL
`lobby` schema. Other contexts communicate through its future HTTP and
versioned-message boundaries; none may read or write these tables directly.

## Initial migration

This expand-only migration creates `lobbies`, `lobby_members`, the locally
owned `game_catalog` and `rank_tiers`, their constraints and indexes, plus the
context-local EF migration history table. It neither reads nor alters another
context's schema.

`lobbies.xmin` is PostgreSQL's optimistic concurrency token. Future commands
must treat `DbUpdateConcurrencyException` as a bounded reload/re-evaluation
path, rather than assuming a stale aggregate can be saved. The row also stores
`members_count`, constrained to `0 <= members_count <= capacity`; the domain
aggregate owns and increments that count with each accepted member.

The database rejects duplicate membership through primary key
`(lobby_id, player_id)`, invalid member snapshots through length and positive
ordinal checks, invalid capacity/rank ranges, and unknown lifecycle values.
The rank catalog has a context-local foreign key to the game catalog and a
unique `(game_id, ordinal)` index. It seeds the Dota 2 catalog already seeded
provisionally by Profile, as required by [ADR-005](../adr/ADR-005-profile-owned-catalog-seed.md), without introducing cross-context SQL access.

## Compatibility and rollback

- The migration is additive: it creates a new schema and new objects only.
- Normal application startup validates configuration and registers a readiness
  check, but never connects or applies migrations.
- Deploy the migration with the dedicated DDL identity before enabling Lobby
  commands. The application identity must have no DDL grant.
- A rollback deploy leaves this additive schema intact. Dropping it is a later
  destructive migration and requires explicit approval plus retention review.

Generate the reviewed SQL artifact from the repository root:

```bash
dotnet tool restore
dotnet ef migrations has-pending-model-changes \
  --context LobbyDbContext \
  --project src/Lobby/SquadUp.LobbyService.Infrastructure \
  --startup-project src/Lobby/SquadUp.LobbyService.Api
dotnet ef migrations script --idempotent \
  --context LobbyDbContext \
  --project src/Lobby/SquadUp.LobbyService.Infrastructure \
  --startup-project src/Lobby/SquadUp.LobbyService.Api \
  --output docs/database/migrations/lobby/001_initial_lobby.sql \
  0 20260904011444_InitialLobby
```

Supply `ConnectionStrings:LobbyDatabase` through User Secrets locally or an
approved deployment secret provider. Never put a real connection string in
commands, source, generated SQL, logs, or artifacts.
