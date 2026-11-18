# Deployment (draft)

How the system is run for now. Not an ADR: it is cheap to change. The choice of a hosting platform, Kubernetes or several nodes will be an ADR when it is made ([open-items.md](../open-items.md), item 23).

## Environments

- **Development:** each developer runs the whole system on a laptop with Docker Compose.
- **Shared testing and demos:** one domestic server with 8 GB of RAM, billed by the hour and powered on only when needed. Its disks persist while it is off, so Postgres, Kafka and the Elasticsearch index come back as they were.

## Rules for one node

- Only Envoy publishes a port; everything else stays on the internal network. The isolated-network assumption of [ADR 0010](../adr/0010-authenticate-service-to-service-calls.md) holds only on one node, so the node must not be exposed beyond the gateway. On a laptop `compose.override.yaml` also publishes Postgres and Kafka, on the loopback address only, for services run from an IDE; on the server run the base file only (`make up-server`).
- PostgreSQL is one instance with one database per service ([ADR 0013](../adr/0013-use-postgresql-as-the-service-database.md)).
- Kafka and Elasticsearch are single nodes with their JVM heaps capped by hand: Kafka at 1 GB, Elasticsearch at 1 to 1.5 GB (see the measurements below).
- Every service runs as one instance; there are no replicas and no database replication. Data is protected by backups.
- With one Envoy, its per-instance rate limit is the real limit.

## The 8 GB server

Measured on a laptop, not on this server, with small data ([MVP stack memory](../evidence/mvp-stack-memory.md), [Elasticsearch load test](../evidence/elasticsearch-matching-load-test.md)). Memory under heavier load will be higher.

| Component | Measured |
|---|---|
| Envoy | about 25 to 30 MB |
| Kafka, single node, heap capped at 1 GB | about 370 to 470 MB (it can grow towards 1 GB or more) |
| PostgreSQL, almost no data | about 115 to 120 MB |
| a Go service (Ad, Matching) | about 22 to 25 MB |
| a .NET service (Negotiation, Swap), Development mode | about 90 to 120 MB |
| Elasticsearch container, heap 1 to 1.5 GB, up to 1,000,000 synthetic Ads | about 2.0 to 2.2 GiB |

The MVP stack without Elasticsearch came to about 0.9 GB at peak, so with Elasticsearch the services need roughly 3 GB, which leaves about 5 GB of the 8 GB for the operating system, Docker, backups and an observability stack. This is an estimate from the measurements above, not a measurement of the whole system. The Communication and Reputation services were not in the MVP.

Kafka keeps its data in a volume: `make down` keeps it and `make reset` deletes it.

Create the Kafka topics before the services start: on a fresh Kafka, a consumer subscribed to a topic that does not exist yet stays not ready until the first event is produced.

Keep the load generator (k6) off the server. No availability while the server is off, and none if it fails.

## Later experiments, not decided

A domestic cloud or PaaS (for example Liara) with its managed services, several nodes (a friend's server, a rented one, a PaaS, the team's computers), replicas, and Kubernetes (tried on local clusters first). Foreign clouds such as Azure, AWS or Google Cloud are set aside: the team judged them too expensive, and not reliable because access to them could be cut off. Several nodes break the isolated-network assumption above: services would need an encrypted network between sites or mTLS.

## Observability on the node

The stack of [ADR 0020](../adr/0020-use-opentelemetry-for-observability.md) was tried with the MVP's real logs and metrics ([evidence](../evidence/observability-stack.md)): the OpenTelemetry Collector, Prometheus, Loki, Tempo and Grafana together used about 270 MB at idle and about 600 MB on average (700 MB at the peak) at 2,000 log lines per second, without Alertmanager and with synthetic traces. With the system's roughly 3 GB, that is about 3.7 GB of the 8 GB, so it can run on the same server. The MVP's real log rate is far lower than the test load.

- Create the Kafka topics before the services start (see above); the readiness check of a service that cannot join its group is logged at error level, which would pollute an error-rate alert.
- A Prometheus in a container cannot scrape the services' health ports while they are bound to loopback: bind them to the container network (`HEALTH_BIND`) or run Prometheus on the host network.
- Loki labels are only `service` and `level`; `request_id` is never a label.
