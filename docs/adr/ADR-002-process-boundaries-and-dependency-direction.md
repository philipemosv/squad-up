# ADR-002: Process boundaries and dependency direction

- Status: Accepted
- Date: 2026-09-01
- Decision owners: Squad-Up maintainers

## Context

Squad-Up is both a product prototype and a deliberate distributed-systems
learning project. It needs boundaries that expose messaging, consistency, and
operational trade-offs without splitting every use case into an independently
deployed service.

Business rules must remain testable without ASP.NET Core, Entity Framework,
MassTransit, Redis, or the Discord SDK. At the same time, the architecture must
remain pragmatic and avoid interfaces or projects that do not protect a real
boundary.

## Decision

Use four deployable processes in the reference architecture:

1. `SquadUp.Api` for the public edge, identity, and profile modules.
2. `SquadUp.LobbyService` for lobby lifecycle and concurrency.
3. `SquadUp.MatchOrchestrator` for the durable match saga.
4. `SquadUp.DiscordIntegration` for Discord provisioning and reconciliation.

Identity and Profile remain modules inside `SquadUp.Api` initially. They may be
extracted only when ownership, scaling, deployment cadence, or security creates
a measurable need.

Inside each bounded context, use this dependency direction:

```text
Domain <- Application <- Infrastructure <- Host
```

- **Domain** contains business rules and depends only on the base class library
  and deliberately approved primitives.
- **Application** coordinates use cases and defines ports needed by those use
  cases.
- **Infrastructure** implements persistence, messaging, cache, and external
  integrations.
- **Host** is the composition root and starts the executable process, such as an
  HTTP API or worker.

`SquadUp.Contracts` contains versioned integration messages and must not depend
on Domain, Entity Framework, MassTransit, or a host. A bounded context must not
reference another context's Infrastructure project.

Do not create a shared `SquadUp.Infrastructure` project. Share only stable,
context-independent primitives through a small building block with a concrete
consumer in more than one context.

Enforce the allowed project references with executable architecture tests.

## Alternatives considered

### Start as a single project

This has the lowest initial ceremony, but it does not protect the domain from
framework dependencies and makes later extraction harder to demonstrate.

### Use a modular monolith as the only deployable process

This reduces operational cost and would be a reasonable product-first choice.
It was not selected for the reference architecture because the learning goals
explicitly include asynchronous messaging, durable orchestration, independent
failure, and transport parity.

### Create a service per entity or use case

This maximizes deployment independence but creates excessive operational and
consistency overhead. Process boundaries should follow cohesive business
capabilities, not database tables.

### Share one Infrastructure project

This reduces short-term duplication but couples bounded contexts through
persistence mappings, vendors, and deployment concerns. It would make the
shared project a hidden integration point.

## Consequences

### Positive

- Business rules can be tested without starting infrastructure.
- External technology choices remain at replaceable boundaries.
- Process and data ownership are explicit.
- Invalid project-reference directions fail an automated test.
- Extraction or consolidation decisions have visible seams.

### Negative

- The solution contains more projects and mapping code.
- Each host needs explicit dependency composition.
- Teams must resist adding abstractions that do not protect a real boundary.
- Cross-context workflows require contracts and eventual-consistency design.

## Enforcement

- `SquadUp.ArchitectureTests` checks allowed direct project references.
- Domain projects do not reference ASP.NET Core, EF Core, MassTransit, Redis,
  or Discord packages.
- Reviews reject cross-context Infrastructure references.
- Exceptions require a new or superseding ADR with a removal or migration plan.

## Revisit when

- Two modules require meaningfully different scaling or security controls.
- Deployment coupling causes measurable delivery or reliability problems.
- Operational cost outweighs the learning or isolation benefit.
- A shared primitive has multiple concrete consumers and stable semantics.
