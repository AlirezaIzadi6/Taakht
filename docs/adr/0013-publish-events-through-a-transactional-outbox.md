# 0013. Publish events through a transactional outbox with a polling relay

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

A service changes its own database and must publish an event about the change to Kafka ([ADR 0010](0010-use-kafka-for-events-between-services.md)). Writing to the database and to Kafka are two separate actions: a crash between them either loses the event or announces a change that did not happen. Deal also needs events inside the service, between its two modules.

## Decision

Each service will write its events to an outbox table in the same local transaction as the state change. A relay inside the service reads the table by polling, publishes the rows to Kafka and marks them as sent. In Deal, rows meant for the other module are handled in-process from the same table. Replacing polling with change data capture later does not change the table.

## Options considered

1. **Outbox with a polling relay (chosen)** - the event is stored atomically with the change, and the relay is a background worker in the service, with no extra component. Cost: a polling delay, some load on the database, and a relay restart can publish a row twice, so consumers must be idempotent.
2. **Outbox with change data capture (Debezium)** - no polling load and lower latency, but Kafka Connect and a connector have to be run and watched, which is extra weight on a single-node demo. Not chosen now; the same table keeps it possible later.
3. **Publish to Kafka directly after the commit** - the simplest, but a crash between the commit and the publish loses the event, and publishing before the commit can announce a change that rolls back.

Options 2 and 3 were argued from the documentation, not tried.

## Consequences

- Positive: no event is lost between the database and Kafka; one pattern in every service; Deal uses the same table for both its modules and for other services.
- Negative: events reach consumers after a polling delay; sent rows must be cleaned up; duplicates are possible.
- Follow-ups: the polling interval and the clean-up of sent rows go in the conventions.

## References

- [Decisions log](../architecture/architecture-decisions-log.md), "Message broker: Kafka" (the outbox relay was left to this decision)
- [Architecture overview](../architecture/architecture-overview.md), "Communication between microservices"
