# Session handoff

Read this file before starting repository work, then verify the recorded state
with `git status --short --branch` and `git log -1 --oneline`. Git and the
current contents of `plan.md` remain authoritative if this document is stale.

## Current state

- Branch: `main`, ahead of `origin/main` by this milestone's commit (not yet
  pushed).
- Last functional milestone: Fase 2, item 7 — Profile bounded slice for
  profile, games, ranks, and the initial Dota 2 catalog.
- Completed plan item: Fase 2, item 7. New `SquadUp.Profile.Domain/.
  Application/.Infrastructure` projects own a `profile` PostgreSQL schema
  (`player_profiles`, `player_games`, `games`, `rank_tiers`). `SquadUp.Api`
  exposes `GET/PUT /me/profile`, `GET/PUT/DELETE /me/games/{gameId}`, and
  public read-only `GET /catalog/games` / `GET /catalog/games/{gameId}/ranks`.
- Verification: locked restore, formatter verification, Release CI build,
  chiseled API container smoke (non-root, `/health/live` healthy), and the
  complete solution test suite passed — 86 tests, including 71 API
  integration tests (up from 40): profile/game service behavior, HTTP
  endpoint, persistence, and architecture-dependency tests. Both profile
  migrations' generated idempotent SQL replayed twice successfully.
- Ownership model: every `/me/*` endpoint resolves the acting player strictly
  from the `sub` claim on the authenticated session; no request DTO accepts a
  caller-supplied player id, role, or verification/version field. `PUT
  /me/profile` requires an `ExpectedVersion` (backed by PostgreSQL `xmin`) to
  update an existing profile and returns 409 on a stale value. `PUT
  /me/games/{gameId}` validates both the game and the rank tier against the
  seeded catalog and rejects a rank tier that belongs to a different game.
  HTTP tests prove two authenticated players cannot read or overwrite each
  other's profile/games, including a request that tries to smuggle another
  player's id through an unmapped JSON field.
- Catalog ownership decision: the initial Dota 2 game/rank catalog is seeded
  and owned inside the `profile` schema rather than a not-yet-built Lobby
  schema — see [ADR-005](adr/ADR-005-profile-owned-catalog-seed.md) and the
  updated `docs/threat-model/data-classification.md` row. This is provisional
  until `SquadUp.LobbyService` gets its own domain and persistence.
- Container packaging: `src/Api/SquadUp.Api/Dockerfile`'s layered restore step
  and `scripts/test-api-container`'s smoke env vars were updated for the three
  new Profile projects and `ConnectionStrings:ProfileDatabase`; verified by an
  actual container build and smoke run in this session, not just gate scripts.
- Known limitations: no HTTP endpoint yet serves the catalog to an
  unauthenticated caller other than the two read-only `/catalog/*` routes
  added this milestone (no search/filter). Server-side browser-session
  revocation and a JWKS endpoint are still not implemented (carried over from
  item 6). Fase 2 item 5 remains conditional and not triggered. TM-04 remains
  only partially mitigated, unchanged from item 6.
- Next task: Fase 2, item 8 — secret redaction and audit logs (`plan.md`
  section "Fase 2", item 8), covering central redaction for Authorization
  headers/cookies/tokens/connection strings and audit events (actor, action,
  target, result, correlation id) for privileged and profile-mutating actions.

## Next-session prompt

> Continue a Fase 2 do Squad-Up a partir deste commit funcional. Leia e
> confirme `docs/session-handoff.md` contra o Git. O item 7 foi concluído: 86
> testes e todos os gates (incluindo o smoke do container da API) passaram,
> com SQL idempotente do schema `profile` replay-testado e negativos de
> isolamento entre dois jogadores autenticados. O item 5 continua condicional
> e não se aplica. A decisão de manter o catálogo inicial dentro do schema
> `profile` (em vez de Lobby, que ainda não existe) está registrada na
> ADR-005 — revisitar quando `SquadUp.LobbyService` ganhar domínio próprio.
> Implemente o item 8: redação central de segredos em logs/telemetria e audit
> logs (actor, action, target, result, correlation id) para ações
> administrativas e de perfil. Ao concluir, faça commit, push e atualize este
> handoff.

## Milestone history

| Functional commit | Completed outcome | Verification |
| --- | --- | --- |
| `088e87d` | Lobby service containerized with a pinned chiseled, non-root image | Repository gates and container smoke passed |
| `d180257` | Fase 1 items 6–7: Testcontainers platform fixture, integration smoke, configuration validation, and User Secrets guidance | Repository gates and local platform integration passed |
| `a81d2aa` | Fase 2 item 1: ASP.NET Core Identity persistence baseline and expand-only PostgreSQL migration | 20 tests, migration checks, and chiseled API smoke passed |
| `81ddbdc` | Fase 2 item 2: Discord OAuth transport with state/correlation and minimum scope | 27 tests, repository gates, sanitized-log check, and chiseled API smoke passed |
| `c58e7b7` | Fase 2 item 3: transactional external-login upsert with explicit collision-safe link/unlink operations | 35 tests, repeated concurrency tests, repository gates, and chiseled API smoke passed |
| `90ae366` | Fase 2 item 4: bounded BFF session and asymmetric, short-lived, audience-specific API-to-Lobby JWT boundary | 50 tests, repository gates, and chiseled API/Lobby smokes passed |
| `715b646` | Fase 2 item 6: canonical claims/roles, administrative policies, delegated-role boundary, and Lobby owner-or-moderator authorization harness | 54 tests, repository gates, idempotent migration SQL, and role-escalation/different-user negatives passed |
| (this milestone) | Fase 2 item 7: Profile bounded slice — profile/games/ranks CRUD, Dota 2 catalog seed (ADR-005), xmin concurrency, ownership isolation | 86 tests, repository gates, idempotent profile migration SQL, and chiseled API container smoke passed |
