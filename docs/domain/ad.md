# Ad Domain Design

This document specifies the domain model, invariants, state machine, and communication contracts for the **Ad** service in Taakht. Terms follow [ubiquitous-language.md](ubiquitous-language.md); architecture decisions follow [ADR 0017](../adr/0017-take-the-exclusive-ad-lock-in-the-ad-service.md) and [architecture-overview.md](../architecture/architecture-overview.md).

---

## 1. Overview and Responsibility

The Ad service is the single authority and system of record for barter advertisements in Taakht. It is implemented in Go with a PostgreSQL datastore.

Key responsibilities:
- Managing the lifecycle of an Ad (`Published`, `Hidden`, `Locked`, `Closed`).
- Maintaining immutable, versioned snapshots (`AdVersion`) for every change to an item.
- Answering synchronous queries (`GetAd`, `GetAdVersion`) for other services—most notably **Deal**, which keeps no local copy of Ad data and references Ad versions in immutable `Proposal` snapshots.
- Managing the atomic, two-item exclusive lock claim (`ClaimLock`) called by Deal during the agreement finalization phase.
- Publishing domain events to the `ad.events` Kafka topic to drive downstream reactions in Matching, Deal, and Communication.

---

## 2. Ubiquitous Language & Core Concepts

| Term | Kind | Definition |
|---|---|---|
| **Ad** | Aggregate Root | The persistent advertisement of an item, with a stable `id` and an incrementing `version`. Belongs to a single user (`owner_id`) and exactly one city. |
| **AdVersion** | Value Object / Snapshot | An immutable record of an Ad's content at a specific version number. Created on initial creation (`version=1`) and incremented on every edit. |
| **Item ("I have")** | Entity Component | The item being offered. Requires a category (from Taxonomy) and a neighbourhood (which implies the city). Description and photos (referencing Divar) are optional. |
| **Criteria ("I want")** | Value Object | What the owner seeks in exchange: target categories (empty means any), target neighbourhoods (empty means whole city), and an optional maximum distance (2, 5, 10, or 20 km). Item value and price are excluded in Phase 1. |
| **ClaimLock** | Command / Transaction | Synchronous atomic locking of two Ads in one database transaction, ensuring neither Ad has been locked by another swap or modified since the agreement was reached. |

---

## 3. Aggregates and Invariants

### 3.1 The `Ad` Aggregate

The `Ad` aggregate consists of the mutable aggregate root (`Ad`) and its append-only sequence of immutable versions (`AdVersion`).

```
+-------------------------------------------------------+
| Ad (Aggregate Root)                                   |
|   id: UUID                                            |
|   owner_id: String                                    |
|   state: AdState                                      |
|   previous_state: AdState                             |
|   current_version: Int                                |
|   locked_by_swap_id: Nullable<UUID>                   |
|   created_at: Timestamp                               |
|   updated_at: Timestamp                               |
+---------------------------+---------------------------+
                            | 1..*
                            v
+-------------------------------------------------------+
| AdVersion (Immutable Snapshot)                        |
|   ad_id: UUID                                         |
|   version: Int (1, 2, 3...)                           |
|   -- "I have" side                                    |
|   category_id: String                                 |
|   city_id: String                                     |
|   neighbourhood_id: String                            |
|   description: String                                 |
|   photo_urls: List<String>                            |
|   -- "I want" side (Criteria)                         |
|   target_category_ids: List<String>                   |
|   target_neighbourhood_ids: List<String>              |
|   max_distance_km: Nullable<Int>                      |
|   created_at: Timestamp                               |
+-------------------------------------------------------+
```

### 3.2 Invariants (Aggregate Consistency Rules)

The aggregate root strictly enforces the following domain invariants across all state transitions:

#### A. Structural and Scope Invariants
1. **Identity & Ownership:** Every Ad must have a unique `id` and a valid `owner_id` (extracted from the authenticated JWT `x-user-id`). Both remain strictly immutable for the entire lifecycle.
2. **Mandatory Item Attributes:** An Ad cannot be created without a valid `category_id` and `neighbourhood_id`. Description and photos are optional.
3. **Single City Scope:** Every Ad belongs to exactly one city, determined by its `neighbourhood_id`. Editing the neighbourhood to one outside the original city is forbidden.

#### B. Field Boundary & Size Invariants
4. **Description Length:** Description is optional; when provided, it must not exceed **2,000 characters**.
5. **Photo Reference Bounds:** Photos are optional references (e.g. Divar CDN URL or asset key); an Ad may contain at most **10 photo references**, each up to **2,048 characters** to prevent unbounded storage.
6. **Criteria Category Limit:** Target categories in Criteria are optional (empty means any category); when specified, at most **10 categories** can be selected.
7. **Criteria Neighbourhood Limit:** Target neighbourhoods in Criteria are optional (empty means the entire city); when specified, at most **30 neighbourhoods** can be selected.

#### C. Versioning & Immutability Invariants
8. **Initial Version:** An Ad always begins its lifecycle at `version = 1`.
9. **Sequential Increment:** Every edit operation (`EditAd`) increments the aggregate's version by exactly one (`version = version + 1`) and appends a new `AdVersion` snapshot.
10. **Append-Only History:** Existing `AdVersion` records are strictly immutable; they can never be modified or deleted.

#### D. State Machine & Transition Invariants
11. **Terminal Closed State:** An Ad in `Closed` state is terminal and immutable. No edits, state transitions, or lock claims can occur on a closed Ad.
12. **Lock Guard:** An Ad in `Locked` state cannot be edited, hidden, or republished.

#### E. Exclusive Lock (`ClaimLock`) Invariants
13. **Mutual Exclusivity:** An Ad can be locked by at most one swap (`swap_id`) at any given time.
14. **Eligibility Precondition:** An Ad can transition to `Locked` if and only if:
    - Its current state is `Available` (`Published` or `Hidden`).
    - Its `current_version` exactly matches the version requested by Deal.
15. **Lock Ownership (Fencing Token):** Releasing a lock (`ReleaseLock`) or closing an Ad (`CloseAd`) is valid only when the Ad is in `Locked` state and the caller provides the exact matching `swap_id` that established the lock.

---

## 4. States, Transitions, and Lifecycle

The Ad lifecycle does not maintain a server-side `Draft` state; unsubmitted forms remain client-side (local storage / app cache). Upon submission, `CreateAd` validates and creates the Ad directly in `Published` state.

```
           +----------------+   HideAd (user)         +----------------+
+--------->|   Published    | ----------------------> |     Hidden     |
|          +-------+--------+ <---------------------- +-------+--------+
|                  |            PublishAd (user)              |
|                  |                                          |
|                  +--------------------+---------------------+
|                                       |
|                                       | ClaimLock (Deal)
|                                       v
|                              +----------------+
|      ReleaseLock (Swap cancel|     Locked     |
|      ----------------------- +-------+--------+
|                                      |
|                                      | CloseAd (Swap complete)
|                                      v
|                              +----------------+
|                              |     Closed     | (Terminal)
|                              +----------------+
```

### 4.1 State Definitions

- **Published:** Active and visible for candidate matching, browse/search queries, and new negotiation requests.
- **Hidden:** User manually hides the Ad from search and matching via `HideAd`. Existing open negotiations remain active. Re-activated via `PublishAd`.
- **Locked:** Exclusively claimed by Deal for a finalized agreement. Removed from Matching index. No new negotiations or edits permitted.
- **Closed:** Terminal state after successful completion of a Swap (`SwapCompleted`). The item is consumed and permanently archived.

### 4.2 State Transition Matrix

| Current State | Action / Command | Target State | Published Event | Notes |
|---|---|---|---|---|
| *None* | `CreateAd` | `Published` | `AdCreated`, `AdPublished` | Validated and published at once. |
| `Published` | `HideAd` | `Hidden` | `AdHidden` | Removed from Matching index. |
| `Hidden` | `PublishAd` | `Published` | `AdPublished` | Re-enters Matching index. |
| `Published`, `Hidden` | `EditAd` | Same state | `AdEdited` | `version++`. Deal triggers `RebaseProposal`. |
| `Published`, `Hidden` | `ClaimLock` (Deal) | `Locked` | `ExclusiveLockAcquired` | Atomic claim with agreed version check. |
| `Locked` | `ReleaseLock` (Swap) | `previous_state` | `ExclusiveLockReleased` | Restores previous state (`Published` or `Hidden`). |
| `Locked` | `CloseAd` (Swap) | `Closed` | `AdClosed` | Irreversible final state. |

---

## 5. Exclusive Lock Mechanism (`ClaimLock`)

Following [ADR 0017](../adr/0017-take-the-exclusive-ad-lock-in-the-ad-service.md), the exclusive lock ensures that when two parties agree to swap, neither Ad can be claimed by a competing negotiation, and neither Ad has been altered since both parties approved the proposal.

### 5.1 Atomicity and Concurrency Control

1. **Single Database Transaction:**
   When Deal calls `ClaimLock(swap_id, [(ad_id_1, version_1), (ad_id_2, version_2)])`, Ad processes both items within a single database transaction. Either both Ads are locked, or neither is.
2. **Deadlock Prevention:**
   Rows in PostgreSQL are locked using `SELECT ... FOR UPDATE` ordered strictly by ascending `ad_id` (`ORDER BY id ASC`). This prevents database deadlocks when two concurrent deals involve the same pair of Ads in opposite roles.
3. **Eligibility Condition:**
   For each Ad in the claim:
   - Status must be `Available` (`Published` or `Hidden`). It must not be `Locked`, `Suspended`, or `Closed`.
   - The Ad's `current_version` must exactly match the `version` stated in the request.
4. **Idempotency with `swap_id`:**
   The claim records `locked_by_swap_id = swap_id`. If Deal retries the claim with the same `swap_id` on the same Ads, Ad recognises the existing lock and returns success.
5. **Lock Ownership & Fencing:**
   Subsequent `ReleaseLock` or `CloseAd` calls require the matching `swap_id`. Stale commands from older, cancelled sagas are safely ignored.

### 5.2 Refusal Reasons

When a lock claim cannot be granted, Ad returns an explicit refusal code:
- **`LOCK_REFUSAL_REASON_ALREADY_LOCKED`:** One or both Ads are currently locked (`state == Locked`) by another swap, or are closed/suspended. Deal cancels the losing negotiation with reason "item already traded or locked".
- **`LOCK_REFUSAL_REASON_VERSION_CHANGED`:** One or both Ads were edited after the proposal was approved (`current_version != requested_version`). Deal keeps the negotiation open, rebuilds the proposal with current versions via `RebaseProposal`, and requires re-approval from both parties.

*Outage Rule:* A gRPC network timeout is **never** a rejection. Deal retries `ClaimLock` with exponential backoff until an explicit success or refusal response is received.

---

## 6. Service API Contracts

The contract is defined in Protobuf under package `taakht.ad.v1`.

### 6.1 Public REST / gRPC Endpoints (`ad_service.proto`)

Public endpoints are exposed through the Envoy gateway with verified `x-user-id` metadata.

- **`CreateAd(CreateAdRequest) -> CreateAdResponse`**
  - REST: `POST /v1/ads`
  - Creates the Ad, records version 1, and publishes it at once. Emits `AdCreated` and `AdPublished`.
- **`GetAd(GetAdRequest) -> GetAdResponse`**
  - REST: `GET /v1/ads/{ad_id}`
  - Returns the Ad aggregate and its current (latest) version data.
- **`GetAdVersion(GetAdVersionRequest) -> GetAdVersionResponse`**
  - REST: `GET /v1/ads/{ad_id}/versions/{version}`
  - Returns the exact immutable historical snapshot for the requested version number. Used by Deal to inspect proposal terms.
- **`EditAd(EditAdRequest) -> EditAdResponse`**
  - REST: `PUT /v1/ads/{ad_id}`
  - Updates description, category, neighbourhood, or criteria. Increments version and appends a new snapshot. Emits `AdEdited`.
- **`PublishAd(PublishAdRequest) -> PublishAdResponse`**
  - REST: `POST /v1/ads/{ad_id}/publish`
  - Moves Ad from `Hidden` back to `Published`.
- **`HideAd(HideAdRequest) -> HideAdResponse`**
  - REST: `POST /v1/ads/{ad_id}/hide`
  - Moves Ad from `Published` to `Hidden`.
- **`ListMyAds(ListMyAdsRequest) -> ListMyAdsResponse`**
  - REST: `GET /v1/ads/mine`
  - Returns all Ads owned by the calling user (`x-user-id`).

### 6.2 Service-to-Service Internal Endpoints (`ad_service.proto`)

Guarded by `INTERNAL_TOKEN` or `role=admin` claims. No direct external REST mapping for Deal calls.

- **`ClaimLock(ClaimLockRequest) -> ClaimLockResponse`**
  - Called by Deal (Swap module) upon `AgreementReached`.
  - Takes `swap_id` and a list of `(ad_id, version)` tuples.
  - Returns `success: bool` and refusal code (`LOCK_REFUSAL_REASON_UNSPECIFIED`, `LOCK_REFUSAL_REASON_ALREADY_LOCKED`, `LOCK_REFUSAL_REASON_VERSION_CHANGED`).
- **`ReleaseLock(ReleaseLockRequest) -> ReleaseLockResponse`**
  - Called by Deal when a Swap is cancelled (`SwapCancelled`, e.g. payment timeout).
  - Releases the lock and restores previous state.
- **`CloseAd(CloseAdRequest) -> CloseAdResponse`**
  - Called by Deal when a Swap completes (`SwapCompleted`).
  - Transitions Ads permanently to `Closed`.
- **`SuspendAd(SuspendAdRequest) -> SuspendAdResponse`** (Phase 2)
  - Admin endpoint (`role=admin`). Suspends an Ad following an abuse confirmation.
- **`ReinstateAd(ReinstateAdRequest) -> ReinstateAdResponse`** (Phase 2)
  - Admin endpoint (`role=admin`). Reinstates an Ad after an objection is upheld.

---

## 7. Kafka Domain Events (`ad_events.proto`)

All Ad events are published to the `ad.events` topic with `ad_id` as the partition key, ensuring strict per-Ad ordering. Every event message explicitly carries `ad_id` and the associated `version`.

| Event Message | Trigger | Key Payload Fields | Downstream Reaction |
|---|---|---|---|
| **`AdCreated`** | Initial creation | `ad_id`, `version`, `owner_id`, `category_id`, `neighbourhood_id`, `description`, `photo_urls`, `criteria`, `occurred_at` | Audit / logging. |
| **`AdPublished`** | Ad becomes visible | `ad_id`, `version`, `owner_id`, `category_id`, `neighbourhood_id`, `description`, `photo_urls`, `criteria`, `occurred_at` | Matching indexes Ad; triggers match run. |
| **`AdEdited`** | Ad content updated | `ad_id`, `version`, `owner_id`, `category_id`, `neighbourhood_id`, `description`, `photo_urls`, `criteria`, `occurred_at` | Matching re-indexes. Deal triggers `RebaseProposal`. |
| **`AdHidden`** | User hides Ad | `ad_id`, `version`, `occurred_at` | Matching removes Ad from public index. |
| **`ExclusiveLockAcquired`** | Lock claimed | `swap_id`, `ad_id`, `version`, `occurred_at` | Matching removes Ad from index. Deal moves to Swap execution. |
| **`ExclusiveLockReleased`** | Lock released | `swap_id`, `ad_id`, `version`, `occurred_at` | Matching re-indexes if restored to Published. |
| **`AdClosed`** | Deal completed | `ad_id`, `version`, `swap_id`, `occurred_at` | Permanent removal from Matching. Deal cleans up pair counters. |
| **`AdSuspended`** | Admin suspension (Phase 2) | `ad_id`, `version`, `reason`, `occurred_at` | Matching removes Ad. Deal cancels non-agreed open negotiations. |
| **`AdReinstated`** | Admin reinstatement (Phase 2) | `ad_id`, `version`, `occurred_at` | Matching re-indexes if restored to Published. |

---

## 8. Alignment with Phase 1 Scope & Later Phases

- **Phase 1 Implementation:**
  - Full CRUD with versioning (`AdVersion`).
  - `CreateAd`, `GetAd`, `GetAdVersion`, `EditAd`, `PublishAd`, `HideAd`.
  - Atomic `ClaimLock`, `ReleaseLock`, `CloseAd` for Deal integration.
  - Events: `AdCreated`, `AdPublished`, `AdEdited`, `AdHidden`, `ExclusiveLockAcquired`, `ExclusiveLockReleased`, `AdClosed`.
- **Postponed to Later Phases:**
  - Automated negotiation caps and system holds.
  - Owner-initiated Ad closing (withdrawing an Ad before agreement).
  - Category-specific claim checklists (along with dynamic category attributes in Taxonomy).
  - Admin suspension UI and automated objection queues.
  - Multi-item bundle Ads.
