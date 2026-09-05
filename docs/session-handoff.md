# Session handoff

Read this file before starting repository work, then verify the recorded state
with `git status --short --branch` and `git log -1 --oneline`. Git and the
[implementation catalog](implementation-catalog.md) are authoritative for
execution state; `plan.md` remains authoritative for architectural decisions.

## Current state

- Branch: `main`; functional changes are synchronized with `origin/main` at
  `c7cc5fa` (graceful API-to-Lobby degradation). This document is the separate
  handoff record for that milestone.
- Context workflow milestone: `7dd7d8f docs: add context-efficient ticket
  workflow`. Broad work now uses the repository-local
  `$squad-up-to-tickets` skill, two to five vertical tickets when splitting is
  justified, and one fresh session per ticket. The durable guide is
  [task-slicing.md](development/task-slicing.md).
- Roadmap execution catalog: `docs/implementation-catalog.md` is the compact
  entry point for a ticket's status, dependencies, minimum reading set, and
  evidence. Do not preload `plan.md`; open only a catalog-cited anchor when it
  is required for a design decision.
- Last functional milestone: `c303021 feat: add lobby cancellation command`.
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
- Completed plan item: Fase 3, item 1 — Lobby aggregate, value objects, and
  state transitions as a bounded Domain slice. `Lobby` enforces capacity,
  unique membership, Recruiting-only joins, ordinal catalog rank requirements,
  explicit lifecycle transitions, and a single local `LobbyCompleted` fact on
  the transition to `Full`.
- Data and security: participant snapshots contain only the Confidential local
  player ID, Discord ID, and display name needed for later provisioning. They
  have no persistence, HTTP, logging, caching, or integration-contract path in
  this slice. TM-15 now records that minimization requirement.
- Verification: 11 focused Lobby Domain tests passed. Locked restore, formatter
  verification, Release CI build with zero warnings, and the complete suite
  passed — 103 tests, including 76 API integration tests.
- Completed plan item: Fase 3, item 2 — Lobby-owned EF mappings, constraints,
  local Dota 2 catalog seed, and PostgreSQL `xmin` concurrency token. The
  `lobby` schema contains `lobbies`, `lobby_members`, `game_catalog`, and
  `rank_tiers`; no context reads another context's schema. The aggregate's
  persisted member count is constrained to capacity, membership has a composite
  key, and future commands must reload/re-evaluate on optimistic conflicts.
- Verification: focused Lobby persistence tests passed — 7 tests covering
  migration/schema isolation, catalog seed, duplicate/invalid-count database
  rejections, aggregate materialization, `xmin`, configuration failure, no
  startup migration, and two idempotent-SQL replays. Locked restore, formatter
  verification, Release CI build with zero warnings, and the complete suite
  passed — 110 tests, including 76 API integration tests.
- Migration safety: `001_initial_lobby.sql` is reviewed idempotent expand-only
  SQL. Production must apply it with the dedicated migration identity before
  enabling Lobby commands; application startup never migrates.
- Completed plan item: Fase 3, item 3, ticket 1 of 3 — CQRS creation and
  discovery for Lobby without a broker. Application exposes explicit command
  and query contracts; Infrastructure validates active local catalog values,
  persists a new `Recruiting` lobby, and projects only deliberately publishable
  recruiting-lobby fields with `AsNoTracking`.
- Verification: focused Lobby persistence/CQRS suite passed — 9 tests covering
  successful creation, normalized game ID, inactive/unknown catalog failures,
  invalid capacity, non-recruiting exclusion, minimized projection, and no EF
  tracking. Locked restore, formatter verification, Release CI build with zero
  warnings, and the complete suite passed — 111 tests, including 76 API
  integration tests.
- Security and limits: the creation request excludes owner, member, version,
  and authorization fields; owner identity is a separate service argument for
  the future authenticated boundary. Search intentionally excludes owner and
  participant data. HTTP endpoints, resource authorization, pagination,
  idempotency, and broker/outbox handling remain out of scope.
- Completed plan item: Fase 3, item 3, ticket 2 of 3 — Join/leave CQRS
  commands. The caller identity is a separate argument from the minimized
  participant snapshot; only `Recruiting` lobbies permit leave, preserving the
  irreversible local completion fact when a lobby becomes `Full`. Each
  membership mutation retries one `xmin` conflict by reloading and reapplying
  the domain rule, then returns a deterministic concurrency conflict if it is
  still stale.
- Verification: focused Lobby Domain tests passed (13), focused PostgreSQL
  persistence/CQRS tests passed (11), and locked restore, formatter
  verification, Release CI build with zero warnings, and the complete suite
  passed — 116 tests, including 76 API integration tests. A deterministic
  PostgreSQL test synchronizes the two stale saves and proves the third save is
  the re-evaluated retry; the final lobby is exactly full with two members.
- Security and limits: no endpoint, resource authorization, HTTP idempotency,
  broker/outbox, cache, or external side effect was added. The member snapshot
  remains Confidential Lobby-owned data and does not reach logs, cache, or a
  message in this ticket.
- Completed plan item: Fase 3, item 3, ticket 3 of 3 — Lobby cancellation
  CQRS. The command loads the current Lobby before authorizing the authenticated
  actor through the owner-or-moderator policy, then applies `Cancel`. It retries
  one `xmin` conflict by reloading and re-authorizing, returning a deterministic
  domain rejection when the concurrent winner already cancelled the Lobby.
- Verification: focused Lobby persistence/CQRS suite passed — 13 tests,
  including owner, different-player, moderator, terminal-state, and deterministic
  concurrent-cancellation coverage. Locked restore, formatter verification,
  Release CI build with zero warnings, and the complete suite passed — 118 tests,
  including 76 API integration tests.
- Security and limits: a different player cannot cancel a Lobby they do not own;
  Moderator/Admin may cancel using the current owner resource. No endpoint,
  HTTP idempotency, broker/outbox, audit event, cache, or external side effect
  was added. Caller claims remain only at the internal authenticated boundary.
- Completed plan item: Fase 3, item 4 (`F3-04`) — the Lobby host now exposes
  create/search/join/leave/cancel endpoints over existing CQRS services. Every
  player operation requires a delegated internal JWT; owner and member identity
  are derived from `sub`, never bound from request JSON. Cancellation reloads
  current state before owner-or-moderator authorization, preventing IDOR.
- HTTP failures: unauthenticated, scope/resource denial, validation, not-found,
  domain rejection, and concurrency outcomes use RFC 9457 Problem Details with
  stable codes. The JWT challenge/forbidden paths now also return sanitized
  Problem Details. DTO responses remain minimized and do not expose owner or
  participant snapshots.
- Verification: focused Lobby endpoint tests passed (2) and the whole Lobby
  integration suite passed (15). Locked restore, formatter verification,
  Release CI build with zero warnings, and the full suite passed — 120 tests,
  including 76 API integration tests.
- Security and limits: HTTP tests prove anonymous 401, workload-token refusal,
  ignored owner/player overposting, authenticated-subject membership, and a
  different player receiving 403 when cancelling. HTTP idempotency remains
  explicitly deferred to F3-05; no broker/outbox, cache, pagination, or API to
  Lobby typed client was added.
- Completed plan item: Fase 3, item 5 (`F3-05`) — HTTP `Idempotency-Key`
  ledger for lobby creation and join. A 128-character ASCII key is bound to the
  delegated authenticated `sub` and SHA-256 canonical command hash; the
  Lobby-owned row records the response for replay and expires after 24 hours.
  A PostgreSQL transaction plus advisory lock serializes concurrent duplicates;
  a different hash returns `409 idempotency_key_conflict` without executing the
  command. The ledger and command persist atomically; no automatic HTTP retry
  was added.
- Verification: focused endpoint tests passed (4), the full Lobby integration
  suite passed (17), and the generated idempotent migration scripts replayed
  twice. Locked restore, formatter verification, Release CI build with zero
  warnings, and the complete suite passed — 122 tests, including 76 API
  integration tests. EF reports no pending model changes.
- Retention limitation: expired rows are rejected and opportunistically purged
  by later idempotent commands; a scheduled no-traffic purge remains out of
  scope until operational retention requirements are defined.
- Completed plan item: Fase 3, item 6 (`F3-06`) — keyset pagination and
  minimized read projections for Lobby search. `GET /lobbies` now accepts an
  optional normalized game filter, a URL-safe continuation cursor, and a page
  size of 1–50 (20 by default). The query remains `AsNoTracking`, returns only
  Recruiting lobbies, gets one extra row to establish `nextCursor`, and never
  returns owner or member snapshots.
- HTTP failures: malformed cursor, invalid page size, and invalid game ID
  return RFC 9457 Problem Details with stable codes; anonymous search remains
  rejected by the delegated internal JWT policy.
- Verification: the focused Lobby endpoint/persistence suite passed — 18 tests
  covering keyset continuation, normalization, terminal-state exclusion,
  response minimization, no EF tracking, anonymous rejection, and invalid
  cursor/page-size failures. Locked restore, formatter verification, Release
  CI build with zero warnings, and the complete suite passed — 123 tests,
  including 76 API integration tests.
- Limits: cursor position is intentionally not a global snapshot; lobbies
  created or changing status between pages follow normal keyset semantics.
  Caching, broker/outbox, member pagination, and API-to-Lobby typed-client work
  remain out of scope.
- Completed plan item: Fase 3, item 7 (`F3-07`) — a PostgreSQL integration
  proof starts 50 independent Join commands against a five-seat Lobby. Exactly
  five succeed; the remaining 45 reload the terminal `Full` state and receive
  deterministic domain rejections. The `xmin` retry is bounded by aggregate
  capacity so it can observe every possible fill and one terminal read; it
  never uses an external or unbounded retry.
- Completion-fact evidence: the test interceptor records `LobbyCompleted` only
  after successful `SaveChanges`; it observes exactly one fact for the Lobby.
  The persisted aggregate has status `Full`, `MembersCount` 5, and exactly five
  membership rows—there is no overbooking.
- Verification: focused 50-way proof passed; the Lobby integration suite passed
  19 tests; locked restore, formatter verification, Release CI build with zero
  warnings, and the complete suite passed — 124 tests, including 76 API
  integration tests.
- Completed plan item: Fase 3, item 8 (`F3-08`) — the internal typed
  API-to-Lobby client mints a short-lived delegated JWT on every call and only
  accepts a configured HTTPS origin with absolute-path references. Separate
  read and command pipelines bound concurrency, total/attempt timeouts, and
  circuit breaking. Only reads receive one jittered transient retry; commands
  never retry automatically.
- Verification: focused client tests passed — 5 tests covering minimized JWT,
  fixed origin/SSRF rejection, missing actor, command no-retry, circuit opening,
  read retry, and command timeout. Locked restore, formatter verification,
  Release CI build with zero warnings, and the complete suite passed — 129
  tests, including 81 API integration tests.
- Security and limits: the client neither forwards browser/provider credentials
  nor exposes a public client contract. It adds no API facade, outbox, cache,
  or automatic command retry. The next slice owns graceful translation of
  internal dependency failures to public behavior.
- Completed plan item: Fase 3, item 9 (`F3-09-01`) — graceful API-to-Lobby
  degradation. The public API now proves delegated Lobby search and cancellation
  behavior: connection failures, timeout, open circuit, and Lobby 5xx responses
  become sanitized `503 lobby_temporarily_unavailable` Problem Details. A failed
  cancellation is never represented as a `204`; browser authentication and
  antiforgery remain at the API boundary, while the short-lived delegated JWT
  remains the only credential sent to Lobby.
- Verification: the focused API Lobby-client/gateway suite passed 7 tests,
  including unavailable read, unavailable command, and sensitive-error
  exclusion. Locked restore, formatter verification, Release CI build, and the
  complete suite passed — 131 tests, including 83 API integration tests.
- Limits: this ticket intentionally exposes only search and cancellation as
  representative read/command paths. Cache-stale reads, create/join forwarding,
  and broader public Lobby facade contracts remain out of scope.
- Next task: Fase 4 cache distribuído. Antes de implementar, use o skill
  `$squad-up-to-tickets` para dividir F4 em dois a cinco tickets verticais e
  registre seus IDs no catálogo; então execute somente o primeiro em uma sessão
  nova.
- CI repair: the Lobby container smoke now supplies an ephemeral, credential-free
  valid connection string solely for startup validation and probes `/health/live`.
  It no longer claims database readiness when no PostgreSQL container exists;
  `/health/ready` retains its tagged database check for real deployments.
- Verification: the corrected `./scripts/test-lobby-container` passed with the
  chiseled non-root user 1654 and read-only filesystem. Locked restore,
  formatter verification, Release CI build with zero warnings, and the complete
  suite passed — 111 tests, including 76 API integration tests.

## Next-session prompt

> Inicie uma sessão nova após o milestone `c7cc5fa`, que conclui `F3-09` com
> degradação sanitizada API→Lobby e sem falso sucesso para cancelamento. Gates
> completos passaram com 131 testes (83 de API). Use `$squad-up-to-tickets`
> para fatiar F4 e execute somente o primeiro ticket criado.

## Milestone history

| Milestone commit | Completed outcome | Verification |
| --- | --- | --- |
| `c7cc5fa` | F3-09: graceful API-to-Lobby degradation | Full suite: 131 passed |
| `e7db5dc` | F3-08: resilient typed API-to-Lobby client | Full suite: 129 passed |
| `54d542b` | F3-07: 50-way Lobby join race / no overbooking | Full suite: 124 passed |
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
| `2674296` | Fase 3, item 1: Lobby aggregate, rank value objects, participant snapshots, and explicit state transitions | Repository gates and 103 tests passed; 11 focused Domain tests passed |
| `1badbcd` | Fase 3, item 2: Lobby EF persistence, local catalog, constraints, migration SQL, and `xmin` | Repository gates and 110 tests passed; 7 focused Lobby persistence tests passed |
| `354daf5` | Fase 3, item 3, ticket 1: Lobby CQRS create and recruiting-search services | Repository gates and 111 tests passed; 9 focused Lobby persistence/CQRS tests passed |
| `e3bf4e8` | CI repair: Lobby container smoke validates startup liveness without a database | Container smoke plus repository gates and 111 tests passed |
| `a306e86` | Fase 3, item 3, ticket 2: Lobby join/leave CQRS with bounded optimistic-concurrency retry | Repository gates and 116 tests passed; deterministic PostgreSQL stale-write retry proof added |
| `c303021` | Fase 3, item 3, ticket 3: owner-or-moderator Lobby cancellation CQRS with bounded optimistic-concurrency retry | Repository gates and 118 tests passed; authorization and concurrent-cancellation proof added |
| `1e8d4b5` | Fase 3, item 4: Lobby create/search/join/leave/cancel HTTP endpoints | Repository gates and 120 tests passed; RFC 9457, IDOR, token-kind, and overposting coverage added |
| `4b501eb` | Fase 3, item 6: Lobby keyset pagination and minimized search projections | Repository gates and 123 tests passed; continuation, input failures, anonymous rejection, and projection-minimization coverage added |
| `9f804b7` | Fase 3, item 5: HTTP idempotency ledger for Lobby creation and join | Repository gates and 122 tests passed; duplicate, concurrent replay, conflict, owner isolation, expiry, and migration-replay coverage added |
