# Session handoff

Read this file before starting repository work, then verify the recorded state
with `git status --short --branch` and `git log -1 --oneline`. Git and the
current contents of `plan.md` remain authoritative if this document is stale.

## Current state

- Branch: `main`, clean and synchronized with `origin/main` after the milestone
  push.
- Last functional milestone: `c58e7b7 feat: persist Discord external accounts
  safely`.
- Completed plan item: Fase 2, item 3 — transactional external-login upsert,
  explicit link/unlink outcomes, collision rejection, and concurrent orphan
  prevention.
- Verification: locked restore, formatter verification, Release CI build, and
  the complete solution test suite passed. The suite had 35 passing tests,
  including 21 API integration tests. The chiseled API container smoke passed
  read-only as non-root user 1654.
- Concurrency verification: the three race tests passed five consecutive runs.
  The existing `(login_provider, provider_key)` primary key serializes a
  Discord identity, while local-account row locks serialize link/unlink. No
  migration or schema contraction was needed.
- Security boundary: the callback validates the Discord snowflake before the
  transactional upsert; only the stable Discord ID is stored, never the OAuth
  token or username. Collisions never merge accounts, and unlink cannot remove
  the last authentication method.
- Known limitation: link/unlink are application operations only and are not
  exposed over HTTP. They require the authenticated, reauthenticated ceremony
  planned with the browser session boundary. A successful login still returns
  204 after clearing the temporary external cookie and does not yet establish a
  Squad-Up browser session.
- Next task: Fase 2, item 4 — issue the bounded Secure/HttpOnly/SameSite BFF
  cookie after external login and add asymmetric, short-lived,
  audience-specific internal JWT issuance and validation.

## Next-session prompt

> Continue a Fase 2 do Squad-Up a partir do commit funcional `c58e7b7`. Leia e
> confirme `docs/session-handoff.md` contra o Git. O item 3 foi concluído; os 35
> testes, os gates e o smoke chiseled passaram. Implemente o item 4 do plano:
> cookie BFF limitado e JWT interno assimétrico, curto e audience-specific,
> respeitando o ADR-003 e o AGENTS.md. Ao concluir, faça commit, push e atualize
> este handoff conforme as instruções do repositório.

## Milestone history

| Functional commit | Completed outcome | Verification |
| --- | --- | --- |
| `088e87d` | Lobby service containerized with a pinned chiseled, non-root image | Repository gates and container smoke passed |
| `d180257` | Fase 1 items 6–7: Testcontainers platform fixture, integration smoke, configuration validation, and User Secrets guidance | Repository gates and local platform integration passed |
| `a81d2aa` | Fase 2 item 1: ASP.NET Core Identity persistence baseline and expand-only PostgreSQL migration | 20 tests, migration checks, and chiseled API smoke passed |
| `81ddbdc` | Fase 2 item 2: Discord OAuth transport with state/correlation and minimum scope | 27 tests, repository gates, sanitized-log check, and chiseled API smoke passed |
| `c58e7b7` | Fase 2 item 3: transactional external-login upsert with explicit collision-safe link/unlink operations | 35 tests, repeated concurrency tests, repository gates, and chiseled API smoke passed |
