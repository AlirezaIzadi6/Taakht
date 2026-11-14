# Outbox and sweeper conventions (draft)

Conventions that follow from [ADR 0014](../adr/0014-publish-events-through-a-transactional-outbox.md) and [ADR 0018](../adr/0018-run-the-swap-lifecycle-as-a-saga.md). Not ADRs; cheap to change. The values are the ones the Taakht MVP used.

## The outbox relay

- One relay loop per service polls the outbox every 200 ms for up to 100 unpublished rows, reads them in `created_at, id` order with `FOR UPDATE SKIP LOCKED`, produces them to Kafka with `acks=all`, waits for the acknowledgement, then sets `published_at`.
- A crash between the acknowledgement and the commit publishes the rows again, so delivery is at least once and consumers must be idempotent ([ADR 0015](../adr/0015-deliver-events-at-least-once-with-idempotent-consumers.md)).
- Rows are inserted with `created_at = clock_timestamp()`, not the default `now()`: `now()` is the start of the transaction, so events written in one transaction would have equal timestamps and no order. `id` breaks ties.
- Only one relay per service publishes at a time. The active one holds a session-level PostgreSQL advisory lock; standby instances take over if it dies. The MVP tested this with two instances. Without it, parallel relays would publish different batches at once and break the order per key.
- Published rows are pruned after 24 hours by a housekeeping job; unpublished rows are never deleted, and nothing alerts if they pile up.

## Sweepers

A sweeper is a background job that looks for work that has not progressed and nudges it. Events can be delayed or lost for a consumer that is down, so progress that depends on an event needs one.

- **Agreement pending:** every 30 s, a negotiation that has waited for its swap longer than the timeout (10 minutes by default) is not cancelled at once, because the swap may be mid-flight. `AgreementReached` is written to the outbox again with the agreed Ad versions, up to three times. Before giving up, the sweeper asks the swap whether it is still working and does not cancel if it is. A lock that arrives late reopens a negotiation that was cancelled.
- **Payment deadline:** every 5 s, a swap past its one-hour deadline is cancelled, which starts the compensations in ADR 0018.
- In the MVP the first sweeper lived in Negotiation and the second in Swap; in Deal both run inside the one service.
- Intervals and timeouts are settings, not decisions.
