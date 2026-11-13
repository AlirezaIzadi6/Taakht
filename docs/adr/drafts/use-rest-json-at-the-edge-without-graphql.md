# XXXX. Use REST/JSON at the edge, without GraphQL

- Status: Proposed (draft, not yet numbered; for review)
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Clients reach the system through one public edge. Early on, GraphQL was considered as a complement to REST, for different client access patterns such as web versus mobile ([decisions log](../../architecture/architecture-decisions-log.md)); on 2026-10-06 it was dropped. The project is API-only: the demo has no product UI, at most a minimal one for testing (README), so there is one kind of client.

## Decision

We will expose REST/JSON only at the edge, with no GraphQL. The REST surface comes from the Protobuf contract ([ADR 0004](../0004-define-api-contract-in-protobuf.md)) and is served by the gateway ([ADR 0005](../0005-use-envoy-for-rest-grpc-transcoding.md)).

## Options considered

1. **REST/JSON only (chosen)** - the REST routes are declared once in the proto files and generated, so there is no second API surface to build or keep in step.
2. **REST plus GraphQL** - helps when several client types need different shapes of data, which the API-only demo does not have; it would add a GraphQL layer next to the Protobuf contract.
3. **GraphQL only** - would replace the REST mapping that the contract and the gateway already provide, and give two sources of truth for the API shape.

All three were argued, not tried.

## Consequences

- Positive: one API surface; nothing to build beyond the contract and the gateway.
- Negative: a client that needs different shapes of the same data (for example a mobile app next to a web app) would need new endpoints.
- Revisit when clients with different access patterns appear.

## References

- [Decisions log](../../architecture/architecture-decisions-log.md), "Communication and authentication" and the 2026-10-06 refinements
- README, "Running the demo"
