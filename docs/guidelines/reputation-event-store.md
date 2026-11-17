# Reputation event store (draft)

Conventions that follow from [ADR 0021](../adr/0021-event-source-reputation.md). Not an ADR: the store can be replaced, because it sits inside the Reputation service and nobody else reads it.

## For now

- An append-only table in Reputation's own PostgreSQL database ([ADR 0013](../adr/0013-use-postgresql-as-the-service-database.md)). One row per event with the stream (the user), the version in the stream, an event id, the type, the payload and the time. A unique key on the stream and the version makes an append with an expected version fail when another writer got there first.
- Projections (the score, blocked pairs, penalties, the admin queue) are tables in the same database, rebuilt by replaying the events when the formula or a projection changes.
- Events other services need go through the outbox ([ADR 0014](../adr/0014-publish-events-through-a-transactional-outbox.md)).

## Why PostgreSQL for now

The stack and the platform libraries already use it, it adds no component to the 8 GB server, and writing the stream, the version check and the projections by hand teaches the pattern, which the team counts as a goal.

## Other stores that were considered

- **A library on PostgreSQL such as Marten (.NET):** less code, but it ties the service to a library and hides much of what we want to learn. Its current state was not checked.
- **A dedicated event store such as EventStoreDB:** built for the job, with subscriptions and projections, and attractive to learn. One more component to run, with a memory cost that was not measured on the 8 GB server.

## Moving to another store

Copy the streams in order, keeping their versions, then rebuild the projections. The events themselves do not change shape, so nothing outside the service notices.

## Changing the shape of an event (upcasting)

- Every event carries a schema version. An event that is written is never changed or rewritten in the table.
- When a reader meets an older version, an upcaster turns it into the current shape as it reads it. Upcasters chain (version 1 to 2, then 2 to 3), so each one only knows one step.
- A change that alters what an event means, not only its fields, gets a new event type instead of a new version.
- The old versions stay in the table forever, so replaying the whole history always works through the upcasters.

## The payload of an event

- The payload is a Protobuf message, kept as bytes in the table, with the same toolchain as the other contracts ([ADR 0012](../adr/0012-describe-kafka-events-in-protobuf.md)). `buf breaking` runs in CI on these messages too, because events live forever and a breaking change would be costly to find late.
- The fields a query needs sit in their own indexed columns next to the payload: the user (the stream), the other party of a pair, the time and the event type. The anti-collusion cap, which counts deals between two users in a time window, reads those columns, not the payload.
- The schema version of the previous section is stored in a column as well, so an upcaster can be chosen without decoding the payload.
