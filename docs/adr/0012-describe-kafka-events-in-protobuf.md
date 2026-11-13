# 0012. Describe Kafka events in Protobuf, without a schema registry for now

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Services exchange domain events through Kafka ([ADR 0010](0010-use-kafka-for-events-between-services.md)). Some consumers read old events again, for example Matching rebuilding its index from the Ad topic, so a schema chosen now has to stay readable for a long time and is hard to change later. Producers and consumers are written in Go and .NET ([ADR 0003](0003-split-services-between-go-and-dotnet.md)). The REST and gRPC contract is already in Protobuf ([ADR 0005](0005-define-api-contract-in-protobuf.md)). Phase 1 is a single-node demo run by one team in one repository with one CI ([ADR 0004](0004-use-a-monorepo.md)).

## Decision

We will define Kafka event payloads as Protobuf messages in `api/`, separate from the API messages, and write them to Kafka as plain Protobuf bytes with no schema registry. `buf breaking` runs in CI on the event protos and is a required check. The record headers and the schema rules are in the [Kafka event conventions](../guidelines/kafka-events.md).

## Options considered

1. **Protobuf, no registry (chosen)** - one schema language for APIs and events; compatibility enforced at merge time by `buf breaking`; no extra component to run. Cost: nothing stops an incompatible change at runtime if it bypasses CI (in the spike, a retyped field raised no error and arrived empty); tools need the `.proto` files to decode messages.
2. **Protobuf with a schema registry** - the registry rejects an incompatible schema when a producer registers it, and tools can discover schemas. Cost, per the documentation: another service to run, and a framing prefix (magic byte and schema id) on every message that ties producers and consumers to registry-aware libraries; adding it later means migrating the framing of existing messages. Not run in the spike.
3. **Avro with a registry** - a common Kafka setup, but a second schema language next to Protobuf. Not run.
4. **JSON** - readable and simple, but renaming a field breaks consumers and nothing enforces compatibility. Not run.

## Consequences

- Positive: one toolchain; no extra infrastructure; the record headers leave room to add a registry later in a controlled way.
- Negative: the only guard against a breaking change is CI, so a producer deployed outside CI could silently change the meaning of events.
- Revisit a registry when producers or consumers appear outside this repository's CI.
- Reputation keeps its own event store; how that store handles schema changes is not covered here.

## References

- [Evidence: Kafka events in Protobuf](../evidence/kafka-events.md) (Go to .NET works with plain Protobuf; `buf breaking` rejected a type change that otherwise passed silently; a registry was not tried; tried only with a hello-world event)
- Protobuf guidance on safe changes and long-term storage (protobuf.dev best practices)
- Confluent Protobuf serializer documentation
