# Session handoff

Read this file before starting repository work, then verify the recorded state
with `git status --short --branch` and `git log -1 --oneline`. Git and the
current contents of `plan.md` remain authoritative if this document is stale.

## Current state

- Branch: `main`; tracked files are synchronized with `origin/main` after the
  Discord OAuth CI-contract milestone push.
- Context workflow milestone: `7dd7d8f docs: add context-efficient ticket
  workflow`. Broad work now uses the repository-local
  `$squad-up-to-tickets` skill, two to five vertical tickets when splitting is
  justified, and one fresh session per ticket. The durable guide is
  [task-slicing.md](development/task-slicing.md).
- Last functional milestone: `83f8bfe test: verify Discord OAuth token exchange
  contract`.
- Completed plan item: Fase 2, item 7 — Profile CRUD, player games/ranks, and
  the initial Dota 2 catalog. Profile owns the new `profile` PostgreSQL schema
  provisionally under [ADR-005](adr/ADR-005-profile-owned-catalog-seed.md).
- Verification: locked restore, formatter verification, Release CI build, and
  the complete suite passed — 88 tests, including 73 API integration tests.
  The Profile-focused suite passed 33 tests; Discord OAuth passed 10 tests with
  `ConnectionStrings__ProfileDatabase` removed from the environment. Both
  generated idempotent SQL scripts replayed twice, the EF model has no pending
  changes, and the chiseled API container was healthy as non-root user 1654.
- Security and ownership: every `/me/*` route derives the player from the
  authenticated `sub`; request DTOs cannot bind ownership, verification, role,
  or concurrency fields. Every protected endpoint has unauthenticated coverage,
  two-player isolation is tested for profiles and games, and all Profile
  mutations explicitly validate antiforgery before touching persistence.
- Concurrency and catalog: `PUT /me/profile` requires the PostgreSQL `xmin`
  version for updates and returns 409 on stale writes. Game/rank selections are
  validated against the seeded local catalog and protected by a composite
  foreign key. The API never runs migrations at startup.
- Regression repaired: adding Profile configuration originally made the Discord
  OAuth fixture depend on an ambient connection string. The fixture now supplies
  an unreachable synthetic value, so fail-fast production validation remains
  enabled while the test is hermetic.
- Known limitations: server-side browser-session revocation and a JWKS endpoint
  are not implemented; production must provide shared protected Data Protection
  storage and approved signing-key storage. The API-to-Lobby typed client will
  consume the issuer in Fase 3, item 8. Fase 2 item 5 remains conditional and is
  not triggered because no public bearer client or refresh token is exposed.
  TM-04 remains only partially mitigated until real identifier endpoints load
  current state and invoke the resource policy with their own negative tests.
- Completed plan item: Fase 2, item 8, ticket 1 of 3 — centralized structured
  log redaction. `RedactingTextWriterSink` removes Restricted values from named
  and nested properties, OAuth callback query values, connection strings, and
  exception messages/stacks before compact JSON is written; only exception type
  is retained. Canary tests cover authorization, cookies, tokens, Discord body,
  connection string, OAuth code/state, and exception failure paths.
- Verification: locked restore, formatter verification, Release CI build, and
  the complete suite passed — 89 tests, including 73 API integration tests.
- Completed plan item: Fase 2, item 8, ticket 2 of 3 — structured audit events
  for Profile mutations. Profile update, game upsert, and game removal now emit
  event 2100 with action, result, actor, target type/ID, and correlation ID.
  The event carries no request body, nickname, timezone, rank, region, cookie,
  or credential. It is emitted only after authorization and antiforgery pass;
  it records success and expected mutation failures.
- Verification: the Profile endpoint suite passed 10 tests, including audit
  success/validation-failure and field-exclusion coverage. Locked restore,
  formatter verification, Release CI build, and the complete suite passed — 90
  tests, including 74 API integration tests.
- Limitation: audit events currently use the centralized structured-log sink;
  append-only audit storage, retention, and access controls are not implemented.
- Completed plan item: Fase 2, item 8, ticket 3 of 3 — structured audit events
  for Identity security actions. Successful Discord sign-in and authenticated
  logout emit event 2200 with action, result, local actor/target IDs, and
  correlation ID. The event deliberately excludes the Discord ID, OAuth code,
  access token, browser cookie, antiforgery token, and client secret.
- Verification: the OAuth/Identity-focused suite passed 12 tests, covering
  anonymous logout (401), antiforgery rejection, and allowlisted audit-event
  fields. Locked restore, formatter verification, Release CI build, and the
  complete suite passed — 92 tests, including 76 API integration tests.
- Completed plan item: Fase 2, item 9 — the in-memory Discord OAuth HTTP test
  double now verifies the token request is an authenticated Authorization Code
  exchange (`grant_type`, client ID/secret, code, and exact redirect URI), then
  verifies the Bearer request to `/users/@me`. It uses runtime-generated
  synthetic Restricted values only in memory and makes no network call.
- Verification: the focused Discord OAuth suite passed 12 tests. Locked
  restore, formatter verification, Release CI build with zero warnings, and
  the complete suite passed — 92 tests, including 76 API integration tests.
- Next task: Fase 3, item 1 — model the Lobby aggregate, value objects, and
  state transitions as a bounded Domain slice.

## Next-session prompt

> Inicie uma sessão nova para a Fase 3 após o milestone `83f8bfe` do contrato
> de CI de Discord OAuth. Confirme este handoff contra o Git e use
> `$squad-up-to-tickets` para o item 1: modelar o agregado Lobby, value objects
> e transições de estado somente no Domain. Os gates passaram com 92 testes
> (76 de API).

## Milestone history

| Milestone commit | Completed outcome | Verification |
| --- | --- | --- |
| `088e87d` | Lobby service containerized with a pinned chiseled, non-root image | Repository gates and container smoke passed |
| `d180257` | Fase 1 items 6–7: Testcontainers platform fixture, integration smoke, configuration validation, and User Secrets guidance | Repository gates and local platform integration passed |
| `a81d2aa` | Fase 2 item 1: ASP.NET Core Identity persistence baseline and expand-only PostgreSQL migration | 20 tests, migration checks, and chiseled API smoke passed |
| `81ddbdc` | Fase 2 item 2: Discord OAuth transport with state/correlation and minimum scope | 27 tests, repository gates, sanitized-log check, and chiseled API smoke passed |
| `c58e7b7` | Fase 2 item 3: transactional external-login upsert with explicit collision-safe link/unlink operations | 35 tests, repeated concurrency tests, repository gates, and chiseled API smoke passed |
| `90ae366` | Fase 2 item 4: bounded BFF session and asymmetric, short-lived, audience-specific API-to-Lobby JWT boundary | 50 tests, repository gates, and chiseled API/Lobby smokes passed |
| `715b646` | Fase 2 item 6: canonical claims/roles, administrative policies, delegated-role boundary, and Lobby owner-or-moderator authorization harness | 54 tests, repository gates, idempotent migration SQL, and role-escalation/different-user negatives passed |
| `93fa531` | Fase 2 item 7: protected Profile slice with profile/game/rank CRUD and provisional Dota 2 catalog ownership | 88 tests, hermetic Discord regression test, antiforgery/ownership negatives, migration replay/model check, and non-root API container smoke passed |
| `7dd7d8f` | Repository-local `to-tickets` adaptation and durable context-efficient task workflow | Skill validation, diff checks, and all repository gates with 88 tests passed |
| `2af2551` | Fase 2 item 8, ticket 1: centralized structured-log secret redaction | Repository gates and 89 tests passed; canary and failure-path proof added |
| `aed5f8e` | Fase 2 item 8, ticket 2: structured audit events for Profile mutations | Repository gates and 90 tests passed; success, validation-failure, correlation, and field-exclusion coverage added |
| `fcbbf6c` | Fase 2 item 8, ticket 3: structured audit events for Identity security actions | Repository gates and 92 tests passed; anonymous/antiforgery negatives and Restricted-field exclusion coverage added |
| `83f8bfe` | Fase 2, item 9: Discord OAuth in-memory HTTP double verifies the authenticated authorization-code token request | Repository gates and 92 tests passed; 12 focused OAuth tests passed |
