# Discord OAuth transport boundary

The API uses the server-side OAuth 2.0 authorization-code flow. Discord is an
external identity provider, not the Squad-Up authorization authority. A
successful provider callback creates only a short-lived external ticket; it
does not create a local user, grant a role, or establish an application
session. Those decisions belong to the Identity application slice.

## Invariants and trust boundaries

- `/auth/discord/login` has a fixed local completion path and accepts no client
  supplied return URL.
- The provider redirect URI is the fixed `/auth/discord/callback` path and must
  exactly match the URI registered for each environment.
- The protected OAuth `state` and a random correlation cookie bind the callback
  to the browser that initiated it. Missing or altered values fail before the
  authorization code is exchanged.
- Authorization, token, and `/users/@me` endpoints are fixed HTTPS Discord URLs;
  configuration cannot redirect the backchannel to another host.
- The adapter requests exactly the `identify` scope. It does not request guild,
  email, bot, or webhook access.
- Authorization codes, access tokens, client secrets, and cookies are
  Restricted. They are not returned, logged by application code, persisted, or
  saved in authentication properties.
- The Discord user identifier in the encrypted external ticket is Confidential.
  The ticket expires after five minutes and is deleted by the completion
  endpoint.

Callback validation, token exchange, and user-information failures return the
same sanitized RFC 9457 Problem Details shape. The adapter does not retry token
exchange or user-information calls: an ambiguous external result must be
resolved by starting a new login flow. Discord authorization codes remain
provider-controlled and single-use; Squad-Up does not claim end-to-end
exactly-once behavior.

The framework OAuth handler's diagnostic category is suppressed because some
provider failures can embed a remote response body in its message. Squad-Up
emits only the stable `DiscordOAuthCallbackFailed` event, without exception,
query string, identifiers, or provider payload.

Integration tests use an in-memory HTTP handler, runtime-generated synthetic
credentials and tokens, and synthetic Discord identifiers. They exercise the
real ASP.NET Core OAuth state/correlation machinery without contacting Discord
or placing static secrets in fixtures.
