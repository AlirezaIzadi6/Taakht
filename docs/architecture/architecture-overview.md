# Architecture Overview: Taakht (Divar Barter Feature)

> This document shows the **current state** of the design, not the history of how we got here (that goes in `docs/adr/` and `architecture-decisions-log.md`). Whenever an important decision is finalised, this document is updated, not just appended to.

## 1. Purpose of the system

A feature for matching and settling item swaps, built as an extra layer on top of Divar's existing classifieds platform, not as an independent marketplace. The design assumes a real connection to Divar; wherever that assumption is not real, it is marked explicitly.

## 2. System boundary: what belongs to Divar and what to this project

**Belongs to this project (designed and implemented, Core):**
- The matching engine (Matching) and the eligibility configuration (Eligibility Config: a global policy, a whitelist of categories plus the neighbourhoods active in the pilot; it has nothing to do with the parties of a deal. It is the source of the options a user can choose from, so a disallowed item or neighbourhood is never shown, and server-side validation exists only for API security).
- The deal, covering multi-candidate negotiation and the Swap execution lifecycle in one context (the **Deal** service, a Modular Monolith).
- Chat (independent of Negotiation) and system notifications (Communication).
- Locker handover orchestration and the claim checklist. **No money passes through this system:** there is no escrow or blocked funds, and any financial transaction (for example the price difference) is entirely between the two parties.

**Side services: Interface + Mock (Ports & Adapters):**
Decision: no side service is merely "assumed or consumed from Divar". Each sits behind an interface defined by this project, with a mock implementation in phase 1. Replacing it later with a real implementation only changes the wiring, with no change to the Core.

| Interface | Responsibility | Phase 1 (this project) | Later phase (optional, if time allows) |
|---|---|---|---|
| `AuthProvider` | Login / session / JWT | Issues a fake JWT for a seeded user with a `UserId`, with no real password check | Real validation |
| `TaxonomyProvider` | Reads Divar's category taxonomy and city/neighbourhood division | A small fixed dataset | Real integration with Divar's taxonomy |
| `KYCProvider` | Higher-level identity verification for locker handover | Always returns true/passed (some seeded users are rejected to exercise errors) | Integration with a real KYC service |
| `DeliveryProvider` | Integration with the **locker** (the only handover method with an external partner), user eligibility check, receiving payment and status webhooks, payment status inquiry, `CancelReservation` | Mock with fixed/simulated statuses (behind an ACL) | Integration with a real locker partner |
| `ReportProvider` | Abuse reports (any user can be reported, even without a shared deal or negotiation: fake Ad, harassment, fraud intent) | Records a simple entry; the review workflow is the admin queue described in the Reputation service | Integration with Divar's real abuse-report system |
| `NotificationProvider` | Sending notifications to external channels (Divar OHS) | Mostly a mock | Integration with Divar's messaging system |

**Removed:** `WalletProvider` and `PSP/EscrowGateway` do not exist. The platform does not lock, hold or move any money; wallet, gateway and escrow licensing are outside the project scope.

The full category taxonomy and the geographic division (city/neighbourhood) are mocked in phase 1 as a fixed/simplified dataset inside the `TaxonomyProvider` mock.

## 3. Service map (current state)

| Service | Responsibility | Data ownership | Class | Implementation | Language/stack |
|---|---|---|---|---|---|
| **Ad** | Create, edit (versioned), `Published ⇄ Hidden`, lock and close with a deal; managing the open-negotiation counter; answering the synchronous atomic lock request from Deal (both Ads in one transaction) | **Ads** (versioned; durable PostgreSQL storage) | Core | Full | Go 1.27 |
| **Matching** | Candidate discovery by neighbourhood/category/trust; applying Eligibility Config; two-way matching of Ads and running Criteria (sync and async) | Matching index (Elasticsearch, with the trust score copied into it); category tree and neighbourhood distances in service memory | Core | Full | Go 1.27 |
| **Deal** | The unified deal service, a **Modular Monolith** with two isolated modules:<br>1. **Negotiation module:** open negotiations (cap of 10 per Ad), snapshot proposals, approvals, the request-right counter per Ad pair, and starting the agreement.<br>2. **Swap module:** the final handover state machine, locker payment relay, and evidence recording (Audit Vault) | Deal database (PostgreSQL) with separate schemas/tables for Negotiation and Swap; internal communication via Outbox | Core | Full | C# / .NET 10 |
| **Communication** | Downstream listener (Conformist) of domain events, translating them into text messages for `NotificationProvider`; plus a minimal chat module for the demo (independent of negotiation) | Messages and notification records | Supporting | Minimal | Go 1.27 |
| **Reputation** | Internal trust score with **Event Sourcing**; penalties based on Swap outcomes; review question after a swap; abuse reports and objections (admin queue only for low-standing reporters and for objections); user blocking (`UserBlocked`, consumed by Communication, Matching and Deal); user suspension by an admin; publishes `TrustScoreUpdated` for Matching | Own Event Store | Supporting | Minimal | C# / .NET 10 |
| `DeliveryProvider` (adapter) | Talking to the locker partner's API behind the anti-corruption layer (ACL) | No durable state | Generic | Mock | none |

**Client entry:** an **API Gateway** based on Envoy (load balancing, rate limiting, JWT validation, REST-to-gRPC transcoding). The project is **API only**; clients speak REST/JSON, and internal communication is gRPC and Kafka.

## Ubiquitous language

The full glossary is in [domain/ubiquitous-language.md](../domain/ubiquitous-language.md). Core terms:

| Term | Kind | Description |
|---|---|---|
| **Deal** | Bounded Context / Service | The large container of the deal lifecycle, a Modular Monolith covering two phases: negotiation and physical execution. |
| **Ad** | Entity (versioned) | The item with a stable identity and immutable `AdVersion`s. States: `Published ⇄ Hidden`, transition to `Locked` on agreement, and the final state `Closed`. |
| **Criteria** | Value Object | Search and filter criteria; not stored, passed directly to Matching. |
| **Negotiation** | Entity / Aggregate | First phase of a Deal: the bargaining space between two Ads. Capped at 10 concurrent negotiations per Ad. |
| **Proposal** | Value Object / Snapshot | A three-component snapshot: `(Ad1_Version + Ad2_Version + Terms)`. Any change to an item or the terms produces a new proposal, which carries no approval. |
| **Terms** | Value Object | Logistics terms and the agreed price difference in a proposal (delivery method per leg and the price difference). |
| **Agreement** | Domain Concept / Event | The moment both parties have approved the same active proposal (`AgreementReached`). It is tentative until the lock is acquired. |
| **Swap** | Entity / Aggregate | Second phase of a Deal: the final, blind execution of the handover, locker payment relay and evidence recording. |

## 4. Key flows

### Flow 1: from Ad to agreement and exclusive lock (synchronous lock)
1. **Creating and publishing an Ad:** user A creates an Ad. If "publish" is selected, `AdCreated` and `AdPublished` are emitted. Matching indexes the Ad, publishes `MatchFound` if there is a match (once per Ad pair), and Communication sends a notification.
2. **Requesting and opening a negotiation:** user B sends `RequestNegotiation` on A's Ad, which starts the negotiation (`NegotiationOpened` with an initial system proposal). Each directed Ad pair (requester's Ad, target Ad) has only 3 request rights; every request consumes one at the moment it is sent, only one negotiation can be open per pair, and acceptance resets the counter to zero. A request with no rights left is rejected and only the user sees an error. The Ad owner accepts with `AcceptNegotiation` (`NegotiationActive`). The open-negotiation counter of both Ads goes up by one (when it reaches 10, the Ad is temporarily hidden with the `system_hold` flag).
3. **Bargaining and changes (three-component snapshot model):**
   - The parties can send text messages (chat is independent and does not extend the negotiation deadline).
   - Every proposal is a snapshot of `(Ad1_Version + Ad2_Version + Terms)`.
   - If a party changes the terms (`ReviseProposal`), a new proposal is created with the new Terms and the previous Ad versions, with no approval.
   - If a party edits their Ad in the Ad service (`AdEdited`), the system (`RebaseProposal`) creates a new proposal with the new Ad version and the previous terms, with no approval. Both parties, including the editor, must approve explicitly again; there is no implicit approval. The system-made proposal does not extend the deadline.
   - Deadline: a fixed 3 days before the Ad owner accepts; after acceptance, 3 days without activity, with an absolute cap of 30 days from opening. Accepting the negotiation, `ProposalApproved` and a user's manual edit of the terms extend it.
4. **Final approval and the lock:**
   - Both parties send `ApproveProposal` on the current proposal (`ProposalApproved`).
   - When the system sees both approvals, it issues `ConcludeAgreement` and `AgreementReached` is published. The UI shows "Finalizing agreement...". The agreement is still tentative: nothing is binding yet and the Ads are not locked.
   - The Swap module in Deal calls `ClaimLock` on the Ad service (a synchronous gRPC call), carrying both Ads with the version fixed in the proposal. It is idempotent by swap id.
   - **Happy path:** the Ad service claims both Ads in one local transaction, in ascending id order. For each Ad the status must be `Available` and the current version must equal the agreed version. Both Ads become `Locked` and leave the Matching index, and `ExclusiveLockAcquired` is emitted. The system sends `CancelNegotiation` (reason: given to someone else) to all competing negotiations of both Ads (`NegotiationCancelled`). The Swap module in Deal is activated and `SwapCreated` is emitted. From here the agreement is binding.
   - **Failure path:** if no Ad changed, `ExclusiveLockRejected` is returned with a reason. `AlreadyLocked`: another negotiation won, and Deal cancels the losing negotiation (`CancelNegotiation`) with the message "unfortunately the item was given away". `VersionChanged`: an Ad was edited in the meantime; the negotiation stays open, `RebaseProposal` builds a new proposal on the current versions, and both parties approve again.
   - **Outage rule:** a timeout is never treated as a rejection. Only an explicit `ExclusiveLockRejected` is. `ClaimLock` is retried with backoff until a reply arrives, and the negotiation stays in "waiting for the lock". The Taakht MVP tested this by stopping the Ad service during the lock.

### Flow 2: running the Swap and recording trust
1. **Start of the physical phase:** once `SwapCreated` is emitted, the Swap module freezes the agreement terms (audit trail). The platform holds no money. The agreed price difference is settled directly between the parties, outside the system.
2. **Handover methods:**
   - **In person / courier:** the platform does not take part. `SwapCompleted` is emitted immediately after `SwapCreated` (a separate event, so consumers do not need to distinguish locker from non-locker paths) and the Ads become `Closed` permanently.
   - **Locker:** for each locker leg a one-hour payment session is created with the partner. Each user pays their own leg's fee directly to the partner's gateway. On receiving the webhook, `LockerFeePaid` is emitted.
3. **Completion or cancellation:**
   - When both locker legs are paid, `SwapCompleted` is emitted and both Ads become `Closed`.
   - When the one-hour deadline passes without payment, `PaymentTimedOut` is emitted, the Swap is cancelled (`SwapCancelled`), the Ad locks are released (`ExclusiveLockReleased`) and the locker reservation is cancelled (`CancelReservation`). There is no voluntary withdrawal after `SwapCreated`: the agreement is binding.
4. **Reputation:**
   - After `SwapCompleted`, the question "were you satisfied with the deal?" opens for both parties (`SubmitReview`, `ReviewSubmitted`); the answer shape (yes/no or score) and the time window (currently 5 days) are decided later. The trust score is internal and is not shown to users.
   - The system recalculates the user's score and publishes `TrustScoreUpdated`.
   - When a swap is cancelled by a payment timeout, the system issues `ApplyPenalty` and records a penalty for the offending user (`PenaltyApplied`).
   - Abuse reports (`ReportAbuse`, `AbuseReported`): a reporter whose score is at or above the minimum has an instant effect on the target, and nobody reviews it unless the target files an objection (`FileObjection`). The penalty stays until an admin decides; if the objection is upheld (`ObjectionUpheld`) the penalty is waived and the reporter's score drops. A report from a reporter below the minimum has no effect until an admin confirms it (`AbuseConfirmed`) or rejects it (`AbuseRejected`, which lowers the reporter's score). Objections have no deadline for now.
   - Matching listens to `TrustScoreUpdated` and updates the user's weight in the Elasticsearch index and in search ranking.

## Key domain events

```
-- Ad cluster
AdCreated, AdPublished, AdHidden, AdEdited (→ new AdVersion), AdSuspended, AdReinstated
MatchFound (Matching → Communication)
ExclusiveLockAcquired, ExclusiveLockRejected (AlreadyLocked | VersionChanged), ExclusiveLockReleased, AdClosed

-- Deal cluster (Negotiation module)
NegotiationOpened, NegotiationActive
ProposalRevised (by a user, or by the system via RebaseProposal), ProposalApproved, ApprovalRetracted
NegotiationDeclined, NegotiationWithdrawn, NegotiationExpired, NegotiationCancelled
AgreementReached (tentative until the lock is acquired)

-- Deal cluster (Swap module)
SwapCreated
LockerFeePaid, PaymentTimedOut, SwapCancelled, SwapCompleted

-- Reputation & Trust cluster
ReviewSubmitted
AbuseReported, AbuseConfirmed, AbuseRejected, ObjectionFiled, ObjectionUpheld, ObjectionDismissed
UserSuspended, UserReinstated
UserBlocked, UserUnblocked
TrustScoreUpdated, PenaltyApplied, PenaltyWaived

-- Communication cluster
NotificationDispatched, NotificationRead
ChatStarted, MessageSent, MessageRead (demo)
[later phase] ChatMuted / ChatUnmuted, AdWatched / AdUnwatched, WatchNotificationsEnabled / WatchNotificationsDisabled
```

Details of actors, commands and policies: [domain/event-storming.md](../domain/event-storming.md). Context relationships: [domain/context-map.md](../domain/context-map.md).

**Blocking:** `UserBlocked` is owned by Reputation & Trust and works both ways until `UserUnblocked`. Communication refuses new messages (old ones stay), Matching hides each user's Ads from the other and emits no `MatchFound`, and Deal refuses new requests and withdraws open negotiations that have not reached `AgreementReached`; a negotiation waiting for the lock and any existing Swap are untouched. The blocked user is notified.

**Suspension.** An admin can suspend an Ad (`AdSuspended`) or a user (`UserSuspended`); neither is automatic. `Suspended` is separate from `Hidden`, so the owner cannot publish the Ad; the owner can edit it and can object. Matching removes the Ad, and open negotiations that have not reached `AgreementReached` are cancelled; a negotiation waiting for the lock and any existing Swap are untouched. Suspending a user suspends all their Ads and refuses their new requests; they can still use open chats, search and browse, and chat with an admin, and login is not blocked in the demo.

**Admin.** There is no admin panel: each service exposes admin endpoints that work with the `role=admin` JWT claim (see [event-storming.md](../domain/event-storming.md), section 5).

## 5. Data model and consistency

- **Communication between microservices:** fully asynchronous through **Apache Kafka** (with the Transactional Outbox pattern in each service; at-least-once delivery and the aggregate id as partition key). Internal synchronous communication uses **gRPC**.
- **Intra-service communication in Deal:** the Negotiation and Swap modules communicate inside the .NET process through **Transactional Outbox / Domain Events**, keeping the databases and domain models of the contexts isolated, with no distributed transaction inside Deal. The Swap module receives an immutable snapshot of the agreement only after `ExclusiveLockAcquired` arrives from Ad.
- **Exclusive lock:** the Ad service owns it; Deal calls it synchronously and the winner is decided by the atomic claim of both Ads in one Ad-service transaction, with a version check ([ADR 0017](../adr/0017-take-the-exclusive-ad-lock-in-the-ad-service.md)). The swap lifecycle after the lock (locker fees, completion or cancellation with compensations) runs as a Saga ([ADR 0018](../adr/0018-run-the-swap-lifecycle-as-a-saga.md)).
- **Request-right counter:** kept in the Negotiation module, incremented by an atomic statement in the same transaction that creates the negotiation; no Saga.
- **Datastores:**
  - **PostgreSQL:** durable source of truth for Ad and Deal (version records and snapshots).
  - **Elasticsearch:** the main Matching index and ranking of results by Criteria and neighbourhood filters.
  - **In service memory, no shared cache for now:** the category tree and the precomputed neighbourhood distances ([ADR 0019](../adr/0019-use-elasticsearch-for-the-matching-index.md)).
  - **Separate Event Store:** for the Reputation service, storing raw events and computing scores lazily.
- **Authentication:** the JWT issued by `AuthProvider` is validated at the Envoy Gateway and the `x-user-id` header is passed to the services. In the demo phase there is no authentication between services.

## 6. Deployment topology

- **Single-node demo:** all components run as Docker containers on one machine. Process boundaries, network serialisation, concurrency races and messaging are fully real.
- **Scale horizon:** in production, the heavy services (Ad and Matching in Go) scale horizontally, the Matching index is sharded, and the Deal context in .NET acts as the core that coordinates transactions.

## 7. Testing and observability

- **Tests:** a test pyramid of unit tests, integration tests (with Docker and test containers for Kafka and PostgreSQL), and full end-to-end tests over the API.
- **Concurrency and load testing:** concurrency tests for Ad lock races and the Saga's compensating transactions with stress-test tools, with logs and results recorded in the repository. The Taakht MVP's fault-injection tests already stop the Ad service in the middle of the lock; the same scenarios are to be repeated on this design's implementation.
- **Observability:** centralised logs in JSON format, with Logstash for aggregation.

---
*Last updated: 2026-11-06*
