# Configuration and local secrets

The Lobby host reads configuration through the standard ASP.NET Core providers.
Repository configuration contains only non-sensitive defaults. Environment
variables override JSON by replacing `:` with `__`, for example:

```text
Lobby__ServiceName=SquadUp.LobbyService
```

`Lobby:ServiceName` is required, limited to 64 safe ASCII characters, and
validated when the host starts. Invalid configuration stops startup before the
service accepts traffic. `OTEL_EXPORTER_OTLP_ENDPOINT` remains optional; when
set, it must be an absolute HTTP or HTTPS URI. Validation errors name the key
and rule but never echo the configured value.

Do not add mandatory database, broker, cache, OAuth, or bot settings until the
host actually consumes them. Each future required setting must use strongly
typed options with startup validation and a negative test.

## User Secrets

The Lobby project has a stable `UserSecretsId`, so local secrets can be stored
outside the repository. User Secrets is a development convenience, not an
encrypted vault. Never use it for production material or commit its backing
files.

When a secret-bearing Lobby setting is introduced, store its development value
with the project explicitly selected:

```bash
dotnet user-secrets set "<configuration-key>" "<local-development-value>" \
  --project src/Lobby/SquadUp.LobbyService.Api
```

Remove a value that is no longer needed:

```bash
dotnet user-secrets remove "<configuration-key>" \
  --project src/Lobby/SquadUp.LobbyService.Api
```

Avoid `dotnet user-secrets list` in recorded terminals, CI, support output, or
AI sessions because it prints values. Never paste connection strings, tokens,
cookies, signing material, invite codes, or the local `.env` contents into
logs, issues, fixtures, screenshots, or prompts.

The Lobby validates internal JWTs with one or more RSA public keys under
`InternalAuthentication:PublicKeys:<key-id>`. Public keys are not credentials,
but their configured key IDs and lifecycle must match the API signer. Never
configure the API private key in Lobby. The remaining Compose credentials stay
in the ignored local `.env`; do not duplicate them in User Secrets until the
Lobby receives the corresponding database, broker, or cache adapter.

`SquadUp.Api` requires `ConnectionStrings:IdentityDatabase`. Store its complete
local value against the API project, using a value obtained from your local
environment rather than a value copied into documentation or shell history:

```bash
dotnet user-secrets set "ConnectionStrings:IdentityDatabase" "<local-value>" \
  --project src/Api/SquadUp.Api
```

The application validates the connection string shape at startup without
opening the database or echoing the value. Schema migration remains a separate,
explicit operation with a distinct deployment identity.

The API also requires the Discord OAuth application credentials
`Discord:ClientId` and `Discord:ClientSecret`. Register the exact callback URI
`https://<api-host>/auth/discord/callback` in the Discord application, then set
the local values without placing them in tracked configuration:

```bash
dotnet user-secrets set "Discord:ClientId" "<discord-application-id>" \
  --project src/Api/SquadUp.Api
dotnet user-secrets set "Discord:ClientSecret" "<local-secret>" \
  --project src/Api/SquadUp.Api
```

The adapter fixes the Discord authorization, token, and user-information URLs
in code and requests only the `identify` scope. It never stores the Discord
access token in an authentication cookie. The five-minute external cookie is
Secure, HttpOnly, SameSite=Lax, and is deleted as soon as the transport-level
callback completes. A successful local-account upsert then creates a separate,
host-only BFF session cookie with a 30-minute absolute lifetime and no sliding
renewal. Mutating cookie-authenticated endpoints require the antiforgery token
obtained from `GET /auth/antiforgery`; `POST /auth/logout` is the first enforced
endpoint.

Multiple API replicas must share a protected Data Protection key ring. Set
`BrowserSession:DataProtectionKeysPath` to an absolute path backed by the same
encrypted, access-controlled volume for every replica. Leaving it unset uses
the host default and is suitable only where session continuity across restart
or replicas is not required.

## Internal JWT signing and validation

The API signs only RS256 tokens for the fixed Lobby audience. Tokens live for
two minutes and contain `iss`, `aud`, `sub`, `jti`, `client_id`, `scope`, and
`token_kind`. A workload token has the API client as `sub`; a delegated token
has the local user ID as `sub` and `token_kind=delegated_user`. The API refuses
unknown audiences, clients, or scopes before signing.

Generate a development-only RSA key outside the repository and store its PEM
only in API User Secrets. The examples deliberately use placeholders so key
material is never copied into documentation or terminal output:

```bash
dotnet user-secrets set "InternalTokens:ActiveKeyId" "<key-id>" \
  --project src/Api/SquadUp.Api
dotnet user-secrets set "InternalTokens:PrivateKeyPem" "<private-key-pem>" \
  --project src/Api/SquadUp.Api
```

Configure the corresponding public PEM at
`InternalAuthentication:PublicKeys:<key-id>` in Lobby. Rotation is additive:
deploy the new public key to Lobby first, switch the API active key second, and
remove the previous public key only after the maximum token lifetime plus clock
skew has elapsed. Lobby startup rejects private PEMs, undersized keys, missing
keys, or malformed issuer/audience/client/scope configuration.

Internal JWTs and private keys are Restricted data: never print, log, cache,
persist in application tables, place in browser storage, or include them in
messages. Production private keys and Data Protection material belong in the
approved secret/key service, with only public verification keys distributed to
Lobby.

The API image remains chiseled and non-root. Its container-only liveness smoke
uses a deliberately unreachable, credential-free connection string and a
runtime-generated synthetic OAuth secret and RSA key to prove that normal
startup neither contacts the database, Discord, nor applies migrations:

```bash
./scripts/test-api-container
```

Production uses workload identity where possible and AWS Secrets Manager for
credentials that cannot use IAM. Environment variables and command-line
arguments are not an approved long-term secret store.
