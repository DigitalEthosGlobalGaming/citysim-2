# All 48 cards — descriptions, tags and balance review

Historical pre-implementation snapshot: 30 October 2026. The [current implemented catalog](CARD_CATALOG.md) supersedes its current/proposed rules and prices for playtesting. [Balance proposal and evidence](BALANCE_REVIEW_2026-10-30.md). [Sortable CSV](balance/2026-10-30-card-review.csv).

Current descriptions below are copied verbatim from the implemented catalog. Proposed copy is for the unimplemented prototype; it deliberately changes linking, Home eligibility and Park scoring. Do not paste it into the live catalog before implementing those rules. All existing tags, draw weights, rarities, road requirements and ownership limits are retained in the first prototype.

## Current catalog and metadata

Price is the live fixed purchase price. Unlock is first market round; round defaults can give Commons for free. Weight is relative within a rarity, not an overall probability. Limit 0 means unlimited per player; purchase eligibility counts owned and held copies. Road means an adjacent road of any owner. Park size is base size, before growth. Forest is the mature Sapling state, not a 49th card.

| Card | Type | Tags | Rarity | Price | Unlock | Road | Limit | Weight | Park size | Tray |
|---|---|---|---|---:|---:|---|---:|---:|---:|---|
| Street | Road | None | Common | 1 | 1 | False | 0 | 14 | 0 | False |
| House | Residential | Home | Common | 1 | 1 | True | 0 | 10 | 0 | False |
| Cottage | Residential | Home | Common | 1 | 1 | False | 0 | 4 | 0 | False |
| Shop | Commerce | Trade | Common | 1 | 1 | True | 0 | 6 | 0 | False |
| Farm | Industry | Nature | Common | 1 | 1 | True | 0 | 6 | 0 | False |
| Park | Green | Nature | Common | 1 | 1 | False | 0 | 8 | 1 | False |
| Wild Garden | Green | Nature | Common | 1 | 1 | False | 0 | 3 | 1 | False |
| Grant | Event | None | Common | 1 | 1 | False | 0 | 2 | 0 | False |
| Avenue | Road | None | Uncommon | 2 | 1 | False | 0 | 3 | 0 | False |
| Roundabout | Road | None | Uncommon | 2 | 1 | False | 0 | 2 | 0 | False |
| Orchard | Industry | Nature | Uncommon | 2 | 1 | True | 0 | 2 | 1 | False |
| Lake | Green | Nature | Uncommon | 2 | 1 | False | 0 | 2 | 0 | False |
| Vineyard | Growth | Trade, Nature | Uncommon | 2 | 1 | True | 0 | 2 | 0 | False |
| Startup | Growth | Trade | Rare | 3 | 2 | True | 0 | 2 | 0 | False |
| Market | Commerce | Trade | Rare | 3 | 2 | True | 0 | 3 | 0 | True |
| Contractor | Civic | Industry, Civic | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Apartment | Residential | Home | Uncommon | 2 | 1 | True | 0 | 3 | 0 | False |
| Cafe | Commerce | Trade, Culture | Uncommon | 2 | 1 | True | 0 | 4 | 0 | False |
| Fountain | Green | Nature, Culture | Uncommon | 2 | 1 | False | 0 | 3 | 2 | False |
| Factory | Industry | Industry | Uncommon | 2 | 1 | True | 0 | 3 | 0 | False |
| Sapling | Growth | Nature | Uncommon | 2 | 1 | False | 0 | 3 | 1 | False |
| Bank | Civic | Trade | Rare | 3 | 2 | False | 3 | 4 | 0 | True |
| Town Hall | Civic | Civic | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Museum | Civic | Culture, Civic | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Chamber of Commerce | Civic | Trade, Civic | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Warehouse | Civic | Industry | Uncommon | 2 | 1 | True | 1 | 2 | 0 | True |
| Builders' Yard | Civic | Industry | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Hospital | Civic | Civic | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Tourist Office | Civic | Culture, Civic | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Tax Office | Civic | Civic | Uncommon | 2 | 1 | False | 1 | 2 | 0 | True |
| Depot | Civic | Industry | Uncommon | 2 | 1 | True | 1 | 2 | 0 | True |
| School | Civic | Culture, Civic | Uncommon | 2 | 1 | True | 1 | 2 | 0 | True |
| Granary | Civic | Civic | Uncommon | 2 | 1 | False | 1 | 2 | 0 | True |
| Stadium | Landmark | Culture, Civic | Rare | 3 | 2 | True | 0 | 2 | 0 | False |
| Airport | Landmark | Industry, Civic | Rare | 3 | 2 | True | 0 | 2 | 0 | False |
| Skyscraper | Landmark | Trade, Civic | Rare | 3 | 2 | True | 0 | 2 | 0 | False |
| Industrial Estate | Civic | Industry, Civic | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Housing Authority | Civic | Home, Civic | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Eco Council | Civic | Nature, Civic | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Gardener's Guild | Civic | Nature, Civic | Uncommon | 2 | 1 | False | 1 | 2 | 0 | True |
| Architect's Studio | Civic | Culture, Civic | Rare | 3 | 2 | True | 1 | 2 | 0 | True |
| Renovation | Event | None | Uncommon | 2 | 1 | False | 0 | 2 | 0 | False |
| Ribbon Cutting | Event | None | Uncommon | 2 | 1 | False | 0 | 2 | 0 | False |
| Rush Hour | Event | None | Uncommon | 2 | 1 | False | 0 | 2 | 0 | False |
| Land Claim | Event | None | Uncommon | 2 | 1 | False | 0 | 2 | 0 | False |
| Central Station | Landmark | Trade, Civic | Legendary | 5 | 4 | True | 0 | 1 | 0 | True |
| Senate | Landmark | Civic | Legendary | 5 | 4 | True | 0 | 1 | 0 | True |
| Monument | Landmark | Culture | Legendary | 5 | 4 | False | 0 | 1 | 0 | True |

## Exact current card descriptions

| Card | Current text |
|---|---|
| Street | Payout: +1 VP per adjacent building, counted once across your Streets. |
| House | Payout: +1 coin per Shop within 2 tiles (max 2; Shop capacity applies). Place: +1 VP per adjacent house. |
| Cottage | Payout: +1 coin beside a Park or Lake. Place: +1 VP per adjacent park. No Links. |
| Shop | Payout: +1 coin per house within 2 tiles (max 3; house capacity applies), and +1 for a Farm within 3. Both owners earn a coin. |
| Farm | Links to up to 3 Shops within 3 tiles. Each adjacent Farm pays both owners +1 coin. |
| Park | Joins and scores the connected park. |
| Wild Garden | A park tile of size 1. Can start or bridge parks. |
| Grant | +4 coins now, then discard. |
| Avenue | Road worth 2 tiles for Longest Road. Payout: +1 coin beside at least 2 buildings. |
| Roundabout | Your Roundabout scores +1 VP whenever anyone places a building beside it. |
| Orchard | A Farm that also counts as a park tile. |
| Lake | Not a park; adjacent houses earn +1 coin and count it as a park neighbour. |
| Vineyard | Pays nothing for 2 payouts, then +3 coins each payout. |
| Startup | Payout: +1, then +2, then +3 coins, remaining at +3. |
| Market | Payout: +1 coin per own Shop within 2 tiles (max 4). |
| Contractor | +1 coin when you place a House or Shop, up to 2 per round. |
| Apartment | Payout: Counts as 2 houses for nearby Shops. Place: +2 VP per adjacent Home building. |
| Cafe | Payout: +1 coin per adjacent house or shop (max 3). End: +2 VP beside a park. |
| Fountain | A park tile of size 2. |
| Factory | Payout: +2 coins. End: adjacent houses and cafes lose 1 VP, including yours. |
| Sapling | Park size 1. After 2 payouts grows into a Forest of size 3 and scores the park. |
| Bank | Payout: +2 coins. Stackable, max 3. |
| Town Hall | Payout: +2 coins if you own at least 3 Civic-tagged buildings, including this. |
| Museum | End: +1 VP per other Culture building you own (max 8). |
| Chamber of Commerce | Payout: +1 coin per 2 Trade buildings you own (max 4). |
| Warehouse | Gain a Street at round start. The first Street you place each round pays +1 coin. |
| Builders' Yard | Gain a Street or random Common of your choice at round start. |
| Hospital | The nearest Hospital within 4 tiles of an emergency scores +1 VP. |
| Tourist Office | Payout: +1 coin per own Culture building (max 3). |
| Tax Office | Payout: +1 coin per 4 own houses. |
| Depot | Payout: +1 coin per 5 own road tiles (max 4). |
| School | Payout: +1 coin per own house within 2 tiles (max 4). |
| Granary | Payout: +1 coin per own Shop without a Farm Link. |
| Stadium | Adjacent houses gain a coin and Cafes lose one each payout. End: +1 VP per adjacent house. |
| Airport | Payout: +3 coins. End: each house within 2 loses 1 VP, capped at 3 per player across Airports. |
| Skyscraper | Place beside at least 2 buildings. End: +1 VP per building within 2 tiles (max 6). |
| Industrial Estate | Payout: +1 coin per Industry building you own, including this (max 5). |
| Housing Authority | End: +1 VP per 3 Home buildings you own, including this (max 6). |
| Eco Council | End: +1 VP per 2 Nature buildings you own (max 8), minus 2 per Industry building (min 0). |
| Gardener's Guild | +1 coin whenever you place a park tile. |
| Architect's Studio | Your first building each round pays +2 coins if placed beside your own building. |
| Renovation | Replace one of your non-road buildings with a different building card from your hand. |
| Ribbon Cutting | +1 coin per building you placed this round (max 4). |
| Rush Hour | Your Links pay +1 coin this payout (max 8). |
| Land Claim | Reserve an empty tile against other players until next round. |
| Central Station | Payout: +1 coin per Link you receive (max 6). |
| Senate | End: +2 VP per tag on at least 3 own buildings (max 12). |
| Monument | End: +6 VP. No payout. |

## Proposed descriptions and individual decisions

Read Home/Trade/Nature/Industry/Culture/Civic as tags; House card and Shop name specific cards. Adjacent means a shared edge; within N tiles includes diagonals. Any owner's qualifying neighbor counts unless the rule says your. Buildings exclude roads/events. General tag counts include their source unless they say other. Payout is each round; End is game end. Place and Play consume a card from hand. Park increase uses the new formula in the balance proposal. Road/ownership restrictions remain separate requirements, rather than being repeated in every sentence.

Prototype A prices below list buy + build. They are experimental, not measured fair prices. Free Common defaults pay no purchase cost. Events and roads have no play/build cost.

| Card | Buy + build | Proposed copy | Decision / watchpoint |
|---|---:|---|---|
| Street | 1 + 0 | Payout: +1 VP per building adjacent to your Streets. Count each building once. | Keep. Any owner's building counts; only this road type pays recurring VP. |
| House | 1 + 0 | Place: +1 VP per adjacent Home building. Payout: +1 coin if a Shop is within 2 tiles. | Replace linking with one independent condition; generalize Home adjacency to the tag. |
| Cottage | 1 + 0 | Place: +1 VP per adjacent park tile or Lake. Payout: +1 coin if adjacent to a park tile or Lake. | Keep. A Lake's own payout can give this Cottage another coin. Remove No Links. |
| Shop | 1 + 0 | Payout: +1 coin per Home building within 2 tiles, max 3. | Independent count; remove shared capacity, both-owner payment and Farm supply bonus. |
| Farm | 1 + 0 | Payout: +1 coin if adjacent to another Farm or Orchard. | Replace Farm-Shop and Farm-Farm linking with one condition. Nature tag stays. |
| Park | 1 + 0 | Park size 1. Place: score the park's increase. | Use new shared Park help rule; owner-only marginal scoring capped at size 10. |
| Wild Garden | 1 + 0 | Park size 1. Place: score the park's increase. | Same mechanics as Park. Keep as art variant for now; it has no exclusive bridging power. |
| Grant | 1 + 0 | Play: gain 4 coins, then discard this card. | Keep initially. Costs a market opportunity; Common purchase gives a nominal 3-coin gain. |
| Avenue | 3 + 0 | Counts as 2 tiles for Longest Road. Payout: +1 coin if adjacent to at least 2 buildings. | Keep. Does not receive Street's recurring VP rule. |
| Roundabout | 3 + 0 | When anyone builds beside this: gain 1 VP. | Keep. Buildings only; roads and events do not trigger it. No retroactive award. |
| Orchard | 3 + 0 | Park size 1. Place: score the park's increase. Payout: +1 coin if adjacent to a Farm or another Orchard. | Explicit independent Farm role plus new Park rule. Retain Nature only. |
| Lake | 3 + 0 | Payout: each adjacent Home building earns its owner 1 coin. Counts as a park neighbor, but is not a park tile. | Keep payout; standardize to Home tag. Never joins a park region. |
| Vineyard | 3 + 0 | Payout: nothing for its first 2 payouts; then +3 coins each payout. | Keep timing/amount initially; force early/late tests. Zero plays in baseline. |
| Startup | 5 + 2 | Payout: +1 coin the first time, +2 the second, then +3 each time. | Keep ramp; force timing tests. Zero plays is not proof of weakness. |
| Market | 5 + 2 | Payout: +1 coin per Shop you own within 2 tiles, max 4. | Keep. Shop card only, not every Trade building. Force engine scenario. |
| Contractor | 5 + 2 | When you build a House card or Shop: gain 1 coin, up to 2 coins per round. | Keep the specific-card restriction; Apartment and Cottage do not trigger it. |
| Apartment | 3 + 0 | Place: +2 VP per adjacent Home building. Payout: +2 coins if a Shop is within 2 tiles. | State its own income. Remove counts-as-two and capacity allocation. |
| Cafe | 3 + 0 | Payout: +1 coin per adjacent Home building or Shop, max 3. End: +2 VP if adjacent to a park tile or Lake. | Direct count, no link object. Preserve Lake bonus; standardize Home tag. |
| Fountain | 3 + 0 | Park size 2. Place: score the park's increase. | Keep size, replace repeated region scoring. |
| Factory | 3 + 0 | Payout: +2 coins. End: each adjacent Home building or Cafe loses its owner 1 VP, including yours. | Keep amount and stacking pollution; standardize Home tag. Test with total build cost. |
| Sapling | 3 + 0 | Park size 1. Place: score the park's increase. After its second payout, grows to size 3 and scores the increase again. | Use marginal size increase; Forest is a mature state, not another draw. |
| Bank | 5 + 2 | Payout: +2 coins. | Keep as reference income card; separate requirement line says limit 3. |
| Town Hall | 5 + 2 | Payout: +2 coins if you own at least 3 Civic buildings, including this. | Keep threshold. Civic means tag, not card type. |
| Museum | 5 + 2 | End: +1 VP per other Culture building you own, max 8. | Keep. Excludes itself; force Culture scenario. |
| Chamber of Commerce | 5 + 2 | Payout: +1 coin per 2 Trade buildings you own, max 4 coins. Includes this. | Keep threshold/cap; zero plays requires targeted coverage. |
| Warehouse | 3 + 0 | Round start: gain a Street if your hand has room. When you build your first Street each round: gain 1 coin. | Keep. Default arrives next round; generated Streets may be especially strong when money is scarce. |
| Builders' Yard | 5 + 2 | Round start: gain a Street or a random Common, if your hand has room. Choose which before the round starts. | Keep. Choice is Street versus random Common, not selecting a specific Common. Defaults to Street. |
| Hospital | 5 + 2 | Each round: one random Home building has an emergency. The nearest Hospital within 4 tiles earns its owner 1 VP. Ties go to the earlier Hospital. | Standardize emergency eligibility to Home tag; no emergency when none exist. Keep award/range; force coverage. |
| Tourist Office | 5 + 2 | Payout: +1 coin per Culture building you own, max 3. Includes this. | Keep. Self-count means at least 1 while owned. |
| Tax Office | 3 + 0 | Payout: +1 coin per 4 Home buildings you own. | Unify residential-only predicate to Home tag. No cap currently; monitor large cities. |
| Depot | 3 + 0 | Payout: +1 coin per 5 road tiles you own, max 4 coins. | Keep physical-card count. Avenue counts once here, twice only for Longest Road. |
| School | 3 + 0 | Payout: +1 coin per Home building you own within 2 tiles, max 4. | Standardize residential-only predicate to Home tag. School is not Home and cannot count itself. |
| Granary | 3 + 0 | Payout: +1 coin per Farm or Orchard you own, max 3. | Replace unsupplied-Shop calculation; no supply status remains. |
| Stadium | 5 + 2 | Payout: each adjacent Home building earns its owner 1 coin; each adjacent Cafe loses its owner 1 coin. End: +1 VP per adjacent Home building. | Keep amounts, any owner; standardize Home tag. No debt; payout deductions can use saved coins. |
| Airport | 5 + 2 | Payout: +3 coins. End: each Home building within 2 tiles loses its owner 1 VP. Maximum loss: 3 VP per player across all Airports. | Keep combined penalty cap, standardize Home tag. Test income after total cost. |
| Skyscraper | 5 + 2 | Requires at least 2 adjacent buildings. End: +1 VP per other building within 2 tiles, max 6. | Keep. Any owner qualifies. Show buildability before purchase; none played in baseline. |
| Industrial Estate | 5 + 2 | Payout: +1 coin per Industry building you own, max 5. Includes this. | Keep Industry TAG count. Farms and Orchards do not count. |
| Housing Authority | 5 + 2 | End: +1 VP per 3 Home buildings you own, max 6. Includes this. | Keep thresholds; it also counts for the prototype's general Home interactions. |
| Eco Council | 5 + 2 | End: +1 VP per 2 Nature buildings you own, max 8; then subtract 2 VP per Industry building you own, minimum 0. Includes this in Nature. | Keep cap-before-penalty order; Industry tag only. Do not give Farm an Industry tag. |
| Gardener's Guild | 3 + 0 | When you build a park tile: gain 1 coin. | Keep placement-only trigger; no coin for Sapling growth. Test after park nerf. |
| Architect's Studio | 5 + 2 | When you build your first building each round beside one of your buildings: gain 2 coins. This Studio can trigger its own effect. | Keep self-trigger. If first building fails the condition, a later building does not qualify. |
| Renovation | 3 + 0 | Play: replace one of your buildings with a different building card from your hand. Discard both cards. Pay the new building's build cost. | No roads/events as replacement or target. Keep earned VP; new age and placement effects apply. Build charge is new. |
| Ribbon Cutting | 3 + 0 | Play: gain 1 coin per building you built this round, max 4, then discard this card. | Keep first trial; at new Uncommon price 3 only net +1 at cap. Consider Common weight 1 if focused tests confirm weakness. |
| Rush Hour | 3 + 0 | Play: this payout, gain 1 extra coin per Shop you own, max 4. Discard this card. Once per round. | Replace Link bonus. One-round nonstacking effect, not permanent income. |
| Land Claim | 3 + 0 | Play: reserve an empty tile until next round starts. Only you may build there. Discard this card. | Keep tactical role; costs a purchase opportunity and does not stop rivals building elsewhere. |
| Central Station | 8 + 4 | Payout: +1 coin per Shop or Market you own, max 4. | Replace received Links. Does not count itself. Validate Legendary total price against other engines. |
| Senate | 8 + 4 | End: +2 VP for each tag you have on at least 3 buildings, max 12. Includes this. | Keep six-tag scoring; buildings can qualify for multiple tags. |
| Monument | 8 + 4 | End: +6 VP. | Keep flat VP. No income; compare all-in Legendary price against Station and Senate. |

## Current text accuracy issues

- House placement counts residential Home cards (including Cottage); its income only uses House and Apartment. Shop income ignores Cottage and Housing Authority. Apartment can earn up to 4 linked coins, but its current text never states its own income.
- Cottage beside Lake can receive its own 1 coin plus the Lake's 1 coin. Lake pays residential Homes, not every Home-tagged building. Cafe's end park bonus includes Lake, and its income includes Cottage. House/Home is currently inconsistent across cards.
- Farm and Orchard currently share the Farm category; Farm-Farm adjacency pays per adjacent Farm pair, not once if any are adjacent. Prototype Farm income is deliberately lower and simpler.
- Park size is capped at 10 for computing the award, but additions beyond 10 still award full VP. Shared awards go to each represented owner. Wild Garden is exactly a Park with a different draw weight/name. Sapling's mature size is 3; its ID stays sapling.
- Bank has no road requirement and three copies are allowed. Many Civic-TYPE cards have no Civic TAG; this is implemented gameplay, not a presentation omission.
- Builders' Yard chooses Street versus a random Common. Hospital uses one random residential emergency per round, nearest eligible Hospital, and earlier placement for ties. No homes means no emergency.
- Tax Office/School, Stadium, Factory and Airport currently use residential Homes; Apartment placement and Housing Authority scoring use Home tags. The proposal intentionally unifies these to tags. Hospital and the Home-based goals/contracts must follow the same glossary.
- Contractor triggers only House card or Shop, not all Home/Trade buildings. Architect's Studio can trigger itself if it is your first building and beside an owned building. Placing an ineligible first building consumes that round's opportunity.
- Depot counts physical road cards; Avenue's weight 2 only applies to Longest Road. Roundabout and Avenue do not inherit Street's recurring VP.
- Eco Council caps Nature VP before subtracting Industry penalties. Airports share one 3-VP loss cap per player across all Airports; Factory penalties have no shared cap. Cafe deductions can use saved coins but cannot create debt.
- Renovation consumes both cards, retains previously earned VP, resets age and applies new placement effects. Land Claim expires at the next round START. Rush Hour currently adds coins per received link unit and does not stack; proposed version is different.

## Baseline card observations

32 current-rule games, 128 player outcomes. Offered counts are per eligible player and exclude ownership-ineligible copies. Received includes free cards. Played includes events and replacements. Purchases are observations of bot preferences, not card-strength rankings. Aggregate placement VP includes other players' shared awards; payout coin totals can include amounts given to neighbors. End VP can include penalties to neighbors. Growth awards and indirect synergies are not all attributed here.

| Card | Offered | Bought | Received | Played | Discarded | Placement VP, all recipients | Payout coins, all recipients | End VP, all recipients |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Street | 3018 | 44 | 115 | 115 | 0 | 0 | 0 | 0 |
| House | 1552 | 214 | 495 | 430 | 10 | 279 | 2908 | 0 |
| Cottage | 777 | 260 | 344 | 344 | 0 | 478 | 1540 | 0 |
| Shop | 1096 | 165 | 394 | 379 | 1 | 1 | 5425 | 0 |
| Farm | 992 | 60 | 188 | 166 | 4 | 0 | 2153 | 0 |
| Park | 1279 | 874 | 1060 | 1060 | 0 | 12454 | 0 | 0 |
| Wild Garden | 451 | 277 | 361 | 361 | 0 | 4372 | 0 | 0 |
| Grant | 396 | 25 | 73 | 73 | 0 | 0 | 0 | 0 |
| Avenue | 209 | 21 | 21 | 21 | 0 | 0 | 46 | 0 |
| Roundabout | 169 | 5 | 5 | 5 | 0 | 0 | 0 | 0 |
| Orchard | 172 | 111 | 111 | 107 | 0 | 561 | 1390 | 0 |
| Lake | 116 | 2 | 2 | 2 | 0 | 0 | 0 | 0 |
| Vineyard | 152 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Startup | 75 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Market | 96 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Contractor | 49 | 2 | 2 | 2 | 0 | 0 | 0 | 0 |
| Apartment | 185 | 58 | 58 | 57 | 1 | 86 | 677 | 0 |
| Cafe | 248 | 41 | 41 | 41 | 0 | 0 | 187 | 58 |
| Fountain | 326 | 276 | 276 | 276 | 0 | 3083 | 0 | 0 |
| Factory | 194 | 59 | 59 | 58 | 0 | 0 | 558 | -7 |
| Sapling | 235 | 113 | 113 | 113 | 0 | 1204 | 0 | 0 |
| Bank | 210 | 32 | 32 | 32 | 0 | 0 | 198 | 0 |
| Town Hall | 103 | 2 | 2 | 2 | 0 | 0 | 6 | 0 |
| Museum | 67 | 14 | 14 | 14 | 0 | 0 | 0 | 46 |
| Chamber of Commerce | 24 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Warehouse | 204 | 13 | 13 | 12 | 0 | 0 | 0 | 0 |
| Builders' Yard | 123 | 8 | 8 | 8 | 0 | 0 | 0 | 0 |
| Hospital | 114 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Tourist Office | 48 | 21 | 21 | 21 | 0 | 0 | 124 | 0 |
| Tax Office | 187 | 10 | 10 | 10 | 0 | 0 | 47 | 0 |
| Depot | 199 | 22 | 22 | 22 | 0 | 0 | 17 | 0 |
| School | 160 | 23 | 23 | 21 | 0 | 0 | 124 | 0 |
| Granary | 172 | 7 | 7 | 7 | 0 | 0 | 6 | 0 |
| Stadium | 88 | 9 | 9 | 9 | 0 | 0 | 0 | 7 |
| Airport | 98 | 17 | 17 | 15 | 0 | 0 | 270 | -14 |
| Skyscraper | 61 | 4 | 4 | 0 | 0 | 0 | 0 | 0 |
| Industrial Estate | 112 | 22 | 22 | 22 | 0 | 1 | 341 | 0 |
| Housing Authority | 124 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Eco Council | 44 | 6 | 6 | 6 | 0 | 0 | 0 | 46 |
| Gardener's Guild | 168 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Architect's Studio | 88 | 4 | 4 | 4 | 0 | 0 | 0 | 0 |
| Renovation | 113 | 53 | 53 | 53 | 0 | 0 | 0 | 0 |
| Ribbon Cutting | 208 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Rush Hour | 156 | 4 | 4 | 4 | 0 | 0 | 0 | 0 |
| Land Claim | 76 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Central Station | 64 | 28 | 28 | 28 | 0 | 0 | 492 | 0 |
| Senate | 108 | 38 | 38 | 38 | 0 | 0 | 0 | 286 |
| Monument | 92 | 74 | 74 | 74 | 0 | 0 | 0 | 444 |
