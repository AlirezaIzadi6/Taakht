# Kafka event conventions (draft)

Conventions that follow from [ADR 0012](../adr/0012-describe-kafka-events-in-protobuf.md). Not an ADR; cheap to change.

## Records

- The payload is plain Protobuf bytes.
- Each record carries three headers: `message-type` (the fully qualified message name), `schema-version` and `event-id`.
- `event-id` is the id of the outbox row (a UUID) and stays the same when the relay sends the row again. A consumer records it in `processed_events` and skips an event it has seen ([ADR 0015](../adr/0015-deliver-events-at-least-once-with-idempotent-consumers.md)).
- The partition key is the aggregate id ([ADR 0011](../adr/0011-use-kafka-for-events-between-services.md)).

## Topics

- One topic per service, named `<service>.events` (for example `ad.events`). It carries every event the service publishes, and the `message-type` header tells them apart.
- The key is the aggregate id, so all events of one aggregate stay in one partition and arrive in order. Kafka gives no order across topics, which is why event types of one aggregate do not get a topic each.
- A consumer reads the whole topic and skips the types it does not handle.
- The compose setup creates `ad.events`, `matching.events`, `deal.events` and `reputation.events`, each with 3 partitions. 3 is a default for development and the first deployments; the count is not tuned.
- Topics are created before the services start (see the [deployment guideline](deployment.md)).
- Known cost: Kafka UI decodes a topic with one message name, so on a mixed topic it cannot show every event decoded. Not tried yet.

## Schema rules

- Never reuse or retype a field number; use `reserved` for removed fields.
- Event messages are separate from API messages.
- A consumer treats a field that must be set in practice as invalid when it is empty, because a retyped field arrives empty without an error.

## Still to decide

- Kafka UI: it decodes events when given the `.proto` directory (`protobufFilesDir`) and one message name per topic; how to read a topic that mixes message types is not tried.
- Partition count beyond the development default.
- Whether a topic is compacted, so that Matching can rebuild its index from the Ad topic ([ADR 0011](../adr/0011-use-kafka-for-events-between-services.md) says it can be). Compaction keeps the last record per key, which is unclear on a topic that mixes event types under one key.

## Consumer failures

- A failing handler is retried with backoff (200 ms doubling to 5 s in the MVP) and blocks its partition.
- A handler can mark a failure permanent, and so can the consumer for a payload that does not decode. The event is then logged at error level and parked in a dead-letter table with its payload and error; the dedupe row is recorded, so the event is not retried.
- Parked events are listed, shown, replayed and purged with a small tool (`tools/dlq` in the Taakht MVP). Nothing alerts on the table yet.
- A wrong "permanent" classification drops an event apart from the log, so each handler classifies its own failures with care.
- `processed_events` rows are pruned after a retention window (7 days in the MVP); it must stay well above any realistic redelivery window.
