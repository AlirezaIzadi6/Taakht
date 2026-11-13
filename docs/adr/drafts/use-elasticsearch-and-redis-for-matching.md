# XXXX. Use Elasticsearch for the Matching index and Redis for neighbourhood distances and caching

- Status: Proposed (draft, not yet numbered; to be discussed first)
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): Matching

## Context

Matching ([ADR 0002](../0002-split-the-system-into-five-services.md)) keeps a derived index of Ads. It is built from Ad events and can be rebuilt from them ([ADR 0010](../0010-use-kafka-for-events-between-services.md)). Search is neighbourhood-based: a user picks neighbourhoods, there is no coordinate-level filter, and the distance between two neighbourhoods adds to the match score. Distances come from a precomputed matrix ([matching rules](../../domain/matching.md)). Ranking also uses the owner's trust score, which Matching keeps as a local copy. The durable source of truth for Ad data is PostgreSQL (README, [architecture overview](../../architecture/architecture-overview.md)).

## Decision

We will keep the Matching index in Elasticsearch, which also ranks results by the Criteria, the neighbourhood filters and the trust score, and use Redis for the neighbourhood distance data and for caching. PostgreSQL stays the durable source of truth; the index is rebuilt from events when needed.

## Options considered

1. **Elasticsearch for the index and ranking, Redis for distances and caching (chosen)** - the decisions log gives two reasons: result ranking depends on a search engine, and all the data cannot be kept in Redis. Redis serves the precomputed neighbourhood distances and fast lookups.
2. **A PostgreSQL read model in Matching, scored in Go** - what the Taakht MVP did (`ad_index` filtered in SQL, six static neighbourhoods, no Elasticsearch or Redis). It worked at the MVP's small scale; the point where it stops working was not measured. Not chosen because the design targets a large platform (ADR 0001, product driver) and a search engine is the intended stack for ranking at that size. Not tried at scale.
3. **Elasticsearch without Redis** - the neighbourhood distance matrix is small and could be held in the service's memory or in the index. This would remove a component; Redis stays for now because of the decision above, and its role is the least settled part of this ADR.

## Consequences

- Positive: ranking, filtering and scoring in one engine built for them; the index is derived, so it can be dropped and rebuilt from the Ad events.
- Negative: two more components to run, including on a single-node demo; the index is eventually consistent with the Ads and with the trust scores it copies ([open-items.md](../../open-items.md), item 11); idempotent handling of Ad events is needed to rebuild safely ([ADR 0014](../0014-deliver-events-at-least-once-with-idempotent-consumers.md)).
- Revisit Redis when Matching is built: if the distance matrix fits in memory and no shared cache or sorted set is needed, Redis can go. Matching's sharding at scale is a separate open question (item 16).

## References

- [Decisions log](../../architecture/architecture-decisions-log.md), "Stack" section (Elasticsearch, Redis, PostgreSQL) and the neighbourhood-based search entry
- [Architecture overview](../../architecture/architecture-overview.md), "Datastores"
- Taakht MVP: `docs/product/mvp-plan.md` ("Search: plain SQL in the matching database")
