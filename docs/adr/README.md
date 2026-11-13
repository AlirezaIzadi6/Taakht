# Architecture Decision Records

ADRs are written in English.

## Rules

- One decision per ADR.
- An accepted ADR is immutable. If a decision changes, write a new ADR and mark the old one `Superseded by`.
- Numbers are sequential, four digits, and never reused. (The first ten ADRs were renumbered once, on 2026-11-13, before anything depended on them, so that each ADR follows the decisions it relies on. From now on numbers are permanent.)
- File name: `NNNN-short-imperative-slug.md`.
- Start from [template.md](template.md). Keep an ADR under about one page; if it grows, it probably holds more than one decision.
- Always list the rejected options with the reason.

## What is not an ADR

| Not an ADR | Where it goes |
|---|---|
| Domain discovery: events, aggregates, context boundaries | Event Storming / context map docs |
| How the system works | Architecture overview |
| Cheap, reversible choices (variable names, library versions, log format) | Code, PR description, guidelines |
| Coding style and conventions (topic names, retry/DLQ patterns, client libraries, deploy config) | Convention / guideline docs |
| Remaining work and open questions | `open-items.md` |
| Operational procedures | Runbooks |
| Product decisions with no architectural impact | Product docs |
| Decisions not yet made | `open-items.md` until decided |

**Rule of thumb:** if reversing it later is expensive or painful, if it touches several contexts or teams, or if a newcomer would ask "why did you build it this way?", write an ADR. Otherwise do not.

## Status values

- **Proposed** - under discussion.
- **Accepted** - decided; the team follows it.
- **Deprecated** - no longer applies, with no replacement.
- **Superseded by NNNN** - replaced by a newer ADR.

## Index

| # | Title | Status | Context |
|---|---|---|---|
| [0001](0001-build-the-system-as-microservices.md) | Build the system as microservices | Accepted | cross-cutting |
| [0002](0002-split-the-system-into-five-services.md) | Split the system into five services | Accepted | cross-cutting |
| [0003](0003-split-services-between-go-and-dotnet.md) | Split services between Go and .NET by workload | Accepted | cross-cutting |
| [0004](0004-use-a-monorepo.md) | Use a single monorepo for all services | Accepted | cross-cutting |
| [0005](0005-define-api-contract-in-protobuf.md) | Define the API contract in Protobuf | Accepted | cross-cutting |
| [0006](0006-use-rest-json-at-the-edge-without-graphql.md) | Use REST/JSON at the edge, without GraphQL | Accepted | cross-cutting |
| [0007](0007-use-envoy-for-rest-grpc-transcoding.md) | Use Envoy as the edge gateway for REST to gRPC transcoding | Accepted | cross-cutting |
| [0008](0008-validate-jwts-at-the-edge.md) | Validate JWTs at the Envoy edge | Accepted | cross-cutting |
| [0009](0009-authenticate-service-to-service-calls.md) | Authenticate service-to-service calls: isolated network now, mTLS in production | Accepted | cross-cutting |
| [0010](0010-use-kafka-for-events-between-services.md) | Use Apache Kafka for events between services | Accepted | cross-cutting |
| [0011](0011-use-grpc-for-requests-and-events-for-state-changes.md) | Use gRPC for requests and events for state changes between services | Accepted | cross-cutting |
| [0012](0012-describe-kafka-events-in-protobuf.md) | Describe Kafka events in Protobuf, without a schema registry for now | Accepted | cross-cutting |
| [0013](0013-publish-events-through-a-transactional-outbox.md) | Publish events through a transactional outbox with a polling relay | Accepted | cross-cutting |
| [0014](0014-deliver-events-at-least-once-with-idempotent-consumers.md) | Deliver events at least once and make consumers idempotent with a processed-events table | Accepted | cross-cutting |
| [0015](0015-structure-deal-as-modular-monolith.md) | Structure Deal as a modular monolith in .NET combining negotiation and swap | Accepted | Deal (Negotiation and Swap) |
| [0016](0016-take-the-exclusive-ad-lock-in-the-ad-service.md) | Take the exclusive Ad lock in the Ad service with a synchronous atomic claim | Accepted | Ad, Deal (Negotiation and Swap) |
| [0017](0017-run-the-swap-lifecycle-as-a-saga.md) | Run the swap lifecycle as a Saga with compensating transactions | Accepted | Deal (Swap), Ad, Reputation |
