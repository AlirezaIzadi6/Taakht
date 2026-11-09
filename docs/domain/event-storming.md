# Event Storming: Actor, Command, Event, Policy

Output of steps 2 and 3 of the Event Storming for all clusters and boundaries of the Taakht domain. Working draft: some points are marked as open.

---

## 1. Ad cluster

Main aggregate root for the data of the items.

| Actor | Command | Event | Policy afterwards |
|---|---|---|---|
| Ad owner | `CreateAd` | `AdCreated` | If the publish option is on, `AdPublished` is emitted immediately after. |
| Ad owner | `PublishAd` | `AdPublished` | Matching indexes the Ad and starts looking for matches. |
| Ad owner | `EditAd` | `AdEdited` | 1. Matching updates its index.<br>2. The Deal context invalidates all active proposals tied to the old version and creates a new proposal based on the new version.<br>3. Communication notifies the parties of the negotiations. |
| Ad owner | `HideAd` | `AdHidden` | Matching removes the Ad from the public index. |
| System (negotiation cap) | `HideAd` (reason: cap) | `AdHidden` + `system_hold` | Notification to the Ad owner that the cap of 10 concurrent negotiations was reached. |
| Admin | `SuspendAd` / `ReinstateAd` | `AdSuspended` / `AdReinstated` | `Suspended` is a state of its own, separate from `Hidden`, so the owner cannot publish the Ad again; reinstating restores the state it had before (Published or Hidden).<br>1. Matching removes the Ad from the index, like `Hidden`.<br>2. Open negotiations on the Ad that have not reached `AgreementReached` are cancelled by the system (`CancelNegotiation`, reason: suspended). A negotiation waiting for the lock and any existing Swap are not touched, because an agreement is binding.<br>3. The owner is notified. The owner can still edit the Ad (a new `AdVersion`), but cannot publish it, and can file an objection (see the Reputation & Trust cluster). |
| System (Matching) | `FindMatches` | `MatchFound` (per pair) | Communication sends a match-suggestion notification to the owners of both Ads. |
| System (Saga / lock) | `ClaimLock` (both Ads with the agreed version, one transaction) | `ExclusiveLockAcquired` / `ExclusiveLockRejected` (reason: `AlreadyLocked` or `VersionChanged`) | On success: both Ads become `Locked` together and leave the index.<br>On rejection: no Ad changes. `AlreadyLocked`: the negotiation is cancelled. `VersionChanged`: the negotiation stays open and a new proposal is built on the current version. |
| System (end of deal) | `CloseAd` | `AdClosed` | Final state of the Ad; irreversible, consumed by the deal. |

**Key decisions:**
- The item-condition claims (the claim checklist) live directly on the Ad and on `AdVersion`. There is no separate `ItemConditionClaimed` event in Negotiation.
- The post-receipt confirmation checklist was removed entirely. Checking the item is the parties' own responsibility (Zero Liability).

---

## 2. Deal cluster

The unified deal service, built as a **Modular Monolith** with two isolated logical modules.

### 2.1. Negotiation module

| Actor | Command | Event | Policy afterwards |
|---|---|---|---|
| Requester | `RequestNegotiation` | `NegotiationOpened` | The open-negotiation counter of both Ads goes up by one. One of the 3 rights of the Ad pair (requester's Ad, target Ad) is consumed; if no right is left the command is rejected and only the user sees an error (no event). Proposal version 1 is created with default values. Notification to the Ad owner. |
| Ad owner | `AcceptNegotiation` | `NegotiationActive` | The negotiation room becomes active and the parties enter the bargaining phase. |
| Ad owner | `DeclineNegotiation` | `NegotiationDeclined` | The negotiation is closed and the Ad counters go down. |
| Either party | `WithdrawNegotiation` | `NegotiationWithdrawn` | The negotiation is cancelled and the Ad counters go down. |
| System (time) | `ExpireNegotiation` | `NegotiationExpired` | After 3 days without activity, or at the absolute cap of 30 days from opening, the negotiation expires and the counters go down. Activities that extend: acceptance of the negotiation, `ProposalApproved`, and a user's manual edit of the terms. Activities that do not extend: `ApprovalRetracted`, rejecting a revision, the system's `RebaseProposal`, chat, and viewing. Before acceptance the deadline is a fixed 3 days. |
| System (Saga compensating transaction) | `CancelNegotiation` (reason: transferred to someone else) | `NegotiationCancelled` | Counters go down and both parties are notified. |
| Either party | `ReviseProposal` | `ProposalRevised` | A new proposal with the new terms is born; the previous approvals of both parties are reset. |
| System (Policy reacting to `AdEdited` or to `ExclusiveLockRejected` with reason `VersionChanged`) | `RebaseProposal` | `ProposalRevised` (reason: Ad changed) | A new proposal is built with the current Ad versions and the same terms. It carries no approval, and both parties (including the editor) must explicitly approve again. It does not extend the negotiation deadline. |
| Either party | `ApproveProposal` | `ProposalApproved` | If the other party has also approved the same active proposal, the system policy issues the `ConcludeAgreement` command. |
| Either party | `RetractApproval` | `ApprovalRetracted` | The user's approval is removed from the current proposal. |
| System (full-approval Policy) | `ConcludeAgreement` | `AgreementReached` | 1. The negotiation moves to "waiting for lock".<br>2. The Saga sends the `ClaimLock` command to the Ad service. |

**Request cap rule (per Ad pair):**
- Each directed Ad pair (requester's Ad → target Ad) has 3 request rights. Every `RequestNegotiation` consumes one right at the moment it is sent; rejection, expiry and withdrawal add nothing to the counter.
- On acceptance (`NegotiationActive`) the counter of that pair resets to zero. When either Ad becomes `Closed`, the counter row is deleted.
- A system cancellation before acceptance (`NegotiationCancelled`, for example the Ad was locked by another negotiation) gives the right back.
- Only one open negotiation is allowed per Ad pair at a time.
- The counter is kept in the Negotiation module (Deal) and incremented by an atomic statement (`UPDATE ... SET n = n + 1 WHERE n < 3`) in the same transaction that creates the negotiation; no Saga is needed.

**Proposal snapshot rule (Snapshot Model):**
- Every proposal has three components: `Proposal = (Ad1_Version + Ad2_Version) + Terms`.
- Any change to the Ads or to the logistics terms invalidates the current proposal and creates a new one that keeps the unchanged components; all approvals are reset.
- Double approval is obsolete; each party has exactly one approval (`ProposalApproved`).
- There is no implicit author approval: every approval is explicit and attached to an immutable proposal. A new proposal has no approval, so a stale approval cannot exist and no separate event is needed for invalidation. Acceptance of the negotiation (`NegotiationActive`) belongs to the negotiation itself and does not change when the proposal changes.
- The gap between an Ad edit and `AdEdited` reaching Deal (Kafka) is closed by the lock: `ClaimLock` carries the agreed version of both Ads, and Ad grants the lock only if that version is still the latest; otherwise `VersionChanged` is returned and `RebaseProposal` runs.

### 2.2. Swap module (physical exchange and handover)

| Actor | Command | Event | Policy afterwards |
|---|---|---|---|
| System (following `ExclusiveLockAcquired`) | `CreateSwap` | `SwapCreated` | The agreement terms and Ad versions are frozen (Audit Vault); for locker handover a one-hour payment session is created. |
| Locker partner (webhook) | `RecordLockerPayment` | `LockerFeePaid` (per leg) | If both legs are paid (or the exchange is in person): the `CompleteSwap` command is issued. |
| System (end of the one-hour deadline) | `TimeOutPayment` | `PaymentTimedOut` | The `CancelSwap` command is issued and a penalty for the non-paying party is sent to the Reputation context. |
| System | `CancelSwap` | `SwapCancelled` | The Ad service is told to release the lock (`ExclusiveLockReleased`, the Ads are freed) and the locker reservation is cancelled. |
| System | `CompleteSwap` | `SwapCompleted` | The `CloseAd` command is sent to the Ad service for both Ads; the question "were you satisfied?" opens for both parties (Reputation). |

---

## 3. Reputation & Trust cluster

Fully **Event Sourced**; it needs no initial record (`UserProvisioned`) (lazy rehydration).

| Actor | Command | Event | Policy afterwards |
|---|---|---|---|
| Party to the deal | `SubmitReview` (satisfaction with the deal; the answer shape is decided later: yes/no or score) | `ReviewSubmitted` | Valid only after `SwapCompleted` (time window currently 5 days, not confirmed); input to the other party's internal score; not shown to the user. |
| User | `ReportAbuse` (target, reason) | `AbuseReported` | If the reporter's score is at or above the minimum, `ApplyPenalty` hits the target at once and nobody reviews the report unless the target objects. Otherwise the report has no effect and waits in the admin queue until an admin confirms it. The target is notified of a penalty and of the way to object. |
| Admin | `ConfirmReport` | `AbuseConfirmed` | For a report from a reporter below the minimum: `ApplyPenalty` on the target. |
| Admin | `RejectReport` | `AbuseRejected` | For a report from a reporter below the minimum: the report is closed with no effect, and the reporter's score drops. |
| Penalised user, or owner of a suspended Ad | `FileObjection` (one per penalty or suspension, no deadline for now) | `ObjectionFiled` | The objection goes to the admin queue. The penalty or suspension stays in force until the admin decides. |
| Admin | `UpholdObjection` | `ObjectionUpheld` | `WaivePenalty` (or `ReinstateAd`). The reporter's score drops, because the report was not justified. |
| Admin | `DismissObjection` | `ObjectionDismissed` | The penalty or suspension stays. |
| System (following `PaymentTimedOut`, an instant `AbuseReported`, or `AbuseConfirmed`) | `ApplyPenalty` | `PenaltyApplied` | A negative mark is recorded in the Event Store and the score is recalculated. |
| System (following `ObjectionUpheld`) or admin | `WaivePenalty` | `PenaltyWaived` | The user's score is recalculated and `TrustScoreUpdated` is emitted. |
| Admin | `SuspendUser` / `ReinstateUser` | `UserSuspended` / `UserReinstated` | Never automatic. While suspended: all the user's Ads are suspended (see the Ad cluster) and Deal refuses their new requests. The user can still use their open chats, search and browse, and chat with an admin. Login is not blocked (Auth is a mock in the demo). Their open negotiations are handled like those of a suspended Ad. Reinstating restores the Ads to their earlier state. |
| User | `BlockUser` (target) | `UserBlocked` | Two-way block, effective for both users until unblocked:<br>1. Communication refuses new messages between them (old messages stay visible).<br>2. Matching hides each user's Ads from the other and emits no `MatchFound` between them.<br>3. Deal refuses new `RequestNegotiation` between them and withdraws their open negotiations that have not yet reached `AgreementReached` (system `WithdrawNegotiation`, reason: blocked). A negotiation waiting for the lock, and any existing Swap, is not touched (an agreement is binding).<br>4. The blocked user is notified. The confirmation dialog tells the blocker what blocking means. |
| User | `UnblockUser` (target) | `UserUnblocked` | Chat, visibility and new requests are restored; negotiations withdrawn by the block do not come back. |
| System | `CalculateTrustScore` | `TrustScoreUpdated` | 1. The new internal score is stored.<br>2. The event goes to Matching so its ranking is updated. |

**Decisions:**
- `Review` is the question "were you satisfied with the deal?" after completion (not necessarily a 1 to 5 score with a text comment), because abuse reports do not cover things like "arrived late". Yes/no versus score is decided later.
- The internal trust score stays (reporter threshold, Matching ranking) but is not shown to users. Inputs: `SwapCompleted`, `PaymentTimedOut`, `AbuseConfirmed` / `AbuseRejected`, `ObjectionUpheld`, `ReviewSubmitted`.
- Abuse reports work like Stack Overflow, with an appeal instead of a review of every report: a reporter at or above the minimum score has an instant effect and nobody reviews it unless the target objects; everyone else needs an admin to approve. A report that is rejected, or whose objection is upheld, lowers the reporter's score, so a bad reporter automatically loses the instant effect (no separate counter).
- An objection has no deadline for now (one per penalty or suspension, so the admin's work stays bounded). A deadline can be added when the admin workload or old cases become a problem.

**Hotspots and ideas:**
- The minimum score value and the size of penalties.
- Suspending a user is never automatic: a user in very bad standing is suspended by an admin (the earlier automatic trigger on a critical score drop is dropped).
- Block is owned by this cluster (the safety side of trust), not by Communication, because it affects three contexts; Communication, Matching and Deal consume `UserBlocked` and keep a local copy of the blocked pairs. There is a small eventual-consistency window between the block and the consumers seeing it (a request may slip through in that moment).
- Whether the blocked user's notification should be neutral in wording is a product detail to confirm.
- Later idea (after business feedback): a badge or tick for users with a good record.

---

## 4. Communication cluster (chat and notifications)

This cluster is a downstream listener (Conformist / Translator) of the core system's events. In the demo only notifications and simple chat (sending and seeing messages) are built; rows marked **[later phase]** are modelled but not built.

### 4.1. Notifications

| Actor | Command | Event | Policy afterwards |
|---|---|---|---|
| System (reacting to domain events) | `DispatchNotification` | `NotificationDispatched` | Events such as `MatchFound`, `NegotiationOpened`, `NegotiationActive`, `ProposalRevised`, `SwapCreated`, `PaymentTimedOut`, `SwapCompleted`, `NegotiationCancelled` and `AdHidden` (reason: cap) are translated to text and sent through `NotificationProvider` (Divar's messaging system). |
| User | `MarkNotificationAsRead` | `NotificationRead` | Read state in the UI only. |

### 4.2. Chat

Chat is independent of negotiation and has no effect on negotiation timers or counters. Conversation key: **(pair of users, Ad)**, like Divar; each chat lives under its own Ad.

| Actor | Command | Event | Policy afterwards |
|---|---|---|---|
| User | `StartChat` (on an Ad) | `ChatStarted` | No effect on the negotiation. |
| User | `SendMessage` | `MessageSent` | Notification to the recipient, unless the chat is muted. |
| User | `ReadMessages` | `MessageRead` | Seen marker. |
| User **[later phase]** | `MuteChat` / `UnmuteChat` | `ChatMuted` / `ChatUnmuted` | Notifications of that chat are switched off/on. |

### 4.3. Ad watch **[later phase]**

Bookmark and watch are the same thing.

| Actor | Command | Event | Policy afterwards |
|---|---|---|---|
| User | `WatchAd` / `UnwatchAd` | `AdWatched` / `AdUnwatched` | |
| User | `EnableWatchNotifications` / `DisableWatchNotifications` | `WatchNotificationsEnabled` / `WatchNotificationsDisabled` | |
| System (reacting to `AdEdited`, `AdHidden`, `AdClosed`, ...) | `DispatchNotification` | `NotificationDispatched` | Only to watchers who have notifications on. |

Chat respects blocking: `SendMessage` between two blocked users is refused (see the Reputation & Trust cluster).

---

## 5. Admin (API only)

There is no admin panel. Each service exposes admin endpoints that work only with the `role=admin` claim in the JWT; an admin uses Postman or a command-line client. A panel can be built later on top of the same endpoints.

| Action | Owner | Effect |
|---|---|---|
| `ConfirmReport` / `RejectReport` | Reputation & Trust | Decides a report from a reporter below the minimum score |
| `UpholdObjection` / `DismissObjection` | Reputation & Trust | Decides an objection to a penalty or a suspension |
| `WaivePenalty` | Reputation & Trust | Removes a penalty |
| `SuspendUser` / `ReinstateUser` | Reputation & Trust | Suspends or restores a user |
| `SuspendAd` / `ReinstateAd` | Ad | Suspends or restores one Ad |

The queues an admin works from: reports waiting for approval, and objections.

---

## 6. User / Auth cluster

In the MVP/demo phase it works as a **Mock** and issues JWT tokens carrying the `UserId` for clients. No domain event or separate database is designed for this part.

---

## 7. Resolved decisions and hotspots

1. **Concurrent lock race:** solved with a **Saga and eventual consistency** with a compensating transaction between Negotiation and Ad (ADR 10).
2. **Negotiation/Swap boundary:** combined in one unified service called **Deal**, built as a **Modular Monolith**, to avoid unnecessary network complexity and distributed transactions.
3. **Proposal model:** the three-component snapshot model replaced double approval; the item and the logistics terms are one inseparable package.
4. **Chat:** independent of negotiation, with no effect on counters or the expiry timer.
5. **Satisfaction question:** after `SwapCompleted`, for both parties; the time window (currently 5 days) and the answer shape are confirmed later.
