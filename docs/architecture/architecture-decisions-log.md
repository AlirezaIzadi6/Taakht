# Architecture Decisions: Taakht Project (Divar)

> This file is the raw, chronological log of decision-making (a rough draft). For the refined version and the current state, see `architecture-overview.md`.
> **Terminology note (1405/07/02):** "Barter Intent" → **Ad**; "Match Request" / "Negotiation Thread" → **Negotiation** (opened by `SwapRequested`) and **Proposal** (versions of the details); "Trade" → **Swap**. Older sections of this log keep the old names to preserve history; see the shared-language table in `architecture-overview.md`.

## Priority of depth versus breadth
- Decision: **breadth is preferred**, not depth-first from the start. Alireza explicitly said that going deep early on one area loses the big picture.
- Real time frame: 4 to 6 months or more.
- Implication: a full microservice architecture (3 services + shared infrastructure; technologies TBD) is kept as the overall shape; the exact build order is still open.

## Idea recorded for later (not part of this project)
- Optional filming of the item from several angles while creating an Ad: both good input for computer vision and more trust for the counterparty.

## High-level service map (redefined from scratch)
Three services + shared infrastructure:
- **Matching:** candidate discovery only; no transactional logic; a light, read-only copy of active Ads (synced through events).
- **Trade Orchestration:** full owner of the deal lifecycle, exclusive lock, escrow, checklist, **and from now on: scoped chat + negotiation threads**.
- **Delivery & Notification:** a stateless adapter layer for courier/locker/notification.
- Ephemeral storage (locks, cache) and durable storage (Trade Orchestration's source of truth): technology TBD. Gateway: client entry. Sync between services: event-based (broker TBD).
- **General principle:** the public Ad / user / photo belong to Divar; they are only referenced, not rebuilt.

*(Note: the three-service map above was later broken into Matching / Negotiation / Settlement / Communication / Delivery & Notification; see below. The current and final version is always in `architecture-overview.md`.)*

## Chat
- Real architecture (in Divar): use Divar's existing chat; only a reference/thread is opened.
- For the demo: a minimal, scoped chat (only between the two parties of one negotiation thread) built on the same real-time channel (technology TBD) that was needed for timers/deal state. Owner: Trade Orchestration (later: Communication). **Obsolete (07/13):** chat is not tied to Negotiation; see item 7 below.

## Multi-candidate matching model (important decision)
- A distinction was made between the "exclusive lock on the item" (active only at the moment of final two-sided approval) and the "negotiation thread" (several can be open at once for one item, non-exclusive).
- Until a thread reaches CONFIRMED, the item stays `AVAILABLE` and can be offered to other candidates (C, D, ...).
- When a thread reaches CONFIRMED: (1) the real atomic lock is taken, (2) an event is published that CANCELS all other open threads and informs their parties, (3) the item is removed from the active Matching index.
- Full owner of the thread lifecycle and the final lock: Trade Orchestration (later: Negotiation for the thread lifecycle, Settlement for the final lock). Matching is unaware of threads and only sees the item's availability.

## Photos
- Public Ad photos: only a reference URL to what Divar has; we build no upload.
- ~~Delivery-checklist photos: a minimal upload is needed.~~ **Corrected:** the checklist is only flags/text (no breakage, matches the description, physical test), with no photos. No upload mechanism is needed for the checklist.

## Negotiation Thread cap
- Decision: **5 concurrent threads allowed** per Ad/user for the current phase. It may later become elastic based on the reputation score.

## Bounded Context review: Trade Orchestration was getting fat
- It was recognised that "Trade Orchestration" put two different domain models with different consistency/scale needs under one roof: (1) **Negotiation** (before final approval: threads, chat, multi-candidate; high volume, short life, no money) and (2) **Settlement** (after final approval: exclusive lock, escrow, checklist; lower volume, critical, strict).
- **Final decision (1405/07/01):** Negotiation and Settlement become two separate physical services, not one service with two bounded contexts. Reason: the consistency/scale profiles of the two had already been found asymmetric (high volume / short life / no money versus low volume / critical / strict), and by the "merge rule" below, that asymmetry alone is enough to keep them apart.

## Authentication
- Basic level (login/session): entirely external, only consumed (Divar's JWT/token is validated at the Gateway).
- Higher KYC / identity level (for locker/courier handover): **unknown external assumption**; it is not known whether Divar has it for all ordinary users or only for certain sections. It is mocked for the demo and documented as a known dependency/limitation.

## Transport (locker / courier / in person). **Revision 1405/07/11:** only two methods, see below
- Entirely new, belongs to this feature (Divar does no logistics). Owner: **Delivery & Notification** (later: Delivery/Logistics).
- In person: metadata only, no external contact. Courier/locker: an adapter to an external partner's API + receiving status through a webhook + publishing an event to Settlement to advance the checklist/state.

## Reference data
- Geographic division (city/neighbourhood) and the full category taxonomy belong to Divar and are referenced by ID only.
- **Whitelist of categories allowed for swapping:** a new business rule specific to this feature; a small independent configuration that we own. Where it applies: when an Ad is indexed in the **Matching Service**.

## General principle for the number of services (important: a guide for later decisions)
- Running on a laptop naturally pulls every project toward a monolith; but since Alireza already has enough experience writing modular monoliths and this project is exactly the place to practise **real microservices**, the conscious decision is to go toward real microservices, not a monolith for convenience of running.
- **Merge rule:** two Bounded Contexts may share one service/monolith only if their scalability/consistency profiles are really comparable. If it is already "obvious" that two are not comparable in scalability (like Negotiation versus Settlement), they must not be merged, regardless of ease of running.
- This rule directly affected the Negotiation/Settlement decision (above, resolved): because their profiles were already found asymmetric, they were split under this rule. *(Note: this was later revised on 1405/08/14; see the last section.)*

## Bounded Context review: Delivery & Notification was also getting fat
- It was recognised that three different concerns lived under this name: (1) chat between two users, (2) system notification (simple fan-out, best-effort), (3) the B2B logistics adapter (courier/locker, webhook-driven).
- Decision: **Delivery/Logistics** is separated (the B2B adapter). Chat + notification stay together as the **Communication Service** (both reach the client through the same real-time channel (technology TBD); separating them is not worth the extra complexity).
- Note: the number of services naturally grows from the "initial 3" (Matching, Negotiation, Settlement, Communication, Delivery/Logistics). This was accepted consciously, because breadth has priority.

## Ad creation and the extra data specific to this feature
- The base Ad: entirely Divar's; only events are consumed.
- **The category whitelist must expose a small public API from Matching**, because Divar's ad-creation flow has to ask us in order to decide "show the swap toggle or not".
- **Barter Intent** (item-specific attributes/dimensions + the initial self-declared condition): a new record and API, owner: **Matching**.
- The delivery checklist: still belongs to the settlement phase, owner **Settlement**.

## Eligibility Config (category whitelist + active pilot neighbourhoods)
- Both are conceptually one thing: "is this Ad/neighbourhood eligible for swapping?". They were merged into a single **Eligibility Config**.
- Location: a small config under **Matching**, not a separate service.

## Commission and payment. **Obsolete (1405/07/11)**, see the section "No money, two delivery methods" below
- Raw payment/card data: **never** stored/processed by this system; always through a redirect to Divar's existing PSP + receiving a webhook.
- ~~Escrow orchestration and commission calculation: belongs to Settlement.~~ Removed.

## Search model: fuzzy browsing versus exact request (new idea: needs integration with Flow 1)
- New insight: the user's interaction with Matching has two separate phases, not one.
  1. **Browse:** the user specifies only a general category and sees the list of available options in it; at this stage they need not have entered the full details of their own item. Search in this phase can be **fuzzy** (wider range, looser filter), even from both sides (both what the user sees and what is shown to others).
  2. **Match Request:** when the user decides to make a request to one of the options, **they must first enter the full details of their own item (Barter Intent)**, and then the match request / negotiation thread opens.
- Effect on the current design: Flow 1 (section 4 of the architecture overview) assumes the Barter Intent is already complete before Matching indexes the Ad; this idea shows a separate step, "browsing without commitment to details", should be added before it.
- Possible effect on the API: Matching probably needs two separate endpoints/models: one for fuzzy browse (light, no Barter Intent needed from the searching user) and one for submitting the exact request (which makes the full Barter Intent a prerequisite).
- The final decision/details are still open; see `open-items.md`.

## Three follow-up discussions on the browse model (still under discussion, not final)
- **Showing identity:** the initial proposal and inclination: a user's identity/name is shown to the counterparty only once they have entered a real Negotiation Thread (that is, chosen and made a Match Request). In the Browse phase an anonymous id/generic avatar is enough. This is a subset of the Browse/Match Request distinction above, not a separate rule.
- **Broader/fuzzier results next to exact matching:** since exact pairing is inherently rare, it was suggested to show, next to "exact matching", a separate, explicitly labelled row of more related/broader results (a wider category or radius), complementing the existing Cold Start solution (limited pilot + adjustable radius), this time from the angle of loosening the category rather than only geography.
- **Search with "what I'm looking for" as the entry point (not my own Ad):** the example raised: someone looking for a specific book who has not yet posted their own Ad. This needs no new mechanism: it is the same Browse phase, only the entry point is the desired category/item instead of the user's own Ad. New point: a user with no active Barter Intent must be able to Browse but not to make a Match Request; this must be explicit in the UX (a CTA to create an Ad).

## Side services: Interface + Mock (Ports & Adapters), 1405/06/27
- Decision: all side services (Auth/JWT, ad registry, KYC, Wallet, PSP/Escrow, Delivery, abuse report) exist inside the project at least as a **mock behind an interface**; without them the demo is not doable. Replacing one later with a real implementation is only a wiring change. A full plugin system (registry/dynamic load) is **not** built.
- This decision **obsoletes** the earlier principle "Ad / user / authentication belong to Divar and are not rebuilt" and the sections that depend on it (above), for the demo scope.
- Depth criterion: **Core** = real/deep, **Supporting** = real/shallow, **Generic** = mock. For external dependencies: mock if impossible or carrying no engineering signal. For internal product components (such as geo search inside Matching) the question "mock or real" is meaningless; only "how deep".
- Delivery was split in two: Orchestration (Supporting, real/shallow) and DeliveryProvider (Generic, mock).

## Minimum-filter rule, 1405/06/28
- Browse is anonymous and not stored. To be discoverable / to send a request: at least one filter + an explicit "show me" tick. "Anything for anything" is deliberately not covered (an accepted limitation; abuse is contained by abuse reports / rate limiting).

## ⭐ Key decision: Barter Intent is independent, persistent and class-oriented (1405/06/28)
- **Decision (model A):** our discoverable entity is an independent **Barter Intent**, not a toggle on a Divar Ad. The user is not forced to create an ad / complete a per-item checklist to be seen.
  - **Persistent:** stored in durable storage (technology TBD) and the Matching index is rebuilt from it after a reboot; the user can leave and get a notification (through Communication) when a match appears.
  - **Class-oriented with incomplete information:** both the "I have" and "I want" sides can be incomplete/unknown (the unknowns can outnumber the knowns) and do not necessarily match the criteria of an ordinary Divar Ad. The specific items and the checklist are fixed only at the moment of negotiation.
  - ~~**Optional link** to a Divar Ad (through `AdRegistryProvider`); an ad is not a prerequisite.~~ **Obsolete (1405/07/02):** an Ad has no link to a Divar ad; its structure and lifecycle differ and we manage it ourselves.
- **Reason model B was rejected** (forcing the user to complete their own side fully before being seen): someone with several items of the same kind would have to post an ad for each, which is unreasonable.
- **Consequences:**
  - Replaces/redefines the earlier "Barter Intent" (item attributes before a Match Request) and corrects Flow 1 (which started from a toggle on the ad).
  - Matching now **owns** the Intents (not merely a read-only copy of Ads) and must do two-way matching with incomplete information + async matching (with each new Intent).
  - The (global) Eligibility Config also applies to the Intent.
- Open details (lifecycle states, incomplete-match logic, fields shared with a Divar ad): `open-items.md`.

## A specific item before final approval + per-item checklist with group tick, 1405/06/28
- **Rule:** the Intent is class-oriented, but before final approval both parties must fix the exact list of the deal's items (with title/attributes). A "several × several" deal is allowed (for example 3 books for 5 specific books), but nothing is locked without specific items.
- **Checklist:** the checklist record is stored **per item** (because a claim of "the item did not match" is at item level); the UX allows a group tick and an individual override. The group tick is only UI convenience, not the data model. (Rejected options: one checklist for everything, which makes an item-level claim impossible; separate mandatory filling for every item, which is too much friction.)
- Photos: per the earlier decision, the checklist has no photos; Ad photos are only a reference to Divar.

## An Intent is editable and is consumed by the deal, 1405/06/28
- Decision: an Intent is one Intent but editable. If the user declared 5 and 3 were agreed, the Intent is edited down to 3 and the deal takes it; the remaining 2 later get a new Intent.
- Rejected option: partial capacity (reducing the Intent's stock after each deal), which added quantity logic and its interaction with the atomic lock.
- **Effect of an edit on open threads:** the edit applies to all open threads and the counterparties are notified. Each approval is tied to a **specific version of both parties' Intent** (and the list of agreed items); each edit advances the version, approvals tied to the old version become invalid, and the thread returns to CHATTING. The edit "reduce to the agreed items" must be done before final approval (order: agree on items → edit Intent → two-way approval → lock). After the final lock, editing is impossible. The previous Intent version is kept for reference in disputes.
- A multi-item Intent (table + dumbbell + barbell...): **outside the MVP**, a parked idea for later; one category per Intent for now.

## Eligibility Config is a source of options, not a rejecting gate, 1405/06/28
- Because Intent creation and lifecycle are in our own domain, a "disallowed category" means nothing to the user: the UI shows only allowed categories/neighbourhoods. Server-side validation is kept only for API security.
- Decision: the taxonomy is Divar's (referenced by ID, from `AdRegistryProvider`); the whitelist of categories and active pilot neighbourhoods is an independent list that we own (neither a flag on Divar's data nor a separate taxonomy). Selectable list = taxonomy ∩ whitelist.
- Reason a separate taxonomy was rejected: it would go out of step with Divar, and an optional link from an Intent to a Divar ad needs shared IDs. Reason a flag on Divar's data was rejected: it requires a change to Divar's data.
- A change to the Eligibility Config affects only **new** registrations; existing Intents in a category/neighbourhood that left continue to work.
- The whitelist API previously planned for Divar's ad-creation flow (toggle display decision) becomes obsolete with the Intent model; the main consumer is now our own UI.

## Bounded Context discovery from scratch (Event Storming), 1405/07/02
Method: Event Storming without assuming the earlier services. Decisions:

**Naming**
- The main concept is **Ad**, not Intent/Order/Indication. "Intent" feels like a mental state; "Order" suggests a firm commitment and automatic execution (with us, a match only finds a candidate); "Indication" is vague without a financial context and clashes with medical/UI concepts. "Ad" was chosen because the link to a Divar ad was removed and there is no direct homonym collision.
- "Criteria" was preferred to "Search/Query" (more business-like). "Swap" for the exchange (Barter is the name of the whole domain; Exchange has a financial load).
- "Terms / agreement document" felt legal → **Proposal**. "Agreement" is an event (`AgreementReached`), not an entity. `SwapAccepted` was dropped because it implied final approval; `ProposalOffered` was not needed either (a Proposal is created with the request itself). Each party's approval is `ProposalApproved`.

**Model**
- **Criteria** is a value (no identity or lifecycle; not stored; passed directly to matching). **Ad** is a persistent entity that has one Criteria (the "I want" side). They are not the same kind.
- **Ad**: a stable identity + immutable `AdVersion`s; states are only `Published ⇄ Hidden` (no deletion; Draft was dropped because a half-done form stays in the client). An edit creates a new version and Negotiations see the new version.
- To make a SwapRequest you must have created an Ad, but it does not matter whether it is Published (Hidden is enough).
- **`SwapRequested`** = "shall we swap?" (an event). With that initial request, a **Negotiation** is opened and **Proposal** version 1 is created; SwapRequest is not a separate entity. Reason: each requester's Ad can have at most 5 open Negotiations; for a sixth, one of the 5 must be resolved (agreement, rejection, withdrawal, expiry). With `SwapRequested`, the requester automatically gets `ProposalAccepted` (meaning they accept that the negotiation take place; separate from final approval). **Revision 07/13:** `ProposalAccepted` was replaced by `AdApproved` (section "Proposal and double approval" below). Each party's final approval is `ProposalApproved`.
- **Negotiation** = the negotiation stage; two Ads + chat + TTL + state. Non-binding and without money. Its name was chosen at the user's suggestion for the entity, and Proposal became the name of the detail versions inside it.
- **Proposal** = the version of the deal details inside a Negotiation (items, delivery method, cost share, courier window), versioned. An edit creates a **Pending** version (a copy); with the counterparty's approval it becomes Active and the previous one Superseded; only one Pending at a time. Copy was preferred to mutate: each party's approval is tied to a specific version and history comes for free. **Revision 07/13:** the Pending/Active/Superseded model was replaced by "active Proposal + previous version" and double approval, and items left the Proposal (section "Proposal and double approval" below).
- **Cascade rule:** a change to the Ad itself (one-sided) automatically gives all involved Negotiations a new Proposal; approvals become invalid; notification. A change to the terms of one Proposal (two-sided, needs approval) affects only that Negotiation. **Obsolete (07/13):** the cascade was removed; an Ad change only makes the counterparties' `AD` approval stale. *(Reintroduced in a different form on 2026-11-04 and 2026-11-06 as `RebaseProposal`; see the last sections.)*
- **Swap** = the exchange; created from `AgreementReached`, keeps the agreed version immutable and goes up to the history ("my swaps").

**Checklist, transport, money**
- The checklist is two independent concepts: **claim** (each party, before finalisation) and **confirmation** (after physical receipt). If the checklist is mandatory (depending on the item or user), both parties' claims are needed before final approval.
- The delivery method (default in person), the delivery-cost share (default half and half) and the courier time window are part of the Proposal and agreed **before** final approval (not after the lock, so there is no deadlock after it becomes binding). Courier price fluctuation is absorbed by the platform. **Revision:** the courier was removed (07/11); the delivery method and locker fee are now per leg (07/13, below).
- ~~Money is truly blocked at the moment of each party's approval.~~ **Revision (same day):** no money is blocked during negotiation for now; the Proposal is a non-binding negotiation-stage thing. When both parties approve the details, the agreement (Swap) is created, and securing/blocking money comes after it (timing and behaviour on failure: open).
- A Negotiation has a TTL. Each user can approve a Proposal in only one Negotiation at a time for an Ad (this rule stayed even after removing blocked money); the counterparty's approvals in other Negotiations are visible, and to approve another one they must cancel the earlier one. **Obsolete (07/13):** the concurrent-approval restriction was removed.
- The list of events and the chain: `architecture-overview.md`. Undecided items: `open-items.md`.

## Framing "distributed" in the project introduction, 1405/07/02
- Question: the project is practice and will not go to production; is it right to present it as a distributed system from the start?
- Decision: yes, separating three claims: distributed architecture (claimed), distributed deployment (only a design horizon), scale (only with measurement). Text in `README.md` and `architecture-overview.md` section 6.
- Reason: the only environment where this feature works is a platform with enough users for swap liquidity (the same Cold Start argument); so designing for scale is a result of the problem itself, not an unsupported claim. Being distributed comes from the process/data boundary, not the number of machines.
- Consequence: infrastructure overhead on a single node (for example broker memory) is a deployment cost, not an argument against a distributed design. Consistent with the earlier "real microservices" decision and the merge rule: this is a feature on a platform that is itself microservice-based, not a standalone product with zero users.

## Team meeting, 1405/07/02
Source: summaries of the team meeting (the raw files were deleted after being recorded here). Process decisions (Scrum, GitHub, meetings) are in `development-process.md`; only architecture/product decisions are here.

**Storage and search**
- **Elasticsearch:** decided, because the scoring system (ranking of match results) depends on it and all the data cannot be kept in Redis.
- **Redis:** for spatial (geo-spatial) capabilities and sorting.
- **PostgreSQL:** confirmed for strong support and scalability at the SQL level. Open: whether a NoSQL database is needed at all or Postgres is enough.
- The exact division of work between Elasticsearch and Redis in the Matching index (both have geo) is open; see `open-items.md`.

**Infrastructure**
- A professional **API Gateway** for load balancing, rate limiting and authentication (JWT validation).
- **Logstash** was considered for monitoring/observability (the rest of the stack is still open).
- Kafka at this meeting was only an "initial option"; the next day (07/03) it was made final; see the section "Message broker: Kafka" below.
- Language: Go for heavy-load parts, .NET for lighter parts (optional). Whether to build for high scale from the start is not final. (Later on 07/03: "mostly Go, some services .NET".)

**Communication and authentication**
- Internal sync communication: **gRPC** (final). External communication: **REST**.
- **GraphQL** as a complement to external communication (for different client access patterns, such as web versus mobile) will be examined; not final.
- User authentication: **JWT**.
- Service-to-service authentication: **API Key**; a proposal, not finalised.

**Domains**
- The main domains raised: Reputation, Matching, Negotiation, Settlement, Chat, Ad Registry. (Consistent with the separate Ad service made final on 07/03.)
- **Delivery** is separate and for now implemented very shallowly.
- The User and Payment services are mocked (Payment was later removed, 07/11); **notification is also mostly mocked** (chat remains real/shallow).
- A **pricing sub-domain** based on market conditions is probably needed, affecting the proposed price/value (below).

**AI-based pricing**
- Decision: AI is used for price extraction and matching: prices on the internet are extracted and indexed/cached, and the physical condition and damage of the item are taken into account in price estimation. ML or fixed rules are still on the table.
- Because the effect of damage differs by item type (a scratch on a table ≠ a torn book cover), these calculations are probably done **ahead of run time** by an independent system (a secondary/offline processor) and cached so they do not affect real-time performance.

**Product/UI**
- **API only** is built; there is no main UI. If needed, a very simple UI for testing only, at very low priority.
- Home page: the user sees Ads directly.
- **Hotspots:** the user can see the areas with the most demand/deals for a type of item (for example which neighbourhoods have the busiest book swapping).
- **Reporting a user you have not dealt with:** allowed; they might post a fake ad, cause harassment or intend fraud. So an abuse report is not tied to a shared Swap/Negotiation.
- **Watch:** a user can follow changes to an Ad without having a negotiation and get notifications (details: the 07/03 section below).

**Testing and performance**
- Tests: unit + **integration (mandatory)** + end-to-end. TDD was not made final; as a pilot on some services.
- **Load testing is final:** with tools that send synthetic requests; the scripts are kept in the repository; including stress tests. Results and performance-degradation scenarios are documented both as log records and on an overview page (achievements / overall status).
- No target numbers for performance metrics for now (we have no idea of real values); suggestion: consult experts (for example on Reddit).
- Domain and event discovery with **Event Storming** (the work begun on 07/02). An incomplete list of user-level events was also prepared that has not yet been reconciled with the events list of `architecture-overview.md`; see `open-items.md`.

## Separate Ad service from Matching + the watch feature, 1405/07/03
- Decision: Ad creation and lifecycle is a separate service (**Ad**); Matching is only the index and candidate discovery. There is no reason for them to be one.
- New feature (raised at the 07/02 meeting): a user can **watch** an Ad and get notified of its changes (edit, hide, being dealt). Proposal (not final): the watch list lives in Communication, because it is "who wants to hear about what", not Ad domain data.
- Consequence: Ad events cross the service boundary and have several consumers; for example `AdEdited` → Matching, Negotiation, Communication.

## Message broker: Kafka, 1405/07/03
- Decision: **Apache Kafka** for async communication between services. Languages: mostly Go, some services .NET. Internal sync communication: gRPC; edge: REST/JSON.
- Reasons:
  1. **Message pattern:** the list of async messages showed they are mostly domain events (not single-receiver commands/tasks): about 15 events versus one real command (`ArrangeDelivery`).
  2. **Several consumers + ordering per aggregate:** Ad events (after the Ad service was separated) have three consumers and the order of versions per `AdId` is vital; likewise `DeliveryStatusChanged` per shipment. The partition key gives this without tricks.
  3. **Compacted topic:** the Ad topic keyed by `AdId` can be the source for rebuilding the Matching index after a reboot.
  4. **Outbox + CDC (Debezium):** the Outbox relay can be done with CDC; details are settled with the database/Outbox decision.
  5. Secondary (explicitly stated): the industry's common ecosystem and the learning goal.
- Reason against (accepted): memory overhead on a single node.
- Rejected options: **RabbitMQ** (strong in routing/ack/retry/per-message DLQ and suited to a command queue; replay and per-key ordering only with Streams/Super Streams, which is not its native model), **NATS JetStream** (lighter and sync+async with one tool, but we keep gRPC for the protobuf contract and its ecosystem is smaller), **Redis Streams**, **Postgres as a queue**. Temporal is not a broker replacement (the Settlement saga is inside the service itself).
- Boundaries: Kafka is not a source of truth (no Event Sourcing on Kafka); ordering across different aggregates is not guaranteed (the lock/index consistency question is not solved by the broker); domain timeouts (Negotiation TTL, Settlement deadlines) use a scheduler inside the service, not the broker.
- The ADR (in English) will be written later; implementation details (topic names, retry/DLQ pattern, envelope, clients, KRaft settings) do not go in the ADR but in a convention document. Related decisions that need their own ADR: Outbox, delivery semantics (at-least-once + idempotent consumer), partition/ordering strategy, event contract format.

## Reputation: a separate service with Event Sourcing, 1405/07/03
- Decision: Reputation is an independent service built with **Event Sourcing**: relevant events (`SwapCompleted`, `AbuseReported`, withdrawals/expiries, etc.) are appended as Reputation's own facts in its own event store and the score is a projection of them.
- Reason: the score formula will certainly change and must be recomputable from scratch; anti-collusion rules (a cap on deals between a pair of users in a time window) need queries over history; it is a low-risk place to practise ES.
- A conscious note: Reputation has no complex commands/invariants and is inherently a projection; so ES here gives mostly "recomputability and history" rather than a full showcase of the pattern.
- The source of truth is Reputation's own event store, not Kafka retention.
- Hot read path: Reputation publishes a `ReputationChanged` event and Matching keeps the score inside its own index (no sync call per candidate). *(Renamed `TrustScoreUpdated` on 1405/08/14.)*

## No money, two delivery methods, 1405/07/11
Source: Alireza's decision. Two changes that obsolete older sections of this log (escrow, PSP, commission, courier).

**1. The platform locks no money**
- Decision: no escrow, no blocked funds, no wallet, no deposit. Financial transactions (for example the price difference) are entirely the parties' responsibility and take place outside the system.
- Removed: `WalletProvider`, `PSP/EscrowGateway`, the `FundsReleased` event, commission on money, the commitment deposit, the PSP/escrow licensing issue.
- Remains: the Proposal can still record the price difference as part of the agreed terms (recording the agreement only, not moving money).
- Consequence: Settlement is no longer a financial "Settlement"; it owns the Swap from agreement to final outcome, without money. The question of the name (Fulfillment/Swap) became more serious (`open-items.md`). The assumption "money is blocked from the moment the Swap is created" and the question "what if funding fails" are also gone.
- Consequence for the revenue model: only paid matching priority remains.
- Reason (my inference, to be confirmed): removing the legal/financial dependency and simplifying the scope.
- Accepted limitation: the platform does not guarantee payment.

**2. Only two delivery methods**
- **Locker:** the only method the platform integrates with (adapter, status webhook, KYC).
- **In person / courier:** one single option; the parties coordinate themselves. The platform does not call the courier, track it, or absorb its cost.
- Removed: courier integration in `DeliveryProvider`, the courier time window in the Proposal, "the platform absorbing courier price fluctuation", the risk of damage in courier transit.
- Consequence: Delivery Orchestration gets smaller (locker tracking only); for in person/courier only a two-sided receipt confirmation is recorded in the checklist. **Revision 07/14:** receipt confirmation and delivery tracking were also made obsolete; Delivery Orchestration is only the locker payment relay (payment, webhook, inquiry, `CancelReservation`).

## Corrections of 1405/07/11 (after reviewing the README)
- **The Negotiation cap is configurable:** 5 is only a sample/default value, not a hard-coded constant (a correction of the section "Negotiation Thread cap" and references to 5 in the documents).
- **An exact item is optional:** the earlier rule "nothing is locked without specific items" is obsolete. The parties know what they are exchanging; for exact recording, they edit their Ad so the agreement is recorded on that version.
- **An Ad is not deleted; it is closed forever on agreement:** `Published ⇄ Hidden` + the final state `Closed` (still no deletion). `Closed` happens at the moment of `AgreementReached`, together with cancelling the parallel Negotiations, and does not come back. After the agreement the parties follow up themselves. **Revision 07/13:** `Closed` is not at `AgreementReached`; the Ad first becomes `Locked` and becomes `Closed` when no cause can cancel the agreement any more (below).
- **The confirmation checklist (after receipt) was removed:** checking the item is the parties' responsibility. Only the claim checklist remains; `ItemConditionConfirmed` was removed. `SwapCompleted` took a different meaning: the agreement becoming final (07/14), not receipt confirmation.
- **Trust was removed from secondary metrics:** only engagement and inventory activation.

## Proposal and double approval, locker per leg, 1405/07/13
Source: Alireza's decision (the Ad / Negotiation / Proposal versioning discussion). The related older sections (Proposal Pending/Active, the cascade rule, the concurrent-approval rule, `ProposalAccepted`) become obsolete.

**1. Versioning is state-based, not Event Sourcing**
- Ad: identity + immutable `AdVersion` + `current_version_id`. Proposal: an immutable row; only pointers and states change. Event Sourcing only for Reputation (07/03).
- Retention: only the active Proposal, the previous version (for rollback) and the agreed version (forever, part of the Swap). Rejected/cancelled versions are deleted. Reason: only the agreed and active/previous have vital value; the history of the rest is only UX and anti-collusion, for which we have no rule.
- **A Proposal has no items:** everything about items is on the Ad side. The Proposal is only the delivery method per leg and the price difference.

**2. The edit model (active Proposal + previous)**
- A Negotiation always has one active Proposal; at the start: a system Proposal 1 with defaults (both parties must approve; it has no reject option because it is the lowest point).
- Each party: approve or edit. An edit activates the new Proposal immediately, and its author is implicitly approved. The counterparty has only approve or reject (back to the previous) and does not see the edit. Because the counterparty cannot edit a pending Proposal, the depth is always 2 and no stack is needed.
- The author can cancel their own edit (the same rollback); "reject and edit" is a combined action in one transaction.
- Concurrency: each command sends the Proposal id it saw and runs only if it is still active.
- Rollback is one step only and only to the previous version of the same Negotiation; there is no jumping to an arbitrary version (the content can be copied and edited). *(Implicit author approval was dropped on 2026-11-06; see the last section.)*

**3. Two approvals per party**
- An `AD` approval (on the counterparty's Ad version) and a `TERMS` approval (on the Proposal). The agreement is reached when all 4 approvals are valid; valid = `target_id` equals the current value. The `approval` table is overwritten; becoming stale is automatic.
- Entering a negotiation = approving the Ad (the requester with `SwapRequested`; the Ad owner with an explicit "accept negotiation" action that records the version seen).
- Consequence: the cascade rule was removed; an Ad change only makes the `AD` approval stale and the Proposal is untouched. *(Note: this item was revised and cancelled on 1405/08/14 with the three-component snapshot model.)*

**4. No concurrent-approval restriction**
- A user can approve in all open Negotiations; whichever counterparty gives final approval first, that agreement is concluded.
- Consequence: the hypothesis "exclusivity without a distributed lock with the one-approval-per-Ad rule" no longer holds; exclusivity comes only from the atomic lock on the Ads (`open-items.md`, the exclusive lock item).

**5. Locker fee**
- Each person pays their own leg's locker fee. Rejected: wallet/blocking at edit time; it conflicts with the 07/11 decision (no money) and each edit/reject/expiry would be a block and a return.
- The chosen path: an eligibility check (KYC + payment method at the partner) when choosing the locker; direct payment to the partner after `AgreementReached` with a one-hour deadline; non-payment → Swap cancelled + `PaymentTimedOut` for Reputation; refund by the partner's policy (`CancelReservation`).

**6. When an Ad closes (correction of the Closed item of 07/11)**
- An Ad becomes `Closed` when no cause can cancel the agreement any more. For now the only cause of cancellation is non-payment of the locker fee; so `Closed` = payment of both locker legs, or immediately if there is no locker leg.
- Between the lock (`AgreementReached`) and that moment, the Ad is in the transient `Locked` state (out of the index). Cancelling the Swap returns both Ads to their previous state; the Reputation penalty is only for the non-paying party.
- The exclusive lock is the atomic `Locked` transition (not `Closed`); `open-items.md`.
- Parallel Negotiations cancelled at lock time do not come back when the Swap is cancelled, and are not kept; their users must find a new negotiation from scratch (decision, no temporary suspension).

**7. Locker payment details and chat (07/13)**
- A leg = handing over one party's item; its locker fee is with that same sender (they leave the item, the counterparty collects it). Editing the method of both legs is possible for each party (both giving and receiving matter); the eligibility check is for the leg's owner.
- The payment deadline is one hour. The partner handles the payment method and reports status with a callback; if the deadline passes, we expire it ourselves.
- Last-moment payment: we do not reverse the cancellation (the Ad may have been locked again). Instead: the partner's payment session is valid at most until the deadline; at expiry the status is first inquired from the partner (the callback may have been lost); a late payment after cancellation is refunded automatically.
- **Chat is independent of Negotiation:** any user who sees any Ad can open a chat with its owner (like Divar); neither their own Ad nor a Negotiation is needed. So chat is no longer part of Negotiation and its owner is Communication; the old items "chat scoped to the Negotiation thread" are obsolete.
- Divar's wallet is an optional adapter (`LockerFeePaymentProvider`), not the default.
- TTL: not final (under consideration). *(Fixed on 2026-11-06; see the last section.)*

## Incoming cap and automatic Hidden, 1405/07/14
Source: Alireza's decision (reconciling the ES list).
- There is also a cap for **incoming** Negotiations on an Ad. When it fills, `NegotiationLimitReached` is published and the Ad service automatically makes the Ad `Hidden`; its owner is notified. The effect on Matching is the same as an ordinary `Hidden` (it leaves the index), not a separate mechanism.
- ~~No automatic return. The Ad owner must `Publish` again and the cap check is done at that toggle; while there is no room it gets an error.~~ **Obsolete (the counter item below, same day):** automatic publish exists, conditional on the user's tick; manual `Publish` also gets an error while the cap is full.
- The ES list sentences about approval after the agreement (confirming the exchange, item-match checklist) are obsolete. The **claim** checklist (before the agreement) stays. `TermsAccepted` belongs to Taakht. Bookmark and watch are for now the same (to be checked with Arman); the owner of watch, `TermsAccepted` and block/mute will be decided later.
- Cancelling competing negotiations after the lock is a **Policy**, not a collective event: each negotiation emits its own event (`NegotiationCancelled` with a reason). `AdRemovedFromIndex` is also not a domain event (an internal reaction of Matching) and `CounterpartyNotifiedOfEdit` is a Communication reaction.
- There is no ad deletion (only `Hidden`); `NegotiationOpened` is simultaneous with `SwapRequested` and "accept negotiation" is the Ad owner's `AdApproved`. An admin can suspend an Ad (`AdSuspended` / `AdReinstated`; the 6 Mehr decision). Review of `scratch/event-storming.txt`: `event-storming-reconciliation.md`, section 4.
- Locker delivery events, `NoShowReported`, `DeliveryWindowExpired` and `SwapDisputed` are not needed (obsolete). `SwapCompleted` stays but its meaning changed: **the agreement becoming final** (no cause can cancel it any more: payment of both locker legs, or immediately if there is no locker) and the Ads closing, not confirmation of item delivery. It is also a Reputation input; `AdClosed` is a reaction to it. The `Suspended` state is decided with Arman.
- **Delivery Orchestration was merged with Settlement:** after delivery tracking and claim forwarding became obsolete, only the locker payment relay remained (session, webhook, inquiry, `CancelReservation` through `DeliveryProvider`; the one-hour timer). Settlement (Core/Full) owns it. `DeliveryProvider` remains a Generic/mock adapter.
- **The Settlement service was renamed Swap** (07/14): there is no money and the service owns the Swap lifecycle and the locker payment relay. The old names remain in the historical sections of this log; the current documents (`architecture-overview.md`, README) use the new name.
- **Showing identity:** whatever Divar's current behaviour is; a mock in the demo. The rule "identity only after Negotiation" (the earlier proposal in this log) is obsolete.
- **Negotiation cap:** 10 per Ad, **the total of incoming and outgoing** (configurable); it replaces "cap of 5 per requester's Ad" and the separate incoming cap. When it fills, the Ad becomes `Hidden` and a system flag (`system_hold`) is put next to hide/publish to show that the system locked it.
- **The price difference and the risk of its non-payment:** no technical action for now; the risks will be researched later. **In person / courier:** time in the Proposal is a range and optional (it can be left undefined).
- **The open-Negotiation counter in the Ad** (Alireza's idea): the Ad raises and lowers its counter with Negotiation events (opened / ended); reaching 10 → the Ad itself becomes `Hidden` + flag `system_hold` (replacing the separate `NegotiationLimitReached` event); when the counter drops the flag is cleared and the Ad **automatically becomes `Published` again, unless the user removed the tick "show after the limit lifts"** (the tick is visible in the UI after reaching the cap; default on); manual `Publish` checks locally, with no sync call to Negotiation. The counter is implemented with a set of open Negotiation ids (idempotent under at-least-once). The cap is still applied atomically by Negotiation when a negotiation opens (the Ad's counter is an eventual copy).
- **The locker eligibility check in the mock:** pass by default, and rejected for some seeded users so the error path is also seen in the demo.
- **No GraphQL:** only REST at the edge (previously "under consideration").
- **Service-to-service authentication:** none in phase 1; for production later. The old "API Key" proposal (team meeting section 07/02) was dropped.
- **NoSQL and the Elasticsearch/Redis split:** open; NoSQL is examined further and the ES/Redis split needs an ADR. The rest of the stack decisions (Matching scalability, Negotiation/Communication storage, real-time channel, deployment, observability, load-test tooling, Gateway product) each need a separate ADR.
- **Search is neighbourhood-based** (not location-based): the user picks several neighbourhoods, inter-city is allowed, there is no maximum-distance filter, but distance (at neighbourhood level) affects scoring. Effect: a neighbourhood filter and distance data between neighbourhoods are needed instead of exact geo; this feeds the ES/Redis split ADR.

## Phase 2 of Event Storming review and major architecture decisions, 1405/08/14 (2026-11-04)
Source: the phase 2 and 3 Event Storming session, the Open Items review, and finalising context boundaries.

### 1. Merging Negotiation and Swap as a Modular Monolith (.NET) in the Deal context
- **Problem:** physically separating Negotiation and Swap into two independent microservices was examined. Because the platform does not intervene in physical execution or arbitrating the contract (Zero Liability and Blind Handoff) and Swap only acts as a webhook listener and locker-payment state machine, building an independent microservice for Swap would be a nano-service and over-engineering.
- **Decision:** these two clusters are implemented under one Bounded Context and one service called **Deal** in C#/.NET, but following the **Modular Monolith** pattern:
  - Two logically independent modules (`Negotiation` and `Swap`) with explicit boundaries, separate namespaces and isolated domain models.
  - Communication between the modules goes through the **Transactional Outbox and in-memory / domain events**: the agreement state is recorded in Negotiation, the event is put in the Outbox table and then handed to the Swap module with delivery guarantees.
  - This architecture gives high speed and zero network overhead, avoids distributed transactions between the negotiation and exchange phases, and fully keeps the calibre needed for physical extraction (microservice extraction) in the future.
  - *(Refined on 2026-11-06: Swap is created only after the lock is acquired from Ad; see the last section.)*

### 2. Managing the race condition and exclusive Ad lock with a Saga and eventual consistency (ADR 10)
- **Problem:** several negotiations racing to finalise an agreement over one Ad (concurrent `ConcludeAgreement`).
- **Two options were examined:**
  1. Strong consistency by moving the lock fully inside the Ad aggregate.
  2. Eventual consistency using a process coordinator (Saga / Process Manager) with compensating transactions.
- **Decision and driving forces:**
  - Option 2 was chosen.
  - Explicit driving forces and weighting (ADR driving forces):
    - Simplicity of implementation and maintenance: low weight.
    - Performance and scalability: medium weight.
    - Learning value and demonstrating mastery of distributed-system challenges (the criterion of Divar's engineering interviews): **high and decisive weight**.
  - **Flow:** the final approval button sends the `ConcludeAgreement` command to Negotiation, which fires `AgreementReached`. On receiving this event the Saga sends the `ClaimLock` command to Ad. On confirmation (`ExclusiveLockAcquired`) the Ad is locked and the other competing negotiations are cancelled by issuing `NegotiationCancelled` (reason: the item went to someone else). On rejection (`ExclusiveLockRejected`) the compensating transaction runs, the second negotiation is cancelled and the user is informed. In the front end the transient state "Finalizing agreement..." is shown.
  - *(Refined on 2026-11-06: one atomic claim over both Ads with a version check; see the last section.)*

### 3. Revisiting the Proposal and approval model: the single-approval, three-component snapshot model (double approval cancelled)
- **Problem:** the "double approval" model (approved 07/13), where the Ad tick (`AD`) was independent of the terms tick (`TERMS`), was examined. It was found to create false domain complexity and to have legal bugs (if a user adds a defect to their item, the counterparty should not complete the agreement with the same earlier terms; the item and terms are one package in the user's mind).
- **Decision:** return to the **three-component snapshot model**:
  - Proposal formula: `Proposal = (Ad1_Version + Ad2_Version) + Terms`.
  - Every proposal has three components. With each change:
    - Editing the first Ad (`AdEdited`): open proposals are invalidated and a new proposal is born with the new Ad1 + a copy of the previous Ad2 + a copy of the previous Terms, and approvals are reset.
    - Editing the second Ad: a new proposal is born with the new Ad2 + a copy of the previous Ad1 + a copy of the previous Terms.
    - Changing the terms (`ReviseProposal`): a new proposal is born with the new Terms + a copy of the previous Ad versions.
  - There is only one kind of approval tick (`ProposalApproved`) per party.
  - Disk use is negligible because a proposal is just a few foreign keys and a small text record, and invalidated versions are deleted.
  - Simplicity of the state machine and complete legal safety for the platform are ensured.

### 4. Closing the Reputation and Trust cluster
- Event-Sourced architecture: lazy rehydration of state from the event stream.
- No need for a `UserProvisioned` event: a user without an initial record has the default state.
- Commands and policies:
  - `SubmitReview` (score 1 to 5 + comment) → `ReviewSubmitted`: only up to **5 days** after `SwapCompleted`. After 5 days it is rejected. Immediate publication with the possibility of an abuse report. *(Revised on 2026-11-06.)*
  - `CalculateTrustScore` → `TrustScoreUpdated`: on a sharp drop, trigger `SuspendUser`.
  - `ApplyPenalty` → `PenaltyApplied`: for example for not paying the locker fee (`PaymentTimedOut`).
  - `RemoveReview` / `WaivePenalty` → `ReviewRemoved` / `PenaltyWaived`.
- The Matching service listens to `TrustScoreUpdated` to update its local index for ranking Ads.

### 5. Closing the Communication cluster
- The Communication service is fully downstream (Downstream / Conformist) of the core of the system (Ad and Deal) and turns their events into text templates.
- To send notifications to the client it uses the `NotificationProvider` interface connected to Divar's public service (Open Host Service).
- Chat: only an isolated module for the project's demo, working fully independently of negotiation. Sending a chat message does not reset the negotiation expiry timer.

### 6. The User/Auth cluster is a mock
- Authentication only injects the `UserId` through the JWT and takes no part in Taakht's domain registration or login processes.

### 7. Drawing the Context Map
- **Ad (Go):** upstream of Deal (a Customer/Supplier relationship with the shared language of Kafka/Protobuf) and upstream of Matching (Conformist).
- **Deal (.NET):** downstream of Ad; upstream of Reputation.
- **Reputation (.NET):** downstream of Deal; upstream of Matching (through `TrustScoreUpdated`).
- **Matching (Go):** downstream of Ad and Reputation.
- **Locker Partner:** upstream of the Swap module in Deal through an **Anti-Corruption Layer (ACL)**.
- **Communication:** downstream of all domain events; a translator of events into outgoing messages and caller of `NotificationProvider`.

## Refinements of Event Storming step 2 and the lock ADR, 2026-11-06
Source: working sessions with Alireza, reviewing the step-2 output. Details are in `docs/domain/event-storming.md` and the locking ADR.

- **Lock covers both Ads in one transaction.** `ClaimLock` carries both Ads with the version fixed in the proposal; the Ad service claims them together in one local transaction in ascending id order (no deadlock) and succeeds only if each is `Available` at the agreed version. Rejection reasons: `AlreadyLocked` (cancel the negotiation) and `VersionChanged` (keep the negotiation, rebase the proposal).
- **No implicit author approval; `RebaseProposal`.** Every approval is explicit and attached to an immutable proposal. An Ad edit (or `VersionChanged`) makes the system build a new proposal on the current versions, with no approvals; both parties approve again. This supersedes the 07/13 "implicit author approval" and the "Ad change only stales the AD approval" rules. The rebase does not extend the negotiation deadline.
- **Negotiation deadline.** Before the Ad owner accepts: a fixed 3 days. After acceptance: 3 days without activity, absolute cap of 30 days from opening. Extending activities: accepting the negotiation, `ProposalApproved`, and a user's manual edit of terms. Not extending: `ApprovalRetracted`, rejecting a revision, system rebase, chat, viewing. This closes the TTL item left open on 07/13.
- **Request cap.** Each directed Ad pair (requester's Ad → target Ad) has 3 request rights; each request consumes one at the moment it is sent; acceptance resets the counter; a system cancellation before acceptance refunds the right; only one open negotiation per Ad pair. Kept in the Negotiation module and incremented atomically in the same transaction as opening the negotiation.
- **Agreement is binding once the lock is acquired.** `AgreementReached` is tentative; `SwapCreated` follows `ExclusiveLockAcquired`. No voluntary withdrawal after `SwapCreated`; the only cancellation cause is non-payment. With no locker leg, `SwapCompleted` follows `SwapCreated` immediately as a separate event.
- **Outage rule (not tested).** A timeout is never treated as failure; only an explicit `ExclusiveLockRejected` is. `ClaimLock` is idempotent and retried until a reply arrives. To be verified with a spike that stops the Ad service in the middle of a Saga. Deferred options: a synchronous fast path with Saga fallback, a circuit breaker on approvals, redundant Ad instances.
- **Reputation.** Numeric 1 to 5 reviews with comments are dropped; `Review` is the question "were you satisfied?" after completion (yes/no or score, decided later; time window currently 5 days). The internal trust score stays but is not shown to users. Abuse reports work like Stack Overflow: every report goes to an admin queue; a reporter at or above the minimum score has an instant effect; a rejected report lowers the reporter's score. `SuspendUser` stays on hold.
- **Communication.** Chat key is (pair of users, Ad), like Divar. In the demo only notifications and simple chat (send, read) are built; mute and watch are a later phase.
- **Blocking (`UserBlocked`).** Owned by Reputation & Trust, because it affects Communication, Matching and Deal. It works both ways until `UserUnblocked`: no new chat messages (old ones stay visible), Ads hidden from each other and no `MatchFound`, no new negotiation requests, and open negotiations that have not reached `AgreementReached` are withdrawn by the system. A negotiation waiting for the lock and any existing Swap are not touched, so blocking cannot be used to escape a binding agreement. The blocked user is notified. Consumers keep a local copy of the blocked pairs (a small eventual-consistency window is accepted). Withdrawn negotiations do not come back on unblock.
- **Claims checklist.** The item-condition claim checklist is part of the Ad and `AdVersion`; there is no separate `ItemConditionClaimed` event.
- **Abuse reports and objections (2026-11-09, with Arman).** A report from a reporter at or above the minimum score has an instant effect and is not reviewed unless the target objects; a report from a reporter below the minimum waits for an admin. The penalised user can file an objection (one per penalty, no deadline for now); the penalty stays until an admin decides, and an upheld objection lowers the reporter's score. This replaces "every report goes to the admin queue".
- **Admin.** No admin panel: each service has admin endpoints that work with the `role=admin` JWT claim.
- **Suspension.** `Suspended` is a separate Ad state (not `Hidden`); Matching removes the Ad, negotiations that have not reached `AgreementReached` are cancelled, a negotiation waiting for the lock and any Swap are untouched, the owner can edit and object but not publish. User suspension is admin-only, suspends all the user's Ads and refuses new requests; the user keeps open chats, search and a chat with an admin, and login is not blocked in the demo.

## Languages per service and the monorepo, 2026-11-12 (with Arman)
Source: review of the draft ADRs; recorded in ADR 0002 and ADR 0003.

- **Language split.** Go for Ad, Matching and Communication; .NET for Deal and Reputation. The rule is a rule of thumb (Go where the main work is concurrency and throughput, .NET where it is rich domain state), not a measured result; the learning goal and balancing the work between the two people also count. This replaces the open assignment of the two supporting services.
- **Monorepo.** All services, the contracts (`api/`), the gateway config and the docs stay in one repository; services share contracts only. Infrastructure code that several services need (outbox, gRPC interceptors) is not shared for now: each service keeps its own copy until the duplication hurts.

## API contract in Protobuf, 2026-11-12 (with Arman)
Source: review of the draft ADRs; recorded in ADR 0004.

- **Protobuf is the single source of truth** for service APIs, in the shared `api/` directory; REST routes come from `google.api.http` annotations; code, the gateway descriptor and the OpenAPI document are generated. Tried only with a hello-world service; the alternatives were argued, not tried.
- **Generated code is not committed.** It is produced at build time (`buf generate` for Go through a `make generate` target, `Grpc.Tools` for .NET). This is a convention, not an ADR: it is cheap to reverse and lives in the API conventions guideline.
- **No shared interceptor library.** Following ADR 0003, each service keeps its own gRPC interceptor setup.

## Envoy as the edge gateway, 2026-11-12 (with Arman)
Source: review of the draft ADRs; recorded in ADR 0005.

- **Envoy is the single public entry point** and transcodes REST/JSON to gRPC with `grpc_json_transcoder`, driven by a descriptor built from the shared protos. Only annotated RPCs are public. grpc-gateway, in-service transcoding, APISIX, Kong and Connect were compared from documentation and one spike; grpc-gateway was the close alternative.
- **Not decided here:** where JWTs are validated (separate ADR), and the edge API style (REST only, no GraphQL; open item 17, reasoning not written yet). Rate limit values are configuration, not an ADR.

## JWT validation at the edge, 2026-11-12 (with Arman)
Source: review of the draft ADRs; recorded in ADR 0006.

- **JWTs are verified in Envoy** (`jwt_authn`: signature, expiry, issuer, audience). Envoy forwards only the user id as `x-user-id`; services read it through their own gRPC interceptor and pass it on unchanged. No revocation, so tokens must be short-lived; the lifetime, identity provider and key rotation are still open.
- **Known risk:** a service reachable without Envoy would accept a forged identity. For now the internal network is isolated; service-to-service authentication is decided separately.

## Service-to-service authentication, 2026-11-13 (with Arman)
Source: review of the draft ADRs; recorded in ADR 0007.

- **Phase 1: no service identity is enforced between services.** The internal network is isolated (only Envoy publishes a port) and the risk is documented in the README. Each service creates its channels and interceptors in one place so authentication can be added there. **Production target: mutual TLS**; the certificate mechanism is decided with the deployment topology. API keys and an internal token issuer were rejected; reasoning only, nothing was tried.
- **Cost of the per-service setup:** adding mTLS later means changing each service, since ADR 0003 shares no infrastructure code.

---
*Dates: 1405/06/21 (2026-09-12), added on 1405/06/26 (2026-09-17), 1405/06/28 (2026-09-19), 1405/07/02 (2026-09-24), 1405/07/03 (2026-09-25), 1405/07/11 (2026-10-03), 1405/07/13 (2026-10-05), 1405/07/14 (2026-10-06), 1405/08/14 (2026-11-04) and 2026-11-06*
