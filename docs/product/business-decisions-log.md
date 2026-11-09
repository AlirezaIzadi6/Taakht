# Consolidated Business Decisions: Taakht Project (Divar)

This file keeps the decisions finalised in the business discussions so nothing is lost when the summary is written. (The refined output of this file is in `business-summary.md`; this version is the rougher, more detailed one.)

## Revenue model
- Instead of assuming a "fixed ad fee" (which does not hold for the whitelisted low-value categories, since they are generally listed for free on Divar), the revenue model sits on Divar's proven pattern: **paid matching priority (Priority Matching Tier)**.
- ~~Part of the commitment deposit as a processing fee.~~ **Obsolete (1405/07/11):** the deposit and any money in the system were removed; only matching priority remains.

## Tax evasion / lack of formal registration of deal value
- Decision: the risk is treated as negligible because the scope is limited to low-value goods.
- Refinement: since the out-of-scope categories (gold, cars, digital goods) are permanent, not temporary, the caveat "if the scope expands" was irrelevant and was removed. The only remaining point is value growth inside the current categories (for example an expensive sofa); the solution of an overall value cap for this was rejected (next item), so this risk remains as a small, accepted possibility with no extra containment mechanism.

## Cannibalisation of Nardeban (ladder) / urgent ads
- Decision: negligible risk, even with item value growth inside the current categories, because Nardeban is not used to any meaningful degree in these categories (books, tools, sofas, sports equipment), regardless of item price.
- An overall value cap for items eligible for swapping: **rejected**, not needed.

## Logistics risk (loss/damage in transit by courier or locker)
- Separated from item-condition mismatch: a condition mismatch is between two users (no platform arbitration), but damage in transit is the logistics partner's fault, not the deal parties'.
- **Revision (1405/07/11):** this risk applies only to **locker** handover; the courier is coordinated by the parties themselves and is no concern of the platform.
- Decision: contractual liability with the locker partner (Ganjeh); the partner must accept a duty to compensate up to the value declared in the initial checklist; the platform only forwards the claim and does not enter content arbitration.

## Existing informal competitors
- Not yet examined. Working assumption for now: Divar has the advantage because of network effects (more ads = better chance of a match + historical price data), not merely because it is "bigger".
- Real research was added to the work-in-progress list (`open-items.md`).

## Unsourced figures (90% ghosting, 15% versus 2% conversion target)
- Decision: these figures are removed from the document; only the qualitative direction of the claim is kept (for example "the conversion rate of traditional methods is very low" without a claimed exact number).

## Legal liability model (no platform arbitration)
- Decision: stated in the document as a known limitation; not solved in this phase.

## ~~Escrow and internal wallet~~ (obsolete, 1405/07/11)
- ~~Both are mocked.~~ **New decision:** the platform locks or holds no money; financial transactions (such as the price difference) are entirely the parties' responsibility. Escrow, wallet, deposit, commission on money and the PSP licensing issue were removed from the scope.
- Accepted limitation: the platform does not guarantee payment; the risk of the price difference not being paid is the parties'.
- The risk of "money laundering through a fake price difference" is no longer a platform risk (money does not pass through us); whether the agreed amount in the Proposal needs a cap or a flag is open.

---
*Date: 1405/06/19 (2026-09-10)*
