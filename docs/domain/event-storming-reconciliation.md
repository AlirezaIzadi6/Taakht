# Reconciliation of the "incomplete ES list" with the domain events, 1405/07/14

Source: the "incomplete ES list" from the 2 Mehr meeting (34 sentences, user level). Reference: the "key domain events" section in `architecture-overview.md`.

## 1. Mapping of the sentences

**✓ = already in the overview, + = new event added to the overview, ○ = not a domain event (query/view only), ✗ = obsolete (confirmation after agreement; decisions 07/11 and 07/14).**

| # | Sentence in the list | Event / status | Mark |
|---|---|---|---|
| 1 | View the home page | none (Browse, stateless) | ○ |
| 2 | Register | `UserRegistered` (AuthProvider, mock) | + |
| 3 | Log in | `UserLoggedIn` (AuthProvider, mock) | + |
| 4 | Read the terms of use | none (viewing) | ○ |
| 5 | Accept the terms of use | `TermsAccepted` (Taakht's own terms; owner TBD) | + |
| 6 | View the list of swaps | none (query over Swaps) | ○ |
| 7 | View the list of negotiations | none (query over Negotiations) | ○ |
| 8 | Search among Ads | none (Browse, Criteria not stored) | ○ |
| 9 | Create an Ad | `AdCreated` (+ `AdPublished`) | ✓ |
| 10 | Edit an Ad | `AdEdited` (→ new `AdVersion`) | ✓ |
| 11 | Submit a negotiation offer | `NegotiationOpened` (following `RequestNegotiation`) | ✓ |
| 12 | Accept a negotiation offer | `NegotiationActive` (following `AcceptNegotiation`) | ✓ |
| 13 | A new negotiation is created | `NegotiationOpened` | ✓ |
| 14 | A negotiation expires | `NegotiationExpired` | ✓ |
| 15 | The number of concurrent negotiations reaches the cap | `AdHidden` with reason cap (the Ad's open-negotiation counter reached 10 → Ad automatically `Hidden` + system flag `system_hold`, notification to its owner) | + |
| 16 | Mark an Ad | `AdWatched` (bookmark and watch are the same) | + |
| 17 | Remove from marks | `AdUnwatched` | + |
| 18 | Watch an Ad | `AdWatched` (Communication) | + |
| 19 | Remove from watches | `AdUnwatched` (Communication) | + |
| 20 | Start a new chat | `ChatStarted` (Communication; independent of Negotiation) | + |
| 21 | Send a message | `MessageSent` | + |
| 22 | View a message | `MessageRead` | + |
| 23 | Block a user | `UserBlocked` (owner: Reputation & Trust) | + |
| 24 | Unblock | `UserUnblocked` | + |
| 25 | Mute a chat | `ChatMuted` (owner: Communication) | + |
| 26 | "The negotiation was cancelled" (system) | `NegotiationCancelled` (with a reason, for example the Ad lock because it was given to someone else; cancelling competitors is done by a Policy) | + |
| 27 | "The user cancelled the negotiation" | `NegotiationWithdrawn` | ✓ |
| 28 | Reject a negotiation request | `NegotiationDeclined` | ✓ |
| 29 | Change the delivery method or terms | `ProposalRevised` | ✓ |
| 30 | View the rules and regulations | none (viewing) | ○ |
| 31 | Confirm that the exchange took place | removed: there is no confirmation after agreement (confirming the agreement is `ProposalApproved`) | ✗ |
| 32 | "The exchange is final" | removed; the agreement is `AgreementReached`. `SwapCompleted` stays with the meaning "the agreement became final" (not physical confirmation of delivery) | ✗ |
| 33 | Fill in the item-match checklist | removed (confirmation checklist after receipt); the **claim** checklist before the agreement lives on the Ad | ✗ |
| 34 | Report the other party of the deal | `AbuseReported` (not tied to a Swap/Negotiation) | ✓ |

## 2. Complementary system events
- `ExclusiveLockAcquired`, `ExclusiveLockRejected`, `ExclusiveLockReleased`: lifecycle of the lock in Ad.
- `LockerFeePaid`, `PaymentTimedOut`, `SwapCancelled`, `SwapCompleted`: lifecycle of the physical exchange phase.
- `ReviewSubmitted`, `TrustScoreUpdated`, `PenaltyApplied`: lifecycle of reputation and trust.
