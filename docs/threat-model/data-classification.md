# Data classification and handling baseline

- Status: Initial baseline
- Date: 2026-09-01
- Scope: Squad-Up pilot architecture
- Review owners: Squad-Up maintainers

## Purpose

Classification answers two questions before data is collected: how harmful
would unauthorized disclosure or modification be, and which systems are
allowed to handle it? It applies to production data and to every copy in
messages, caches, logs, traces, backups, exports, DLQs, screenshots, fixtures,
support tools, and AI prompts.

Classification is based on content, not storage location. An identifier remains
confidential when copied from PostgreSQL into a log or message.

## Levels

| Level | Meaning | Examples | Minimum handling |
| --- | --- | --- | --- |
| Public | Deliberately approved for anyone to read. | Public game catalog, published documentation, intentionally public lobby summary fields. | Integrity controls; explicit publication decision. |
| Internal | Operational information not intended for public release, with limited individual impact. | Service names, non-sensitive configuration, aggregate metrics, deployment version, internal queue names. | Authenticated access, least-privilege write, bounded retention in artifacts/logs. |
| Confidential | Personal, pseudonymous, or business data whose disclosure can identify, profile, or affect a user or expose system behavior. | Local user ID, Discord user ID, display name, profile, rank, region, timezone, lobby membership, IP/user agent when collected, channel ID, audit events. | Need-to-know access, encryption in transit and at rest, data minimization, retention/deletion policy, no production values in source or fixtures. |
| Restricted | Credentials or security material that can directly grant access, impersonate an identity, or expose protected resources. | Password hash, session cookie, OAuth code, Discord access/refresh token, refresh token, bot token, signing/private keys, database credentials, connection strings with credentials, invite code, cloud access token. | Secret store or purpose-built hashed storage, never log/message/cache/prompt, narrowly scoped access, rotation/revocation, no repository or CI artifact copies. |

The highest classification of any field determines the handling of a combined
payload unless sensitive fields are irreversibly removed. Pseudonymization
reduces direct identifiability but does not make Discord IDs or stable local IDs
anonymous.

## Inventory and allowed flows

| Data | Class | System of record / owner | Allowed secondary use | Prohibited use |
| --- | --- | --- | --- | --- |
| Game and rank catalog | Public | Lobby | API responses and cache | Client-controlled rank ordinal or verification state |
| Local user ID | Confidential | Identity/Profile | Audience-limited internal JWT subject, lobby ownership, audit correlation | Public metric label or unrestricted export |
| Discord user ID | Confidential | Identity/Profile | Minimum lobby participant snapshot and Discord provisioning contract when required | Authorization by itself, public logs, analytics label |
| Display name / nickname | Confidential by default | Identity/Profile | User-approved profile response and minimum match participant snapshot | Treating it as a stable identity or logging full payloads |
| Profile, games, rank, region, timezone | Confidential | Identity/Profile | Authorized profile/lobby matching use cases; minimized projections | Full entity serialization, unrelated analytics, production fixtures |
| Lobby ID / Match ID | Internal alone; Confidential when joined to a person | Lobby / Orchestrator | Correlation in restricted operational logs and contracts | Public high-cardinality metric labels |
| Lobby membership and ownership | Confidential | Lobby | Authorized lobby views and minimized completion event | Cross-context SQL access, public cache without authorization |
| Discord guild/channel ID | Confidential | Discord Integration | Provisioning state, reconciliation, restricted operations view | Public logs or disclosure to unrelated users |
| Audit event | Confidential | Owning service / audit sink | Security investigation with actor/action/target/result | Credential, request body, or token capture |
| OAuth authorization code and correlation value | Restricted | Ephemeral API flow | One server-side exchange/validation | Persistence after use, logs, URLs retained by analytics |
| Browser session cookie | Restricted | API/Data Protection | Browser authentication only | JavaScript access, internal service token, logs |
| Internal JWT | Restricted while valid | API issuer | Exact service audience and scope | Browser storage, another service audience, logs/messages |
| Future refresh token | Restricted | Identity | Hashed token verifier, family/revocation metadata | Plaintext database/log value or reuse after rotation |
| Discord user OAuth token | Restricted | API only if a feature requires it | Narrow Discord call with consent and scope | Squad-Up authorization, integration event, default retention |
| Discord bot token | Restricted | Discord Integration | Discord bot API only | API/Lobby/Orchestrator access, local files, prompts |
| Signing private key | Restricted | Token issuer | Signing approved internal tokens | Distribution to token consumers or source control |
| Public signing key / JWKS | Public or Internal by exposure | Token issuer | Token signature validation | Treating possession as caller authentication |
| Database/cloud credentials | Restricted | Secret store/IAM | Exact workload or migration operation | Shared roles, source, logs, Terraform plan output |
| Discord invite code/URL | Restricted while valid | Discord Integration | Delivery to intended participants, then expiration | Events, logs, telemetry, permanent storage without requirement |
| Message/DLQ body | Highest field contained | Owning producer/consumer | Intended consumer and restricted recovery tooling | General log ingestion, unaudited bulk replay |
| Logs and traces | Internal by default; Confidential if IDs included | Observability pipeline | Debugging, reliability, security investigation | Restricted data, full request/message bodies, unbounded retention |
| Synthetic test data | Internal | Test suite | Local development and CI | Reusing identifiers or payloads copied from real users |

## Handling rules by lifecycle

### Collect

- Collect only fields required by a named use case.
- Request the Discord `identify` scope initially; new scopes require a consent
  and threat-model review.
- Do not collect email, direct messages, payment data, or precise location in
  the pilot.
- Validate allowed values, lengths, and counts before persistence or forwarding.

### Store and access

- Encrypt Confidential and Restricted data in transit and at rest.
- Use separate least-privilege database users and schemas for each bounded
  context; no cross-context SQL joins.
- Store refresh-token verifiers as hashes if refresh tokens are ever introduced.
- Store application secrets outside the repository: User Secrets locally and a
  managed secret store with workload identity in AWS.
- Backups and exports inherit the highest classification of their content and
  require equivalent access controls and deletion processes.

### Transfer and cache

- Contracts and DTOs use explicit field allowlists rather than serializing
  persistence entities.
- Commands and events contain only what the consumer needs. They never contain
  session cookies, OAuth codes, access/refresh tokens, bot tokens, permanent
  invite URLs, email, or connection strings.
- Cache only minimized projections. Do not cache credentials, authorization
  decisions, refresh-token state, seat reservations, or the bot token.
- Use TLS for external and cross-host traffic and an authenticated, authorized
  identity at each receiving service.

### Log and observe

- Never record Authorization headers, cookies, OAuth query codes, tokens,
  passwords, connection strings, signing material, invite codes, or full
  request/message bodies.
- Allowlist structured fields. Sanitize control characters and enforce the
  classification accepted by the destination logging system.
- IDs may appear only where operationally necessary and access is restricted;
  never use user, lobby, or match IDs as metric labels.
- Audit privileged actions with actor, action, target, result, timestamp, and
  correlation ID, not the authenticating credential.

### Retain and delete

Before real-user data is accepted, every persistent table, cache, log group,
queue/DLQ, backup, and artifact must have an owner and an approved retention
period. Until that review:

- use synthetic data in local development, CI, and the AWS sandbox;
- keep cache TTLs short and message/DLQ retention bounded;
- keep sandbox logs and CI artifacts short-lived;
- do not promise account deletion while undisclosed copies remain unmanaged.

Deletion must cover owned database records and documented derived copies. Some
security/audit records may need separate justified retention and access rules;
they must not retain credentials or unnecessary profile fields.

## Required review evidence

For every new field or copy, the pull request identifies:

1. collection purpose and owning bounded context;
2. classification and authorized readers/writers;
3. DTO, message, cache, log, backup, and analytics propagation;
4. retention, deletion, and failure/DLQ behavior;
5. negative tests for unauthorized access and excess disclosure;
6. redaction evidence if the field can reach diagnostics.

Changes involving Confidential or Restricted data also update the
[threat model](README.md). Real-user cloud processing requires a dedicated
privacy and regional review; this engineering baseline is not legal advice or
proof of LGPD compliance.

## Sources

- [OWASP Logging Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html)
- [OWASP API Security Top 10 (2023)](https://owasp.org/API-Security/editions/2023/en/0x11-t10/)
- [OWASP Threat Model Library](https://owasp.org/www-project-threat-model-library/)
