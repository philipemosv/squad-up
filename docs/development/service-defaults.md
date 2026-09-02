# Service defaults

`SquadUp.ServiceDefaults` contains host-level conventions shared by deployable
services. It does not contain business rules and must not be referenced by a
Domain or Application project.

Think of it as a building's standard utility panel: every apartment remains
independent, while electricity, grounding, and safety labels follow the same
conventions.

## What it configures

- RFC Problem Details responses for unhandled HTTP failures;
- sanitized structured JSON logs through Serilog;
- OpenTelemetry HTTP, `HttpClient`, runtime, and custom instrumentation;
- OTLP trace and metric export when an endpoint is configured;
- request correlation through `X-Correlation-ID`;
- `/health/live` and `/health/ready` endpoints.

The Lobby host consumes the defaults in two places:

```csharp
builder.AddSquadUpServiceDefaults();
app.UseSquadUpServiceDefaults();
app.MapSquadUpHealthEndpoints();
```

Registration belongs before `Build`; middleware belongs before application
endpoints.

## Trace ID and correlation ID

A trace ID identifies one distributed execution path and follows the W3C Trace
Context standard. A correlation ID is an application-level identifier that a
caller may use to connect related diagnostics.

The middleware accepts exactly one `X-Correlation-ID` containing at most 64
ASCII letters, digits, dots, underscores, or hyphens. Invalid values are not
reflected; the current trace ID or a generated identifier replaces them. The
accepted value is returned in the response, added to the JSON log scope, and
stored on the trace as `squadup.correlation_id`.

Correlation values may be high-cardinality. They can be searchable log and
trace fields but must never become metric labels.

## Error handling

Unhandled exceptions produce a generic `application/problem+json` response
with an HTTP status and trace ID. The response never includes the exception
message or stack trace.

The sanitized error log records a stable `UnhandledRequestException` event and
the exception type, but not the exception object, message, stack, request body,
headers, cookies, or credentials. OpenTelemetry exception recording is disabled
for automatic HTTP instrumentation for the same reason.

## Local telemetry

The launch profile sets:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
```

When this setting is absent, instrumentation remains registered but no OTLP
exporter is created. An unavailable Collector must not stop request handling;
the exporter retries and drops telemetry according to its bounded queue.

Development samples every trace. Other environments currently use parent-based
10% sampling, as specified by the initial observability plan. OTLP logs are not
enabled because console JSON already has one explicit destination.

Start the platform and API with:

```bash
./scripts/dev-up
dotnet run --project src/Lobby/SquadUp.LobbyService.Api
```

Then inspect traces at <http://localhost:16686>.
