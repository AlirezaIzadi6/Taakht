# XXXX. Structure Deal as a modular monolith in .NET combining negotiation and swap

- Status: Proposed (draft, not yet numbered; for review)
- Date: 2026-11-04
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): Deal (Negotiation and Swap)

## Context

The initial domain exploration identified two distinct stages in the trade lifecycle:
1. **Negotiation:** Human bargaining, proposal revisions, and tentative agreements—characterized by high volume, frequent updates, and transient lifecycles.
2. **Swap (Settlement):** Execution of the finalized agreement, physical locker coordination, fee payment relays, and immutable evidence auditing.

A prior proposal considered separating Negotiation and Swap into two distinct physical microservices. However, business decisions established that Taakht operates on a **zero-liability, blind handoff** principle: the platform does not arbitrate item conditions, hold user funds in escrow, or insure hand-offs. As a result, the post-agreement logic of Swap is lean (primarily a state machine for locker partner payment sessions and timeouts). 

Creating two physical microservices communicating over gRPC and Kafka for these closely related phases would introduce artificial nano-services, latency, and unnecessary distributed transactions between negotiation conclusion and swap creation.

## Decision

We will build **Deal as a single deployable .NET service structured as a Modular Monolith**, containing two logically isolated internal modules: **`Negotiation`** and **`Swap`**.

Key architectural rules:
1. **Logical isolation:** The modules maintain separate namespaces, internal domain models, aggregate roots, and database schemas. Module boundaries are enforced via project references and architecture tests.
2. **In-process communication via Transactional Outbox and Domain Events:** When agreement is reached, the `Negotiation` module records the agreement and writes `AgreementReached` to its outbox table within its local database transaction. The lock Saga (see the locking ADR) then asks the Ad service to claim both Ads. Only when `ExclusiveLockAcquired` arrives does Deal hand an immutable snapshot of the agreement in-process to the `Swap` module, which creates the Swap. Integration events for other services go to Kafka through the same outbox.
3. **No direct database coupling:** The `Swap` module cannot execute SQL joins or directly query `Negotiation` entity models; it receives immutable snapshots of the agreement and ad versions.
4. **Extraction readiness:** If future requirements (such as multi-carrier courier orchestration or complex dispute resolution) cause the Swap module to grow significantly, it can be extracted into an independent microservice with minimal refactoring by switching the internal dispatcher from in-process MediatR/Outbox to Kafka.

## Options considered

1. **Modular monolith in .NET with separate domain modules and internal outbox (chosen)** - Eliminates cross-service network latency and distributed transactions between the `Negotiation` and `Swap` modules. The one cross-service step on this path, claiming the Ads, is covered by the locking Saga. Keeps domain boundaries clean and ready for extraction while avoiding the operational burden of a nano-service.
2. **Two separate physical microservices from day one** - Maximizes distributed separation, but rejected as unjustifiable over-engineering given that Swap is a lean state machine and locker payment webhook listener. Imposing gRPC and Kafka network hops between two halves of the same trade contract provides no business value.
3. **Traditional unstructured single service** - Combines negotiation and swap with a shared `DbContext` and direct entity references. Rejected because it invites accidental coupling, schema entanglement, and makes future extraction or reasoning about the state machine exceedingly difficult.

## Consequences

- Positive: No network hop between the `Negotiation` and `Swap` modules; high domain cohesion; no distributed transaction inside Deal (the only cross-service step, the Ad lock, uses the Saga from the locking ADR); clean codebase ready for microservice extraction if needed.
- Negative / trade-offs accepted: Shared OS process/container means an unhandled crash or memory leak in the locker payment listener could affect negotiation availability; requires developer discipline and architecture linting (e.g. NetArchTest) to prevent boundary leaks.
- Revisit when: Swap requires independent horizontal scaling, independent deployment cadences, or integration with diverse third-party logistics networks demanding distinct security/isolation boundaries.

## References

- [Architecture Overview](../../architecture/architecture-overview.md)
- [Architecture Decisions Log](../../architecture/architecture-decisions-log.md)
- [Context Map](../../domain/context-map.md)
- [Build the system as microservices](../0001-build-the-system-as-microservices.md)
- [Split services between Go and .NET](split-services-between-go-and-dotnet.md)
