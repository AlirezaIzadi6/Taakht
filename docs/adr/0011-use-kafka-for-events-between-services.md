# 0011. Use Apache Kafka for events between services

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Services ([ADR 0001](0001-build-the-system-as-microservices.md)) talk to each other over gRPC for requests and publish state changes as events ([ADR 0005](0005-use-grpc-for-requests-and-events-for-state-changes.md)), which needs a broker. The decision was taken on 2026-09-25 and is recorded in the [decisions log](../architecture/architecture-decisions-log.md); this ADR writes it down with its reasons.

- Almost all asynchronous messages are domain events, not commands: about 15 events against one real command at the time.
- Events have several consumers. Ad events go to Matching, Deal and Communication, and the order of versions per Ad matters.
- Matching's index must be rebuilt from the Ad events after a restart.

## Decision

We will use Apache Kafka as the broker for domain events between services, with the aggregate id as the partition key so events of one aggregate stay in order. Kafka is not a source of truth: each service's database is, and a compacted topic is only a replayable feed (for example to rebuild Matching's index).

## Options considered

1. **Apache Kafka (chosen)** - ordering per key, several independent consumers, replay and compacted topics. Secondary reasons, stated openly: the common ecosystem and the learning goal. Cost: memory overhead on a single node and more to operate.
2. **RabbitMQ** - strong in routing, acknowledgements, retries and per-message dead-letter queues, and suited to a command queue. Replay and per-key ordering exist only with Streams or Super Streams, which is not its native model.
3. **NATS JetStream** - lighter, and one tool for sync and async. The decisions log records only that its ecosystem is smaller, which was not measured.
4. **Redis Streams** - Redis is not in the stack, so it would be a new component whose memory would hold the event history, which limits retention and replay; per-aggregate ordering would have to be built by sharding streams by hand, where Kafka gives it with the partition key. Reasoning, not tried.
5. **Postgres as a queue** - no new infrastructure and fine for a simple command queue, but several independent consumers per event, an offset per consumer, retention, replay and a compacted feed (needed to rebuild Matching's index) would all be ours to build. Reasoning, not tried.

## Consequences

- Positive: per-aggregate ordering through the partition key; several consumers per event; replay.
- Negative: the broker does not order events across different aggregates, so the lock and index consistency are not solved by it and need their own mechanism; memory overhead on one node; Kafka is one more component to run.
- Follow-ups, each its own decision: the event format is ADR 0012, the transactional outbox is ADR 0014, delivery semantics are ADR 0015, and the partition count and the topic layout are still open. Domain timeouts (negotiation expiry, payment deadlines) use a scheduler inside the service, not the broker. Topic names, retry and dead-letter handling and client settings go in a convention document.

## References

- [Decisions log](../architecture/architecture-decisions-log.md), "Message broker: Kafka"
- [Evidence: Kafka events in Protobuf](../evidence/kafka-events.md) (a single-node KRaft broker worked with a Go producer and .NET consumers). The Taakht MVP ran Kafka through twelve fault-injection scenarios (restarts, a 15 s and a 70 s outage, replayed events) with Go and .NET services (`docs/testing/chaos-test-results.md` in `Taakht-MVP`).
- The comparison with RabbitMQ and NATS comes from the discussion in the log; nothing was benchmarked.
