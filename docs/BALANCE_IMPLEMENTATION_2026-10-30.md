# Balance implementation — 30 October 2026

Historical evidence from before balance 2026-10-30.4. See [the current placement and park balance pass](BALANCE_LOCAL_PLACEMENT_2026-10-30.md) for the replacement rules and fresh simulations.
Implemented the independent-income and park redesign, with **prototype B's simpler market economy** selected after comparison. All placements remain free. [Current cards](CARD_CATALOG.md) supersedes the historical review's pre-change catalog; [GDD v0.10](GDD.md) describes the active rules.

## What changed

- Removed economic linking, shared capacities, order-dependent allocation, paid-link counters and the Best Connected competition. The three remaining city competitions are Longest Road, Biggest Park Owner and Full House.
- House and Apartment receive a fixed coin reward if a Shop is nearby. Shops independently count Home-tagged buildings. Farms and Orchards check adjacent farms. Cafe, Granary, Rush Hour and Central Station use direct counts. Entrepreneur now checks three Shops with two nearby Homes each and awards 6 VP.
- Home consistently means its tag, including Housing Authority, in income, placement scoring, penalties, goals, contracts and emergencies. The six tags otherwise stay unchanged.
- Park additions and growth award only the owner for their capped size increase. No shared placement awards and no repeatable size-10 payout. Fountain size, joins and Sapling growth use the same calculation.
- Base income is 2. Common/Uncommon/Rare/Legendary purchase prices are 1/4/7/11. Starting money remains 8; free defaults remain one random Common per round; three market selections and the five-card hand limit remain. Spare coins still convert at 5:1.
- Ribbon Cutting and Rush Hour moved to Common with weight 1 each. At the chosen Uncommon price of 4, their capped reward of 4 could never generate net profit from buying them. They were not played in the initial prototype samples. At price 1 they retain a useful conditional cash role without increasing their payout caps. Grant remains Common weight 2.
- Rewrote all 48 card descriptions; updated interaction highlights, help, costs, onboarding, payout presentation and the end-score reveal. Removed stale pairing visuals from cosmetic traffic. Updated bot valuation to consider acquisition costs and affordability.
- Optional Rare/Legendary build-cost settings retain prototype A for research, defaulting to zero. Transaction checks cover insufficient funds, failed placements, renovation, duplicate commands, stale hands and migration. They are not an extra rule in the standard game.

## Why prototype B

Prototype A added a second cost at placement but only modestly constrained purchases. The documented B alternative made more market choices budget-sensitive with fewer rules. A stronger price schedule was also screened; it created more pressure but worsened the tested strategy spread. The selected economy keeps Common buildings accessible rather than balancing solely by forcing players to pass.

Each row below is 32 four-player games, 128 player outcomes, eight board seeds with four seat rotations. Shared seed 30102026; Normal Balanced/Industry-tag/Park Rush/Economy First bots; 15×15 board. Source fingerprints and individual outcomes are recorded in each JSON. The final row includes the two low-weight event rarity changes; the earlier B row does not.

| Configuration | Mean VP | Mean final coins | Median coins | Mean market spend | Mean build spend | Purchases / 24 | Unaffordable market views |
|---|---:|---:|---:|---:|---:|---:|---:|
| Before changes | 235.70 | 129.43 | 115.00 | 36.55 | 0.00 | 23.58 | 0.00% |
| A: 1/3/5/8 + build fees | 64.99 | 94.02 | 81.00 | 39.53 | 3.89 | 22.48 | 5.67% |
| B: 1/4/7/11, free placement | 65.45 | 89.11 | 80.00 | 46.18 | 0.00 | 22.09 | 15.19% |
| Screened: 2/5/9/14, free placement | 57.02 | 70.89 | 62.50 | 61.58 | 0.00 | 20.52 | 33.38% |
| Final: B + event rarity fixes | 61.67 | 79.49 | 71.50 | 48.79 | 0.00 | 22.79 | 13.05% |

Affordability is sampled once per eligible player at selection reveal: a view is counted when any otherwise legal option costs more than the player's balance. It does not prove that card was wanted. Bot policy changed to account for costs between the original baseline and the redesign; this is an end-to-end comparison, not a causal attribution to one rule. A, B and the stronger schedule use the same redesigned bot and card rules. Board rotations are dependent; there are eight distinct board blocks, not 128 independent board samples. These screening results do not establish human balance.

## Final strategy sample

| Strategy | Win share | Mean VP | Mean final coins |
|---|---:|---:|---:|
| Balanced | 18.75% | 61.53 | 63.94 |
| Tag | 62.50% | 70.22 | 75.72 |
| ParkRush | 3.12% | 53.31 | 53.59 |
| EconomyFirst | 15.62% | 61.62 | 124.72 |

## What remains uncertain

The final median bank is 72 coins, still above the original 5–20 target. The final payout cannot fund another market, and economy strategies continue to accumulate excess money. This pass improves purchase pressure and removes the park inflation, but does not establish that late-game money or strategy balance is finished. Park Rush and Industry bot results need human testing and more independent boards; do not equalize bot win rates by blindly buffing every park card.
The ten-card Common pool slightly changes default-card distribution. Low-weight cash events occasionally replace a free building. Test early recovery, blocked cards and late purchases with humans before changing more card values. Keep the existing opt-in analytics; placement records now include explicit build cost and settled balance for optional research settings.

## Verification

- **512 rule checks passed**, including growth, card interactions, authority privacy, checkpoint restoration and paid-build transaction cases. [Saved output](balance/2026-10-30-final-rule-checks.txt).
- Full game and Razor project compiled with **zero errors and zero warnings**, using installed s&box assemblies and repository-local build outputs.
- 128 post-change screening/final games completed: 32 A, 32 B, 32 stronger-price and 32 final. All completed eight rounds, recorded all payouts, had zero rejected actions or budget violations, conserved tied-win shares, and reconciled coin ledgers. No gameplay state was changed in the editor.
- No editor was running for this project, so rendered layout, actual controller operation and real-client multiplayer play were not verified. The source and Razor build proves compilation, not visual layout. Start a new match for playtesting; old in-progress saves use incompatible link-era DTOs and are rejected rather than silently mis-scored. Match/recipient/host checkpoint versions are 2, session version 3; balance analytics version is 2026-10-30.2.

## Evidence and reproduction

- [Pre-change current-rule experiments](balance/2026-10-30-economy-seed-30102026.json)
- [Prototype A](balance/2026-10-30-prototype-a-seed-30102026.json)
- [B versus stronger pricing](balance/2026-10-30-pricing-comparison-seed-30102026.json)
- [Final implementation](balance/2026-10-30-final-seed-30102026.json)

From `city_sim`:

```powershell
dotnet run --project tests/CitySim.Rules.Tests.csproj
dotnet run --project docs/balance/lab/BalanceLab.csproj -- .build/final-replay.json 32 30102026 final
python docs/balance/lab/report_implementation.py
dotnet build code/city_sim.csproj -p:OutputPath=../.build/balance-game/ -p:DocumentationFile=../.build/balance-game/city_sim.xml
```

The historical JSON files preserve the card definitions, settings and code fingerprints used in their runs. Re-running old modes against newer source does not reproduce an old card catalog; use the recorded fingerprinted source for historical replay. The final mode always uses active default settings. The exporter reads recorded files and writes this report plus the current catalog and CSV.
