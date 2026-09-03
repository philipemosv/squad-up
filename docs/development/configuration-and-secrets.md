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

The current Lobby skeleton consumes no secret-bearing application setting.
Compose credentials remain in the ignored local `.env`; do not duplicate them
in User Secrets until the Lobby receives the corresponding database, broker,
or cache adapter.

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

The API image remains chiseled and non-root. Its container-only liveness smoke
uses a deliberately unreachable, credential-free connection string to prove
that normal startup neither contacts the database nor applies migrations:

```bash
./scripts/test-api-container
```

Production uses workload identity where possible and AWS Secrets Manager for
credentials that cannot use IAM. Environment variables and command-line
arguments are not an approved long-term secret store.
