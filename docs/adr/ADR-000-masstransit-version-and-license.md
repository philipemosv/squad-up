# ADR-000: MassTransit version and license baseline

- Status: Accepted
- Date: 2026-09-01
- Decision owners: Squad-Up maintainers

## Context

Squad-Up needs asynchronous messaging, transactional outbox support, durable
saga state machines, retry and redelivery policies, and transport integration
for RabbitMQ locally and Amazon SQS/SNS in AWS.

Implementing this coordination directly on each broker client would add a large
amount of lifecycle, topology, serialization, failure-handling, and testing
code. MassTransit provides these patterns through a consistent .NET programming
model while still requiring transport-specific behavior to be understood and
tested.

MassTransit changed its licensing model between major versions. The `8.5.x`
packages are published under Apache-2.0. Version 9 is a commercial,
source-available product that requires a valid license, including when an
organization qualifies for a discounted license.

At the time of this decision:

- `8.5.10` is the latest stable `8.5.x` release, declares Apache-2.0, and has a
  `net10.0` target.
- `9.2.1` is the latest stable version in the commercial major line.
- The vendor states that organizations below USD 1 million in annual gross
  revenue may qualify for a 100% license discount without commercial support.

Sources:

- [MassTransit 8.5.10 package](https://www.nuget.org/packages/MassTransit/8.5.10)
- [MassTransit 9.2.1 package](https://www.nuget.org/packages/MassTransit/9.2.1)
- [Massient licensing FAQ](https://massient.com/)

## Decision

Use **MassTransit 8.5.10** as the initial messaging framework baseline.

When MassTransit packages are first introduced:

- Pin every MassTransit package to exactly `8.5.10` in
  `Directory.Packages.props`.
- Commit NuGet lock files and restore them in locked mode in CI.
- Reference MassTransit only from Infrastructure or Host projects. Domain,
  Application, and Contracts remain independent of the framework.
- Keep consumers as transport adapters that invoke application use cases.
- Exercise both RabbitMQ and SQS/SNS behavior; a common API does not erase
  broker-specific semantics.

Do not adopt version 9 merely because it is newer. Adoption requires a
superseding ADR, confirmed license eligibility or purchase, secure license-key
handling, and successful compatibility and failure-mode tests.

## Alternatives considered

### Adopt MassTransit 9 immediately

This provides the current commercial line and its ongoing releases. It is not
required for the learning goals, and introducing license qualification and key
management before any messaging code exists adds cost without immediate value.

### Use broker clients directly

`RabbitMQ.Client` and the AWS SDK provide lower-level control and avoid a
framework dependency. They would require Squad-Up to implement and maintain
consumer lifecycle, topology conventions, retry, outbox dispatch, saga
coordination, observability integration, and test infrastructure.

### Build a custom transport abstraction

This can appear to reduce vendor coupling, but usually creates a smaller,
project-specific messaging framework. It also tends to hide important
differences between RabbitMQ and SQS/SNS rather than making them explicit.

## Consequences

### Positive

- The initial line is permissively licensed and compatible with .NET 10.
- The required outbox, saga, retry, and testing capabilities are available.
- One programming model can be exercised across the selected transports.
- Exact versions and lock files make dependency resolution reproducible.

### Negative

- Version 8 is no longer the newest major line and may receive fewer future
  fixes than the commercial line.
- The team owns the risk of remaining on the older line and must monitor
  vulnerabilities and runtime compatibility.
- A future move to version 9 includes code migration, license governance, and
  secret management.
- MassTransit is a substantial framework dependency and does not remove the
  need to understand delivery semantics, topology, or idempotency.

## Upgrade gate

Reassess this decision when any of the following occurs:

- A relevant vulnerability has no patched `8.5.x` release.
- A supported .NET runtime becomes incompatible with the pinned version.
- A version 9 feature or commercial support has measurable project value.
- Squad-Up moves from a learning project toward real production use.

Before upgrading:

1. Confirm and document license terms and eligibility.
2. Store any license key outside the repository using the approved secret store.
3. Upgrade all MassTransit packages together on an isolated branch.
4. Run contract, integration, transport-parity, saga, and fault-injection tests.
5. Create a superseding ADR and update the dependency lock files.
