# Session handoff

Read this file before starting repository work, then verify the recorded state
with `git status --short --branch` and `git log -1 --oneline`. Git and the
current contents of `plan.md` remain authoritative if this document is stale.

## Current state

- Branch: `main`, clean and synchronized with `origin/main` after the milestone
  push.
- Last functional milestone: `90ae366 feat: establish bounded authentication
  boundaries`.
- Completed plan item: Fase 2, item 4 — bounded BFF cookie plus asymmetric,
  short-lived, audience-specific internal JWT issuance and Lobby validation.
- Verification: locked restore, formatter verification, Release CI build, and
  the complete solution test suite passed. The suite has 50 passing tests,
  including 36 API integration tests. Both chiseled container smokes passed
  read-only as non-root user 1654.
- Browser boundary: successful Discord completion now issues only a host-only,
  Secure, HttpOnly, SameSite=Lax session cookie with a 30-minute absolute,
  non-sliding lifetime. Logout requires explicit antiforgery and API failures
  return status codes rather than login redirects. A configurable shared Data
  Protection key-ring path supports multiple API replicas.
- Service boundary: API emits only RS256 two-minute tokens for the configured
  Lobby audience and allowlisted scopes, with explicit workload/delegated-user
  identity. Lobby validates algorithm, signature, named public key, issuer,
  audience, lifetime/maximum lifetime, client, actor kind, `sub`, `jti`, and
  scope. Additive public-key configuration proves a rotation window; Lobby
  rejects private PEM configuration.
- Known limitations: server-side browser-session revocation and a JWKS endpoint
  are not implemented; production must provide shared protected Data Protection
  storage and approved signing-key storage. The API-to-Lobby typed client will
  consume the issuer in Fase 3, item 8. Fase 2 item 5 remains conditional and is
  not triggered because no public bearer client or refresh token is exposed.
- Next task: Fase 2, item 6 — add claims, roles, policies, and a resource-based
  authorization test harness, including role-escalation and different-user
  negative tests.

## Next-session prompt

> Continue a Fase 2 do Squad-Up a partir do commit funcional `90ae366`. Leia e
> confirme `docs/session-handoff.md` contra o Git. O item 4 foi concluído; os 50
> testes, os gates e os smokes chiseled de API/Lobby passaram. O item 5 é
> condicional e não se aplica enquanto nenhum cliente bearer público for
> exposto. Implemente o item 6: claims, roles, policies e harness de autorização
> baseada em recurso, incluindo testes negativos de role escalation e acesso
> por outro usuário. Ao concluir, faça commit, push e atualize este handoff.

## Milestone history

| Functional commit | Completed outcome | Verification |
| --- | --- | --- |
| `088e87d` | Lobby service containerized with a pinned chiseled, non-root image | Repository gates and container smoke passed |
| `d180257` | Fase 1 items 6–7: Testcontainers platform fixture, integration smoke, configuration validation, and User Secrets guidance | Repository gates and local platform integration passed |
| `a81d2aa` | Fase 2 item 1: ASP.NET Core Identity persistence baseline and expand-only PostgreSQL migration | 20 tests, migration checks, and chiseled API smoke passed |
| `81ddbdc` | Fase 2 item 2: Discord OAuth transport with state/correlation and minimum scope | 27 tests, repository gates, sanitized-log check, and chiseled API smoke passed |
| `c58e7b7` | Fase 2 item 3: transactional external-login upsert with explicit collision-safe link/unlink operations | 35 tests, repeated concurrency tests, repository gates, and chiseled API smoke passed |
| `90ae366` | Fase 2 item 4: bounded BFF session and asymmetric, short-lived, audience-specific API-to-Lobby JWT boundary | 50 tests, repository gates, and chiseled API/Lobby smokes passed |
