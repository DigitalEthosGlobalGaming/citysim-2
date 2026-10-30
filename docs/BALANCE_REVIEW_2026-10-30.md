# Card and economy balance review — 30 October 2026

**Recommendation:** remove linking completely, replace it with independent local income, and test a stronger spending economy. Fix repeated park scoring alongside the economy: cheap, repeatable VP currently makes buying an income engine less attractive.

**Status:** Prototype B is the selected implementation: free placement, prices 1/4/7/11, base income 2, independent income and marginal owner-only park scoring. Prototype A remains an optional research configuration. See [implementation notes](BALANCE_IMPLEMENTATION_2026-10-30.md) for the comparison. The historical simulations and proposal below describe earlier rules and decisions; prototype balance still needs human playtesting. The [current implemented catalog](CARD_CATALOG.md) lists the live 48 cards. The historical [complete card review](CARD_REVIEW_2026-10-30.md) contains all 48 cards, pre-change descriptions, types, tags, rarities, prices, road requirements, limits, weights, proposed descriptions and individual decisions. A [CSV](balance/2026-10-30-card-review.csv) is supplied for sorting and review.

## 1. Findings that explain the playtest

The implementation has already moved from auctions to a fixed-price market. Each player can buy a copy of the same offer; a purchase does not remove it. Prices are 1/2/3/5 for Common/Uncommon/Rare/Legendary. Every round permits up to three purchases per player. Once a player's income covers their preferred three cards, extra money buys no more actions. Buildings cost nothing to place. That creates a low spending ceiling beneath a compounding income engine.

Starting money is 8; base income is 3 per payout; last-place catch-up is 1 from round 2. Eight base payouts plus starting money supply 32 coins before buildings or catch-up. The final payout happens after the last market, so its coins can only help final scoring and the tiebreaker. Saved coins convert at 5:1. Coins already score points; the missing experience is having to choose what to spend them on.

The old [27 October baseline](BALANCE_BASELINE.md) predates the current fixed-price behavior. Its “keep current values” recommendation does not settle this review. This report uses fresh runs of the current code.

There are also documentation mismatches: the GDD says a free Street plus a Common each round, but code gives **zero Streets and one Common**. It says the market refills to Hand Target, but the live implementation admits anyone below **Hand Limit 5**, for up to three selections. `HandTarget=3` affects bot discarding rather than market admission. The simulation reproduces the code. Keep the three-purchase limit in the prototype and correct the GDD; refilling only to three would reduce spending opportunities further.

## 2. Evidence: 128 completed matches

Research runner: [balance/lab/Program.cs](balance/lab/Program.cs). Raw results and complete source fingerprints: [2026-10-30-economy-seed-30102026.json](balance/2026-10-30-economy-seed-30102026.json). Generated tables: [experiment summary](balance/2026-10-30-experiment-summary.md).

Four variants each ran 32 four-player matches, 128 player outcomes per variant. Seed 30102026; eight distinct board seeds, each with all four seat rotations. Balanced, Industry-tag, Park Rush and Economy First retain strategy-specific seeds across rotations. Board: 15×15 with the real seeded road cross. Normal difficulty, fixed 0.25-second steps. Each variant changes one setting family; all cards and linking remain current.

| Variant | Mean final coins | Mean total VP | Mean spent | Purchases / player | Eligible markets with any unaffordable legal option | Mean coin-conversion VP |
|---|---:|---:|---:|---:|---:|---:|
| Current | 129.43 | 235.70 | 36.55 | 23.58 / 24 | 0.00% | 25.45 |
| Base income 3 → 1 | 113.27 | 233.59 | 36.28 | 23.52 / 24 | 0.73% | 22.24 |
| Prices 1/2/3/5 → 1/3/5/8 | 117.51 | 235.07 | 47.81 | 23.60 / 24 | 2.88% | 23.09 |
| Coins per VP 5 → 3 | 138.12 | 247.44 | 35.75 | 23.51 / 24 | 0.00% | 45.69 |

“Unaffordable” is measured at selection reveal, per eligible player; ownership-ineligible cards are excluded. It measures whether money could constrain selection, not whether a player wanted that particular card. Zero does not mean zero passes. All 128 matches completed with zero rejected actions and zero budget violations; coin ledgers reconciled.

Verification: all 500 existing rule checks passed ([saved output](balance/2026-10-30-rule-checks.txt)). A separate four-match replay of every variant reproduced the first four recorded matches exactly, including telemetry, and matched the source fingerprints. The catalog export contains exactly 48 unique reviewed cards.

Current mean coin balance after payouts 1–8: **10.21, 16.38, 27.05, 40.78, 58.29, 79.34, 103.45, 129.43**. Accumulation accelerates. Current players buy 98.2% of the possible 24 cards. Cutting base income alone removes about 16 coins while leaving purchases almost unchanged. Moderately increasing prices also leaves purchases unchanged. Neither addresses the underlying spending ceiling.

Current Economy First ends with 195.69 coins on average. At 3 coins per VP, its win share rises from 28.13% to 59.38% in this matchup. That is evidence against using more generous coin conversion as the main fix: it promotes saving without establishing meaningful purchases. It is not proof that economy strategies dominate human play.

Parks are a second major issue. At connected size 10+, each addition still gives the placer 5 VP and **each other represented owner 3 VP**, because `ParkAwardsAfterCap=true`. With four owners that is up to **14 VP across the table per addition**, indefinitely while space and cards remain. Size 10 caps the scoring size, not the number of scoring events. Sapling growth triggers another park award. Current runs played 1,060 Parks, 361 Wild Gardens and 276 Fountains; they awarded 12,454, 4,372 and 3,083 placement VP respectively across all recipients. These totals include rivals' shared awards and are not the buyer's personal return. Parks alone generated 11.75 table-wide VP per placement on average. This helps explain high final scores and why a delayed coin engine may feel unexciting.

**Limits:** these are eight paired board blocks, not 128 independent board samples. Bots use a short horizon and shared evaluation logic; they often favor the same cheap parks. Ten cards were never played in the current sample, including Vineyard, Startup, Hospital and Housing Authority. Absence is a coverage warning, not evidence that a card is weak. These tests do not simulate the proposed removal of linking, proposed build costs, or the combined prototype. Do not label these numbers as outcomes of that redesign. No human telemetry was used.

## 3. Remove linking as a mechanic

Delete shared capacity, first-placed allocation, link units, received-link counters and link-specific scoring. Players should evaluate a card by looking at its own neighbors. No pairing selection, capacity reservation, supply state or order-dependent allocation remains. Cards count qualifying buildings independently; rivals' eligible neighbors count unless a card explicitly says “your”. Each card pays only its owner unless its text explicitly names other recipients.

First-prototype replacements:

| Card | New rule | Why |
|---|---|---|
| House | Payout: +1 coin if a Shop is within 2 tiles. Keep placement VP. | One yes/no condition; no capacity. |
| Apartment | Payout: +2 coins if a Shop is within 2 tiles. Keep placement VP. | No “counts as two houses” exception. |
| Shop | Payout: +1 coin per Home building within 2 tiles, max 3. | Includes Cottage and Housing Authority consistently. Remove Farm bonus. |
| Farm | Payout: +1 coin if another Farm or Orchard is adjacent. | One condition; each owner calculates their own income. |
| Orchard | Farm income above, plus park size 1. | Explicit dual role. |
| Cafe | Keep +1 per adjacent Home building or Shop, max 3; +2 VP beside Park/Lake at end. | Calculate directly, without creating a link object. |
| Granary | Payout: +1 coin per Farm or Orchard you own, max 3. | Supports farming without tracking unsupplied Shops. |
| Rush Hour | Payout this round: +1 coin per Shop you own, max 4. | Simple one-round Trade event. |
| Central Station | Payout: +1 coin per Shop or Market you own, max 4. | Clear Trade engine; no received-link total. |

Also remove the **Best Connected** 6-VP competition. Do not automatically replace it: roads, parks, homes, public goals and contracts already supply several scoring layers. Replace **Entrepreneur** with “Own 3 Shops. Each must have at least 2 Home buildings within 2 tiles.” Award 6 VP for the prototype; no capacities and the same Home can qualify for multiple Shops.

“Connected” may still describe a physical road chain or contiguous park. Those are visible board shapes. The removed mechanic is economic linking. UI should say “adjacent”, “within 2 tiles”, “road chain” or “park region” as appropriate.

## 4. Coin economy prototype

Test the following as a coherent **unvalidated prototype A** after implementing the independent income above. Keep prototype A separate from the current-rule experiments.

| Parameter | Current | Prototype A | Rationale |
|---|---|---|---|
| Starting coins | 8 | 8 | Preserve a useful opening budget. |
| Base income | 3 | 2 | Some reduction, retaining a recovery floor. The income-1 experiment alone did not solve accumulation. |
| Catch-up | +1 when last in VP, rounds 2–8 | Keep | Cheap recovery; no new rule. |
| Market prices C/U/R/L | 1/2/3/5 | 1/3/5/8 | More deliberate premium purchases; insufficient on its own in tests. |
| Market cap | 3 purchases per round | Keep | Do not add another purchasing phase. |
| Build cost | Free | Common 0, Uncommon 0, Rare 2, Legendary 4 coins | Recurring spending choice for powerful builds; one visible rarity rule. Roads/events cost 0 to play. |
| Free round cards | 1 random Common | Keep | Preserve a free action when money runs out. Guarantee one Street option in every market, as current. |
| Spare coins | 1 VP / 5 | Keep | Avoid strengthening hoarding while testing spending. |
| Park scoring | Repeated region awards, including above cap | New rule below | Reduce cheap repeatable VP and shared award inflation. |

Build cost is the one added rule proposed in exchange for removing the much larger linking system. Show it beside purchase price, including total commitment; it is paid only on successful placement. A renovation pays the replacement's build cost too. A failed placement costs nothing. Bought cards can remain in hand until affordable. Bots, previews, network validation, checkpoint tests and tooltips must all account for it.

Before purchase, show e.g. **“5 to buy · 2 to build”** on a Rare building. These prices are not its tested fair value. Factory and Airport remain income engines but now must earn back acquisition and build cost. Common housing, farming and parks still provide accessible play.

If build costs make the game feel fussy, test **prototype B**: free placement, prices 1/4/7/11, base income 2, same independent income and new park scoring. Compare A and B; do not combine the two price schedules. Prefer B if it produces comparable purchase decisions with less friction. Do not add upkeep, debt, mandatory taxes, a shop refresh currency or a second currency during this pass.

### Park rule for prototype A and B

On placement or Sapling growth: let `S_before` be the combined size of the distinct park regions being joined, before adding the new tile or growth size. Let `S_after` be the resulting size. Award **only the placer/growing tile's owner** `ceil(min(S_after,10)/2) - ceil(min(S_before,10)/2)` VP. No shared placement awards. A new isolated size-1 park gives 1 VP; extending 1→2 gives 0; 2→3 gives 1; 9→10 gives 0; 10→11 gives 0. Fountain and growth contribute their actual size increase. Joining regions sums their sizes once each, avoiding a bridge windfall. Awards are never negative.

This caps a continuously extended region's placement/growth score at 5 total VP and removes the repeatable 5-VP addition. Separate regions can each score, but merging them never recovers earlier awards or creates an extra payout. Retain the existing 6-VP Biggest Park Owner competition, measured by physical owned tiles in one region. Park size and physical tile count must be explained separately. This is a substantial nerf; monitor whether parks remain desirable before buffing other VP cards around it.

## 5. Description and tag standard

Use these terms everywhere:

- **Home building** means any card with the Home tag: House, Cottage, Apartment, Housing Authority. Use **House card** for that specific card. Unify general home bonuses and penalties around the tag in the prototype. This deliberately changes several current effects; it is not a text-only fix.
- **Farm or Orchard** names those two cards. They retain Nature only; CardType Industry does not give an Industry tag. Adding Industry would change Industrial Estate, Eco Council, Senate and contracts simultaneously.
- **Building** means any board card except a road or event. A park tile is a building. Road requirements accept any owner's adjacent road, including neutral roads.
- **Adjacent** means the four shared-edge neighbors. **Within N tiles** includes diagonals (Chebyshev distance). Ownership and caps must be explicit. Generic tag counts include the source if it has that tag, unless text says “other”.
- Print timing consistently: **Place**, **Payout**, **Round start**, **When you build**, **End**. Put road requirement and per-player limit on separate requirement lines. No vague “a Farm that also…” or unexplained “No Links”.
- Use a shared **Park** keyword/help panel for the new scoring formula. Park cards state their size and that they use Park scoring. Avoid repeating the full formula on five card faces.

Keep the six existing tags for this prototype. Counts in the current 48-card catalog: Home 4, Trade 9, Nature 10, Industry 7, Culture 8, Civic 19. Civic is broad; monitor Town Hall and Senate rather than automatically giving every Civic-type card the Civic tag. Bank is Trade only; Warehouse, Yard and Depot are Industry only; Tax Office and Granary are Civic only. Those are current design choices, not missing display chips.

Specific accuracy fixes are documented per card. Most urgent: Apartment omits its own income; Cottage/Lake can generate two distinct coins for the Cottage owner; Cafe's park bonus includes Lake; School and Tax Office currently use residential homes while Housing Authority is a nonresidential Home-tagged building; Architect's Studio can trigger itself; Builders' Yard offers a choice of Street versus a random Common, not a choice of any Common card; Hospital resolves one random residential emergency per round with placement-order tie breaking.

## 6. Balance decisions across the remaining set

The companion catalog makes an explicit decision for every card. Keep most individual numbers initially; resolve structural problems before tuning 48 cards at once.

- **Roads:** keep Street VP and road competition; watch Street after parks are reduced. Avenue and Roundabout remain distinctive. Document that only Street gives the recurring adjacent-building VP.
- **Nature:** keep Fountain size 2, Sapling size 1→3 after two payouts, Orchard dual role and Lake's direct neighboring-home income. Park and Wild Garden are mechanically identical today. Keep their art variants in this pass; revisit duplicate naming after balance. Do not advertise Wild Garden as having a unique bridging power.
- **Economy:** Bank's unconditional +2 and limit 3 is the reference point. Vineyard costs more and skips two payouts; Startup ramps. Test early and late placements directly before buffing them. Market, Farm and Granary become legible dependencies. Keep Factory 2 and Airport 3 initially; their noise/pollution penalizes any owner's matching neighbors, including the builder's.
- **Tag engines:** Town Hall, Tourist Office, Industrial Estate, Museum, Senate and the other tag engines keep their thresholds/caps. Preserve self-counting where applicable. Housing Authority, Museum and Eco Council need targeted tests because a full-game bot's purchase rate does not establish their value.
- **Triggers/builders:** keep per-round limits and free-card engines initially. A free card is valuable when purchases cost more; Warehouse and Yard can become stronger without a numeric buff. Architect's Studio self-trigger should be stated explicitly. Do not silently change it while rewriting copy.
- **Events:** Grant at market price 1 immediately becomes 4 coins, a guaranteed +3 nominal coins at the cost of a purchase opportunity. Keep it temporarily as a simple cash injection and measure its pick rate under scarce money. Ribbon Cutting pays at most 4 against prototype price 3, so it needs a focused test; if persistently undesirable, move it to Common with weight 1 rather than increasing its payout and adding more money. Land Claim expires at next round start and cannot stop building elsewhere; preserve its limited tactical role.
- **Landmarks:** keep Monument 6 and Senate's six-tag cap 12. Central Station's replacement is a proposed economy card, not automatically worth its Legendary price. Evaluate it against a 6-VP Monument at different rounds. Skyscraper's minimum-neighbor placement requirement must appear before purchase; zero plays despite four purchases in the baseline indicates a usability/AI coverage problem to investigate.

## 7. Goals, contracts and secondary scoring

| Rule | Prototype decision |
|---|---|
| Longest Road | Keep 6 VP, Avenue weight 2, neutral roads excluded. |
| Biggest Park Owner | Keep 6 VP; most physical tiles you own in one park, not total park size. |
| Best Connected | Remove with linking. |
| Full House | Keep 4 VP; count Home-tagged buildings consistently. |
| Entrepreneur | Replace with the three-Shop / two-nearby-Homes condition above; 6 VP. |
| Hoarder | Keep 15 coins / 5 VP for initial scarce-economy test. Track if it becomes automatic or discourages building. |
| Rainy Day | Keep 12 coins at Build end. Its timing is before payout. |
| Other public goals | Keep thresholds initially; connected Farms means physical adjacency, not economic links. |
| Other contracts | Keep thresholds and awards; standardize Home/Nature/Industry terminology. |

Remove link mentions from help, card interactions, final-score panels, Best Connected progress, contract progress and the GDD. Update bot evaluation, payout visualization, analytics version, checkpoints and multiplayer replication when the implementation changes. Cosmetic traffic may remain; it must not imply a pairing/capacity mechanic or determine income.

## 8. Implementation and evidence plan

1. Reconcile the GDD with current free cards and market eligibility. Implement the description glossary and independent-income rules, remove Best Connected and replace Entrepreneur. Keep a saved current baseline.
2. Implement new park scoring and add focused examples for isolated parks, joins, growth, sizes 9/10/11 and multi-owner regions. Verify no shared placement payout remains.
3. Implement prototype A's prices, base income and build costs behind explicit research settings. Add transaction checks: rejected placements cost zero, successful placements charge once, renovation charges once, insufficient funds leave the hand/board intact, multiplayer replay cannot charge twice. Bot lookahead must include total cost.
4. Run identical paired seeds for current, independent-income-only, park-change-only, combined income+park, A and B. Start with 32 each; increase the shortlisted comparison to **at least 100 distinct board seeds with four rotations**. Preserve card and policy fingerprints. Paired mean differences and uncertainty should be calculated over board-seed blocks, not player seats. Run additional two- and six-player samples because shared-board effects scale with player count.
5. Force every card into early, middle and late scenarios, particularly the ten never played here. Compare coin generated, personal VP, opponent VP, total acquisition cost and buildability. Do not rank cards from aggregate source VP: that can include rivals' awards and omit downstream synergies.
6. Human playtest the shortlisted variant. Ask players to identify one purchase they saved for, one they passed for cost, and whether they could predict payouts without explanation. Record offer/purchase/play/discard events and exact settings using the existing opt-in analytics.

Provisional acceptance targets, chosen for the next test rather than inferred from the current sample:

| Measure | Target / reason to revisit |
|---|---|
| Early agency | A player can buy an affordable Common and play their free card after a poor round. |
| Purchase pressure | In rounds 2–6, about 15–35% of eligible market views have an unaffordable legal option; also inspect whether it was wanted. |
| Saved money | Median final coins roughly 5–20; inspect final-payout income separately. Persistent 50+ balances need explanation. |
| Coin VP | Typically under 20% of final VP; buying useful buildings remains competitive with saving. |
| Card comprehension | Players can predict House/Shop/Farm income from the board without explaining allocation order. |
| Card coverage | Every card receives targeted scenario coverage; no buff justified solely by zero bot purchases. |
| Strategy spread | Investigate a strategy above 40% or below 10% over the larger four-strategy sample, with paired uncertainty and human results. Do not tune directly to equal bot win rates. |
| Integrity | No rejected bot actions, negative coins, double charges or ledger mismatches. |

## 9. Reproduce

From `city_sim`:

```powershell
dotnet run --project docs/balance/lab/BalanceLab.csproj -- docs/balance/2026-10-30-economy-seed-30102026.json 32 30102026
python docs/balance/lab/summarize.py docs/balance/2026-10-30-economy-seed-30102026.json
dotnet run --project tests/CitySim.Rules.Tests.csproj
```

The research project compiles existing Cards/Match/Ai source directly. It does not patch sources, change live defaults, start the editor or simulate rendering performance. Card totals describe observed actions; offered counts are per eligible player per selection, received includes free cards, and placement awards include all recipients. Full match outcomes retain seat assignments, bot seeds, scores, score components, purchases, coin curves and final boards by card ID.
