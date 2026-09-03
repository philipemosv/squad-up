# Integration tests

The integration-test fixture starts disposable PostgreSQL, RabbitMQ, and Redis
containers with Testcontainers. It uses random host ports and runtime-generated
credentials, and it never reads the persistent developer Compose environment.

Run the smoke tests from the repository root:

```bash
./scripts/test-integration
```

Docker must be running and accessible to the current user. Testcontainers waits
for each dependency to become ready and removes its containers after the test
class finishes. If fixture startup fails, it attempts the same cleanup before
surfacing the original infrastructure failure.

The smoke tests cross the host-to-container trust boundary with each service's
native protocol. They authenticate and perform a minimal read/write round trip:

- a temporary PostgreSQL table is created, written, and queried;
- a RabbitMQ exclusive auto-delete queue receives and returns one message;
- a Redis key with a one-minute TTL is written, read, and explicitly deleted.

All values are synthetic. Generated credentials exist only in the ephemeral
test process and container configuration; they are not persisted in repository
files or artifacts. Tests must not log connection strings or container
environment variables.

The Testcontainers modules are pinned to `4.14.0` and licensed under MIT. The
protocol clients are test-only dependencies: Npgsql `10.0.3` (PostgreSQL
license), RabbitMQ.Client `7.2.2` (Apache-2.0 or MPL-2.0), and
StackExchange.Redis `3.1.31` (MIT).
