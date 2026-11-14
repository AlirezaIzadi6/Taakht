# 0014. Publish events through a transactional outbox with a polling relay

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

A service changes its own database and must publish an event about the change to Kafka ([ADR 0011](0011-use-kafka-for-events-between-services.md)). Writing to the database and to Kafka are two separate systems: a crash between them either loses the event or announces a change that was rolled back. Deal also needs events inside the service, between its two modules.

The Taakht MVP (a one-week build kept in the separate `Taakht-MVP` repository) implemented and tested this decision in Go and .NET, so the evidence below is from running code, not from reasoning.

## Decision

Each service will write its events to an `outbox` table in the same database transaction as the state change. A relay inside the service reads the table by polling, publishes the rows to Kafka and marks them as sent. Rows get `created_at = clock_timestamp()` and the relay reads them in `created_at, id` order with `FOR UPDATE SKIP LOCKED`, so events of one transaction keep their order. In Deal, rows meant for the other module are handled in-process from the same table. Replacing polling with change data capture later does not change the table.

## Options considered

1. **Outbox table with a polling relay (chosen)** - the event is stored atomically with the change, there is no extra infrastructure, and it is easy to test against a plain database. Cost: up to one poll interval of latency (200 ms in the MVP), a table that grows, duplicates after a crash, and a relay to write once per language.
2. **Produce to Kafka directly after the commit** - the least code, but a crash or Kafka outage between the commit and the produce loses the event with no record that it was owed.
3. **Produce first, then write the row** - the reverse failure: it announces changes that never happened.
4. **Outbox with change data capture (Debezium)** - no polling and lower latency, but Kafka Connect and a connector per database have to be run and watched. Not chosen now; the table and the row format stay the same, so the relay can be replaced later. Not tried.
5. **One transaction across Postgres and Kafka** - not available: Postgres cannot take part in a Kafka transaction.

## Consequences

- Positive: a committed change always has its event recorded; a Kafka outage delays events but does not break writes; the rows are an inspectable record of what was published.
- Negative: delivery is at least once, so consumers must be idempotent; each hop adds up to a poll interval; published rows must be pruned (the MVP keeps them 24 hours); only one relay per service publishes at a time (the active one holds an advisory lock and standby instances take over; the MVP tested this with two instances), which keeps the order per Kafka key.
- Not covered by the MVP tests: a crash between the broker acknowledgement and the `published_at` update (the batch would be published again), and alerting on a growing backlog.

## Evidence (Taakht MVP, one laptop)

- Outbox lag (`published_at - created_at`) was at most 1.06 s in Negotiation, 0.40 s in Swap, 0.26 s in Ad and 0.30 s in Matching over the load-test runs, read on freshly wiped databases after a fix for a Windows `localhost` stall; before the fix the .NET services showed 3.7 to 4.2 s.
- A bug found while building: `created_at DEFAULT now()` is the transaction start, so events of one transaction had equal timestamps and no order; `clock_timestamp()` plus `id` as tie-breaker fixed it.
- Fault injection: with Kafka stopped for 15 s, for 70 s, or the producer killed with the agreement still in its outbox, the row stayed unpublished and the flow completed after the restart.
- Sources: `docs/testing/chaos-test-results.md` (scenarios 3, 5, 11), `docs/testing/load-test-results.md` and `docs/adr/drafts/use-a-transactional-outbox-for-events.md` in `Taakht-MVP`.

## References

- [Decisions log](../architecture/architecture-decisions-log.md), "Message broker: Kafka" (the outbox relay was left to this decision)
- [Architecture overview](../architecture/architecture-overview.md), "Communication between microservices"
