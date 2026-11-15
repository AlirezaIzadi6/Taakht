# Evidence: memory of the Taakht MVP stack

Date: 2026-11-15. Backs the [deployment guideline](../guidelines/deployment.md). The MVP is the separate `Taakht-MVP` build.

## What was measured

The MVP stack on a Windows 11 laptop with 16 GB of RAM: Go and .NET services as host processes (working set), Postgres, Kafka and Envoy in Docker (`docker stats`). Idle was sampled about six minutes after start; the peak is during one run of the happy-path demo (publish, match, three negotiations, agreement, lock, locker fees, completed).

| Component | Idle (MB) | Peak (MB) |
|---|---|---|
| ad (Go) | 22.5 | 25.1 |
| matching (Go) | 22.7 | 24.1 |
| negotiation (.NET 10, Development mode) | 91.7 | 116.9 |
| swap (.NET 10, Development mode) | 95.5 | 110.7 |
| Postgres 17 | 114.5 | 121 |
| Kafka 3.9 (KRaft, JVM heap capped at 1 GB) | 366 | 466 |
| Envoy 1.34 | 24 | 29 |
| Total | about 737 | about 893 |

## Caveats

- The four services ran as Windows host processes, not Linux containers, and the .NET services in Development mode with default GC settings.
- Kafka's resident memory can grow towards 1 GB and more with traffic and log segments.
- Postgres held almost no data; one small demo flow; single instances; no Elasticsearch, Redis or observability stack; no kernel, Docker daemon or agents counted.
- Memory under load was not measured, and neither were the chaos or load tests.

## A startup problem found while measuring

On a fresh Kafka the swap service stayed not ready: its consumer subscribes to the topic `negotiation.events`, which did not exist until the first negotiation event. It recovered by itself once the demo produced that event. For a deployment, create the topics before the services start.
