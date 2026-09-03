# Squad-Up repository instructions

These instructions apply to the entire repository. Add a nested `AGENTS.md`
only when a directory has genuinely different rules; do not duplicate this
baseline.

## Before editing

- Read `docs/session-handoff.md` first and verify its recorded branch and commit
  against `git status` and `git log`. Treat the repository as authoritative if
  the handoff is stale, then refresh it at the next milestone.
- Read the relevant ADRs in `docs/adr/`, the initial threat model in
  `docs/threat-model/`, and the affected section of `plan.md`.
- State the business invariant, trust boundary, failure paths, and assumptions
  before changing authentication, concurrency, messaging, migrations, or
  external effects.
- Keep each change within one bounded context and one verifiable outcome unless
  a cross-context contract change is the explicit objective.
- Preserve unrelated user changes. Do not broaden scope merely to clean up
  nearby code.

## Architecture

- Preserve `Domain <- Application <- Infrastructure <- Host` dependency
  direction.
- Domain contains business rules and must not depend on ASP.NET Core, Entity
  Framework, MassTransit, Redis, Discord, or host configuration.
- A bounded context owns its data and never reads another context's tables.
- Integration messages live only in `SquadUp.Contracts`, use simple
  serializable types, and are explicitly versioned.
- Do not introduce a shared Infrastructure project or abstraction without at
  least two concrete consumers and a stable boundary.
- Keep technology selection and composition in Infrastructure and Host.

## Distributed-systems safety

- Assume messages can be duplicated, delayed, redelivered, and reordered.
- Never claim exactly-once delivery end to end.
- Every consumer documents its idempotency key, transaction boundary, retry
  policy, and external side effects.
- Publish database-coupled messages through the transactional outbox; do not
  publish after an unrelated commit.
- Do not add automatic retries to unsafe HTTP operations without idempotency or
  reconciliation for ambiguous outcomes.
- Treat broker acknowledgement or SQS deletion as the final step after the
  required local work succeeds.

## Data, migrations, and security

- Follow `docs/threat-model/data-classification.md` for every new field, DTO,
  message, cache entry, log, trace, backup, and fixture.
- Never read, print, commit, or place real secrets in prompts, logs, fixtures,
  source, shell output, or CI artifacts.
- Treat Discord and stable local identifiers as pseudonymous Confidential data;
  treat tokens, cookies, credentials, and invite codes as Restricted.
- Add negative authorization tests for every protected endpoint, including a
  different authenticated user attempting to access the resource.
- Bind request DTOs explicitly. Never let clients set roles, ownership,
  verification state, concurrency versions, or server-managed fields.
- Do not run EF migrations automatically at production application startup.
- Production schema changes follow expand/backfill/contract. Do not create a
  destructive or contract migration without explicit human approval and a
  rollback/compatibility plan.

## Implementation style

- Prefer the smallest vertical slice that proves behavior over speculative
  frameworks or unused interfaces.
- Use nullable reference types, async I/O, propagated `CancellationToken`, and
  existing central package management.
- Return RFC 9457 Problem Details at HTTP boundaries without stack traces or
  sensitive payloads.
- Use structured logs with stable event names. Do not use user, lobby, match, or
  message IDs as metric labels.
- Add dependencies only for a demonstrated requirement, verify their current
  license, and update the lock files in the same change.

## Verification

- Run focused tests for edited behavior, including negative and failure paths.
- Then run the repository gates:

  ```bash
  dotnet restore SquadUp.slnx --locked-mode
  dotnet format SquadUp.slnx --no-restore --verify-no-changes
  dotnet build SquadUp.slnx --configuration Release --no-restore -p:ContinuousIntegrationBuild=true
  dotnet test SquadUp.slnx --configuration Release --no-build --no-restore
  ```

- Messaging changes require duplicate, reorder, retry-exhaustion, and
  crash-window tests proportional to the behavior changed.
- Concurrency or idempotency bug fixes require a test that fails before the fix.
- Migration changes require generated SQL review and compatibility evidence.
- Report commands run, their results, and any check not run with the reason.

## Session handoff

- Treat a validated, committed, and pushed milestone as a natural session
  boundary.
- After each milestone push, update `docs/session-handoff.md` with the completed
  outcome, pushed commit, verification results, assumptions or limitations, and
  the next exact plan item. Commit and push that handoff update separately.
- A handoff-only commit does not trigger another handoff update; this exception
  prevents an infinite sequence of documentation commits.
- Keep the current-state section concise and append one compact row to the
  milestone history. Never place secrets, tokens, cookies, invite codes,
  connection strings, or real user/provider identifiers in the handoff.
- At each milestone, remind the user that starting a fresh Codex session can
  reduce context usage.
- Before suggesting a fresh session, provide a concise handoff prompt containing
  the completed milestone, latest commit, verification status, and next planned
  step.
- Keep durable project knowledge in the repository rather than relying only on
  conversation history.

## Human approval boundaries

Do not perform these actions without explicit human approval:

- apply Terraform or change production/cloud resources;
- rotate or retrieve real secrets;
- replay a DLQ or trigger an external side effect with real users;
- apply a destructive migration or contract phase;
- publish a package, release, image, or deployment;
- weaken an accepted ADR, security control, required CI check, or test to make a
  change pass.

## Code Review Rules

- Flag correctness, security, authorization, data exposure, concurrency,
  idempotency, contract compatibility, migration safety, and observability
  defects before style preferences.
- For each finding, identify the file/line, a concrete failure scenario, impact,
  and the smallest safe correction with a proving test.
- Reject cross-context database access, reversed project dependencies,
  unversioned integration contracts, blind retries of external writes, and
  messages published outside the required outbox transaction.
- Reject secrets or Restricted data in code, configuration, logs, telemetry,
  messages, fixtures, screenshots, prompts, plans, and artifacts.
- Do not request cosmetic changes already enforced by the formatter or analyzer.
