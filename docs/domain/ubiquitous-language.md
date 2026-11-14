# Ubiquitous Language

This document defines the standard, agreed vocabulary of the Taakht system, following Domain-Driven Design, so that all architecture documents, domain models, C# and Go code, variable names and database tables use the same terms without ambiguity.

---

## 1. High-level terms: containers and phases of a deal

| Term | Kind | Precise definition |
|---|---|---|
| **Deal** | Bounded Context / Service | The largest container of a deal in Taakht, implemented as a Modular Monolith in .NET. It contains both the negotiation phase (Negotiation) and the physical exchange (Swap). There is no event called `DealCreated`; the name is only an architectural boundary. |
| **Negotiation** | Module / Aggregate Root | The first phase of a Deal: the human world of bargaining, changing minds and uncertainty. It starts with the first request and ends with agreement, withdrawal or expiry. |
| **Swap** | Module / Aggregate Root | The second phase of a Deal: the definite, physical and logistical world. It is born only after `AgreementReached` is emitted and the exclusive lock (`ExclusiveLockAcquired`) is acquired, and it records the evidence (Audit Vault) and relays the locker-fee payment. |

---

## 2. Entities and documents inside a negotiation

| Term | Kind | Precise definition |
|---|---|---|
| **Ad** | Aggregate Root | The persistent advertisement of an item in the Ad service, with a stable id and immutable versions (`AdVersion`). It has an "I have" side (required: a category, which can be very general, and the neighbourhood of the item; optional: description, photos that only reference Divar's, and the claim checklist) and an "I want" side (a Criteria, all optional). An Ad belongs to one city. An Ad can have at most 10 concurrent open negotiations. See [matching.md](matching.md). |
| **Criteria** | Value Object | What someone is looking for: categories (empty means any), neighbourhoods of the city (empty means the whole city) and an optional maximum distance (2, 5, 10 or 20 km); no value or price in the first version. Inside an Ad it is the "I want" side and is stored with the Ad; in a search it is ad-hoc, not stored, and passed directly to the Matching index. |
| **Proposal** | Value Object / Snapshot | A snapshot document of the deal terms. A proposal has three components: `(Ad1_Version + Ad2_Version + Terms)`. A proposal is immutable; any change to the items or the terms invalidates the current proposal and a new proposal is born. |
| **Terms** | Value Object | The logistics details and the agreed price difference in a proposal: the delivery method for each leg (locker or in person/courier) and the agreed price difference, settled outside the system. |
| **Agreement** | Domain Concept / Event | The final, frozen version of a proposal approved by both parties (`AgreementReached`). It is not an independent entity but the transition of the active proposal into a binding agreement and the input of the Swap module. |

---

## 3. Verbs and actions (Commands & Actions)

To avoid overlap and conflicts between concepts, these verbs are standardised strictly:

| Verb / term | Scope | Meaning |
|---|---|---|
| **Request** (`RequestNegotiation`) | Only for starting contact | The requester asks for a negotiation to start over two Ads. The negotiation is born in the `Pending` state. Each directed Ad pair (requester's Ad, target Ad) has only 3 request rights; each request consumes one right at the moment it is sent, and the counter resets to zero on acceptance. |
| **Accept** (`AcceptNegotiation`) | Only in response to a Request | By accepting the request, the Ad owner opens the chat/negotiation room and the negotiation becomes `Active`. Nothing has been agreed about the items yet. |
| **Decline** (`DeclineNegotiation`) | In response to a Request | The Ad owner rejects the request to start a negotiation and the negotiation is closed. |
| **Revise** (`ReviseProposal`) | Only on the content of the Terms | One of the parties edits the delivery terms or the price difference. A new proposal is produced that has no approval; both parties must explicitly approve again (there is no implicit approval). |
| **Rebase** (`RebaseProposal`) | System-driven; reaction to `AdEdited` or `VersionChanged` | A new proposal is built with the current Ad versions and the same terms. It has no approval and does not extend the negotiation deadline. |
| **Approve** (`ApproveProposal`) | Only on the snapshot of the active proposal | The user declares that they accept the terms in this version of the proposal (`ProposalApproved`). Approval by both parties leads to `AgreementReached`. |
| **Retract** (`RetractApproval`) | On an active approval | The user takes back an earlier approval before the final agreement is reached. |
| **Withdraw** (`WithdrawNegotiation`) | During an open negotiation | One of the two parties voluntarily leaves the negotiating table. |
| **Expire** (`ExpireNegotiation`) | System-driven (time) | 3 days pass without new activity, or the absolute cap of 30 days is reached. |
| **Cancel** (`CancelNegotiation`) | System-driven (external event) | The system cancels the negotiation with a stated reason (for example the item was given away in a competing negotiation following a rejected lock). |

---

## 4. Reputation and trust lifecycle

| Term | Kind | Precise definition |
|---|---|---|
| **Trust Score** | Projection / State | A user's trust score, calculated from the history of Event Sourced events. |
| **SubmitReview** | Command | The deal party's answer to "were you satisfied with the deal?" after `SwapCompleted` (the answer shape, yes/no or score, and the time window are decided later). It is an input to the other party's internal score and is not shown to the user. |
| **ReportAbuse** | Command | A report of a user's misconduct. A reporter with the minimum score has an instant effect; others need admin confirmation. A rejected report lowers the reporter's score. |
| **BlockUser** | Command | One user blocks another. The block works both ways until `UnblockUser`: no new chat messages, no visibility of each other's Ads and no `MatchFound` in Matching, no new negotiation requests, and open negotiations that have not reached `AgreementReached` are withdrawn. Old messages stay; a negotiation waiting for the lock and any existing Swap are untouched. The blocked user is notified. |
| **Objection** | Command | The penalised user, or the owner of a suspended Ad, disputes the penalty or suspension. One per penalty or suspension, no deadline for now. The penalty or suspension stays in force until an admin decides; if the objection is upheld it is lifted and the reporter's score drops. |
| **Suspended** | Ad state | An admin has suspended the Ad. It is separate from `Hidden`: the owner cannot publish it and can only edit it or object. Reinstating restores the earlier state. |
| **Penalty** | Domain Concept | A negative mark and penalty recorded for a user's misconduct (for example not paying the locker fee, which led to `PaymentTimedOut`). |
