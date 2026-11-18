# Open items

Questions that are not decided yet, and decisions that will need an ADR when their time comes. An item is open when its answer has to be decided or needs an ADR (see the [ADR rules](adr/README.md)). Details that only become clear when building go in the matching guideline under "To decide when building", not here. Decisions already taken are recorded in the [ADR index](adr/README.md); the item numbers below stay stable because ADRs and guidelines refer to them.

No design question blocks the work at the moment. What is left is listed under "Later" and in the domain and product questions.

## Later

Each of these will need an ADR when its time comes.

| # | Topic | Note |
|---|---|---|
| 16 | Matching sharding and scaling | A production concern. The city is a natural shard key, since matching never crosses cities. Needed when the index outgrows one node |
| 21 | WebSocket push for chat | Phase 1 chat is REST with polling, and the message model is the source of truth. Push comes later on top of it, as an exception to ADR 0007; across several Communication instances it also needs a backplane |
| 23 | Hosting beyond one node | A domestic cloud or PaaS, Kubernetes (tried locally first), several nodes and replicas. For now one node per environment, see the [deployment guideline](guidelines/deployment.md). Several nodes break the isolated-network assumption of ADR 0010 |
| 25 | Ad content moderation by an LLM | Direction (2026-11-15): not needed from day one; parked until the design is complete and the first development phase runs. To design: what is checked (text now, media if uploads come), before or after publishing, which model and provider the team can reach and pay, appeals |
| 27 | A hold on the Ad after `AgreementReached` | Postponed (2026-11-15), if the need appears; it would extend ADR 0017. To decide then: whether the hold blocks only new requests or also a second agreement on the same Ad, the release rules, whether a negotiation waiting for the lock is frozen, and what "available" means for `ClaimLock` (Published or Hidden, not Locked, Closed or Suspended), with a rejection reason `NotAvailable` that cancels the negotiation |
| 28 | mTLS between services | The production target of ADR 0010; the certificate mechanism is chosen with the hosting platform |
| 29 | Redis | Out of the design (ADR 0019). It returns, as its own ADR, only for a concrete need: a cache shared by several instances, a backplane for chat across several Communication instances, or a global rate limit across several gateways |

## Closed

Decided. The ADR index is the record; the numbers stay for the references.

| # | Topic | Closed by |
|---|---|---|
| 1 | Monorepo | ADR 0004 |
| 2 | Microservice architecture | ADR 0001 |
| 3 | Service decomposition | ADR 0002 (Deal: ADR 0016) |
| 4 | Languages: Go and .NET | ADR 0003 |
| 5 | Sync versus async rule | ADR 0005 |
| 6 | Kafka | ADR 0011 |
| 7 | API contract in Protobuf | ADR 0006 |
| 8 | Kafka event format | ADR 0012 |
| 9 | Transactional outbox | ADR 0014 |
| 10 | Consistency and cross-service workflows | ADR 0015, 0017 and 0018 |
| 11 | Data ownership and local read models | ADR 0002, 0005 and 0015; the read models are listed in the [architecture overview](architecture/architecture-overview.md) |
| 12 | PostgreSQL | ADR 0013 (Communication uses it too) |
| 13 | Event sourcing in Reputation | ADR 0021 (Accepted): the admin queue and blocking are modelled as events too; the store, the upcasting policy and the payload are in the [event store conventions](guidelines/reputation-event-store.md) |
| 14 | Search | ADR 0019 |
| 15 | Neighbourhood distance and the role of Redis | ADR 0019 |
| 17 | Edge API style | ADR 0007 |
| 18 | Envoy as the edge gateway | ADR 0008 |
| 19 | JWT validation at the edge | ADR 0009 |
| 20 | Service-to-service authentication | ADR 0010 |
| 22 | Observability stack | ADR 0020 (Accepted); alert thresholds, retention, sampling and where it runs are in the [observability conventions](guidelines/observability.md) |
| 24 | Ad and Proposal versioning | The domain docs, no ADR (the MVP draft "bind approvals to versions" described the earlier approval model and is not carried over) |
| 26 | Rate limiting at the edge | The [rate limiting conventions](guidelines/rate-limiting.md) |

## Domain and product questions

Open questions that are not ADRs. Carried over from the Persian working list in TaakhtDraft and checked against the decisions made up to 2026-11-17. Items resolved by the Event Storming work are not repeated here (see [event-storming.md](domain/event-storming.md)).

**Ad**
- Expiry rules for an Ad, and whether `AdExpired` is needed (the states are only `Published ⇄ Hidden`, plus `Locked` and `Closed`).
- Pruning of `AdVersion`s that no Swap refers to (data retention).

**Trust and safety**
- Reputation: the formula, the minimum score for an instant abuse-report effect, and the size of penalties.
- The shape of the review answer (yes/no or score) and its time window (currently 5 days).

**Partners and real-team questions**
- Locker partner: the integration capabilities needed (payment session with expiry, status webhook, status inquiry, `CancelReservation`, automatic refund for a late payment), and the locker price model (who sets it).
- Paying the locker fee with Divar's wallet: public sources say it cannot be relied on. Keep a `LockerFeePaymentProvider` interface; a Divar-wallet adapter is optional and depends on Divar allowing payments to a partner.
- For a real Divar team: is Divar's existing chat enough, or is a deal-specific chat needed (moderation, evidence in disputes)?
- Research on informal swap competitors (Telegram and Instagram groups, Vinted-like apps), so that "why Divar wins" rests on evidence.

**Parked ideas** (pricing sub-domain, hotspots, multi-item Ads, filming, trust badge, and the user-interface questions) live in [product/parked-ideas.md](product/parked-ideas.md).

## Not ADRs (go elsewhere)

| Topic | Where |
|---|---|
| Internal service structure (Clean Architecture, ports and adapters, CQS) | Guidelines |
| Identifier strategy | Guidelines (promote to an ADR if it turns out to be expensive to change) |
| Config and secrets handling | Guidelines |
| Retry, timeout and DLQ patterns | Guidelines |
| API versioning, errors, pagination, caller identity | API conventions (draft) |
| External providers behind ports with mocks | README, "Key design decisions" |
| Privacy of location and user data | Product or domain docs |
| Media storage | Not in the README scope yet; add an item if images become part of it |
| Load and stress test tooling | Testing docs |
| Commit, PR and CI conventions | Process docs |
