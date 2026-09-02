# Local platform

The local platform supplies infrastructure that application services will use:

- PostgreSQL 17 stores each bounded context's owned data;
- RabbitMQ is the local asynchronous message transport;
- Redis 7 provides short-lived distributed cache and lease primitives;
- OpenTelemetry Collector receives telemetry and routes traces;
- Jaeger stores and displays development traces.

Think of Compose as the wiring diagram for a small laboratory. Each service is
a separate appliance, the Compose network is their private workbench, and only
the explicitly listed ports are connected to the host machine.

## Start and stop

From the repository root, run:

```bash
./scripts/dev-up
```

On its first run the script creates an ignored `.env` file with random,
local-only passwords and restrictive file permissions. Never commit that file.
The containers wait for their health checks before the command returns.

Stop the containers without deleting their data:

```bash
./scripts/dev-down
```

Named volumes preserve PostgreSQL, RabbitMQ, and Redis state between runs. To
avoid accidental data loss, the normal shutdown script deliberately has no
volume-deletion option.

## Local endpoints

| Component | Endpoint | Purpose |
| --- | --- | --- |
| PostgreSQL | `localhost:5432` | Database connections |
| RabbitMQ | `localhost:5672` | AMQP connections |
| RabbitMQ management | <http://localhost:15672> | Inspect local exchanges and queues |
| Redis | `localhost:6379` | Cache connections |
| OTLP/gRPC | `localhost:4317` | Send telemetry to the collector |
| OTLP/HTTP | `localhost:4318` | Send telemetry to the collector |
| Collector health | <http://localhost:13133> | Collector readiness |
| Jaeger | <http://localhost:16686> | Search and inspect traces |

RabbitMQ's local username is `squadup`. Passwords live only in `.env` and in
the environment or temporary memory of their local containers.

All published ports bind to `127.0.0.1`, so the services are not intentionally
exposed to other devices on the network. These credentials and settings are for
development only and must never be reused in a shared or production system.

## Troubleshooting

Inspect current state and logs with:

```bash
docker compose ps
docker compose logs --tail=100 <service-name>
```

Common failure paths are a stopped Docker daemon, a host port already in use,
or a manually edited `.env` with missing values. `./scripts/dev-up` reports the
failing service; inspect that service's logs without pasting `.env` contents.

Tests will use isolated Testcontainers rather than depending on the persistent
state in this developer Compose stack.

## Optional profiles

Profiles are optional groups of services. They are like extra laboratory
benches that stay powered off during the normal development loop.

Start the AWS emulator, the network fault injector, or both:

```bash
./scripts/dev-up aws
./scripts/dev-up chaos
./scripts/dev-up aws chaos
```

The `aws` profile starts LocalStack at <http://localhost:4566> with only SQS,
SNS, and Secrets Manager enabled. It accepts synthetic local credentials; never
pass real AWS credentials to this container. LocalStack is an emulator and does
not prove complete AWS parity.

The `chaos` profile starts the Toxiproxy API at <http://localhost:8474> and
reserves `localhost:8666` for a future explicitly configured proxy. It starts
with no proxy or external upstream, so enabling the profile alone cannot send a
request to Discord or another external service.

Both services run as non-root users, bind host ports only to `127.0.0.1`, and
are stopped by the normal `./scripts/dev-down` command. LocalStack state uses a
named volume and is preserved between runs.

Pinned optional dependencies:

| Dependency | Version | License |
| --- | --- | --- |
| LocalStack | 4.14.0 | Apache-2.0 |
| Toxiproxy | 2.12.0 | MIT |
