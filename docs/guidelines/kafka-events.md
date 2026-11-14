# Kafka event conventions (draft)

Conventions that follow from [ADR 0012](../adr/0012-describe-kafka-events-in-protobuf.md). Not an ADR; cheap to change.

## Records

- The payload is plain Protobuf bytes.
- Each record carries two headers: `message-type` (the fully qualified message name) and `schema-version`.
- The partition key is the aggregate id ([ADR 0011](../adr/0011-use-kafka-for-events-between-services.md)).

## Schema rules

- Never reuse or retype a field number; use `reserved` for removed fields.
- Event messages are separate from API messages.
- A consumer treats a field that must be set in practice as invalid when it is empty, because a retyped field arrives empty without an error.

## Still to decide

- Topic layout: one message type per topic, or a type header on shared topics.
- Kafka UI: it decodes events when given the `.proto` directory (`protobufFilesDir`) and one message name per topic.
- Retry and dead-letter handling, and topic naming.

## Consumer failures

- A failing handler is retried with backoff (200 ms doubling to 5 s in the MVP) and blocks its partition.
- A handler can mark a failure permanent, and so can the consumer for a payload that does not decode. The event is then logged at error level and parked in a dead-letter table with its payload and error; the dedupe row is recorded, so the event is not retried.
- Parked events are listed, shown, replayed and purged with a small tool (`tools/dlq` in the Taakht MVP). Nothing alerts on the table yet.
- A wrong "permanent" classification drops an event apart from the log, so each handler classifies its own failures with care.
- `processed_events` rows are pruned after a retention window (7 days in the MVP); it must stay well above any realistic redelivery window.
