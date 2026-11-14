# 0019. Use Elasticsearch for the Matching index, without Redis for now

- Status: Accepted
- Date: 2026-11-14
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): Matching

## Context

Matching ([ADR 0002](0002-split-the-system-into-five-services.md)) keeps a derived index of Ads, built from Ad events and rebuildable from them ([ADR 0011](0011-use-kafka-for-events-between-services.md), [ADR 0015](0015-deliver-events-at-least-once-with-idempotent-consumers.md)). The [matching rules](../domain/matching.md) set what it must do:

- Two-way matching: each side's "have" must fit the other's "want".
- Categories form a tree, and the further apart two compatible categories are, the lower the score.
- Search is neighbourhood-based, one city per Ad, with a distance between neighbourhoods taken from a precomputed matrix.
- The score has three weighted factors (category fit, closeness, trust) and is ranked per viewer. There is no text search.

The durable source of truth for Ad data stays in PostgreSQL ([ADR 0013](0013-use-postgresql-as-the-service-database.md)).

## Decision

We will keep the Matching index in Elasticsearch, which also ranks results by the weighted factors. The category tree and the neighbourhood distance matrix are small, static reference data and stay in the memory of the Matching service. We will not use Redis for now; it returns only with a concrete need, as its own decision.

## Options considered

1. **Elasticsearch for the index and ranking, reference data in service memory (chosen)** - filtering, scoring and ranking in one engine that can be sharded when the index grows (the city is a natural shard key, since matching never crosses cities). Cost: a JVM component with its own memory and mapping to run, and a reindex when the fields used for scoring change.
2. **Elasticsearch and Redis, as the README planned earlier** - Redis was meant for geo queries and sorting. Search later became neighbourhood-based, so the geo need went away; ranking is done by Elasticsearch; the distance matrix is small enough for service memory (a few tens of thousands of numbers if a city has a few hundred neighbourhoods, an estimate); and a second copy of the same data would have to be kept in step with the index.
3. **A PostgreSQL read model in Matching, scored in Go** - what the Taakht MVP did (`ad_index`, six static neighbourhoods). At the MVP's small data size and on one laptop, search p95 stayed under 25 ms up to about 1,500 requests per second; this says nothing about a large index. Not chosen because the design targets a large platform (ADR 0001) and the project wants a search engine as the ranking stack. Not tried at scale.

None of the three was tried at scale.

## Consequences

- Positive: ranking, filtering and scoring in one engine; the index is derived, so it can be dropped and rebuilt from Ad events; one fewer component than the README planned.
- Negative: Elasticsearch is heavy for a single-node demo; the index is eventually consistent with the Ads and with the trust scores copied into it ([open-items.md](../open-items.md), item 11); rebuilding needs idempotent event handling ([ADR 0015](0015-deliver-events-at-least-once-with-idempotent-consumers.md)); every Matching instance holds its own copy of the reference data and needs a reload when the data changes.
- Redis comes back, as a separate ADR, only for a concrete need such as a cache shared by several instances, a backplane for real-time chat across several Communication instances, or a global rate limit across several gateways.
- Matching's sharding at scale is a separate open question ([open-items.md](../open-items.md), item 16).

## References

- [Matching rules](../domain/matching.md), [decisions log](../architecture/architecture-decisions-log.md) (the neighbourhood-based search entry), [architecture overview](../architecture/architecture-overview.md)
- Taakht MVP: `docs/product/mvp-plan.md` ("Search: plain SQL in the matching database") and `docs/testing/load-test-results.md`
