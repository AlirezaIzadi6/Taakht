# Matching rules

Working document for what a match is in the Matching context. Decisions are recorded as they are made; open points are listed at the end. Terms are defined in [ubiquitous-language.md](ubiquitous-language.md); events and commands in [event-storming.md](event-storming.md).

## Match versus Search

Both live in the Matching service and use the same engine (Elasticsearch over the Ad index), but they are different capabilities.

| | **Match** | **Search / Browse** |
|---|---|---|
| Who starts it | The system, when an Ad is published or edited (also when an Ad is reinstated or a lock is released) | The user, when they search |
| Input | The "I want" side (Criteria) stored in the Ad | An ad-hoc Criteria that is not stored |
| "I have" side | Required: it must say something, even something very general | Not required |
| "I want" side | Optional; empty means "anything" | Optional; may be completely empty |
| Direction | Two-way: each side must fit the other | One-way: a filter over the index |
| Result | `MatchFound` and a notification | A list on screen only |

Search with an empty Criteria, or with only neighbourhoods, is simply a view of the index; it is not matching.

## Decisions so far

- An Ad has an "I have" side and an "I want" side (Criteria). Item value and price are not part of the first version (see [parked ideas](../product/parked-ideas.md)); Criteria is categories and neighbourhoods only.
- An empty "I want" means "anything": the Ad matches every Ad whose own "I want" accepts its item. Two-way checking still applies.
- Matching runs when something changes (event-driven), not on a timer.
- The trust score only affects ranking: a user with a poor score is pushed down but still matched. A user in very bad standing is suspended later by an admin; it is not automatic.
- One `MatchFound` per pair of Ads; a later edit does not notify the same pair again.
- When several matches are found for one Ad at once, Communication sends a single grouped notification ("5 matches were found for your Ad") instead of one per match. The grouping window is not decided.

### Minimum Ad

- "I have": a category (it can be very general) and the neighbourhood of the item are required. The neighbourhood is mandatory, so the distance can always be computed and the city is known. Description, photos and the claim checklist are optional.
- "I want": nothing is required. The city is the item's city (one city per Ad), so Criteria needs no city of its own; categories, neighbourhoods and the distance filter are optional.
- This replaces the earlier "minimum filter" rule (at least one filter plus an explicit "show me" tick).
- In a search there is no item, so the user picks the city; everything else is optional.

### Categories

Categories form a tree (Divar's taxonomy). Two categories are compatible if they are the same or one is an ancestor of the other; categories in separate branches never match.

| "I have" | "I want" | Match? |
|---|---|---|
| drill | drill | Yes (same) |
| drill | tools | Yes: a drill is a kind of tool; no penalty |
| tools | drill | Yes, with a lower score: the owner was general and it may or may not be a drill |
| books | tools | No |

The further apart two compatible categories are in the tree, the lower the score. An empty "I want" accepts every category.

### Location

- An Ad has exactly one neighbourhood for the item itself.
- One city per Ad and per Criteria. Inter-city exchange is parked (see [parked ideas](../product/parked-ideas.md)): it brings lockers that exist in some cities only, conditional delivery options and more.
- Neighbourhoods in Criteria are optional: none chosen means the whole city (like Divar).
- In a match, chosen neighbourhoods are a filter: the other item must be inside them (the whole city if none are chosen). Distance also adds to the score: the distance between the two items' neighbourhoods, smaller is better.
- An optional maximum-distance filter (for example within 5 or 10 km of the item's neighbourhood) can be used instead of, or together with, chosen neighbourhoods. When both are set, a candidate must satisfy both. Neighbourhoods outside the chosen distance are not offered in the neighbourhood list. This replaces the earlier decision of "no maximum-distance filter".
- Distances between neighbourhoods come from a precomputed matrix, so a neighbourhood is either inside or outside a given distance. How the matrix is computed (distance between centres, or between the nearest borders so that a partly covered neighbourhood counts as inside) depends on the data we have and is decided later; the design does not change. For the first version the matrix uses distances between neighbourhood centres; a polygon-based matrix is parked (see [parked ideas](../product/parked-ideas.md)).
- In a search there is no "my item", so there is no distance (neither score nor distance filter); chosen neighbourhoods are only a filter.

### Scoring

- A pair is a match if it passes the filters (compatible categories, location, the optional distance filter, no block); the score only orders matches. There is no minimum score.
- Three factors: category fit, closeness of the two items' neighbourhoods, and the other party's trust score.
- Category fit is computed per direction, and each viewer ranks by their own direction: how well the other Ad's "I have" fits my "I want". Whoever wants "tools" sees a drill near the top, while whoever wants "drill" sees a general "tools" Ad lower. The pair is still a match for both (both directions pass the filters). Closeness is the same for both viewers; trust is the other party's.
- Priority for each viewer: category fit first, then closeness, then trust, which only separates matches that are otherwise about equal. The weights are configurable so they can be tuned with real data.

### Other decisions

- The selectable distances for the distance filter are fixed steps: 2, 5, 10 and 20 km.
- Search results are ordered like a match without distance: category fit, then trust, then the newest Ad first. With no category, trust and newest first.
- Match notifications are grouped per triggering event: all matches that come out of one publish or one edit go into one notification. Matches that arrive later from other Ads get their own notification. Collecting them over a time window (a digest) is left for when notification volume becomes a problem.
- A separate, labelled row of broader results in Search is parked (see [parked ideas](../product/parked-ideas.md)); search shows only Ads that fit the user's Criteria.
- Matches are not stored. `MatchFound` is only an event for the notification. Opening the notification recomputes the Ad's current matches from the index, so a match that stopped fitting after an edit simply disappears. Only the pairs already announced are kept, for the "once per pair" rule.

## Open points


- Where the neighbourhood centres (and the neighbourhood list) come from. For the demo, a small seeded dataset.
