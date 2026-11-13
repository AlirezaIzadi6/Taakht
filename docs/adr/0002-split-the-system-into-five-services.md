# 0002. Split the system into five services

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

[ADR 0001](0001-build-the-system-as-microservices.md) chose independently deployable services, each owning its data, and left the boundaries open. They come from the domain exploration (Event Storming and the [context map](../domain/context-map.md)). A wrong boundary is expensive to move, so each split below has a stated reason, and the rejected merges are listed.

## Decision

We will build five services:

- **Ad** (Core): Ads, their versions and the atomic claim that locks them.
- **Matching** (Core): the Ad index and candidate discovery.
- **Deal** (Core): negotiation and swap together, as one service.
- **Reputation** (Supporting): the trust score, abuse reports and blocking.
- **Communication** (Supporting): notifications and a minimal chat.

Everything outside the swap domain (the Ad registry, authentication, KYC, the locker partner, the notification channel, report intake) sits behind provider adapters with mock implementations and is not a service.

## Options considered

1. **Five services (chosen)** - each boundary below follows a difference in data or consistency needs.
2. **Merge Ad and Matching** - Ad is the source of truth and carries the atomic lock in a transactional database; Matching is a derived index (Elasticsearch and Redis) that can be rebuilt from Ad events and tolerates eventual consistency. Ad events also have several consumers (Matching, Deal, Communication).
3. **Merge Reputation into Deal** - Reputation keeps its own event store (event sourcing), and `UserBlocked` is consumed by Deal, Matching and Communication, so it cannot belong to one of its consumers.
4. **Merge Communication into Ad or Deal** - Communication is a downstream listener of all domain events that calls the external notification provider; inside Ad or Deal it would tie them to that provider and to the text of every event.
5. **Fewer, coarser services** - ADR 0001 already covers this; going coarser removes the Ad, Deal and Matching boundaries where the consistency problems appear. Whether Negotiation and Swap are one service or two is decided separately.

All merges were argued from the existing design documents; nothing was prototyped.

## Consequences

- Positive: each service has one reason to change and its own storage model.
- Negative: data copied between services is only eventually consistent (for example Matching's copy of trust scores); moving a boundary later means moving code, possibly across languages.
- Revisit when two services are repeatedly changed together, as ADR 0001 says.

## References

- [Architecture overview](../architecture/architecture-overview.md), [context map](../domain/context-map.md), [decisions log](../architecture/architecture-decisions-log.md) ("Separate Ad service from Matching", "Closing the Reputation and Trust cluster", "Closing the Communication cluster")
