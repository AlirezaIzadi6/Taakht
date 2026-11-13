# 0016. Use a Saga with compensating transactions for exclusive Ad locking

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): Ad, Deal (Negotiation and Swap)

## Context

When several counterparties negotiate on the same Ad, two negotiations can reach mutual approval at the same moment. The invariant: **an Ad can be bound to at most one completed Swap agreement at any time**.

Ad (Go) and Deal (.NET) are different services with separate databases ([ADR 0003](0003-split-services-between-go-and-dotnet.md), [ADR 0015](0015-structure-deal-as-modular-monolith.md)), so one transaction cannot cover both. What decides between the options:

- **Correctness:** in every option the winner is decided by an atomic claim inside the Ad service, so it does not separate them.
- **Demonstration (decisive):** the project exists to show how distributed consistency and failure compensation are handled ([ADR 0001](0001-build-the-system-as-microservices.md)).
- **Simplicity and performance (secondary):** neither was measured.

## Decision

We will lock Ads with an asynchronous Saga and compensating transactions. When both parties approve, Deal emits `AgreementReached`; a Saga coordinator sends an idempotent `ClaimLock` command to Ad, carrying both Ads with the version fixed in the agreed proposal. Ad claims both in one local transaction (rows locked in ascending Ad id order, so concurrent claims cannot deadlock), which succeeds only if each Ad is `Available` and at the agreed version, and replies `ExclusiveLockAcquired` or `ExclusiveLockRejected` (`AlreadyLocked` or `VersionChanged`).

- **Acquired:** the Swap is created and competing negotiations on either Ad are cancelled.
- **`AlreadyLocked`:** the negotiation is cancelled.
- **`VersionChanged`:** the negotiation stays open, the proposal is rebuilt on the current Ad versions and both parties approve again.

Compensation happens on the Deal side only, because both Ads are claimed in one transaction. A timeout is never treated as a rejection: only an explicit `ExclusiveLockRejected` is, and `ClaimLock` is retried until a reply arrives. Until then the user sees "finalizing", not success.

## Options considered

1. **Saga with compensating transactions (chosen)** - Ad and Deal stay decoupled, Deal can record approvals while Ad is briefly unavailable (the claim waits in Kafka, [ADR 0010](0010-use-kafka-for-events-between-services.md)), and it shows the distributed patterns the project is about. Cost: a compensation path and a transient "finalizing" state.
2. **Synchronous claim in Ad over gRPC** - the same correctness, because the atomic claim decides the winner either way. It fails fast during an Ad outage and needs no compensation path or transient state. Rejected because the Saga fits the demonstration driver; this is a weighted choice, not a correctness argument.
3. **Distributed pessimistic lock (Redis Redlock, ZooKeeper)** - a lock before the agreement. It adds a component and lease-expiry and clock hazards, and the atomic claim already decides the winner without it. Reasoning, not tried.

## Consequences

- Positive: clean transactional boundaries; no distributed locks; independent deployment and scaling; explicit compensation for races.
- Negative: compensations must be tested; the UI must handle the "finalizing" state; consumers must be idempotent because events can be delivered twice ([ADR 0014](0014-deliver-events-at-least-once-with-idempotent-consumers.md)); the winner is the claim that reaches Ad first, not the first approval.
- The behaviour while Ad is unavailable is designed but not tested; a spike that stops the Ad service mid-Saga would check it.
- A hold on the Ad after `AgreementReached` (open idea in [open-items.md](../open-items.md)) would make `AlreadyLocked` rare and keep the Ad claim as the final authority. If adopted, it extends this design and may need a new ADR.
- Revisit when the product needs an immediate synchronous confirmation at the edge, or when claim conflicts become too frequent.

## References

- [Architecture overview](../architecture/architecture-overview.md), [decisions log](../architecture/architecture-decisions-log.md), [context map](../domain/context-map.md)
