# 0020. Use OpenTelemetry for observability, with Prometheus, Loki, Tempo and Grafana behind it

- Status: Proposed (by @AlirezaIzadi6; Accepted by @ArmanIzadi99)
- Date: 2026-11-15
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

The services are written in Go and .NET ([ADR 0003](0003-split-services-between-go-and-dotnet.md)) and share one platform library per language ([ADR 0004](0004-use-a-monorepo.md)). One user request touches Envoy, synchronous gRPC calls, outbox rows and Kafka consumers ([ADR 0014](0014-publish-events-through-a-transactional-outbox.md), [ADR 0015](0015-deliver-events-at-least-once-with-idempotent-consumers.md)), so a failure has to be followed across services and across events.

The Taakht MVP built four features in each service: health checks (liveness and readiness), JSON logs with a request id, the request id carried through gRPC metadata and the event envelope, and Prometheus metrics with the same names in both languages. It had Prometheus and Grafana, but no tracing, no alerts and no place to collect and search logs. The README names only Logstash, which moves logs but does not store or search them. The team also wants to practise observability seriously.

## Decision

We will instrument the services with OpenTelemetry, inside the platform libraries, and send metrics, logs and traces through an OpenTelemetry Collector to Prometheus (metrics), Loki (logs) and Tempo (traces). Grafana is the place to look, and Alertmanager sends the alerts. The request id that travels in gRPC metadata and in the event envelope becomes the trace context, so a trace follows a request through Kafka. We will build it in stages: metrics and logs first, then traces, then alerts and runbooks, tested with the fault-injection scenarios of the MVP. Service level objectives wait until there is a running system with real data. The backends are the current choice and can be replaced without changing the services.

## Options considered

1. **OpenTelemetry with Prometheus, Loki, Tempo and Grafana (chosen)** - one vendor-neutral way to instrument both languages; the MVP already uses Prometheus and Grafana; every part is open source and self-hostable. Loki indexes only labels, which in general makes it lighter than a log search engine (not measured here). Cost: several components to run, the instrumentation to write in two platform libraries, and memory on the 8 GB server that has not been measured.
2. **The Elastic stack for logs** - the team already works with it, but a log index would compete for the fixed heap of the Matching Elasticsearch ([ADR 0019](0019-use-elasticsearch-for-the-matching-index.md)), and a separate cluster costs more memory. Not chosen, also to learn a second stack.
3. **Only the MVP's own metric and log code** - it works and needs nothing new, but it cannot follow a request across services and events, and the metric and log conventions stay home-made. Not chosen because tracing across Kafka is part of the goal.
4. **A hosted observability service** - foreign providers are set aside (see the [deployment guideline](../guidelines/deployment.md)).
5. **An all-in-one open-source platform** (for example SigNoz or OpenObserve) - one product for metrics, logs and traces, so fewer parts to run; but each brings its own storage engine, whose memory we have not measured, and assembling the parts teaches more about each pillar. Reasoning, not tried.

Options 2 to 5 were argued, not tried. The chosen stack was tried in a test with the MVP's real logs and metrics (see the evidence below); only synthetic traces and no Alertmanager were part of it.

## Consequences

- Positive: one trace can follow a request through gRPC calls and Kafka events; the same names in both languages; the backends can change without touching the services.
- Negative: more components to run and keep healthy; `request_id` must stay out of the Loki labels (it exhausted the default stream limit in the test); the platform libraries carry the instrumentation; metric labels, log volume, trace sampling and retention need care so the stack does not use more than the server has.
- Follow-ups, in the [observability conventions](../guidelines/observability.md): the alert rules (outbox backlog, consumer lag, dead-letter rows, a swap stuck waiting for the lock), retention and sampling, and where the stack runs (the same node or another one), decided by measuring its memory.

## Evidence (the MVP, one laptop)

- Logs of all four services reached Loki through the Collector with the labels `service` and `level`, and one `request_id` could be followed across them with one query (32 lines in 0.02 s), asynchronous consumers included. Prometheus showed the MVP metrics; Grafana queried all three backends; synthetic traces reached Tempo; an alert rule on the outbox age fired.
- The five containers together used about 270 MB at idle and about 600 MB on average (700 MB at the peak) at 2,000 log lines per second, with no drops. Details, problems found in the MVP and limits: [evidence](../evidence/observability-stack.md).

## References

- Taakht MVP: `docs/architecture/mvp-architecture.md`, "Operability"
- [open-items.md](../open-items.md), item 22
