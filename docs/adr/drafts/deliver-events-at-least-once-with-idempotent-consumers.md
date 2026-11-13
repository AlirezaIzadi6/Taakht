# XXXX. Deliver events at least once and make consumers idempotent

- Status: Proposed (draft, not yet numbered; for review)
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Events travel through Kafka ([ADR 0008](../0008-use-kafka-for-events-between-services.md)) and are published through a transactional outbox (README; the outbox has its own decision). A producer can retry, and a consumer can restart before it commits its offset, so the same event can arrive more than once. A lost event is worse: for example an Ad that never reaches Matching's index would stay invisible with nothing to show it.

## Decision

We will deliver events at least once and make every consumer idempotent, so a duplicate has no further effect. How a consumer achieves this (for example by recording the ids of processed events, or by updates that are idempotent by nature, such as setting state to a given version) is chosen per consumer and written down in the conventions. Commands are idempotent too; the lock claim is idempotent by negotiation id.

## Options considered

1. **At least once with idempotent consumers (chosen)** - no event is lost. Cost: every consumer must handle duplicates, and that has to be tested.
2. **At most once** - no duplicates, but events can be lost silently, which would leave services inconsistent with no way to notice.
3. **Exactly once through Kafka transactions** - the guarantee covers reading and writing inside Kafka; writing to a service's database and committing the offset are still two separate actions, so consumers would need idempotency anyway, on top of more complexity. From the documentation, not tried.

## Consequences

- Positive: no lost events; behaviour on retries and restarts is well defined.
- Negative: every consumer carries idempotency logic and its tests; ordering is guaranteed only per aggregate, through the partition key ([ADR 0008](../0008-use-kafka-for-events-between-services.md)), not across aggregates.
- Follow-ups: conventions for idempotency (a table of processed event ids, or natural idempotency), retries and dead-letter handling; tests that replay duplicate events.

## References

- [Decisions log](../../architecture/architecture-decisions-log.md), "Message broker: Kafka" (delivery semantics listed as needing their own ADR)
- [Architecture overview](../../architecture/architecture-overview.md), "Communication between microservices"
