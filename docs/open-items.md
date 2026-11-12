# Open items

Decisions that are not yet recorded as ADRs, in the order we work through them. Rule: an item stays here until it is decided; only then does it become an ADR (see [ADR rules](adr/README.md)). ADR numbers are given in the order ADRs are accepted, not by the position in this list.

## How each item is handled

1. Discuss the question and the real options with the owner.
2. Gather evidence for the options (documentation, spike where it matters) and note what remains unverified.
3. The owner confirms the decision.
4. Write the ADR (rejected options and reasons included), give it the next number, add it to the [ADR index](adr/README.md), and remove or mark the item here.

## Status values

- **Open** - not decided.
- **Stack named** - the README names the choice, but the reasoning and rejected options are not recorded.
- **Draft** - a draft ADR exists on the branch `docs/adr-drafts` (not on `main` until it is reviewed, one ADR per branch); spike-verified drafts say so inside.
- **Done** - ADR accepted (link it).

## Order

| # | Topic | Status | Draft or note | Depends on |
|---|---|---|---|---|
| **Foundation** | | | | |
| 1 | Monorepo | Decided | ADR 0003 (Accepted); README, Makefile and CI already assume it; shared infrastructure code between services is not shared for now | 2, 4 |
| 2 | Microservice architecture | Decided | ADR 0001 (Accepted); reasoning from published guidance, not an experiment; three drivers (product, demonstration, team and learning) | - |
| 3 | Service decomposition | Draft | `structure-deal-as-modular-monolith`; working boundaries: Ad and Matching (Go), Deal (combining negotiation and swap as a modular monolith in .NET) and Reputation (.NET); Communication (Go, minimal) and a mocked `DeliveryProvider`; context map in [docs/domain/context-map.md](domain/context-map.md). The ADR covers Deal only; why Ad is apart from Matching, and why Reputation and Communication are separate services, is not recorded in an ADR yet | 2 |
| 4 | Languages: Go and .NET, and the rule that splits services between them | Decided | ADR 0002 (Accepted); Go for Ad, Matching and Communication, .NET for Deal and Reputation; no benchmark, rests on the learning goal and a workload rule of thumb | 3 |
| **Communication** | | | | |
| 5 | Sync versus async rule (gRPC for requests, events for state changes) | Stack named | README | 3 |
| 6 | Kafka | Stack named | README; project memory | 5 |
| 7 | API contract in Protobuf | Decided | ADR 0004 (Accepted); tried only with a hello-world service in a spike (Go and .NET); the other alternatives were argued, not tried | 1, 5 |
| 8 | Kafka event format (Protobuf, no registry for now) | Draft | `describe-kafka-events-in-protobuf`; spike-verified; registry options compared from documentation only | 6, 7 |
| 9 | Transactional outbox | Stack named | README | 6, 12 |
| 10 | Consistency and cross-service workflows (saga or process manager, idempotent consumers, the exclusive lock) | Draft | `use-saga-and-compensating-transactions-for-locking`; eventual consistency with Saga and compensating transactions; driving forces explicitly weighted | 5, 6, 9 |
| **Data** | | | | |
| 11 | Data ownership and local read models | Open | Known read models: Matching's copy of trust scores and of Ad lifecycle; copies of blocked user pairs in Deal, Matching and Communication; the open-negotiation counter in Ad (an eventual copy, the cap is enforced atomically by Negotiation). Consistency windows are accepted but not measured | 3, 10 |
| 12 | PostgreSQL | Stack named | README; whether any NoSQL store is needed is still open; storage for Communication is not chosen (Deal uses PostgreSQL) | 11 |
| 13 | Event sourcing in Reputation (including upcasting) | Stack named | README; project memory; Reputation's scope grew (blocking, abuse-report queue, penalties), so check that event sourcing still fits the admin queue and blocking | 8, 12 |
| 14 | Search (Elasticsearch for search and match scoring) | Stack named | README | 3 |
| 15 | Neighbourhood distance and the role of Redis | Stack named | README says Redis for geo; search is now neighbourhood-based (precomputed distances between neighbourhoods, no exact coordinates), so the Elasticsearch/Redis split must be redefined | 14 |
| 16 | Matching sharding and scaling | Open | README: approach TBD, production concern | 14, 15 |
| 24 | Ad and Proposal versioning (state-based, immutable `AdVersion`, three-component proposal snapshot with no implicit approval) | Open | Decided in the domain docs; the ADR is not written. Open: pruning of `AdVersion`s unrelated to a Swap | 3, 10 |
| **Edge and security** | | | | |
| 17 | Edge API style | Stack named | Decided 2026-10-06: REST only, no GraphQL (revisit if different clients appear). Reasoning and rejected options are still to be written (not covered by ADR 0005; a short ADR of its own, or a decision-log note) | 7 |
| 18 | Envoy as edge gateway and REST to gRPC transcoding | Decided | ADR 0005 (Accepted); tried only with a hello-world service in a spike, grpc-gateway compared in the same spike; rate limit values are configuration, not an ADR | 7, 17 |
| 19 | JWT validation at the edge | Decided | ADR 0006 (Accepted); tried only with a hello-world service in a spike | 18 |
| 20 | Service-to-service authentication | Draft | `authenticate-service-to-service-calls`; reasoning only, no spike; may merge with 19. Decided 2026-10-06: no service-to-service authentication in phase 1; production later | 19 |
| 21 | Real-time channel (chat, notifications) | Open | Communication service combines WebSocket chat and system notifications; chat is minimal/mocked in demo and independent of negotiation | 5 |
| **Operations** | | | | |
| 22 | Observability stack | Open | README: only Logstash named, rest TBD | - |
| 23 | Deployment topology and scaling | Open | README: open | 1, 2 |

## Product and domain questions

Open questions that are not ADRs. Carried over from the Persian working list in TaakhtDraft and checked against the decisions made up to 2026-11-08. Items resolved by the Event Storming work are not repeated here (see [event-storming.md](domain/event-storming.md)).

**Matching and search**
- Neighbourhood distance: the neighbourhood centres and where the data comes from, and the selectable distances for the optional distance filter (a polygon-based matrix is parked). See [matching.md](domain/matching.md).
- Matching logic with incomplete information: what counts as a match, scoring, thresholds, how unknowns are treated.
- Exact fields of an Ad and of Criteria (category, neighbourhood, photo, description, the "I want" side, visibility, notification; value and price are parked, see [parked ideas](product/parked-ideas.md)). The minimum for an Ad is decided, see [matching.md](domain/matching.md).
- Whether a separate, clearly labelled row of broader or fuzzier results is shown next to exact matches (a complement to the Cold Start solution).
- How "search by what I am looking for" works for a user with no Ad yet, and the UX of the call to action "create an Ad first".
- Home page: which Ads are shown and in what order (no filter, or by the user's neighbourhood).

**Ad**
- Expiry rules for an Ad, and whether `AdExpired` is needed (states are only `Published ⇄ Hidden`, plus `Locked` and `Closed`).
- Cap on the number of items in one deal (a small, configurable MVP value; it belongs to the Ad, not the Proposal).

**Trust and safety**
- Reputation: the formula, the minimum score for an instant abuse-report effect, and the size of penalties.
- The shape of the review answer (yes/no or score) and its time window (currently 5 days).
- Rate limiting and spam control for chat started from any Ad (mute is a later phase).
- The price difference and the risk of non-payment: no technical action for now; risks to be researched.
- Identity edge case in locker handover disputes (mandatory identification, handing the claimant the defendant's claim checklist): probably obsolete under the zero-liability model; confirm and close.

**Partners and real-team questions**
- Locker partner: the integration capabilities needed (payment session with expiry, status webhook, status inquiry, `CancelReservation`, automatic refund for a late payment), and the locker price model (who sets it).
- Paying the locker fee with Divar's wallet: public sources say it cannot be relied on. Keep a `LockerFeePaymentProvider` interface; a Divar-wallet adapter is optional and depends on Divar allowing payments to a partner.
- For a real Divar team: is Divar's existing chat enough, or is a deal-specific chat needed (moderation, evidence in disputes)?
- Research on informal swap competitors (Telegram/Instagram groups, Vinted-like apps), so that "why Divar wins" rests on evidence.

**Negotiation and lock** (to think about later; affects the locking ADR)
- Idea from Arman: when a negotiation on an Ad reaches `AgreementReached`, mark the Ad as being in an agreement, so no new negotiation opens on it and two agreements on the same Ad cannot happen at once. One possible shape: a hold per Ad in Deal's own database (unique `ad_id`), inserted atomically with `AgreementReached` and released when the lock result arrives (rejected), or replaced by the Ad's `Locked` state.
- To decide: whether the hold blocks only new requests, or also a second agreement on the same Ad (the second approval is refused and not recorded); the release rules; whether a negotiation waiting for the lock is frozen (no edit, approve, retract or withdraw, with its expiry timer paused); and what "available" means for `ClaimLock` (Published or Hidden, because an Ad hidden by the negotiation cap must still be able to conclude; not Locked, Closed or Suspended), with a new rejection reason `NotAvailable` that cancels the negotiation.
- Effect: the `AlreadyLocked` path of the Saga would almost disappear and the Ad claim would remain the final authority, so the locking ADR has to be revised.

**Parked ideas** (pricing sub-domain, hotspots, multi-item Ads, filming, trust badge) live in [product/parked-ideas.md](product/parked-ideas.md).

**Scope, process and writing**
- Alignment of technology choices with the skills the team wants to learn (for example for deployment in a later phase).
- Testing and observability details: pilot services for TDD, target numbers for metrics (consult experts), the load-test tool, and the format of the results overview page.
- Name and structure of the directory for AI-generated files (see [development-process.md](process/development-process.md)).
- Structure of the cover letter.

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
