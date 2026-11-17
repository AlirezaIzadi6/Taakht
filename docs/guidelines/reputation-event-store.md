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
