# 0017. Run the swap lifecycle as a Saga with compensating transactions

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): Deal (Swap), Ad, Reputation

## Context

After the Ads are locked ([ADR 0016](0016-take-the-exclusive-ad-lock-in-the-ad-service.md)), a swap goes through steps that touch different services, each with its own database. Each party pays the locker fee for their own leg within one hour. Then the swap is either completed (the Ads are closed for good and the review question opens) or cancelled (the locker reservation is cancelled, the Ads are released and the party who did not pay gets a penalty on their trust score). A failure in the middle has to undo what was already done, and one transaction cannot span Ad, Deal and Reputation.

## Decision

We will run the swap lifecycle as a Saga orchestrated by the Swap module in Deal. The module owns the state machine and the deadline timers, and every step is a local transaction that publishes an event through the outbox ([ADR 0013](0013-publish-events-through-a-transactional-outbox.md)). Each step that can be undone has a compensating action:

- **Payment deadline missed (`PaymentTimedOut`):** the swap is cancelled (`SwapCancelled`); Ad releases the lock on both Ads; the locker reservation is cancelled; Reputation applies a penalty to the party who did not pay; the negotiation is cancelled.
- **Both legs paid (`SwapCompleted`):** Ad closes both Ads for good and the review question opens.
- **Stuck steps:** a sweeper in the Swap module retries or republishes a step that has not progressed. Every step and every compensation is idempotent ([ADR 0014](0014-deliver-events-at-least-once-with-idempotent-consumers.md)), so a retry is safe.

After the lock there is no voluntary withdrawal; the only way out is the deadline.

## Options considered

1. **A Saga orchestrated by the Swap module (chosen)** - the state and the deadline have one owner, the compensations are explicit, and each service only reacts to events about its own data. It also shows the pattern the project is about ([ADR 0001](0001-build-the-system-as-microservices.md)). Cost: eventual consistency between the steps, and compensations that must be tested.
2. **Choreography with no orchestrator** - each service decides on its own from the events it sees. The state of a swap would be spread over services and nobody would own the deadline. Argued, not tried.
3. **One distributed transaction across the services** - not available: the services use separate Postgres databases and Kafka, which cannot take part in one transaction.
4. **No compensation, repair by hand** - a cancelled swap would leave Ads locked until an operator noticed.

## Consequences

- Positive: a cancelled swap always frees the Ads, cancels the reservation and records the penalty, even if a service was down when it happened (it catches up from its events).
- Negative: the release of the Ads can lag behind the cancellation while Ad is down; compensations and the sweeper need their own tests; the penalty step depends on Reputation, which the MVP did not build.

## Evidence (Taakht MVP, one laptop)

- Payment deadline set to 25 s, one fee paid, Ad killed before the deadline: the swap was cancelled 26 s after the lock while Ad was down; after Ad restarted its lock rows were released, the Ads were published again and the negotiation was cancelled (chaos scenario 7).
- Not covered: the penalty to Reputation and the locker reservation cancel (both mocked or not built in the MVP).
- Source: `docs/testing/chaos-test-results.md` in `Taakht-MVP`.

## References

- [Architecture overview](../architecture/architecture-overview.md), "Flow 2" (swap completion) and [event storming](../domain/event-storming.md) (`PaymentTimedOut`, `SwapCancelled`, `ApplyPenalty`)
