# Session handoff

Read this file before starting repository work, then verify the recorded state
with `git status --short --branch` and `git log -1 --oneline`. Git and the
current contents of `plan.md` remain authoritative if this document is stale.

## Current state

- Branch: `main`, clean and synchronized with `origin/main` after the milestone
  push.
- Last functional milestone: `715b646 feat: enforce role and resource
  authorization`.
- Completed plan item: Fase 2, item 6 — canonical claims and roles, API
  administrative policies, and Lobby resource-based owner-or-moderator
  authorization harness.
- Verification: locked restore, formatter verification, Release CI build, and
  the complete solution test suite passed. The suite has 54 passing tests,
  including 40 API integration tests. Focused authorization tests and repeated
  execution of both idempotent Identity SQL artifacts passed. Container smokes
  were not rerun because this milestone did not change container packaging.
- Identity boundary: `sub`, `discord_user_id`, `role`, and `scope` have explicit
  vocabulary. `Player`, `Moderator`, and `Admin` are seeded by an additive
  migration; existing users are backfilled idempotently and new Discord-backed
  accounts receive `Player` in the account-creation transaction. BFF tickets
  take roles only from the local Identity store and ignore provider-supplied
  role fields.
- Authorization boundary: API policies enforce the explicit
  Moderator/Admin hierarchy. Internal tokens carry allowlisted roles only for
  delegated users; workload identities and unknown roles are rejected. Lobby's
  `lobby.owner-or-moderator` policy requires `lobby.write` and current resource
  ownership or an allowlisted Moderator/Admin role. HTTP tests prove owner
  access, different-user denial, and role-escalation denial.
- Known limitations: server-side browser-session revocation and a JWKS endpoint
  are not implemented; production must provide shared protected Data Protection
  storage and approved signing-key storage. The API-to-Lobby typed client will
  consume the issuer in Fase 3, item 8. Fase 2 item 5 remains conditional and is
  not triggered because no public bearer client or refresh token is exposed.
  TM-04 remains only partially mitigated until real identifier endpoints load
  current state and invoke the resource policy with their own negative tests.
- Next task: Fase 2, item 7 — implement the Profile bounded slice for profile,
  games, ranks, and the initial Dota 2 catalog, with explicit DTO allowlists,
  ownership checks, negative authorization tests, and migration evidence.

## Next-session prompt

> Continue a Fase 2 do Squad-Up a partir do commit funcional `715b646`. Leia e
> confirme `docs/session-handoff.md` contra o Git. O item 6 foi concluído; os 54
> testes e todos os gates passaram, incluindo SQL idempotente e negativos de
> role escalation/outro usuário. O item 5 continua condicional e não se aplica.
> Implemente o item 7: CRUD de perfil/jogos/ranks e catálogo inicial de Dota 2,
> preservando DTOs explícitos, ownership e fronteiras de dados. Ao concluir,
> faça commit, push e atualize este handoff.

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
