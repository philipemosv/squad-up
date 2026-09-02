# ADR-001: Local and AWS messaging transports

- Status: Accepted
- Date: 2026-09-01
- Decision owners: Squad-Up maintainers

## Context

Squad-Up needs asynchronous commands and events between independently deployed
processes. Development requires a fast, observable local loop, while AWS
production should minimize broker administration and use managed primitives.

The application uses at-least-once delivery. Duplicate and out-of-order
messages are expected operating conditions, not exceptional edge cases. No
broker creates a single transaction across PostgreSQL, messaging, and Discord.

RabbitMQ and Amazon SQS/SNS have materially different models:

| Concern | RabbitMQ | Amazon SQS/SNS |
| --- | --- | --- |
| Delivery | Broker pushes deliveries to subscribed consumers | Consumers poll SQS queues |
| Routing | Exchanges bind messages to queues | SNS topics fan out to subscribed SQS queues |
| Completion | Consumer acknowledgement transfers responsibility | Successful processing deletes the SQS message |
| In-flight work | Prefetch limits unacknowledged deliveries | Visibility timeout temporarily hides received messages |
| Standard ordering | FIFO can be affected by concurrency and redelivery | SQS Standard provides best-effort ordering |
| Duplicate handling | Acknowledgement-based redelivery can repeat work | SQS Standard explicitly provides at-least-once delivery |
| Failed messages | MassTransit error/skipped queues | SQS dead-letter queue and redrive policy |

MassTransit provides a common programming model, but it does not make these
transport semantics identical.

## Decision

Use **RabbitMQ** as the primary local development transport.

- Run it in the local Compose stack with its management interface restricted to
  development.
- Use real RabbitMQ containers in integration tests rather than an in-memory
  transport for transport-sensitive behavior.
- Decide and pin any delayed-redelivery plugin separately before relying on it.

Use **Amazon SQS Standard queues with SNS topics** as the primary AWS transport.

- Send commands directly to the queue owned by their single consumer.
- Publish events to SNS topics and subscribe each interested SQS queue.
- Use SQS Standard by default. Adopt FIFO queues and topics only after a concrete
  ordering requirement, partition/message-group design, throughput analysis,
  and a superseding or amended ADR.
- Configure visibility timeout, retention, redrive policy, and DLQ alarms per
  endpoint through Terraform.

Use **LocalStack SQS/SNS** for transport-parity tests. LocalStack is a local
emulator, not evidence that AWS behavior is identical, so release-critical
topology and failure paths must eventually be tested in an AWS sandbox.

Select the transport in Infrastructure and Host configuration. Domain,
Application, and Contracts must not branch on RabbitMQ or AWS types.

## Message semantics

The system-level contract is:

```text
at-least-once delivery
+ idempotent processing
+ observable reconciliation
```

- Every message has a stable message identifier and correlation identifier.
- Consumers persist deduplication state where repeated effects are unsafe.
- State transitions tolerate older or out-of-order messages.
- The transactional outbox records database changes and publish intent in one
  local transaction; dispatch to the broker remains repeatable.
- Acknowledgement or SQS deletion happens only after the required local work has
  succeeded.
- Poison messages move to an error queue or DLQ after a bounded retry budget.
- Replay is selective, audited, and must not repeat external effects.

Do not claim end-to-end exactly-once processing.

## Logical topology

Use the physical naming convention:

```text
{environment}-{service}-{message-or-endpoint}-v1
```

Commands use imperative names and have one logical owner. Events describe
facts in the past tense and may have multiple subscribers.

In SQS/SNS, publishing a command through a topic introduces unnecessary
forwarding, topology, latency, and request cost. Send commands directly to their
destination queue. Publish events because fan-out is intentional.

## Alternatives considered

### Use RabbitMQ locally and in AWS

This provides closer environment parity and richer broker features. It also
requires capacity, patching, availability, upgrades, monitoring, and recovery
for a long-running broker or a provisioned managed-broker service. The pilot
prefers AWS-native managed queues and topics.

### Use SQS/SNS for every local development loop

This reduces configuration differences, but a cloud-only loop is slower and
costlier, while an emulator still cannot prove complete production parity. We
retain scheduled parity tests instead of making them the fastest inner loop.

### Use only MassTransit's in-memory transport locally

This is useful for focused tests, but it does not exercise durable queues,
acknowledgements, topology, broker outages, redelivery, or dead-letter behavior.
It cannot be the principal development transport.

## Consequences

### Positive

- Developers get a fast local broker and management UI.
- AWS production avoids operating a RabbitMQ cluster.
- SQS queues independently buffer and scale consumers.
- SNS provides explicit event fan-out to durable SQS subscriptions.
- Transport-parity tests make portability claims measurable.

### Negative

- Two transport configurations and topology models must be maintained.
- Local success does not guarantee AWS parity.
- Retry, delayed delivery, ordering, DLQ, and naming behavior need
  transport-specific tests.
- SNS fan-out adds requests, topology, and cost compared with a direct SQS send.
- Standard queues require explicit duplicate and out-of-order handling.

## Enforcement

- RabbitMQ integration tests cover acknowledgements, redelivery, error queues,
  topology, and broker interruption.
- LocalStack parity tests cover SNS fan-out, SQS visibility, redrive, headers,
  naming, and message-size constraints.
- Fault-injection tests duplicate and reorder messages for both transports.
- Infrastructure configuration owns transport-specific code.
- Contracts and business rules remain transport-neutral.
- Runbooks document inspection and selective replay for RabbitMQ error queues
  and SQS DLQs.

## Revisit when

- A business invariant requires ordering that application state cannot handle.
- Measured SNS/SQS cost or latency exceeds the accepted budget.
- Operating one transport everywhere becomes materially cheaper or safer.
- LocalStack behavior diverges from the AWS features required by Squad-Up.
- A new AWS messaging primitive better satisfies the command/event semantics.

## Sources

- [Amazon SQS queue types and delivery guarantees](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-queue-types.html)
- [Amazon SNS fan-out to SQS](https://docs.aws.amazon.com/sns/latest/dg/sns-sqs-as-subscriber.html)
- [RabbitMQ reliability guide](https://www.rabbitmq.com/docs/reliability)
- [RabbitMQ queue ordering](https://www.rabbitmq.com/docs/queues)
- [MassTransit message guidance](https://masstransit.io/documentation/concepts/messages)
