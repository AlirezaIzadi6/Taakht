# Kafka event conventions (draft)

Conventions that follow from [ADR 0009](../adr/0009-describe-kafka-events-in-protobuf.md). Not an ADR; cheap to change.

## Records

- The payload is plain Protobuf bytes.
- Each record carries two headers: `message-type` (the fully qualified message name) and `schema-version`.
- The partition key is the aggregate id ([ADR 0008](../adr/0008-use-kafka-for-events-between-services.md)).

## Schema rules

- Never reuse or retype a field number; use `reserved` for removed fields.
- Event messages are separate from API messages.
- A consumer treats a field that must be set in practice as invalid when it is empty, because a retyped field arrives empty without an error.

## Still to decide

- Topic layout: one message type per topic, or a type header on shared topics.
- Kafka UI: it decodes events when given the `.proto` directory (`protobufFilesDir`) and one message name per topic.
- Retry and dead-letter handling, and topic naming.
