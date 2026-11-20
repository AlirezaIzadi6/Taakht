# Service conventions (draft)

Conventions every service follows, so that the Go and .NET services look the same from the outside. Not an ADR; cheap to change. Decided 2026-11-18.

## Ports

Each service has its own fixed ports, so several services can run on one laptop outside Docker without clashing. Inside Docker Compose, Envoy reaches a service by its container name.

| Service | gRPC | HTTP (health and metrics) |
|---|---|---|
| ad | 9101 | 8101 |
| matching | 9102 | 8102 |
| deal | 9103 | 8103 |
| reputation | 9104 | 8104 |
| communication | 9105 | 8105 |

The mocks of external Providers live in `src/mocks/<name>/` and have ports of their own:

| Mock | gRPC | HTTP (health and metrics) |
|---|---|---|
| taxonomy | 9201 | 8201 |
| delivery | 9202 | 8202 |
| notification | 9203 | 8203 |
| auth | 9204 | 8204 |

## Identifiers

- Categories, neighbourhoods and the named demo users have stable readable keys (a slug such as `electronics`); the names, in Persian and English, are separate fields, so a rename never changes an id.
- The other seeded users follow a pattern (`u0001`, `u0002`, ...).
- An id never depends on an outside source such as OSM.

## Environment variables

The same names in every service, without a service prefix, because each service has its own environment.

| Variable | Meaning |
|---|---|
| `GRPC_ADDR` | gRPC listen address, for example `:9101` |
| `HTTP_ADDR` | health and metrics listen address, for example `:8101`; bound on all interfaces so Prometheus can reach it from another container |
| `DATABASE_URL` | the service's own PostgreSQL database, with a role of its own that only it can connect with (in development `postgres://ad:ad@localhost:5432/ad`) |
| `KAFKA_BROKERS` | Kafka bootstrap addresses |
| `LOG_LEVEL` | log level |
| `INTERNAL_TOKEN` | shared-secret guard for system identities ([ADR 0010](../adr/0010-authenticate-service-to-service-calls.md)) |
| `OTEL_SERVICE_NAME` | the service name (`ad`, `deal`, ...), so traces and metrics are not recorded as `unknown_service` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | the standard OpenTelemetry endpoint, read by the OpenTelemetry libraries |

## Go

- One module per service, and one for `libs/goplatform` (ADR 0004).
- A service finds the platform library with a `replace` directive in its own `go.mod` (`replace ... => ../../libs/goplatform`). A `go.work` file is not used: `go mod tidy` ignores it and would try to download the library. Not tried in this repository yet.
- Migrations use goose: numbered SQL files embedded in the service and applied by the platform library when the service starts. Not tried yet. .NET services choose their own tool.
