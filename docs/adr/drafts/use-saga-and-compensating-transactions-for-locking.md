# XXXX. Use a Saga with compensating transactions for exclusive ad locking

- Status: Proposed (draft, not yet numbered; for review)
- Date: 2026-11-04
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): Ad, Deal (Negotiation & Swap)

## Context

When multiple counterparties simultaneously negotiate on the same Ad, a race condition occurs if two negotiations reach mutual approval at the same moment (`ConcludeAgreement`). A fundamental business invariant is: **an Ad can be bound to at most one completed Swap agreement at any given time**.

The two bounded contexts involved—Ad (written in Go) and Deal (written in C#/.NET)—reside in different services with separate databases. We need a strategy to arbitrate between concurrent agreements without violating context boundaries or causing distributed transaction deadlocks.

### Strategic Drivers and Weighted Criteria

To make an intentional trade-off rather than an accidental one, the decision criteria are weighted as follows:

1. **Simplicity of development and maintenance (Low weight):** While a unified lock or synchronous transactional check is easier to code, simplicity is secondary to demonstrating robust distributed system patterns.
2. **Performance and scalability (Medium weight):** High concurrency on ads must not choke database connection pools with distributed pessimistic locks or distributed two-phase commits.
3. **Learning value and demonstrating distributed systems competence (High / Decisive weight):** Taakht is a portfolio system demonstrating senior-level mastery of distributed consistency, asynchronous messaging with Kafka, and failure compensation—skills directly relevant to high-scale platforms like Divar.

## Decision

We will use **Eventual Consistency with an asynchronous Saga (Process Manager) and compensating transactions** across Deal and Ad:

1. When both parties approve the active proposal, the Deal context's Negotiation module concludes the negotiation tentatively and emits `AgreementReached`.
2. A Saga coordinator listens to `AgreementReached` and dispatches one asynchronous command `ClaimLock` to the Ad service. The command carries **both** Ads of the Swap, each with the `AdVersion` fixed in the agreed proposal, and is idempotent by negotiation id.
3. The Ad service claims both Ads **in a single local database transaction**: all or nothing. Both Ads belong to the same service and database, so no cross-Ad compensation is needed. Rows are locked in ascending Ad id order, so two concurrent claims over the same pair of Ads cannot deadlock. The claim succeeds only if, for each Ad, the status is `Available` and the current version equals the agreed version:
   - **Happy path:** both Ads transition to `Locked` and the service emits one `ExclusiveLockAcquired`. The Saga completes: the winning negotiation advances to `SwapCreated`, and competing open negotiations on either Ad receive `CancelNegotiation` with reason "transferred to another party" (`NegotiationCancelled`).
   - **Already locked:** at least one Ad is no longer `Available` (another negotiation won). Neither Ad is changed and the service emits `ExclusiveLockRejected` with reason `AlreadyLocked`.
   - **Edited meanwhile:** at least one Ad has a newer version than the agreed one. Neither Ad is changed and the service emits `ExclusiveLockRejected` with reason `VersionChanged`.
4. **Compensating transaction:** Upon receiving `ExclusiveLockRejected`, the Saga compensates on the Deal side only, depending on the reason:
   - `AlreadyLocked`: the negotiation is cancelled (`CancelNegotiation`, reason "transferred to another party").
   - `VersionChanged`: the negotiation stays open. The proposal is replaced by a new one built on the current Ad versions, and both parties approve again.
5. **User Experience:** In the UI, the state immediately after both approvals is displayed as *"Finalizing agreement..."* rather than unconditional success. Only when `ExclusiveLockAcquired` arrives is the celebratory notification pushed. If the lock is rejected, the user is notified with *"Unfortunately, the item was finalized in another trade just now."*

## Options considered

1. **Eventual consistency with Saga and compensating transactions (chosen)** - Preserves absolute decoupling between the Ad service (Go) and Deal service (.NET). Asynchronous messaging over Kafka handles high throughput without blocking threads or locking database rows across network boundaries. Serves the decisive learning and architectural demonstration driver.
2. **Strong consistency in the Ad aggregate (Synchronous lock)** - The negotiation client calls Ad synchronously (e.g., via gRPC) to acquire the lock before concluding agreement. Correctness is the same as in the chosen option, because the winner is decided by the atomic claim inside Ad either way. The difference is behaviour during an Ad outage (a synchronous call fails fast and the user sees an error, while the Saga lets the claim wait in Kafka) and cost (the Saga needs a compensation path and a transient "finalizing" state). Rejected because the Saga fits the learning and demonstration driver; this is a weighted choice, not a correctness argument.
3. **Distributed pessimistic locking (e.g. Redis Redlock or ZooKeeper)** - Acquires an explicit distributed lock before agreement. Rejected because distributed locks introduce operational complexity, clock drift hazards, failure-recovery edge cases, and an external dependency on ephemeral lock leases that does not fit the business domain.

## Consequences

- Positive: Clean transactional boundaries; zero distributed database locks; services can deploy and scale independently; Deal can accept and record approvals while Ad is briefly unavailable; explicit compensation models for race conditions.
- Negative / trade-offs accepted: Compensating transactions must be tested thoroughly; UX must manage transient "in progress" states; eventual consistency requires handling potential duplicate event deliveries via idempotent consumers.
- Revisit when: The business requirement demands synchronous immediate confirmation at the edge and cannot tolerate compensating rollbacks, or when traffic patterns make optimistic reservation conflict rates intolerable.

## Known gaps

- Saga behaviour while Ad is unavailable is not tested. A timeout does not prove the claim failed (the request may have been applied and the reply lost). Proposed rule: never treat a timeout as failure; only an explicit `ExclusiveLockRejected` is, and the idempotent `ClaimLock` is retried until a reply arrives. To verify with a spike that stops the Ad service mid-Saga. Options deferred until then: synchronous fast path with Saga fallback, a circuit breaker on approvals, redundant Ad instances.
- Order of winners is the order in which claims reach Ad, not the order of approvals; this is accepted and not measured.

## References

- [Architecture Overview](../../architecture/architecture-overview.md)
- [Architecture Decisions Log](../../architecture/architecture-decisions-log.md)
- [Context Map](../../domain/context-map.md)
- [Build the system as microservices](use-microservices.md)
- [Split services between Go and .NET](split-services-between-go-and-dotnet.md)
