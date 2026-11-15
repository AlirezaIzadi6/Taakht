# 0016. Structure Deal as a modular monolith in .NET combining negotiation and swap

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): Deal (Negotiation and Swap)

## Context

The domain exploration found two stages in a trade:

1. **Negotiation:** bargaining, proposal revisions and tentative agreements.
2. **Swap (settlement):** executing the finalized agreement: locker coordination, fee payment relay and the immutable audit record.

An earlier proposal split them into two services. The platform never touches money, does not arbitrate item-condition disputes and does not track or insure courier and in-person hand-overs (README, "Status and known limitations"). So the logic after the agreement is small, mainly a state machine for locker payment sessions and timeouts ([decisions log](../architecture/architecture-decisions-log.md), "Merging Negotiation and Swap"). Two services for two closely related phases would add a service boundary and a distributed transaction between the end of a negotiation and the creation of its swap.

## Decision

We will build **Deal as a single deployable .NET service ([ADR 0003](0003-split-services-between-go-and-dotnet.md)) structured as a modular monolith** with two isolated internal modules, `Negotiation` and `Swap`.

1. **Logical isolation:** separate namespaces, domain models, aggregate roots and database schemas. Boundaries are enforced by project references and architecture tests.
2. **In-process communication through domain events and an outbox:** when an agreement is reached, `Negotiation` records it and writes `AgreementReached` to its outbox in the same local transaction. The Swap becomes active only after the Ad lock is acquired (until then it waits in a `LOCKING` state), from an immutable snapshot of the agreement handed in-process to `Swap`. Events for other services go to Kafka ([ADR 0011](0011-use-kafka-for-events-between-services.md)) through the same outbox. How the Ad lock is acquired is decided separately.
3. **No direct database coupling:** `Swap` never queries `Negotiation`'s models or tables; it receives the snapshot of the agreement and the Ad versions.
4. **Extraction readiness:** `Swap` is kept separable so it can become its own service if it grows. The effort of extracting it was not estimated.

## Options considered

1. **Modular monolith with separate modules and an internal outbox (chosen)** - no network call or distributed transaction between the two modules; domain boundaries stay clean; no extra service to deploy and run. The one cross-service step on this path, claiming the Ads, is handled outside Deal.
2. **Two separate services from day one** - maximum separation, but Swap is small, and the two halves of one trade contract would talk over gRPC and Kafka, adding a boundary and the distributed transaction described above for logic that is small.
3. **One unstructured service** - a shared `DbContext` and direct entity references. Rejected because it invites accidental coupling and schema entanglement, and makes later extraction and reasoning about the state machine hard.

## Consequences

- Positive: no network hop or distributed transaction between negotiation and swap; high cohesion inside Deal; fewer services to run for two people.
- Negative: one process, so a crash or memory leak in the locker payment listener can affect negotiation availability; boundaries rely on discipline and architecture tests (for example NetArchTest); the transactional outbox is part of this design ([ADR 0014](0014-publish-events-through-a-transactional-outbox.md)).
- Revisit when Swap needs independent scaling or deployment, or integrates with logistics networks that need their own security boundary.

## References

- [Architecture overview](../architecture/architecture-overview.md), [decisions log](../architecture/architecture-decisions-log.md), [context map](../domain/context-map.md)
- [ADR 0001: Build the system as microservices](0001-build-the-system-as-microservices.md)
