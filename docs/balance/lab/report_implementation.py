"""Export the implemented catalog and a comparison of the recorded balance runs."""
import csv
import json
import statistics
from pathlib import Path

folder = Path(__file__).resolve().parent.parent
docs = folder.parent
load = lambda name: json.loads((folder / name).read_text(encoding="utf-8"))
historical = load("2026-10-30-economy-seed-30102026.json")["Runs"][0]
a = load("2026-10-30-prototype-a-seed-30102026.json")["Runs"][0]
comparisons = load("2026-10-30-pricing-comparison-seed-30102026.json")["Runs"]
final_data = load("2026-10-30-final-seed-30102026.json")
final = final_data["Runs"][0]

def metrics(run):
    seats = [s for m in run["Matches"] for s in m["Seats"]]
    assert all(m["InvalidActions"] == m["BudgetViolations"] == 0 for m in run["Matches"])
    assert all(abs(sum(s["WinShare"] for s in m["Seats"])-1) < 1e-8 for m in run["Matches"])
    mean = lambda k: statistics.mean(s[k] for s in seats)
    return seats, dict(vp=mean("TotalVp"), coins=mean("FinalCoins"), median=statistics.median(s["FinalCoins"] for s in seats),
        spent=mean("Spent"), builds=statistics.mean(s.get("BuildSpent", 0) for s in seats), buys=mean("Purchases"),
        blocked=100*sum(s["MarketsWithUnaffordableOption"] for s in seats)/sum(s["EligibleMarkets"] for s in seats),
        coin_vp=statistics.mean(sum(l["Vp"] for l in s["ScoreLines"] if l["Reason"]=="Spare coins") for s in seats))

catalog = final_data["Catalog"]
prices = {r["Rarity"]: r["MinimumBid"] for r in final["Settings"]["Rarities"]}
assert len(catalog) == len({c["Id"] for c in catalog}) == 48
rows = [dict(Id=c["Id"], Name=c["Name"], Type=c["Type"], Tags=c["Tags"], Rarity=c["Rarity"],
    Price=prices[c["Rarity"]], NeedsRoad=c["NeedsRoad"], OwnershipLimit=c["OwnershipLimit"],
    Weight=c["Weight"], ParkSize=c["ParkSize"], Description=c["Rules"], Effects=json.dumps(c["Effects"])) for c in catalog]
with (folder/"2026-10-30-current-cards.csv").open("w",encoding="utf-8-sig",newline="") as f:
    w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
esc=lambda s:str(s).replace("|","/").replace("\n"," ")
lines=["# Current card catalog — 30 October 2026", "", "Implemented v0.10 rules. [Implementation and simulation results](BALANCE_IMPLEMENTATION_2026-10-30.md). [Sortable CSV](balance/2026-10-30-current-cards.csv).", "",
    "All 48 cards. Market prices are paid on purchase; placement is free. Limit 0 means unlimited. Road requirements accept any adjacent road, including a neutral road. Tags are gameplay categories, not card types. Home includes House, Cottage, Apartment and Housing Authority. Adjacent means shared edges; ranges include diagonals. Forest is a mature Sapling, not an additional draw. Weight is relative within rarity. There are 10 Common cards; Street is excluded from free Common draws and guaranteed as a market option.", "",
    "Park scoring: only the builder or growing Sapling's owner gains the difference between rounded-up half sizes before and after the addition, with size capped at 10. Joined regions count once; other owners gain no park-placement award. Physical tiles still determine Biggest Park Owner.", "",
    "| Card | Type | Tags | Rarity | Price | Road | Limit | Weight | Park size |", "|---|---|---|---|---:|---|---:|---:|---:|"]
for r in rows:
    lines.append("| "+" | ".join(esc(r[k]) for k in ("Name","Type","Tags","Rarity","Price","NeedsRoad","OwnershipLimit","Weight","ParkSize"))+" |")
lines += ["", "| Card | Description |", "|---|---|"]
lines += [f"| {esc(r['Name'])} | {esc(r['Description'])} |" for r in rows]
(docs/"CARD_CATALOG.md").write_text("\n".join(lines)+"\n",encoding="utf-8")

seats,m=metrics(final)
checks=(folder/"2026-10-30-final-rule-checks.txt").read_text(encoding="utf-8-sig")
count=sum(line.startswith("PASS ") for line in checks.splitlines())
assert "M8 simulation checks passed." in checks
out=["# Balance implementation — 30 October 2026", "", "Implemented the independent-income and park redesign, with **prototype B's simpler market economy** selected after comparison. All placements remain free. [Current cards](CARD_CATALOG.md) supersedes the historical review's pre-change catalog; [GDD v0.10](GDD.md) describes the active rules.", "",
    "## What changed", "",
    "- Removed economic linking, shared capacities, order-dependent allocation, paid-link counters and the Best Connected competition. The three remaining city competitions are Longest Road, Biggest Park Owner and Full House.",
    "- House and Apartment receive a fixed coin reward if a Shop is nearby. Shops independently count Home-tagged buildings. Farms and Orchards check adjacent farms. Cafe, Granary, Rush Hour and Central Station use direct counts. Entrepreneur now checks three Shops with two nearby Homes each and awards 6 VP.",
    "- Home consistently means its tag, including Housing Authority, in income, placement scoring, penalties, goals, contracts and emergencies. The six tags otherwise stay unchanged.",
    "- Park additions and growth award only the owner for their capped size increase. No shared placement awards and no repeatable size-10 payout. Fountain size, joins and Sapling growth use the same calculation.",
    "- Base income is 2. Common/Uncommon/Rare/Legendary purchase prices are 1/4/7/11. Starting money remains 8; free defaults remain one random Common per round; three market selections and the five-card hand limit remain. Spare coins still convert at 5:1.",
    "- Ribbon Cutting and Rush Hour moved to Common with weight 1 each. At the chosen Uncommon price of 4, their capped reward of 4 could never generate net profit from buying them. They were not played in the initial prototype samples. At price 1 they retain a useful conditional cash role without increasing their payout caps. Grant remains Common weight 2.",
    "- Rewrote all 48 card descriptions; updated interaction highlights, help, costs, onboarding, payout presentation and the end-score reveal. Removed stale pairing visuals from cosmetic traffic. Updated bot valuation to consider acquisition costs and affordability.",
    "- Optional Rare/Legendary build-cost settings retain prototype A for research, defaulting to zero. Transaction checks cover insufficient funds, failed placements, renovation, duplicate commands, stale hands and migration. They are not an extra rule in the standard game.", "",
    "## Why prototype B", "",
    "Prototype A added a second cost at placement but only modestly constrained purchases. The documented B alternative made more market choices budget-sensitive with fewer rules. A stronger price schedule was also screened; it created more pressure but worsened the tested strategy spread. The selected economy keeps Common buildings accessible rather than balancing solely by forcing players to pass.", "",
    "Each row below is 32 four-player games, 128 player outcomes, eight board seeds with four seat rotations. Shared seed 30102026; Normal Balanced/Industry-tag/Park Rush/Economy First bots; 15×15 board. Source fingerprints and individual outcomes are recorded in each JSON. The final row includes the two low-weight event rarity changes; the earlier B row does not.", "",
    "| Configuration | Mean VP | Mean final coins | Median coins | Mean market spend | Mean build spend | Purchases / 24 | Unaffordable market views |", "|---|---:|---:|---:|---:|---:|---:|---:|"]
runs=[("Before changes",historical),("A: 1/3/5/8 + build fees",a),("B: 1/4/7/11, free placement",comparisons[0]),("Screened: 2/5/9/14, free placement",comparisons[1]),("Final: B + event rarity fixes",final)]
for label,r in runs:
    _,v=metrics(r)
    out.append(f"| {label} | {v['vp']:.2f} | {v['coins']:.2f} | {v['median']:.2f} | {v['spent']:.2f} | {v['builds']:.2f} | {v['buys']:.2f} | {v['blocked']:.2f}% |")
out += ["", "Affordability is sampled once per eligible player at selection reveal: a view is counted when any otherwise legal option costs more than the player's balance. It does not prove that card was wanted. Bot policy changed to account for costs between the original baseline and the redesign; this is an end-to-end comparison, not a causal attribution to one rule. A, B and the stronger schedule use the same redesigned bot and card rules. Board rotations are dependent; there are eight distinct board blocks, not 128 independent board samples. These screening results do not establish human balance.", "",
    "## Final strategy sample", "", "| Strategy | Win share | Mean VP | Mean final coins |", "|---|---:|---:|---:|"]
for strategy in ("Balanced","Tag","ParkRush","EconomyFirst"):
    ss=[s for s in seats if s["Strategy"]==strategy]
    out.append(f"| {strategy} | {100*statistics.mean(s['WinShare'] for s in ss):.2f}% | {statistics.mean(s['TotalVp'] for s in ss):.2f} | {statistics.mean(s['FinalCoins'] for s in ss):.2f} |")
out += ["", "## What remains uncertain", "",
    f"The final median bank is {m['median']:.0f} coins, still above the original 5–20 target. The final payout cannot fund another market, and economy strategies continue to accumulate excess money. This pass improves purchase pressure and removes the park inflation, but does not establish that late-game money or strategy balance is finished. Park Rush and Industry bot results need human testing and more independent boards; do not equalize bot win rates by blindly buffing every park card.",
    "The ten-card Common pool slightly changes default-card distribution. Low-weight cash events occasionally replace a free building. Test early recovery, blocked cards and late purchases with humans before changing more card values. Keep the existing opt-in analytics; placement records now include explicit build cost and settled balance for optional research settings.", "",
    "## Verification", "",
    f"- **{count} rule checks passed**, including growth, card interactions, authority privacy, checkpoint restoration and paid-build transaction cases. [Saved output](balance/2026-10-30-final-rule-checks.txt).",
    "- Full game and Razor project compiled with **zero errors and zero warnings**, using installed s&box assemblies and repository-local build outputs.",
    "- 128 post-change screening/final games completed: 32 A, 32 B, 32 stronger-price and 32 final. All completed eight rounds, recorded all payouts, had zero rejected actions or budget violations, conserved tied-win shares, and reconciled coin ledgers. No gameplay state was changed in the editor.",
    "- No editor was running for this project, so rendered layout, actual controller operation and real-client multiplayer play were not verified. The source and Razor build proves compilation, not visual layout. Start a new match for playtesting; old in-progress saves use incompatible link-era DTOs and are rejected rather than silently mis-scored. Match/recipient/host checkpoint versions are 2, session version 3; balance analytics version is 2026-10-30.2.", "",
    "## Evidence and reproduction", "",
    "- [Pre-change current-rule experiments](balance/2026-10-30-economy-seed-30102026.json)",
    "- [Prototype A](balance/2026-10-30-prototype-a-seed-30102026.json)",
    "- [B versus stronger pricing](balance/2026-10-30-pricing-comparison-seed-30102026.json)",
    "- [Final implementation](balance/2026-10-30-final-seed-30102026.json)", "",
    "From `city_sim`:", "", "```powershell",
    "dotnet run --project tests/CitySim.Rules.Tests.csproj",
    "dotnet run --project docs/balance/lab/BalanceLab.csproj -- .build/final-replay.json 32 30102026 final",
    "python docs/balance/lab/report_implementation.py",
    "dotnet build code/city_sim.csproj -p:OutputPath=../.build/balance-game/ -p:DocumentationFile=../.build/balance-game/city_sim.xml",
    "```", "",
    "The historical JSON files preserve the card definitions, settings and code fingerprints used in their runs. Re-running old modes against newer source does not reproduce an old card catalog; use the recorded fingerprinted source for historical replay. The final mode always uses active default settings. The exporter reads recorded files and writes this report plus the current catalog and CSV."]
(docs/"BALANCE_IMPLEMENTATION_2026-10-30.md").write_text("\n".join(out)+"\n",encoding="utf-8")
print(json.dumps(m,indent=2))
print(f"Exported 48 current cards; {count} checks passed.")
