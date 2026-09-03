# Session handoff

Read this file before starting repository work, then verify the recorded state
with `git status --short --branch` and `git log -1 --oneline`. Git and the
current contents of `plan.md` remain authoritative if this document is stale.

## Current state

- Branch: `main`, clean and synchronized with `origin/main` after the milestone
  push.
- Last functional milestone: `81ddbdc feat: add Discord OAuth transport`.
- Completed plan item: Fase 2, item 2 — Discord Authorization Code login with
  protected `state`, correlation cookie, exact callback path, and only the
  `identify` scope.
- Verification: locked restore, formatter verification, Release CI build, and
  the complete solution test suite passed. The suite had 27 passing tests,
  including 13 API integration tests. The chiseled API container smoke passed
  read-only as non-root user 1654.
- External verification: Discord was not contacted. OAuth tests used the real
  ASP.NET Core middleware with an in-memory backchannel and runtime-generated
  synthetic credentials and tokens.
- Security boundary: the Discord token is neither returned nor persisted;
  OAuth endpoints and completion redirect are fixed; the short-lived external
  cookie is removed at completion; remote failures return sanitized Problem
  Details and emit only `DiscordOAuthCallbackFailed` without remote payload.
- Known limitation: a successful callback currently proves only the OAuth
  transport and returns 204 after clearing its temporary ticket. It does not
  yet create, link, unlink, or authenticate a local Squad-Up account.
- Next task: Fase 2, item 3 — implement transactional external-login upsert and
  explicit unlink/account-collision handling, with concurrency and negative
  tests proportional to those paths.

## Next-session prompt

> Continue a Fase 2 do Squad-Up a partir do commit funcional 81ddbdc. Leia e
> confirme `docs/session-handoff.md` contra o Git. O item 2 foi concluído e todos
> os gates e o smoke chiseled passaram. Implemente o item 3 do plano: upsert
> transacional de external login e tratamento explícito de unlink/account
> collision, respeitando o AGENTS.md. Ao concluir, faça commit, push e atualize
> este handoff conforme as instruções do repositório.

## Milestone history

| Functional commit | Completed outcome | Verification |
| --- | --- | --- |
| `088e87d` | Lobby service containerized with a pinned chiseled, non-root image | Repository gates and container smoke passed |
| `d180257` | Fase 1 items 6–7: Testcontainers platform fixture, integration smoke, configuration validation, and User Secrets guidance | Repository gates and local platform integration passed |
| `a81d2aa` | Fase 2 item 1: ASP.NET Core Identity persistence baseline and expand-only PostgreSQL migration | 20 tests, migration checks, and chiseled API smoke passed |
| `81ddbdc` | Fase 2 item 2: Discord OAuth transport with state/correlation and minimum scope | 27 tests, repository gates, sanitized-log check, and chiseled API smoke passed |
