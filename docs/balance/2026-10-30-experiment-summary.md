# Economy experiments — 30 October 2026

Seed 30102026; 32 matches per variant, 8 distinct board seeds with four seat rotations each. Current rules remain linked in every variant. Descriptive results only; see the main review for limitations.

| Variant | Mean VP | Mean final coins | Mean spent | Mean purchases | Unaffordable market views | Mean coin VP |
|---|---:|---:|---:|---:|---:|---:|
| current | 235.70 | 129.43 | 36.55 | 23.58 | 0.00% | 25.45 |
| base-income-1 | 233.59 | 113.27 | 36.28 | 23.52 | 0.73% | 22.24 |
| prices-1-3-5-8 | 235.07 | 117.51 | 47.81 | 23.60 | 2.88% | 23.09 |
| coins-per-vp-3 | 247.44 | 138.12 | 35.75 | 23.51 | 0.00% | 45.69 |

## Strategy results

| Variant | Strategy | Games | Win share | Mean VP | Mean final coins |
|---|---|---:|---:|---:|---:|
| current | Balanced | 32 | 15.62% | 231.28 | 108.47 |
| current | Tag | 32 | 40.62% | 243.62 | 110.88 |
| current | ParkRush | 32 | 15.62% | 230.78 | 102.69 |
| current | EconomyFirst | 32 | 28.12% | 237.09 | 195.69 |
| base-income-1 | Balanced | 32 | 15.62% | 229.72 | 92.28 |
| base-income-1 | Tag | 32 | 37.50% | 239.28 | 94.69 |
| base-income-1 | ParkRush | 32 | 15.62% | 229.25 | 88.28 |
| base-income-1 | EconomyFirst | 32 | 31.25% | 236.09 | 177.84 |
| prices-1-3-5-8 | Balanced | 32 | 25.00% | 233.38 | 96.09 |
| prices-1-3-5-8 | Tag | 32 | 28.12% | 237.31 | 100.53 |
| prices-1-3-5-8 | ParkRush | 32 | 15.62% | 232.88 | 93.31 |
| prices-1-3-5-8 | EconomyFirst | 32 | 31.25% | 236.72 | 180.09 |
| coins-per-vp-3 | Balanced | 32 | 6.25% | 240.53 | 114.16 |
| coins-per-vp-3 | Tag | 32 | 31.25% | 249.66 | 119.62 |
| coins-per-vp-3 | ParkRush | 32 | 3.12% | 240.62 | 107.09 |
| coins-per-vp-3 | EconomyFirst | 32 | 59.38% | 258.94 | 211.62 |

## Mean coin balance after each payout

| Variant | R1 | R2 | R3 | R4 | R5 | R6 | R7 | R8 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| current | 10.21 | 16.38 | 27.05 | 40.78 | 58.29 | 79.34 | 103.45 | 129.43 |
| base-income-1 | 8.21 | 12.37 | 21.13 | 32.71 | 48.08 | 67.14 | 89.39 | 113.27 |
| prices-1-3-5-8 | 9.05 | 14.67 | 24.28 | 35.48 | 50.38 | 70.02 | 92.29 | 117.51 |
| coins-per-vp-3 | 10.68 | 17.21 | 28.75 | 43.81 | 62.79 | 85.72 | 110.91 | 138.12 |

Each variant passed match-completion, payout-coverage, action-validity, nonnegative-budget and coin-ledger checks. Win shares sum to one per match. Offered and affordability figures are sampled before purchase, once per eligible player per selection. The raw JSON records source hashes and full outcomes. Re-run the lab against the same sources to reproduce; this summary's card proposals are editorial, not simulation output.
