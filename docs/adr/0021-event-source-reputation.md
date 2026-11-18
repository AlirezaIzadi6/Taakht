# 0021. Event-source Reputation

- Status: Accepted (by @AlirezaIzadi6 and @ArmanIzadi99)
- Date: 2026-11-17
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): Reputation

## Context

Reputation ([ADR 0002](0002-split-the-system-into-five-services.md)) keeps the internal trust score, penalties, abuse reports, objections and blocking. The decisions log gives three reasons for event sourcing it: the score formula will change and the score must be recomputable from scratch; the anti-collusion rule (a cap on deals between a pair of users in a time window) needs queries over history; and it is a low-risk place to practise the pattern, which the team counts as a goal. The same entry notes that Reputation has few commands and invariants, so event sourcing gives it mostly recomputability and history, not a full showcase.

Kafka is not a source of truth ([ADR 0011](0011-use-kafka-for-events-between-services.md)), so Reputation needs a store of its own inside the service. The design principles list event sourcing in Reputation as a one-way door.

## Decision

We will event-source Reputation. Its facts are appended as events to a store inside the service, one stream per user. Each stream has a version, and an append states the version it expects, so two writers to one stream cannot both win. The state of a user is rebuilt from the stream when it is needed (lazy rehydration, no initial record). The trust score, the blocked pairs, the penalties and the admin queue are projections of the events. When the formula changes, the projections are rebuilt by replaying the events. Events other services need (`TrustScoreUpdated`, `UserBlocked` and the like) are published through the outbox ([ADR 0014](0014-publish-events-through-a-transactional-outbox.md)).

Where the events are stored is a convention, not part of this decision: for now an append-only table in Reputation's own PostgreSQL database ([ADR 0013](0013-use-postgresql-as-the-service-database.md)), described in the [Reputation event store conventions](../guidelines/reputation-event-store.md).

## Options considered

1. **Event sourcing (chosen)** - the score can be recomputed from the whole history, the pair cap and other history queries have their data, and the pattern is practised. Cost: more code than a plain table (streams, concurrency, projections, replay), and events are kept forever, so changing their shape later is hard.
2. **No event sourcing: current-state tables and an audit log** - the simplest; but a changed formula could not be applied to the past, history queries would depend on a separate log, and the learning goal would go. Not chosen.
3. **Kafka as the event store** - rejected in ADR 0011: Kafka is not the source of truth.

Options 2 and 3 were argued, not tried.

## Consequences

- Positive: the score can be recomputed when the formula changes; history queries have the data; the store sits inside the service, so replacing it later is contained (the streams would have to be migrated, which is work, but nobody else reads them).
- Negative: this is the one-way door. The events are the facts and stay forever, so changing the shape of an event (upcasting) is hard; replays take longer as the history grows; the projections are eventually consistent with the events unless they are updated in the same transaction.
- The admin queue and blocking are modelled as events too (`AbuseReported`, `ReportConfirmed`, `ObjectionFiled`, `ObjectionUpheld`, `UserBlocked` and the like), with the queue and the blocked pairs as projections; a command checks the current state, from the user's stream or a projection, before it appends. Other services learn about a block from the published `UserBlocked` event, so how Reputation stores it changes nothing for them. The costs: the admin queue is a list across many users, so its projection is fed from many streams, and a report needs the reporter's score, which lives in another stream and is only eventually consistent.
- Follow-ups, in the conventions: the payload format of the events and the upcasting policy (open item 13), snapshots, how the projections are updated, and the query model for the anti-collusion cap.

## References

- [Decisions log](../architecture/architecture-decisions-log.md), "Reputation: a separate service with Event Sourcing" and "Closing the Reputation and Trust cluster"
- [Design principles](../architecture/design-principles.md), [event storming](../domain/event-storming.md) (the Reputation and Trust cluster)
