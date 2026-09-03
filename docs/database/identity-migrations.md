# Identity database migrations

The `SquadUp.Identity` bounded context exclusively owns the PostgreSQL
`identity` schema. `SquadUp.Api` is its only application reader and writer; no
other context may query these tables directly.

## Initial migration

The initial migration is expand-only: it creates a new schema, seven ASP.NET
Core Identity tables, their constraints and indexes, plus the EF migration
history table. It does not alter or read an existing context's objects.

The schema stores these data classes:

| Data | Classification | Handling |
| --- | --- | --- |
| Local user ID, user name, external provider key, roles and claims | Confidential | Identity-only access, encrypted transport/storage, no metric labels or payload logging |
| Password hash, security stamp and user-token value | Restricted when populated | Never logged, messaged, cached, exported, or exposed outside Identity |
| E-mail and phone compatibility columns | Confidential | Nullable and deliberately not collected by the pilot |

Discord login rows store only the provider name, the stable external identifier
and a local-user reference. OAuth codes and Discord access/refresh tokens are
not stored. The `user_tokens` table is part of ASP.NET Core Identity's store but
must not be used for Discord OAuth tokens.

Before real-user data is accepted, account deletion and retention policies must
cover every table in this schema. Tests and local environments use only
synthetic identifiers.

## Compatibility and rollback

- Version `d180257` and earlier do not know this schema and continue running
  while it exists, so the expand migration is backward-compatible.
- The new API requires the expanded schema before identity operations are
  enabled. Normal application startup never runs migrations.
- Rollback deploys the earlier application and leaves the additive schema in
  place. The migration deliberately has no destructive `Down` path. Removing
  the schema requires a separate reviewed migration, explicit human approval,
  and confirmation that no data must be retained.
- The initial migration takes locks only on newly created objects. Later
  migrations require their own lock and N/N+1 compatibility review.

Generate review artifacts from the repository root with the pinned local tool:

```bash
dotnet tool restore
dotnet ef migrations has-pending-model-changes \
  --project src/Api/SquadUp.Identity.Infrastructure \
  --startup-project src/Api/SquadUp.Api
dotnet ef migrations script --idempotent \
  --project src/Api/SquadUp.Identity.Infrastructure \
  --startup-project src/Api/SquadUp.Api \
  --output docs/database/migrations/identity/001_initial_identity.sql
```

Supply `ConnectionStrings:IdentityDatabase` through User Secrets locally or a
secret provider in deployment. Never place the connection string in the
command, repository, generated SQL, logs, or artifacts.

Pinned dependencies used by this slice are ASP.NET Core Identity/EF Core and
the EF Core health check `10.0.11` (MIT), Npgsql EF Core provider `10.0.3`
(PostgreSQL license), and the local `dotnet-ef` tool `10.0.11` (MIT).
