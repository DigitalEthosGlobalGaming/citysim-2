"""Generate reviewable card inventory and descriptive research tables. Standard library only."""
import csv
import json
import statistics as stats
import sys
from pathlib import Path

path = Path(sys.argv[1]).resolve()
data = json.loads(path.read_text(encoding="utf-8"))
docs = path.parent.parent

# Proposed copy for the prototype in BALANCE_REVIEW_2026-10-30.md, not live card text.
# Values are deliberately explicit so reviewers can distinguish design changes from copy fixes.
proposals = {
    "street": ("Payout: +1 VP per building adjacent to your Streets. Count each building once.", "Keep. Any owner's building counts; only this road type pays recurring VP."),
    "house": ("Place: +1 VP per adjacent Home building. Payout: +1 coin if a Shop is within 2 tiles.", "Replace linking with one independent condition; generalize Home adjacency to the tag."),
    "cottage": ("Place: +1 VP per adjacent park tile or Lake. Payout: +1 coin if adjacent to a park tile or Lake.", "Keep. A Lake's own payout can give this Cottage another coin. Remove No Links."),
    "shop": ("Payout: +1 coin per Home building within 2 tiles, max 3.", "Independent count; remove shared capacity, both-owner payment and Farm supply bonus."),
    "farm": ("Payout: +1 coin if adjacent to another Farm or Orchard.", "Replace Farm-Shop and Farm-Farm linking with one condition. Nature tag stays."),
    "park": ("Park size 1. Place: score the park's increase.", "Use new shared Park help rule; owner-only marginal scoring capped at size 10."),
    "wild-garden": ("Park size 1. Place: score the park's increase.", "Same mechanics as Park. Keep as art variant for now; it has no exclusive bridging power."),
    "grant": ("Play: gain 4 coins, then discard this card.", "Keep initially. Costs a market opportunity; Common purchase gives a nominal 3-coin gain."),
    "avenue": ("Counts as 2 tiles for Longest Road. Payout: +1 coin if adjacent to at least 2 buildings.", "Keep. Does not receive Street's recurring VP rule."),
    "roundabout": ("When anyone builds beside this: gain 1 VP.", "Keep. Buildings only; roads and events do not trigger it. No retroactive award."),
    "orchard": ("Park size 1. Place: score the park's increase. Payout: +1 coin if adjacent to a Farm or another Orchard.", "Explicit independent Farm role plus new Park rule. Retain Nature only."),
    "lake": ("Payout: each adjacent Home building earns its owner 1 coin. Counts as a park neighbor, but is not a park tile.", "Keep payout; standardize to Home tag. Never joins a park region."),
    "vineyard": ("Payout: nothing for its first 2 payouts; then +3 coins each payout.", "Keep timing/amount initially; force early/late tests. Zero plays in baseline."),
    "startup": ("Payout: +1 coin the first time, +2 the second, then +3 each time.", "Keep ramp; force timing tests. Zero plays is not proof of weakness."),
    "market": ("Payout: +1 coin per Shop you own within 2 tiles, max 4.", "Keep. Shop card only, not every Trade building. Force engine scenario."),
    "contractor": ("When you build a House card or Shop: gain 1 coin, up to 2 coins per round.", "Keep the specific-card restriction; Apartment and Cottage do not trigger it."),
    "apartment": ("Place: +2 VP per adjacent Home building. Payout: +2 coins if a Shop is within 2 tiles.", "State its own income. Remove counts-as-two and capacity allocation."),
    "cafe": ("Payout: +1 coin per adjacent Home building or Shop, max 3. End: +2 VP if adjacent to a park tile or Lake.", "Direct count, no link object. Preserve Lake bonus; standardize Home tag."),
    "fountain": ("Park size 2. Place: score the park's increase.", "Keep size, replace repeated region scoring."),
    "factory": ("Payout: +2 coins. End: each adjacent Home building or Cafe loses its owner 1 VP, including yours.", "Keep amount and stacking pollution; standardize Home tag. Test with total build cost."),
    "sapling": ("Park size 1. Place: score the park's increase. After its second payout, grows to size 3 and scores the increase again.", "Use marginal size increase; Forest is a mature state, not another draw."),
    "bank": ("Payout: +2 coins.", "Keep as reference income card; separate requirement line says limit 3."),
    "town-hall": ("Payout: +2 coins if you own at least 3 Civic buildings, including this.", "Keep threshold. Civic means tag, not card type."),
    "museum": ("End: +1 VP per other Culture building you own, max 8.", "Keep. Excludes itself; force Culture scenario."),
    "chamber-of-commerce": ("Payout: +1 coin per 2 Trade buildings you own, max 4 coins. Includes this.", "Keep threshold/cap; zero plays requires targeted coverage."),
    "warehouse": ("Round start: gain a Street if your hand has room. When you build your first Street each round: gain 1 coin.", "Keep. Default arrives next round; generated Streets may be especially strong when money is scarce."),
    "builders-yard": ("Round start: gain a Street or a random Common, if your hand has room. Choose which before the round starts.", "Keep. Choice is Street versus random Common, not selecting a specific Common. Defaults to Street."),
    "hospital": ("Each round: one random Home building has an emergency. The nearest Hospital within 4 tiles earns its owner 1 VP. Ties go to the earlier Hospital.", "Standardize emergency eligibility to Home tag; no emergency when none exist. Keep award/range; force coverage."),
    "tourist-office": ("Payout: +1 coin per Culture building you own, max 3. Includes this.", "Keep. Self-count means at least 1 while owned."),
    "tax-office": ("Payout: +1 coin per 4 Home buildings you own.", "Unify residential-only predicate to Home tag. No cap currently; monitor large cities."),
    "depot": ("Payout: +1 coin per 5 road tiles you own, max 4 coins.", "Keep physical-card count. Avenue counts once here, twice only for Longest Road."),
    "school": ("Payout: +1 coin per Home building you own within 2 tiles, max 4.", "Standardize residential-only predicate to Home tag. School is not Home and cannot count itself."),
    "granary": ("Payout: +1 coin per Farm or Orchard you own, max 3.", "Replace unsupplied-Shop calculation; no supply status remains."),
    "stadium": ("Payout: each adjacent Home building earns its owner 1 coin; each adjacent Cafe loses its owner 1 coin. End: +1 VP per adjacent Home building.", "Keep amounts, any owner; standardize Home tag. No debt; payout deductions can use saved coins."),
    "airport": ("Payout: +3 coins. End: each Home building within 2 tiles loses its owner 1 VP. Maximum loss: 3 VP per player across all Airports.", "Keep combined penalty cap, standardize Home tag. Test income after total cost."),
    "skyscraper": ("Requires at least 2 adjacent buildings. End: +1 VP per other building within 2 tiles, max 6.", "Keep. Any owner qualifies. Show buildability before purchase; none played in baseline."),
    "industrial-estate": ("Payout: +1 coin per Industry building you own, max 5. Includes this.", "Keep Industry TAG count. Farms and Orchards do not count."),
    "housing-authority": ("End: +1 VP per 3 Home buildings you own, max 6. Includes this.", "Keep thresholds; it also counts for the prototype's general Home interactions."),
    "eco-council": ("End: +1 VP per 2 Nature buildings you own, max 8; then subtract 2 VP per Industry building you own, minimum 0. Includes this in Nature.", "Keep cap-before-penalty order; Industry tag only. Do not give Farm an Industry tag."),
    "gardeners-guild": ("When you build a park tile: gain 1 coin.", "Keep placement-only trigger; no coin for Sapling growth. Test after park nerf."),
    "architects-studio": ("When you build your first building each round beside one of your buildings: gain 2 coins. This Studio can trigger its own effect.", "Keep self-trigger. If first building fails the condition, a later building does not qualify."),
    "renovation": ("Play: replace one of your buildings with a different building card from your hand. Discard both cards. Pay the new building's build cost.", "No roads/events as replacement or target. Keep earned VP; new age and placement effects apply. Build charge is new."),
    "ribbon-cutting": ("Play: gain 1 coin per building you built this round, max 4, then discard this card.", "Keep first trial; at new Uncommon price 3 only net +1 at cap. Consider Common weight 1 if focused tests confirm weakness."),
    "rush-hour": ("Play: this payout, gain 1 extra coin per Shop you own, max 4. Discard this card. Once per round.", "Replace Link bonus. One-round nonstacking effect, not permanent income."),
    "land-claim": ("Play: reserve an empty tile until next round starts. Only you may build there. Discard this card.", "Keep tactical role; costs a purchase opportunity and does not stop rivals building elsewhere."),
    "central-station": ("Payout: +1 coin per Shop or Market you own, max 4.", "Replace received Links. Does not count itself. Validate Legendary total price against other engines."),
    "senate": ("End: +2 VP for each tag you have on at least 3 buildings, max 12. Includes this.", "Keep six-tag scoring; buildings can qualify for multiple tags."),
    "monument": ("End: +6 VP.", "Keep flat VP. No income; compare all-in Legendary price against Station and Senate."),
}
assert set(proposals) == {c["Id"] for c in data["Catalog"]}, "Every card needs an explicit review."

prices = {r["Rarity"]: r for r in data["Runs"][0]["Settings"]["Rarities"]}
prototype_prices = {"Common": 1, "Uncommon": 3, "Rare": 5, "Legendary": 8}
build_prices = {"Common": 0, "Uncommon": 0, "Rare": 2, "Legendary": 4}
rows = []
for c in data["Catalog"]:
    text, decision = proposals[c["Id"]]
    rows.append(dict(Id=c["Id"],Name=c["Name"],Type=c["Type"],Tags=c["Tags"],Rarity=c["Rarity"],
        CurrentPrice=prices[c["Rarity"]]["MinimumBid"],UnlockRound=prices[c["Rarity"]]["UnlockRound"],
        NeedsRoad=c["NeedsRoad"],Limit=c["OwnershipLimit"],DrawWeight=c["Weight"],ParkSize=c["ParkSize"],
        EffectTray=c["EffectTray"],CurrentDescription=c["Rules"],ProposedDescription=text,
        PrototypePurchasePrice=prototype_prices[c["Rarity"]],
        PrototypeBuildPrice=0 if c["Type"] in ("Road", "Event") else build_prices[c["Rarity"]],Decision=decision))
with (path.parent/"2026-10-30-card-review.csv").open("w",encoding="utf-8-sig",newline="") as f:
    w=csv.DictWriter(f,fieldnames=list(rows[0])); w.writeheader(); w.writerows(rows)

def esc(s):
    return str(s).replace("|", " / ").replace("\n", " ")

lines = ["# All 48 cards — descriptions, tags and balance review", "",
    "Snapshot: 30 October 2026. [Balance proposal and evidence](BALANCE_REVIEW_2026-10-30.md). [Sortable CSV](balance/2026-10-30-card-review.csv).", "",
    "Current descriptions below are copied verbatim from the implemented catalog. Proposed copy is for the unimplemented prototype; it deliberately changes linking, Home eligibility and Park scoring. Do not paste it into the live catalog before implementing those rules. All existing tags, draw weights, rarities, road requirements and ownership limits are retained in the first prototype.", "",
    "## Current catalog and metadata", "",
    "Price is the live fixed purchase price. Unlock is first market round; round defaults can give Commons for free. Weight is relative within a rarity, not an overall probability. Limit 0 means unlimited per player; purchase eligibility counts owned and held copies. Road means an adjacent road of any owner. Park size is base size, before growth. Forest is the mature Sapling state, not a 49th card.", "",
    "| Card | Type | Tags | Rarity | Price | Unlock | Road | Limit | Weight | Park size | Tray |",
    "|---|---|---|---|---:|---:|---|---:|---:|---:|---|"]
for r in rows:
    lines.append("| "+" | ".join(esc(r[k]) for k in ["Name","Type","Tags","Rarity","CurrentPrice","UnlockRound","NeedsRoad","Limit","DrawWeight","ParkSize","EffectTray"])+" |")
lines += ["", "## Exact current card descriptions", "", "| Card | Current text |", "|---|---|"]
for r in rows: lines.append(f"| {esc(r['Name'])} | {esc(r['CurrentDescription'])} |")
lines += ["", "## Proposed descriptions and individual decisions", "",
    "Read Home/Trade/Nature/Industry/Culture/Civic as tags; House card and Shop name specific cards. Adjacent means a shared edge; within N tiles includes diagonals. Any owner's qualifying neighbor counts unless the rule says your. Buildings exclude roads/events. General tag counts include their source unless they say other. Payout is each round; End is game end. Place and Play consume a card from hand. Park increase uses the new formula in the balance proposal. Road/ownership restrictions remain separate requirements, rather than being repeated in every sentence.", "",
    "Prototype A prices below list buy + build. They are experimental, not measured fair prices. Free Common defaults pay no purchase cost. Events and roads have no play/build cost.", "",
    "| Card | Buy + build | Proposed copy | Decision / watchpoint |", "|---|---:|---|---|"]
for r in rows:
    lines.append(f"| {esc(r['Name'])} | {r['PrototypePurchasePrice']} + {r['PrototypeBuildPrice']} | {esc(r['ProposedDescription'])} | {esc(r['Decision'])} |")
lines += ["", "## Current text accuracy issues", "",
    "- House placement counts residential Home cards (including Cottage); its income only uses House and Apartment. Shop income ignores Cottage and Housing Authority. Apartment can earn up to 4 linked coins, but its current text never states its own income.",
    "- Cottage beside Lake can receive its own 1 coin plus the Lake's 1 coin. Lake pays residential Homes, not every Home-tagged building. Cafe's end park bonus includes Lake, and its income includes Cottage. House/Home is currently inconsistent across cards.",
    "- Farm and Orchard currently share the Farm category; Farm-Farm adjacency pays per adjacent Farm pair, not once if any are adjacent. Prototype Farm income is deliberately lower and simpler.",
    "- Park size is capped at 10 for computing the award, but additions beyond 10 still award full VP. Shared awards go to each represented owner. Wild Garden is exactly a Park with a different draw weight/name. Sapling's mature size is 3; its ID stays sapling.",
    "- Bank has no road requirement and three copies are allowed. Many Civic-TYPE cards have no Civic TAG; this is implemented gameplay, not a presentation omission.",
    "- Builders' Yard chooses Street versus a random Common. Hospital uses one random residential emergency per round, nearest eligible Hospital, and earlier placement for ties. No homes means no emergency.",
    "- Tax Office/School, Stadium, Factory and Airport currently use residential Homes; Apartment placement and Housing Authority scoring use Home tags. The proposal intentionally unifies these to tags. Hospital and the Home-based goals/contracts must follow the same glossary.",
    "- Contractor triggers only House card or Shop, not all Home/Trade buildings. Architect's Studio can trigger itself if it is your first building and beside an owned building. Placing an ineligible first building consumes that round's opportunity.",
    "- Depot counts physical road cards; Avenue's weight 2 only applies to Longest Road. Roundabout and Avenue do not inherit Street's recurring VP.",
    "- Eco Council caps Nature VP before subtracting Industry penalties. Airports share one 3-VP loss cap per player across all Airports; Factory penalties have no shared cap. Cafe deductions can use saved coins but cannot create debt.",
    "- Renovation consumes both cards, retains previously earned VP, resets age and applies new placement effects. Land Claim expires at the next round START. Rush Hour currently adds coins per received link unit and does not stack; proposed version is different.", "",
    "## Baseline card observations", "",
    "32 current-rule games, 128 player outcomes. Offered counts are per eligible player and exclude ownership-ineligible copies. Received includes free cards. Played includes events and replacements. Purchases are observations of bot preferences, not card-strength rankings. Aggregate placement VP includes other players' shared awards; payout coin totals can include amounts given to neighbors. End VP can include penalties to neighbors. Growth awards and indirect synergies are not all attributed here.", "",
    "| Card | Offered | Bought | Received | Played | Discarded | Placement VP, all recipients | Payout coins, all recipients | End VP, all recipients |", "|---|---:|---:|---:|---:|---:|---:|---:|---:|"]
current=data["Runs"][0]
for c in data["Catalog"]:
    v=current["Cards"][c["Id"]]
    lines.append("| "+esc(c["Name"])+" | "+" | ".join(str(v[k]) for k in ["Offered","Purchased","Received","Played","Discarded","PlacementVp","PayoutCoins","EndVp"])+" |")
(docs/"CARD_REVIEW_2026-10-30.md").write_text("\n".join(lines)+"\n",encoding="utf-8")

out=["# Economy experiments — 30 October 2026", "", f"Seed {data['Seed']}; {data['MatchesPerVariant']} matches per variant, {data['DistinctBoardSeeds']} distinct board seeds with four seat rotations each. Current rules remain linked in every variant. Descriptive results only; see the main review for limitations.", "", "| Variant | Mean VP | Mean final coins | Mean spent | Mean purchases | Unaffordable market views | Mean coin VP |", "|---|---:|---:|---:|---:|---:|---:|"]
for run in data["Runs"]:
    seats=[s for m in run["Matches"] for s in m["Seats"]]
    assert all(m["InvalidActions"]==m["BudgetViolations"]==0 for m in run["Matches"])
    assert abs(sum(s["WinShare"] for s in seats)-len(run["Matches"]))<1e-8
    mean=lambda key: stats.mean(s[key] for s in seats)
    coin_vp=stats.mean(sum(l["Vp"] for l in s["ScoreLines"] if l["Reason"]=="Spare coins") for s in seats)
    blocked=100*sum(s["MarketsWithUnaffordableOption"] for s in seats)/sum(s["EligibleMarkets"] for s in seats)
    out.append(f"| {run['Name']} | {mean('TotalVp'):.2f} | {mean('FinalCoins'):.2f} | {mean('Spent'):.2f} | {mean('Purchases'):.2f} | {blocked:.2f}% | {coin_vp:.2f} |")
out += ["", "## Strategy results", "", "| Variant | Strategy | Games | Win share | Mean VP | Mean final coins |", "|---|---|---:|---:|---:|---:|"]
for run in data["Runs"]:
    seats=[s for m in run["Matches"] for s in m["Seats"]]
    for strategy in ("Balanced","Tag","ParkRush","EconomyFirst"):
        ss=[s for s in seats if s["Strategy"]==strategy]
        out.append(f"| {run['Name']} | {strategy} | {len(ss)} | {100*stats.mean(s['WinShare'] for s in ss):.2f}% | {stats.mean(s['TotalVp'] for s in ss):.2f} | {stats.mean(s['FinalCoins'] for s in ss):.2f} |")
out += ["", "## Mean coin balance after each payout", "", "| Variant | R1 | R2 | R3 | R4 | R5 | R6 | R7 | R8 |", "|---|---:|---:|---:|---:|---:|---:|---:|---:|"]
for run in data["Runs"]:
    seats=[s for m in run["Matches"] for s in m["Seats"]]
    out.append("| "+run["Name"]+" | "+" | ".join(f"{stats.mean(s['RoundCoins'][i] for s in seats):.2f}" for i in range(8))+" |")
out += ["", "Each variant passed match-completion, payout-coverage, action-validity, nonnegative-budget and coin-ledger checks. Win shares sum to one per match. Offered and affordability figures are sampled before purchase, once per eligible player per selection. The raw JSON records source hashes and full outcomes. Re-run the lab against the same sources to reproduce; this summary's card proposals are editorial, not simulation output."]
(path.parent/"2026-10-30-experiment-summary.md").write_text("\n".join(out)+"\n",encoding="utf-8")
print("Wrote full card review, CSV and experiment summary.")
