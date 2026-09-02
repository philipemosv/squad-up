# ADR-003: Authentication and token boundaries

- Status: Accepted
- Date: 2026-09-01
- Decision owners: Squad-Up maintainers

## Context

Squad-Up uses Discord to establish an external identity, serves browser users,
and has independently deployed services. These are different trust boundaries.
A credential intended for Discord, a browser session, or an internal API must
not silently become valid in either of the other contexts.

Discord documents OAuth 2.0 grant flows, but does not document the OpenID
Connect discovery, ID token, or UserInfo contracts. We therefore treat Discord
as an **OAuth 2.0 external login provider**, not as an OpenID Connect identity
provider. This classification is an inference from Discord's documented
protocol surface and must be revisited if that surface changes.

Authentication proves an identity. Authorization decides whether that identity
may perform a specific action on a specific resource. A successful Discord
login alone must never grant access to every Squad-Up resource.

## Decision

### Discord external login

Use the server-side OAuth 2.0 Authorization Code flow.

- Request only the `identify` scope initially.
- Generate and validate a cryptographically random, short-lived `state` value
  for every authorization attempt.
- Register and compare exact redirect URIs and handle each callback once.
- Exchange the authorization code from the backend; never expose the client
  secret to browser code.
- Call Discord's `/users/@me` endpoint and link the stable Discord user ID to a
  local ASP.NET Core Identity account.
- Make account linking explicit and reject Discord-ID or local-account
  collisions rather than merging accounts automatically.
- Do not retain Discord access or refresh tokens unless a future feature
  demonstrably needs them. Store any required token encrypted and with the
  narrowest scope and lifetime.
- Keep the Discord bot token separate from user OAuth credentials and expose it
  only to the Discord Integration process.

### Browser session

Use the API as a Backend for Frontend (BFF) and issue an ASP.NET Core
authentication cookie after successful external login.

- The cookie is host-only, `Secure`, `HttpOnly`, and `SameSite=Lax`.
- HTTPS is mandatory. Session lifetime is short and has an absolute upper
  bound; renewal must not create an unbounded session.
- Persist and share ASP.NET Core Data Protection keys when more than one API
  instance can serve the same session.
- Apply explicit antiforgery protection to state-changing cookie-authenticated
  requests. CORS is not an antiforgery mechanism.
- Return `401` or `403` from API endpoints instead of redirecting API clients to
  an HTML login page.
- Do not store Squad-Up access or refresh tokens in browser local storage or
  session storage.

### Internal service identity

For synchronous API-to-service calls, the API issues a separate, asymmetric,
short-lived JWT whose audience is the receiving service.

- Include and validate `iss`, `aud`, `sub`, `jti`, expiration, `client_id`, and
  narrowly defined `scope` claims.
- Distinguish a workload identity from an explicitly delegated user identity;
  the receiver must not infer one from the other.
- Validate the signature, allowed algorithm, issuer, audience, lifetime, and
  required scopes on every request, with a small bounded clock skew.
- Keep the private signing key only in the issuer. Receivers get public keys
  through a rotation-capable configuration or JWKS endpoint.
- Use a lifetime measured in minutes and do not create an internal refresh
  token flow.
- Never forward a Discord access token as a Squad-Up service credential.

Mobile and CLI authentication are outside the pilot. If introduced, they need
a separate decision covering short-lived access tokens, opaque refresh-token
rotation, token-family revocation, hashed persistence, and reuse detection.

### Authorization

Use policy and resource-based authorization. The initial claim vocabulary is:

- `sub` for the local Squad-Up user or workload identity;
- `discord_user_id` only when a use case needs the linked external identifier;
- `role` for coarse administrative responsibilities;
- `scope` for allowed API capabilities;
- `iss`, `aud`, and `jti` for token boundary and replay diagnostics.

Ownership and lobby membership are checked against current application state.
Network location, a valid cookie, or a valid JWT is not by itself authority to
read or mutate a resource.

## Alternatives considered

### Store bearer and refresh tokens in browser storage

This simplifies a pure SPA, but makes long-lived credentials directly
accessible to successful script injection. The BFF cookie keeps the credential
out of browser JavaScript and centralizes renewal and revocation.

### Use the Discord access token as the Squad-Up token

The token has a Discord audience, scope, lifecycle, and issuer. Reusing it would
couple internal authorization to an external credential and blur trust
boundaries.

### Treat Discord as OpenID Connect

OAuth authorization does not imply the ID-token and discovery guarantees of
OpenID Connect. The required Discord protocol surface is not documented, so the
application uses the documented authorization-code flow and `/users/@me`.

### Reuse browser cookies between internal services

Cookies model a browser session and carry CSRF and key-sharing concerns.
Audience-bound internal tokens make service identity and delegated authority
explicit.

## Consequences

### Positive

- External, browser, and service credentials have clear audiences and owners.
- Browser JavaScript does not handle a reusable Squad-Up bearer token.
- Internal services can reject tokens minted for another service.
- Discord compromise or token expiry does not automatically become an internal
  authorization model.

### Negative

- The API owns OAuth callback, cookie, antiforgery, and signing-key operations.
- Multiple API instances require durable shared Data Protection keys.
- Key rotation, session revocation, and account-linking failures require tests
  and runbooks.
- A future native client needs a different public-client design.

## Enforcement

- Test mismatched, missing, expired, and replayed OAuth `state` values.
- Test callback replay and account-link collision paths.
- Assert cookie flags and antiforgery rejection on mutating endpoints.
- Test invalid JWT signature, algorithm, issuer, audience, expiration, and
  scope, including a signing-key rotation window.
- Test resource ownership independently of roles and network placement.
- Redact authorization codes, cookies, tokens, secrets, and sensitive claims
  from logs and traces.

## Revisit when

- Discord adds a documented OpenID Connect surface needed by Squad-Up.
- Native mobile or CLI clients enter scope.
- An external identity platform replaces local federation and token issuance.
- Internal call volume or topology makes per-request JWTs unsuitable.

## Sources

- [Discord OAuth2 documentation](https://docs.discord.com/developers/topics/oauth2)
- [ASP.NET Core cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
- [ASP.NET Core JWT bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [ASP.NET Core antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
