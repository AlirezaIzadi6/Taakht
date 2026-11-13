# 0014. Deliver events at least once and make consumers idempotent with a processed-events table

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Events travel through Kafka ([ADR 0010](0010-use-kafka-for-events-between-services.md)) and are published through a transactional outbox ([ADR 0013](0013-publish-events-through-a-transactional-outbox.md)). A producer can retry, and a consumer can see a redelivery after a rebalance or a failed offset commit, so the same event can arrive more than once. A lost event is worse: an Ad that never reaches Matching's index would stay invisible with nothing to show it. Handlers change state (close Ads, cancel negotiations, create swaps), so applying an event twice must be harmless, and events from different topics can arrive in any order relative to each other.

The Taakht MVP (separate `Taakht-MVP` repository) built and tested this in Go and .NET.

## Decision

We will deliver events at least once and give every consuming service a `processed_events (consumer, event_id)` table. For each event the consumer inserts the pair with `ON CONFLICT DO NOTHING` and runs the handler in the same database transaction; if the insert added nothing, the event is skipped. The Kafka offset is committed after the transaction. Handlers also check the current state, so a late or reordered event is ignored instead of applied. Commands are idempotent too; the lock claim is idempotent by swap id.

## Options considered

1. **A dedupe table in the same transaction as the handler (chosen)** - the handler's database writes take effect exactly once, it is simple, and it is testable without Kafka. Cost: one insert per event and a table that grows.
2. **Naturally idempotent handlers only** - no extra table, but some handlers emit new events or create rows (creating a swap, closing an Ad), where a replay would duplicate the output and a mistake would be silent. The state checks stay as a second layer.
3. **Kafka exactly-once (transactions)** - covers reading and writing inside Kafka only; the handlers write to Postgres. Not used.
4. **A version or offset check in each aggregate** - cheap for snapshot events (Matching does it for Ad versions) but it does not generalise to commands such as "close these Ads".
5. **At most once** - no duplicates, but events can be lost silently.

## Consequences

- Positive: replays are absorbed the same way in both languages; a failing handler rolls back its dedupe row, so a retry runs it again.
- Negative: a failing handler is retried with backoff and blocks its partition, unless it marks the failure permanent (then the event is logged and recorded in a dead-letter table, and an operator replays it by hand); a wrong "permanent" classification drops an event apart from the log. `processed_events` rows are pruned after a retention window (7 days in the MVP), which must stay well above any realistic redelivery window. Ordering is guaranteed only per aggregate, through the partition key ([ADR 0010](0010-use-kafka-for-events-between-services.md)).
- Follow-ups: a replay tool and alerting on the dead-letter table.

## Evidence (Taakht MVP, one laptop)

- An `AgreementReached` event re-published with the same `event_id` changed nothing; with a fresh `event_id` it was handled once more but did nothing for a swap that was not waiting for the lock, so exactly one swap existed throughout (chaos scenario 6).
- A parallel-consumer race found by the scenario that replays `AdPublished` lost a `MatchFound` for a pair of Ads; it was fixed, and the replays then added only one `processed_events` row (scenario 8).
- Sources: `docs/testing/chaos-test-results.md` and `docs/adr/drafts/make-consumers-idempotent-with-a-processed-events-table.md` in `Taakht-MVP`.

## References

- [Decisions log](../architecture/architecture-decisions-log.md), "Message broker: Kafka" (delivery semantics were listed as needing their own ADR)
