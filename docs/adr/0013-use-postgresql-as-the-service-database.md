# 0013. Use PostgreSQL as the database of the services

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Every service owns its data ([ADR 0002](0002-split-the-system-into-five-services.md)). Ad needs to claim two Ads in one transaction, Deal keeps proposals and snapshots, and every service writes an outbox and a processed-events table in the same transaction as its state changes ([ADR 0014](0014-publish-events-through-a-transactional-outbox.md), [ADR 0015](0015-deliver-events-at-least-once-with-idempotent-consumers.md)). The decisions log confirmed PostgreSQL early on for its SQL-level support and scalability. Whether any NoSQL store is needed at all was left open. The Reputation event store, the Matching index and the storage of Communication are separate questions and are not covered here.

## Decision

We will use PostgreSQL as the relational database of the services that keep durable transactional state, and as the source of truth for Ad and Deal. Each service owns its own database; no service reads another's tables.

The design depends on these PostgreSQL features, which is where a switch to another engine would cost something:

- row locks with `SELECT ... FOR UPDATE`, and `FOR UPDATE SKIP LOCKED` in the outbox relay;
- `INSERT ... ON CONFLICT DO NOTHING` for the processed-events table;
- `clock_timestamp()` to order the outbox rows of one transaction;
- the Go and .NET drivers and the migrations of each service.

Most of this code lives in the platform libraries ([ADR 0004](0004-use-a-monorepo.md)) and in the migrations, not in the services.

## Options considered

1. **PostgreSQL (chosen)** - the transactions the design needs (the atomic claim of two Ads, the outbox and the dedupe row next to the state change) are ordinary relational features, and the MVP built all of it on PostgreSQL.
2. **Another relational database** - it would have to offer the same features. A switch would mean rewriting and re-testing the outbox relay, the consumer and the migrations in the two platform libraries, and checking the queries; each service would be touched less. The cost was not estimated.
3. **A NoSQL store for some services** - not decided. The claim and the outbox both want ACID transactions, so a relational store is the default; a different store for a specific need is a separate decision per store.

Options 2 and 3 were argued, not tried.

## Consequences

- Positive: ACID transactions where the design needs them; one engine to learn and to run on a single-node demo.
- Negative: the features above tie the platform libraries to PostgreSQL; a PostgreSQL outage stops writes in every service that uses it.
- Revisit when a service needs a data model that a relational database handles badly, or when scale needs another engine.

## Evidence (Taakht MVP, one laptop)

- The four MVP services (Ad, Matching, Negotiation, Swap) ran on PostgreSQL, including the contention test on one hot Ad and the fault-injection scenarios.
- With PostgreSQL stopped for 10 s right after the final approval, the services kept running, calls failed while it was down, no service needed a restart, and the swap completed afterwards (chaos scenario 4).
- Source: `docs/testing/chaos-test-results.md` in `Taakht-MVP`.

## References

- [Decisions log](../architecture/architecture-decisions-log.md), "Stack" section (PostgreSQL)
- [Architecture overview](../architecture/architecture-overview.md), "Datastores"
