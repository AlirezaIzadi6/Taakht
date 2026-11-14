# 0017. Take the exclusive Ad lock in the Ad service with a synchronous atomic claim

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): Ad, Deal (Negotiation and Swap)

## Context

When two parties agree to swap, both Ads must become unavailable to every other negotiation, and of several competing agreements on the same Ad exactly one may win. An Ad can also be edited between the approval and the lock, so the agreed version has to be checked at the moment of locking. Ad (Go) and Deal (.NET) are different services with separate databases ([ADR 0003](0003-split-services-between-go-and-dotnet.md), [ADR 0016](0016-structure-deal-as-modular-monolith.md)), so one transaction cannot cover both.

The Taakht MVP (separate `Taakht-MVP` repository) implemented and tested the lock, so most of the evidence below is from running code. One difference: in the MVP any refused claim ends the negotiation; the `VersionChanged` path below is Taakht's design and was not built there.

## Decision

We will take the lock in the Ad service, which owns the Ad rows. After an agreement, the Swap module in Deal creates the swap in a `LOCKING` state and calls `ClaimLock(swap_id, [(ad_id, version)] x 2)` on Ad with a synchronous gRPC call. Ad claims both Ads in one database transaction, or neither: rows are locked in ascending Ad id order, and the claim succeeds only if each Ad is `Available` and at the agreed version. The call is idempotent by swap id. Ad answers with success, or with a refusal (`AlreadyLocked` or `VersionChanged`), and publishes an event about a successful lock for the services that follow Ads, such as Matching. Only a system identity carrying the internal token may call it ([ADR 0010](0010-authenticate-service-to-service-calls.md)).

- **`AlreadyLocked`:** the negotiation is cancelled.
- **`VersionChanged`:** the negotiation stays open, the proposal is rebuilt on the current Ad versions and both parties approve again.
- **A timeout is never a rejection.** Only an explicit refusal is; the call is retried with backoff until an answer arrives, and meanwhile the negotiation waits for the lock.

## Options considered

1. **The lock in the Ad service, called by Deal (chosen)** - the claim and the data it protects share one transaction, so atomicity and the version check need no distributed lock, and the winner is whoever commits first. Cost: Deal depends synchronously on Ad for this step.
2. **The lock in Negotiation** - it already holds the approvals, but it would have to reach into Ad data it does not own or keep a copy that can be stale, and the version check would race with Ad edits.
3. **The lock in Swap with its own table of locked Ads** - Swap would decide whether an Ad is available while Ad could still edit, hide or publish it: two owners of one fact.
4. **A distributed lock (Redis, advisory locks across services)** - adds a component and the Ad row still has to reflect the state.
5. **An optimistic version bump on each Ad, no separate lock** - gives no way to claim two Ads atomically or to record who holds them.
6. **An asynchronous `ClaimLock` command over Kafka, answered by an event** - the same atomicity, and Deal could take approvals while Ad is down. It needs a reply path and a visible "finalizing" state; the MVP built only the synchronous call, which with retry and a waiting swap survived an Ad outage in its tests; the asynchronous variant was not built or compared.

Options 2 to 6 were argued; only the chosen one was built.

## Consequences

- Positive: all-or-nothing claim with no deadlock between swaps that share an Ad; the agreed version is re-checked where it is decided; retries and redelivery are safe because the call is idempotent.
- Negative: Deal waits on Ad for this step; a refused claim ends the negotiation; Matching learns about the lock through the event, so a locked Ad can briefly appear in search results (a user acting on it is stopped at the lock).
- A hold on the Ad after `AgreementReached` (an open idea in [open-items.md](../open-items.md)) would make `AlreadyLocked` rare; the claim would stay the final authority, so it extends this ADR rather than replacing it.

## Evidence (Taakht MVP, one laptop)

- Ten final approvals on one hot Ad at the same instant, with 40 to 200 requesters: in every run exactly one swap for the Ad was not rejected or cancelled, and the Ad ended closed. One run per setting.
- Ad killed while the swap handled the agreement: the swap stayed in `LOCKING`, the handler was retried with backoff, and the claim succeeded 5 s after Ad was back (chaos scenario 2).
- A stuck agreement was republished by a sweeper, and a late lock reopened a negotiation the sweeper had cancelled (scenarios 9, 10 and 12). Not tested: a crash between the lock commit and the swap update.
- Sources: `docs/adr/drafts/take-the-exclusive-ad-lock-in-the-ad-service.md`, `docs/testing/chaos-test-results.md` and `docs/testing/load-test-results.md` in `Taakht-MVP`.

## References

- [Architecture overview](../architecture/architecture-overview.md), "Flow 1"
- [Decisions log](../architecture/architecture-decisions-log.md), "Phase 2 of Event Storming review" (the lock entries)
