# Business Summary: Taakht Feature (Divar), so far

> This is an interim version, based on the discussions up to today. Several items are still open (`open-items.md`). The aim of this text is to show that the idea "is worth thinking about", not that "everything is solved".

## 1. Problem statement (summarised)
In the current economic situation, users hold a volume of small, little-used assets that existing solutions (cash sales at a steep discount, manual swap forums) make uneconomical and inefficient because of high friction and the lack of a matching or trust mechanism. *(Unsourced quantitative claims were removed from this section; only the qualitative direction of the claim remains.)*

## 2. Why this is worth examining, and why not a first-tier priority
- A defensible angle: activating idle inventory and increasing engagement, not large direct revenue.
- Divar's main revenue comes from heavy verticals (real estate, cars, jobs). This feature works at the opposite end of the spectrum (low value, low frequency), so it does not compete for resources with the main priorities and should not.
- An honest framing: this is a **calculated bet for a Growth/Experiments team**, not a claim about "the future of Divar". Stating it this way is more credible than an unrealistic claim.
- A realistic MVP resource estimate (not a one-person demo): a team of 4 to 6 (PM, 1 to 2 backend, frontend/mobile, part of data, legal/partnerships) for 3 to 6 months to a pilot. The bottleneck is usually legal and contract negotiation, not coding.
- What already exists in the real Divar (a description of market reality for a real team, not a decision about the demo project's scope): basic identity verification, ad creation, geographic search, abuse reports. What is truly new: the matching engine, the reputation system, and locker handover. (Holding users' money, escrow, is deliberately outside the scope; sections 3 and 5.) Whether Divar's existing general chat is enough or a deal-specific chat is needed is itself an open product question (`open-items.md`).
- Return: because of the deliberate restriction to low-value goods, the ceiling on direct revenue is low; the main defence must rest on secondary metrics (engagement, inventory activation), not on a revenue figure.

> **Note:** This section is about the scenario of a real team at Divar. The demo project's technical scope (what is mocked and what is built for real) is documented separately in `architecture-overview.md` and does not depend on "what exists in the real Divar".

## 3. Revenue model
- **Not** an assumed fixed ad fee (which does not hold for these categories, since they are generally listed for free).
- **Yes** to Divar's proven pattern: paid matching priority (Priority Matching Tier).
- **No money passes through the platform** (decision 1405/07/11): no escrow, no blocked funds, no wallet. The price difference (if any) is settled directly between the two parties, so there is **no transaction commission or deposit fee** either; matching priority is the only revenue source.
- Risk of cannibalising Nardeban (ladder) and urgent-ad products: negligible, because these categories are not consumers of Nardeban at all (regardless of item price).

## 4. Identified risks and the approach to containing them (not a full solution)

### Category A: existing Divar risks that this feature makes bigger
| Risk | Minimal response |
|---|---|
| Stolen goods, mismatch in item condition | The existing abuse-report mechanism plus the item-condition claim checklist; checking after receipt is the parties' own responsibility |

### Category B: entirely new risks caused by the feature's mechanism
| Risk | Minimal response |
|---|---|
| Financial abuse through a fake price difference | Because the platform moves no money, it is not itself a money-laundering intermediary; the financial risk is between the parties. Open: whether the platform wants a cap or a flag for the agreed amount in the Proposal (`open-items.md`) |
| Off-system payment risk (one party does not pay the price difference) | The platform does not guarantee it; trust score plus abuse reports. An accepted limitation (section 5) |
| Manipulating the trust score with repeated colluding deals | A limit on the number of deals allowed between a pair of users in a given period |
| Loss or damage of the item in transit by **locker** (different from a condition mismatch) | Contractual liability with the locker partner up to the declared value; the platform only forwards the claim and does not arbitrate content. In-person/courier is coordinated by the parties themselves and the responsibility is theirs |
| The cold-start problem in a hyperlocal two-sided market | A focused pilot in one dense area, plus a user-adjustable radius as a safety valve |

## 5. Known limitations deliberately left unsolved (current phase)
- **No escrow and no payment guarantee (decision 1405/07/11):** the platform holds no money and the parties settle the price difference outside the system; so the PSP/escrow licensing issue and the dependency on a wallet are gone, but there is no payment guarantee either. This is an accepted limitation.
- **Only two handover methods:** locker (integrated with the platform) and in person/courier (fully coordinated by the parties). The platform neither tracks nor insures couriers.
- **Legal liability model in item-condition disputes:** the platform does not arbitrate content; it only offers abuse reports and blocking. This is an accepted limitation, not a bug.
- **Residual tax / lack of formal registration risk from value growth inside the current categories:** accepted as a small possibility, with no extra containment mechanism.
- **Existing informal competitors (Telegram/Instagram groups, similar global apps):** not examined; it is on the work-in-progress list.

## 6. Work in progress
- Research on informal swap competitors, so that the argument "why Divar wins" rests on network effects (not only size).

---
*Interim version, dated 1405/06/19 (2026-09-10). Updated as later decisions are reconciled.*
