# Parked ideas

Ideas that are deliberately not part of the first version. They are kept here so we can come back to them, with the reason each one was parked and what would have to be true to take it up. This is not a backlog and promises nothing; moving an idea into scope is a decision recorded in the decisions log.

Each entry answers five questions: what the idea is, how it helps, why it is parked, what it needs, and when to look at it again.

---

## Item value and price estimation

- **Idea:** Ads carry an estimated value, and the system estimates market prices (extracting prices from the internet, taking the physical condition and damage of the item into account). The estimate could drive a value range in Criteria, match scoring, and a suggested price difference.
- **How it helps:** better matches (similar value on both sides), less haggling over the price difference, and a basis for flagging absurd amounts.
- **Why parked:** it adds a whole sub-domain (scraping, legality of price sources, per-category damage models, offline processing) before the core exchange flow is proven. Matching works on category and neighbourhood without it.
- **What it needs:** a value field on the Ad (with an optional value the user enters themselves; if they enter one, that number is shown and also influences the system's estimate), a pricing sub-domain (a separate service or part of Matching), and a decision on AI versus ML versus fixed rules.
- **Revisit when:** matching on category and neighbourhood alone gives poor matches, or users keep asking about fair price differences.

## Chain (multi-party) trades

- **Idea:** match loops of three or more people: A wants B's item, B wants C's, C wants A's.
- **How it helps:** the "coincidence of wants" problem shrinks a lot when loops are allowed, which raises the match rate.
- **Why parked:** every deal today is between exactly two Ads. A loop needs a commitment from all members at once, and one member backing out breaks the whole chain.
- **What it needs:** loop detection in Matching, an all-or-nothing agreement and lock across several Ads, and a rule for what happens when one member drops out.
- **Revisit when:** two-party matching leaves many Ads without any match.

## Penalty for repeated ghosting

- **Idea:** a user whose negotiations keep expiring without a response loses reputation, and may lose some swap access.
- **How it helps:** discourages opening many negotiations and leaving them unanswered.
- **Why parked:** for now only a locker payment timeout (`PaymentTimedOut`) costs reputation; the request limit and the negotiation deadlines already contain much of this behaviour.
- **What it needs:** a reputation input for expired negotiations, a definition of "repeated", and care that the other party's inactivity does not punish the wrong person.
- **Revisit when:** unanswered requests turn out to be a real problem.

## Inter-city exchange

- **Idea:** an Ad can be matched with Ads in other cities.
- **How it helps:** more candidates, which matters most in small cities.
- **Why parked:** it brings many consequences: lockers exist in some cities only, delivery options become conditional on the pair of cities, distance between neighbourhoods in different cities needs a definition, and handover logistics change.
- **What it needs:** a definition of cross-city distance and eligibility, delivery rules per city pair, and a decision on how the locker partner is involved.
- **Revisit when:** single-city matching leaves too many users without matches, and the locker partner covers more than one city.

## Neighbourhood polygons for distance

- **Idea:** compute the distance between neighbourhoods from their real boundaries, so a neighbourhood that is partly inside a chosen distance counts as inside.
- **How it helps:** the optional distance filter becomes more accurate and friendlier near the edges.
- **Why parked:** it needs boundary data for every neighbourhood; centres are enough for the first version and for the coarse distance steps.
- **What it needs:** polygon data per neighbourhood and an offline job that rebuilds the precomputed distance matrix. The matching design does not change.
- **Revisit when:** users report missing nearby items at the edge of the distance filter, or boundary data becomes available.

## Broader results row in Search

- **Idea:** next to the results that fit the user's Criteria, show a separate, clearly labelled row of broader results (a wider category, a larger distance).
- **How it helps:** softens the cold-start problem when few Ads fit exactly.
- **Why parked:** Search is a plain filter for now; the user can loosen the Criteria themselves.
- **What it needs:** a rule for how far to relax each part of the Criteria and a way to label the results honestly.
- **Revisit when:** searches often return few or no results.

## Hotspots (demand map)

- **Idea:** users see the neighbourhoods with the most demand or swaps for a kind of item (for example where book swapping is busiest).
- **How it helps:** guides users to active areas and supports the cold-start story with data.
- **Why parked:** it needs enough real activity to be meaningful, and it raises privacy questions.
- **What it needs:** an owner (a projection of Swap and Ad events, in Matching or a separate service), neighbourhood-level granularity, and aggregated display.
- **Revisit when:** there is real swap volume, or the demo needs a data-driven showpiece.

## Multi-item Ads

- **Idea:** one Ad holds several different items (a table plus a dumbbell plus a barbell and so on).
- **How it helps:** people with a bundle do not have to post an Ad per item.
- **Why parked:** it multiplies the stories: combined matching, all-or-nothing bundles, eligibility per line, index fan-out.
- **What it needs:** a model for bundles in the Ad, Criteria and the Proposal, and a cap on items per deal.
- **Revisit when:** single-item Ads prove too restrictive.

## Optional filming of the item

- **Idea:** optional video of the item from several angles while creating an Ad.
- **How it helps:** more trust for the counterparty, and good input for computer vision later.
- **Why parked:** media storage and upload are outside the first version (Ads only reference Divar's photos).
- **What it needs:** media storage, upload handling, and a decision on moderation.
- **Revisit when:** media storage is added for another reason.

## Trust badge and a counterparty's record

- **Idea:** show a badge or tick for users with a good record, and possibly a summary of a counterparty's history.
- **How it helps:** builds trust before a negotiation starts.
- **Why parked:** the trust score is internal for now, and showing it needs business feedback on how it should look and what it should reveal.
- **What it needs:** a decision on what is shown, a stable score, and protection against gaming.
- **Revisit when:** the score has been running long enough to be believed, and after business feedback.
